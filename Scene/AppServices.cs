using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace SampleClient.Core
{
    /// <summary>
    /// 앱 전역 서비스 초기화 진입점.
    /// BootFlow보다 먼저 MessagePipe provider를 준비한다.
    /// </summary>
    public static class AppServices
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            InitializeMessagePipe();
            _initialized = true;
        }

        private static void InitializeMessagePipe()
        {
            var services = new ServiceCollection();
            services.AddMessagePipe();
            GlobalMessagePipe.SetProvider(services.BuildServiceProvider());
        }
    }
}
