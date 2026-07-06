using System;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Firebase;
using SampleClient.Utils;

namespace SampleClient.Scene.Login
{
    /// <summary>
    /// LoginScene에서 인증을 수행하고 성공 시 MainScene으로 이동한다.
    /// 현재는 익명 로그인만 사용하며, 이후 다른 로그인 수단도 이 흐름에 추가한다.
    /// </summary>
    public static class LoginFlow
    {
        /// <summary>
        /// Firebase 익명 로그인을 수행한다.
        /// </summary>
        public static async UniTask<bool> SignInAnonymouslyAsync()
        {
            var signedIn = await FirebaseService.Instance.SignInAnonymouslyAsync();
            if (!signedIn)
            {
                return false;
            }

            if (!FirebaseService.IsApiReady)
            {
                Log.LogMessage("[LoginFlow] Firebase API가 준비되지 않았습니다.", Log.LogLevel.Error);
                return false;
            }

            try
            {
                // 익명 로그인 직후 서버에 유저 문서 생성을 보장한다.
                await FirebaseService.Instance.Api.User.Ensure();
                return true;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[LoginFlow] 유저 보장 요청 실패: {e.Message}", Log.LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// 로그인 성공 후 MainScene으로 전환한다.
        /// </summary>
        public static async UniTask LoadMainAsync()
        {
            await SceneLoader.Instance.LoadMainAsync();
        }
    }
}
