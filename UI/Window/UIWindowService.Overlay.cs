using System;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Addressables;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.UI
{
    /// <summary>
    /// UIWindowService의 overlay 관리 partial.
    /// Reward처럼 window 위에 잠깐 표시되는 결과 overlay는 window와 달리 cache하지 않고
    /// 열 때 생성, 닫을 때 즉시 해제한다.
    /// 샘플에서는 Reward overlay만 남기고, 같은 패턴의 다른 연출 overlay는 중략했습니다.
    /// </summary>
    public partial class UIWindowService
    {
        // Reward overlay 관련 상태
        private GameObject _rewardOverlayInstance;
        private bool _rewardLoading;
        private bool _rewardOpened;
        private bool _rewardClosing;
        private bool _closeCurrentWindow; // OpenRewardAsync 호출 시 현재 창도 함께 닫을지 여부

        /// <summary>
        /// 현재 열린 UI 창 위에 Reward overlay prefab을 표시한다.
        /// </summary>
        /// <param name="closeCurrentWindow">Reward overlay를 닫은 뒤 현재 UI 창도 닫을지 여부.</param>
        /// <param name="requiredWindow">
        /// 지정된 경우 Reward overlay가 열리는 동안 현재 window가 이 값과 같을 때만 표시한다.
        /// null이면 window 종류를 검사하지 않는다.
        /// </param>
        /// <returns>overlay 생성과 보상 초기화 준비가 모두 성공하면 true.</returns>
        public async UniTask<bool> OpenRewardAsync(bool closeCurrentWindow, UIWindowType? requiredWindow = null)
        {
            if (_rewardLoading || _rewardOpened)
                return false;

            if (requiredWindow.HasValue && _currentWindow != requiredWindow.Value)
                return false;

            try
            {
                _rewardLoading = true;
                _closeCurrentWindow = closeCurrentWindow;
                var openGeneration = _overlayGeneration;

                var instance = await AddressablePrefabService.InstantiateAsync(UIPrefabAddress.RewardOverlay, transform);

                // await InstantiateAsync 동안 CloseAllAsync가 실행될 수 있다.
                // 그 경우 생성이 끝난 instance를 다시 열지 않고 즉시 해제한다.
                if (openGeneration != _overlayGeneration ||
                    (requiredWindow.HasValue && _currentWindow != requiredWindow.Value))
                {
                    if (instance != null)
                    {
                        AddressablePrefabService.ReleaseInstance(instance);
                    }

                    _closeCurrentWindow = false;
                    return false;
                }

                if (instance == null)
                {
                    Log.LogMessage("[UIWindowService] Reward overlay 생성 실패", Log.LogLevel.Warning);
                    return false;
                }

                _rewardOverlayInstance = instance;
                ApplyOwnerCanvasSorting(instance.transform, RewardSortingOrder);

                // 중략: overlay에서 RewardPresenter를 찾아 보상 표시 데이터로 초기화

                _rewardOpened = true;
                return true;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[UIWindowService] Reward overlay 열기 실패: {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);

                _rewardOpened = false;
                _closeCurrentWindow = false;

                ReleaseRewardOverlayInstance();
            }
            finally
            {
                _rewardLoading = false;

                // Reward overlay가 열리지 못한 실패 경로에서는 현재 창 닫기 예약도 함께 정리한다.
                if (!_rewardOpened)
                {
                    _closeCurrentWindow = false;
                }
            }

            return false;
        }

        /// <summary>
        /// Reward overlay를 닫고 요청된 경우 현재 UI 창도 닫는다.
        /// </summary>
        public async UniTask CloseRewardAsync()
        {
            if (!_rewardOpened || _rewardClosing)
                return;

            _rewardClosing = true;

            try
            {
                var closeCurrentWindow = _closeCurrentWindow;

                if (!ReleaseRewardOverlayInstance())
                    return;

                _rewardOpened = false;
                _closeCurrentWindow = false;

                if (closeCurrentWindow)
                {
                    await CloseCurrentWindowAsync();
                }
            }
            finally
            {
                _rewardClosing = false;
            }
        }

        /// <summary>
        /// 전체 정리용으로 Reward overlay만 닫는다.
        /// 현재 UI 창 닫기는 실행하지 않는다.
        /// </summary>
        private bool CloseRewardOnly()
        {
            if (!_rewardOpened)
                return true;

            if (_rewardClosing)
                return false;

            _rewardClosing = true;

            try
            {
                if (!ReleaseRewardOverlayInstance())
                    return false;

                _rewardOpened = false;
                _closeCurrentWindow = false;

                return true;
            }
            finally
            {
                _rewardClosing = false;
            }
        }

        /// <summary>
        /// Reward overlay prefab instance를 Addressables에서 해제한다.
        /// Reward는 짧은 결과 overlay이므로 window처럼 cache하지 않는다.
        /// </summary>
        /// <returns>해제 성공 또는 이미 닫힌 상태면 true, 해제 실패면 false.</returns>
        private bool ReleaseRewardOverlayInstance()
        {
            if (_rewardOverlayInstance == null)
            {
                return true;
            }

            var released = AddressablePrefabService.ReleaseInstance(_rewardOverlayInstance);
            if (!released)
            {
                Log.LogMessage("[UIWindowService] Reward overlay 해제 실패", Log.LogLevel.Warning);
                return false;
            }

            _rewardOverlayInstance = null;
            return true;
        }
    }
}
