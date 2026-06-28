using System.Diagnostics;

namespace SampleClient.Utils
{
    public static class Log
    {
        // [Conditional("UNITY_EDITOR")]
        // [Conditional("DEVELOPMENT_BUILD")]
        public static void LogMessage(string message, LogLevel level = LogLevel.Debug)
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            switch (level)
            {
                case LogLevel.Debug:
                    UnityEngine.Debug.Log(message);
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(message);
                    break;
            }
            #else
            // 릴리즈에서는 에러 로그만 남긴다.
            if (level == LogLevel.Error)
            {
                UnityEngine.Debug.LogError(message);
            }
            #endif
        }
        
        public enum LogLevel
        {
            Debug,
            Warning,
            Error,
        }
    }
}




