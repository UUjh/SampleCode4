using System;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Firebase;
using SampleClient.Utils;

namespace SampleClient.Scene.Boot
{
    /// <summary>
    /// 앱 시작 시 Firebase를 준비하고 로그인 상태에 따라 첫 씬을 결정한다.
    /// BootScene에서 한 번 호출되며, 이후 bootstrap 단계도 이 흐름에 붙인다.
    /// 로딩 UI 표시/종료도 SceneLoader가 아니라 이 호출 흐름이 담당한다.
    /// </summary>
    public static class BootFlow
    {
        /// <summary>
        /// Firebase 초기화와 로그인 상태 확인 후 첫 씬을 로드한다.
        /// </summary>
        public static async UniTask RunAsync()
        {
            try
            {
                // 중략: 전역 로딩 UI 표시

                var signedIn = await IsSignedInAsync();
                // /user/ensure는 익명 로그인 직후 users/{uid} 문서 보장을 위해 LoginFlow에서만 호출한다.
                // 앱 시작마다 기존 세션에 대해 호출하면 no-op이어도 rate limit(429) 대상이 되어 BootFlow가 막힐 수 있다.

                await SceneLoader.Instance.BootAsync(signedIn);
            }
            catch (Exception e)
            {
                Log.LogMessage($"[BootFlow] 초기화 실패: {e}", Log.LogLevel.Error);
                // 중략: 전역 로딩 UI 종료와 재시도 안내
            }
        }

        /// <summary>
        /// Firebase 초기화 후 익명 로그인 상태를 확인한다.
        /// </summary>
        private static async UniTask<bool> IsSignedInAsync()
        {
            await FirebaseService.Instance.InitializeAsync();
            return FirebaseService.Instance.IsAnonymousUserSignedIn;
        }
    }
}
