using System;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.AppCheck;
using Firebase.Auth;
using SampleClient.Core;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Firebase 코어, AppCheck, Auth, Player API를 앱 생명주기 동안 관리.
    /// BootFlow에서 먼저 초기화, 이후 아웃게임 서비스가 Api를 통해 서버를 호출.
    /// </summary>
    [DefaultExecutionOrder(-110)]
    public class FirebaseService : PersistentSingleton<FirebaseService>
    {
        private FirebaseAuth _auth;
        private FirebaseAppCheck _appCheck;
        private PlayerApiClient _apiClient;
        private PlayerApi _api;
        private bool _ready;
        private bool _initializing;

        private const string AppCheckDebugToken = "";

        public bool IsReady => _ready;
        public FirebaseAuth Auth => _auth;
        public FirebaseAppCheck AppCheck => _appCheck;
        public PlayerApi Api => _api;
        public static bool IsApiReady => HasInstance && CurrentInstance.IsReady && CurrentInstance.Api != null;

        // 익명 로그인 확인
        public bool IsAnonymousUserSignedIn
        {
            get
            {
                var ok = _ready && _auth != null && _auth.CurrentUser != null && _auth.CurrentUser.IsAnonymous;

                Log.LogMessage($"[FirebaseService] IsAnonymousUserSignedIn={ok}", Log.LogLevel.Debug);

                return ok;
            }
        }

        /// <summary>
        /// CheckDependencies, App Check, Auth 핸들까지 한 번만 준비.
        /// 동시에 호출되면 먼저 시작된 초기화가 끝날 때까지 메인 스레드에서 대기.
        /// </summary>
        public async UniTask InitializeAsync()
        {
            while (_initializing && !_ready)
            {
                // Unity 메인 스레드 초기화 중복 호출을 막기 위한 대기. 실제 초기화는 첫 호출만 수행한다.
                await UniTask.Yield();
            }

            if (_ready)
            {
                Log.LogMessage("[FirebaseService] InitializeAsync(이미 완료)", Log.LogLevel.Debug);
                return;
            }

            _initializing = true;
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // 에디터/개발 빌드에서만 Debug AppCheck를 사용.
                // 출시 후에는 플랫폼별 AppCheck 설정을 사용해야 함.
                if (!string.IsNullOrEmpty(AppCheckDebugToken))
                {
                    DebugAppCheckProviderFactory.Instance.SetDebugToken(AppCheckDebugToken);
                }

                FirebaseAppCheck.SetAppCheckProviderFactory(DebugAppCheckProviderFactory.Instance);
#endif

                var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus != DependencyStatus.Available)
                {
                    Log.LogMessage($"[FirebaseService] Firebase 초기화 실패: {dependencyStatus}", Log.LogLevel.Error);
                    return;
                }

                try
                {
                    var token = await FirebaseAppCheck.DefaultInstance.GetAppCheckTokenAsync(forceRefresh: true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    // * 토큰은 절대 로그로 남기지 않기.
                    Log.LogMessage($"[FirebaseService] AppCheck token.Length = {token.Token.Length}, expires={token.ExpireTime}", Log.LogLevel.Debug);
#endif
                }
                catch (Exception e)
                {
                    Log.LogMessage($"[FirebaseService] AppCheck 토큰 실패: {e}", Log.LogLevel.Error);
                }

                _auth = FirebaseAuth.DefaultInstance;
                _appCheck = FirebaseAppCheck.DefaultInstance;
                _apiClient = new PlayerApiClient(_auth, _appCheck);
                _api = new PlayerApi(_apiClient);

                _ready = true;
                Log.LogMessage("[FirebaseService] InitializeAsync(최초 완료)", Log.LogLevel.Debug);
            }
            finally
            {
                _initializing = false;
            }
        }

        // 익명 로그인
        /// <summary>
        /// Firebase 익명 로그인을 수행한다.
        /// </summary>
        public async UniTask<bool> SignInAnonymouslyAsync()
        {
            await InitializeAsync();
            if (!_ready || _auth == null)
            {
                Log.LogMessage("[FirebaseService] SignInAnonymouslyAsync: 초기화 실패로 로그인 시도 안 함", Log.LogLevel.Warning);
                return false;
            }

            try
            {
                var result = await _auth.SignInAnonymouslyAsync();
                var u = result.User;
                Log.LogMessage($"[FirebaseService] anonymous sign-in succeeded. IsAnonymous={u.IsAnonymous}", Log.LogLevel.Debug);
                return true;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[FirebaseService] 익명 로그인 실패: {e}", Log.LogLevel.Error);
                return false;
            }
        }

        public void SignOut()
        {
            DisposeApiClient();

            if (_auth != null)
            {
                _auth.SignOut();
            }

            _ready = false;
            _initializing = false;

            Log.LogMessage("[FirebaseService] 로컬 Firebase 세션과 Player API 클라이언트를 해제했습니다.", Log.LogLevel.Debug);
        }

        /// <summary>
        /// Player API 클라이언트와 전송 큐를 정리한다.
        /// SignOut 또는 서비스 Dispose 시 대기 중인 서버 요청이 다음 세션으로 넘어가지 않게 한다.
        /// </summary>
        private void DisposeApiClient()
        {
            _apiClient?.Dispose();
            _apiClient = null;
            _api = null;
        }

        /// <summary>
        /// Firebase API 클라이언트와 인증 참조를 정리한다.
        /// </summary>
        protected override void Dispose()
        {
            DisposeApiClient();

            _auth = null;
            _appCheck = null;
            _ready = false;
            _initializing = false;
        }
    }
}






