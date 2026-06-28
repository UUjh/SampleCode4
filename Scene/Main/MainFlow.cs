using System;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.Scene.Main
{
    /// <summary>
    /// MainScene 진입 시 아웃게임 데이터를 준비한다.
    /// 실제 로딩 UI, 진입 애니메이션, 재시도 팝업 등 프로젝트별 연출은 샘플에서 중략했다.
    /// </summary>
    public class MainFlow : MonoBehaviour
    {
        private bool _ready;
        public bool IsReady => _ready;

        private async void Start()
        {
            await RunAsync();
        }

        /// <summary>
        /// 유저 상태, 상점 Bootstrap, 카탈로그를 준비한다.
        /// </summary>
        public async UniTask RunAsync()
        {
            _ready = false;

            try
            {
                var data = await AppBootstrapService.LoadAsync();
                if (data == null)
                {
                    Log.LogMessage("[MainFlow] Bootstrap 데이터 로드에 실패했습니다.", Log.LogLevel.Error);
                    return;
                }

                _ready = true;
                Log.LogMessage("[MainFlow] Bootstrap 데이터 로드 완료", Log.LogLevel.Debug);
            }
            catch (Exception e)
            {
                Log.LogMessage($"[MainFlow] 초기화 실패: {e}", Log.LogLevel.Error);
            }
        }
    }
}
