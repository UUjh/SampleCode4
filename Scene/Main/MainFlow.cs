using System;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.Scene.Main
{
    /// <summary>
    /// MainScene 진입 시 아웃게임 Bootstrap 데이터를 준비한다.
    /// Shop/Gacha UI는 GameBootstrapService.Data를 기준으로 초기화한다.
    /// 실제 로딩 UI, 진입 애니메이션, 재시도 팝업 등 프로젝트별 연출은 샘플에서 중략했다.
    /// </summary>
    public class MainFlow : MonoBehaviour
    {
        private bool _ready;
        public bool IsReady => _ready;

        /// <summary>
        /// MainScene 진입 시 Bootstrap 로드를 시작한다.
        /// </summary>
        private async void Start()
        {
            await RunAsync();
        }

        /// <summary>
        /// 아웃게임 Bootstrap 데이터를 로드하고 로딩 UI 상태를 갱신한다.
        /// </summary>
        public async UniTask RunAsync()
        {
            _ready = false;

            try
            {
                // 로딩 UI가 있으면 씬 로드가 아니라 bootstrap 완료 시점까지 진행률을 이어서 표시한다.
                var data = await GameBootstrapService.LoadAsync(onProgress: p =>
                {
                    // 중략: 전역 로딩 UI 진행률 갱신
                });

                if (data == null)
                {
                    Log.LogMessage("[MainFlow] Bootstrap 데이터 로드에 실패했습니다.", Log.LogLevel.Error);
                    return;
                }

                // 중략: 재진입 시 메일 상태만 최신화(RefreshMailAsync)하고 알림 배지를 갱신
                // 중략: 전역 로딩 UI 종료

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
