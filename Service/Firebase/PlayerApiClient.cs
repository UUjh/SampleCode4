using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase.AppCheck;
using Firebase.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using SampleClient.Utils;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Firebase Auth/AppCheck 토큰을 붙여 Player API를 호출.
    /// FirebaseService가 생성하고 상점, 가챠, 인벤토리 같은 아웃게임 서비스에서 사용.
    /// </summary>
    public sealed class PlayerApiClient : IDisposable
    {
        // 기본 타임아웃 시간.
        private const int DefaultTimeoutSeconds = 30;

        private readonly FirebaseAuth _auth;
        private readonly FirebaseAppCheck _appCheck;
        private readonly SendQueue _sendQueue;
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
        private readonly int _timeoutSeconds;
        private bool _disposed;

        /// <summary>
        /// Player API 클라이언트 생성.
        /// </summary>
        public PlayerApiClient(FirebaseAuth auth, FirebaseAppCheck appCheck, SendQueue sendQueue = null, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            _auth = auth;
            _appCheck = appCheck;
            _sendQueue = sendQueue ?? new SendQueue();
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// GET 요청.
        /// </summary>
        /// <param name="path">요청 경로.</param>
        /// <param name="query">요청 쿼리.</param>
        /// <returns>응답 데이터.</returns>
        public UniTask<JToken> GetAsync(string path, Dictionary<string, string> query = null)
        {
            return Enqueue(UnityWebRequest.kHttpVerbGET, path, query, null);
        }

        /// <summary>
        /// POST 요청.
        /// </summary>
        /// <param name="path">요청 경로.</param>
        /// <param name="body">요청 바디.</param>
        /// <returns>응답 데이터.</returns>
        public UniTask<JToken> PostAsync(string path, object body = null)
        {
            return Enqueue(UnityWebRequest.kHttpVerbPOST, path, null, body);
        }

        /// <summary>
        /// PUT 요청.
        /// </summary>
        /// <param name="path">요청 경로.</param>
        /// <param name="body">요청 바디.</param>
        /// <returns>응답 데이터.</returns>
        public UniTask<JToken> PutAsync(string path, object body = null)
        {
            return Enqueue(UnityWebRequest.kHttpVerbPUT, path, null, body);
        }

        /// <summary>
        /// PATCH 요청.
        /// </summary>
        /// <param name="path">요청 경로.</param>
        /// <param name="body">요청 바디.</param>
        /// <returns>응답 데이터.</returns>
        public UniTask<JToken> PatchAsync(string path, object body = null)
        {
            return Enqueue("PATCH", path, null, body);
        }

        /// <summary>
        /// 전송 큐를 정리하고 이후 요청을 막는다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeCts.Cancel();
            _sendQueue.Dispose();
            _disposeCts.Dispose();
        }

        /// <summary>
        /// 요청 큐에 추가.
        /// </summary>
        /// <param name="method">요청 메서드.</param>
        /// <param name="path">요청 경로.</param>
        /// <param name="query">요청 쿼리.</param>
        /// <param name="body">요청 바디.</param>
        /// <returns>응답 데이터.</returns>
        private UniTask<JToken> Enqueue(string method, string path, Dictionary<string, string> query, object body)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerApiClient));
            }

            // 상태 변경 요청은 SendQueue를 거쳐 순서대로 실행.
            // bootstrap/read 요청은 구매, 가챠, 장착 저장 같은 쓰기 요청을 막지 않도록 바로 전송.
            if (ShouldUseQueue(method, path))
            {
                return _sendQueue.Enqueue(cancellationToken =>
                    SendWithRecoveryAsync(method, path, query, body, cancellationToken));
            }

            return SendWithRecoveryAsync(method, path, query, body, _disposeCts.Token);
        }

        private static bool ShouldUseQueue(string method, string path)
        {
            // 조회 요청은 서버 상태를 바꾸지 않으므로 큐를 우회한다.
            // 메일함/프로필 같은 read 요청이 구매나 뽑기 요청 뒤에서 대기하지 않게 하기 위함.
            if (method == UnityWebRequest.kHttpVerbGET)
            {
                return false;
            }

            // bootstrap 요청은 화면 초기화용 상태 조회에 가깝기 때문에 큐를 우회한다.
            // 단, 실제 구매/뽑기 같은 상태 변경 요청은 계속 큐를 탄다.
            if (IsBootstrapPath(path))
            {
                return false;
            }

            return true;
        }

        private static bool IsBootstrapPath(string path)
        {
            // 현재 Player API에서 bootstrap 계열은 POST를 쓰지만, 클라이언트 관점에서는 초기 상태 조회다.
            // 그래서 HTTP 메서드만 보지 않고 path 기준으로 read 성격의 POST를 분리한다.
            return string.Equals(path, "/user/bootstrap", StringComparison.Ordinal) ||
                string.Equals(path, "/shop/bootstrap", StringComparison.Ordinal) ||
                string.Equals(path, "/gacha/bootstrap", StringComparison.Ordinal);
            // 중략: 동일한 기준으로 우회하는 다른 도메인 bootstrap 경로
        }

        /// <summary>
        /// 요청 복구.
        /// </summary>
        /// <param name="method">요청 메서드.</param>
        /// <param name="path">요청 경로.</param>
        /// <param name="query">요청 쿼리.</param>
        /// <param name="body">요청 바디.</param>
        /// <param name="cancellationToken">취소 토큰.</param>
        private async UniTask<JToken> SendWithRecoveryAsync(
            string method,
            string path,
            Dictionary<string, string> query,
            object body,
            CancellationToken cancellationToken)
        {
            // 요청마다 현재 Firebase ID 토큰과 AppCheck 토큰을 붙인다.
            // 클라이언트는 유저 식별만 맡고, 재화/구매 검증은 서버가 최종 판단한다.
            var idToken = await GetIdTokenAsync(forceRefresh: false);
            var appCheckToken = await GetAppCheckTokenAsync(forceRefresh: false);

            try
            {
                return await SendOnceAsync(method, path, query, body, idToken, appCheckToken, cancellationToken);
            }
            catch (PlayerApiException e)
            {
                // ID 토큰 인증 실패는 HTTP 401로 내려오며, 밴 계정이 아니면 토큰을 강제 갱신한 뒤 한 번만 재시도한다.
                // body code가 USER_BANNED이면 contract에 따라 재시도하지 않고 호출부로 예외를 전달한다.
                if (e.HttpStatus == CommonCode.UNAUTHORIZED && e.DomainCode != AdminCode.USER_BANNED)
                {
                    Log.LogMessage($"[PlayerApiClient] ID 토큰 인증 실패, 토큰 갱신 후 재시도: {e.Message}", Log.LogLevel.Debug);
                    var refreshedId = await GetIdTokenAsync(forceRefresh: true);
                    return await SendOnceAsync(method, path, query, body, refreshedId, appCheckToken, cancellationToken);
                }

                // AppCheck 실패는 서버 계약상 HTTP 403과 body code 440/441 조합으로 내려온다.
                // AppCheck 토큰만 강제 갱신한 뒤 같은 요청을 한 번만 재시도한다.
                if (e.HttpStatus == CommonCode.FORBIDDEN &&
                    (e.DomainCode == CommonCode.APP_CHECK_REQUIRED || e.DomainCode == CommonCode.APP_CHECK_INVALID))
                {
                    Log.LogMessage($"[PlayerApiClient] AppCheck 토큰 문제: {e.Message}", Log.LogLevel.Debug);
                    var refreshedAppCheck = await GetAppCheckTokenAsync(forceRefresh: true);
                    return await SendOnceAsync(method, path, query, body, idToken, refreshedAppCheck, cancellationToken);
                }

                throw;
            }
        }

        /// <summary>
        /// 토큰이 포함된 Player API 요청을 한 번 전송하고 응답 envelope을 처리한다.
        /// </summary>
        private async UniTask<JToken> SendOnceAsync(
            string method,
            string path,
            Dictionary<string, string> query,
            object body,
            string idToken,
            string appCheckToken,
            CancellationToken cancellationToken)
        {
            // 요청 URL 생성.
            var url = BuildUrl(path, query);
            // 요청 빌드.
            using var req = BuildRequest(method, url, body);
            // 헤더 설정.
            req.SetRequestHeader("Authorization", $"Bearer {idToken}");
            req.SetRequestHeader("X-Firebase-AppCheck", appCheckToken);

            // UnityWebRequest 단계에서 발생한 오류도 PlayerApiException으로 통일해 호출부가 HTTP 구현을 몰라도 되게 한다.
            try
            {
                await req.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
            }
            catch (UnityWebRequestException e)
            {
                throw new PlayerApiException(e.ResponseCode, GetDomainCode(e), e.Message, e.Text);
            }

            // 응답 텍스트 가져오기.
            var responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            // 응답 상태 코드 가져오기.
            var httpStatus = req.responseCode;

            // 응답 데이터 파싱.
            ApiResponse envelope = null;
            // 응답 텍스트가 있으면 파싱.
            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    // 응답 데이터 파싱.
                    envelope = JsonConvert.DeserializeObject<ApiResponse>(responseText, ResponseJsonSetting);
                }
                catch
                {
                    envelope = null;
                }
            }

            // 성공 시 응답 데이터 반환.
            if (httpStatus == CommonCode.SUCCESS && envelope != null && envelope.isSuccess)
            {
                return envelope.data;
            }

            // 서버가 내려준 도메인 코드를 우선 사용.
            // 이 값으로 밴, AppCheck 갱신, 재화 부족 같은 게임 규칙을 구분.
            var domainCode = envelope != null ? envelope.code : (int)httpStatus;
            var message = envelope != null ? envelope.message : req.error ?? "Unknown error";

            throw new PlayerApiException(httpStatus, domainCode, message, responseText);
        }

        /// <summary>
        /// HTTP 메서드, URL, JSON 바디를 기준으로 UnityWebRequest를 만든다.
        /// </summary>
        private UnityWebRequest BuildRequest(string method, string url, object body)
        {
            // 요청 생성.
            var req = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _timeoutSeconds
            };

            // 바디가 있고 GET 요청이 아니면 바디를 추가.
            if (body != null && method != UnityWebRequest.kHttpVerbGET)
            {
                // Player API는 JSON 바디를 받는다. GET 요청에는 바디를 붙이지 않는다.
                var json = JsonConvert.SerializeObject(body);
                var raw = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(raw) { contentType = "application/json" };
                req.SetRequestHeader("Content-Type", "application/json");
            }

            // 헤더 설정.
            req.SetRequestHeader("Accept", "application/json");
            return req;
        }

        /// <summary>
        /// UnityWebRequest 예외 본문에서 서버 도메인 코드를 추출한다.
        /// </summary>
        private static int GetDomainCode(UnityWebRequestException e)
        {
            if (e == null || string.IsNullOrEmpty(e.Text))
            {
                return (int)(e != null ? e.ResponseCode : 0);
            }

            try
            {
                var envelope = JsonConvert.DeserializeObject<ApiResponse>(e.Text, ResponseJsonSetting);
                return envelope != null ? envelope.code : (int)e.ResponseCode;
            }
            catch
            {
                return (int)e.ResponseCode;
            }
        }

        /// <summary>
        /// Player API 기본 URL, 경로, 쿼리 문자열을 조합한다.
        /// </summary>
        private static string BuildUrl(string path, Dictionary<string, string> query)
        {
            // 경로 정규화.
            var normalized = path.StartsWith("/") ? path : "/" + path;
            // 기본 URL 추가.
            var url = TransportConfig.BaseUrl + normalized;
            if (query == null || query.Count == 0)
            {
                return url;
            }

            var sb = new StringBuilder(url);
            sb.Append('?');

            var first = true;
            foreach (var pair in query)
            {
                if (!first)
                {
                    sb.Append('&');
                }

                sb.Append(UnityWebRequest.EscapeURL(pair.Key));
                sb.Append('=');
                sb.Append(UnityWebRequest.EscapeURL(pair.Value));
                first = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// 현재 Firebase 사용자 ID 토큰을 가져온다.
        /// </summary>
        private async UniTask<string> GetIdTokenAsync(bool forceRefresh)
        {
            var user = _auth.CurrentUser;
            if (user == null)
            {
                throw new InvalidOperationException("No signed-in Firebase user.");
            }

            return await user.TokenAsync(forceRefresh).AsUniTask();
        }

        /// <summary>
        /// Firebase AppCheck 토큰을 가져온다.
        /// </summary>
        private async UniTask<string> GetAppCheckTokenAsync(bool forceRefresh)
        {
            var token = await _appCheck.GetAppCheckTokenAsync(forceRefresh).AsUniTask();
            return token.Token;
        }

        /// <summary>
        /// 서버 응답의 날짜 문자열을 DataTime으로 자동으로 변환하지 않는다.
        /// API 계약상 createAt/expriesAt 같은 시간 값은 원본 문자열 그대로 DTO에 전달한다.
        /// </summary>
        private static readonly JsonSerializerSettings ResponseJsonSetting = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };
    }
}




