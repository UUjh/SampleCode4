using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SampleClient.Core;
using SampleClient.Scene;
using SampleClient.Service.Addressables;
using SampleClient.UI.Shop;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.UI
{
    /// <summary>
    /// MainScene 위에 UI window를 열고 닫는 서비스.
    /// additive scene window와 Addressables prefab window를 같은 경로로 관리하고,
    /// route stack 기반 뒤로가기와 window 작업 동시성 제어를 담당한다.
    /// 샘플에서는 TopBar/OutGameNav 연동과 일부 콘텐츠 분기를 중략했습니다.
    /// </summary>
    public partial class UIWindowService : PersistentSingleton<UIWindowService>
    {
        // MainScene Canvas는 기준값 0을 유지하고, Main 위에 얹는 additive UI만 명시한 정렬값으로 올린다.
        // Overlay는 전체 화면 연출이므로 window보다 위에 표시한다.
        private const int WindowSortingOrder = 100;
        private const int RewardSortingOrder = 500;

        private UIWindowType _currentWindow;
        public UIWindowType CurrentWindow => _currentWindow;

        private string _currentSceneName;
        private Action _onCurrentWindowClosed;

        // prefab window는 전환 중 파괴하지 않고 SetActive로 재사용한다.
        private readonly Dictionary<UIWindowType, GameObject> _windowInstances = new Dictionary<UIWindowType, GameObject>();
        private readonly Dictionary<UIWindowType, Component> _windowPresenters = new Dictionary<UIWindowType, Component>();

        // 뒤로가기 history. 다른 window로 이동할 때 현재 route를 저장한다.
        private readonly Stack<UIWindowRoute> _routeStack = new Stack<UIWindowRoute>();
        private UIWindowRoute _currentRoute;
        private bool _hasCurrentRoute;

        private bool _isWindowBusy;
        private bool _pendingCloseAll;
        private bool _pendingCloseAllInvokeWindowClosed = true;
        private int _overlayGeneration; // 같은 전체 정리 요청이 들어왔는지 overlay async 흐름이 확인하는 버전 값.

        #region 동시성 제어

        /// <summary>
        /// 창 열기, 닫기, 전환, 전체 정리 작업을 시작할 수 있는지 확인한다.
        /// </summary>
        private bool TryBeginWindowOperation()
        {
            if (_isWindowBusy)
            {
                Log.LogMessage("[UIWindowService] 창 작업 중이라 요청을 무시합니다.", Log.LogLevel.Debug);
                return false;
            }

            _isWindowBusy = true;
            return true;
        }

        /// <summary>
        /// 창 작업 중 상태를 해제하고, 예약된 전체 정리가 있으면 이어서 실행한다.
        /// </summary>
        private async UniTask EndWindowOperationAsync()
        {
            _isWindowBusy = false;
            await FlushPendingCloseAllAsync();
        }

        /// <summary>
        /// 현재 창 작업이 끝난 뒤 전체 정리를 다시 시도하도록 예약한다.
        /// </summary>
        private void RequestPendingCloseAll(bool invokeWindowClosed)
        {
            if (!_pendingCloseAll)
            {
                _pendingCloseAllInvokeWindowClosed = invokeWindowClosed;
            }
            else
            {
                // teardown(false)이 한 번이라도 들어오면 callback 호출은 막는다.
                _pendingCloseAllInvokeWindowClosed = _pendingCloseAllInvokeWindowClosed && invokeWindowClosed;
            }

            _pendingCloseAll = true;
        }

        /// <summary>
        /// 예약된 전체 정리가 있으면 현재 작업이 끝난 뒤 실행한다.
        /// </summary>
        private async UniTask FlushPendingCloseAllAsync()
        {
            if (!_pendingCloseAll || _isWindowBusy)
                return;

            var invokeWindowClosed = _pendingCloseAllInvokeWindowClosed;

            _pendingCloseAll = false;
            _pendingCloseAllInvokeWindowClosed = true;

            await CloseAllAsync(invokeWindowClosed);
        }

        #endregion

        #region 열기 / 닫기 / 뒤로가기

        /// <summary>
        /// 지정한 UI window를 연다.
        /// </summary>
        /// <param name="windowType">열 UI 창 타입.</param>
        /// <param name="onWindowClosed">창이 닫힐 때 호출할 콜백.</param>
        /// <returns>창을 정상적으로 열었으면 true.</returns>
        public async UniTask<bool> OpenWindowAsync(UIWindowType windowType, Action onWindowClosed = null)
        {
            switch (windowType)
            {
                case UIWindowType.Shop:
                case UIWindowType.Gacha:
                case UIWindowType.MailBox:
                    return await OpenWindowInternalAsync(windowType, onWindowClosed);

                default:
                    Log.LogMessage($"[UIWindowService] 지원하지 않는 창 타입입니다: {windowType}", Log.LogLevel.Warning);
                    return false;
            }
        }

        /// <summary>
        /// 현재 UI 문맥을 뒤로가기 history에 남기고 Gacha route로 이동한다.
        /// Shop 구매 팝업처럼 사용자가 명시적으로 다른 기능 흐름으로 진입하는 경우에만 사용한다.
        /// </summary>
        /// <returns>Gacha route 이동에 성공하면 true.</returns>
        public async UniTask<bool> NavigateToGachaAsync()
        {
            return await NavigateToRouteWithOperationAsync(new UIWindowRoute(UIWindowType.Gacha), pushHistory: true);
        }

        /// <summary>
        /// 뒤로가기 입력을 처리한다.
        /// history가 있으면 이전 route로 복귀하고, 없으면 현재 UI 흐름을 종료한다.
        /// </summary>
        public async UniTask GoBackAsync()
        {
            if (!TryBeginWindowOperation())
                return;

            try
            {
                if (_routeStack.Count > 0)
                {
                    var previousRoute = _routeStack.Pop();
                    var moved = await NavigateToRouteAsync(previousRoute, pushHistory: false);
                    if (!moved)
                    {
                        // 복귀 실패 시 history를 잃지 않도록 되돌린다.
                        _routeStack.Push(previousRoute);
                        Log.LogMessage("[UIWindowService] 이전 route 복귀 실패", Log.LogLevel.Warning);
                    }

                    return;
                }

                var closed = await CloseCurrentWindowInternalAsync(invokeCallback: true);
                if (!closed)
                {
                    Log.LogMessage($"[UIWindowService] 뒤로가기 창 닫기 실패: {_currentWindow}", Log.LogLevel.Warning);
                }
            }
            catch (Exception e)
            {
                Log.LogMessage($"[UIWindowService] 뒤로가기 처리 실패: {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);
            }
            finally
            {
                await EndWindowOperationAsync();
            }
        }

        /// <summary>
        /// 외부 입력으로 들어온 현재 창 닫기 요청을 처리한다.
        /// Reward 닫힘 후 창 닫기 같은 외부 경로는 창 작업 busy 상태를 확인해야 한다.
        /// </summary>
        public async UniTask CloseCurrentWindowAsync()
        {
            if (!TryBeginWindowOperation())
                return;

            try
            {
                var closed = await CloseCurrentWindowInternalAsync(invokeCallback: true);
                if (!closed)
                {
                    Log.LogMessage($"[UIWindowService] 창 닫기 실패: {_currentWindow}", Log.LogLevel.Warning);
                }
            }
            finally
            {
                await EndWindowOperationAsync();
            }
        }

        /// <summary>
        /// 열린 window와 overlay를 모두 닫는다.
        /// 사용자 닫기 흐름에서는 창 닫힘 콜백을 호출하고, 씬 파괴/로그아웃 teardown에서는 호출하지 않는다.
        /// </summary>
        /// <param name="invokeWindowClosed">현재 window의 닫힘 콜백을 호출할지 여부.</param>
        public async UniTask CloseAllAsync(bool invokeWindowClosed = true)
        {
            if (!TryBeginWindowOperation())
            {
                RequestPendingCloseAll(invokeWindowClosed);
                Log.LogMessage("[UIWindowService] 창 작업 중 CloseAllAsync 요청이 예약되었습니다.", Log.LogLevel.Warning);
                return;
            }

            _overlayGeneration++;

            try
            {
                if (!CloseRewardOnly())
                {
                    Log.LogMessage("[UIWindowService] Reward overlay 정리 실패", Log.LogLevel.Warning);
                }

                await CloseCurrentWindowInternalAsync(invokeCallback: invokeWindowClosed);

                // 전체 UI 정리에서는 전환용으로 cache하던 window prefab도 해제한다.
                ReleaseCachedWindowInstances();
            }
            catch (Exception e)
            {
                Log.LogMessage($"[UIWindowService] 전체 정리 실패: {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);
            }
            finally
            {
                await EndWindowOperationAsync();
            }
        }

        /// <summary>
        /// 이미 창 작업 권한을 확보한 흐름 안에서 현재 창을 닫는다.
        /// </summary>
        /// <param name="invokeCallback">창 닫힘 콜백을 호출할지 여부.</param>
        private async UniTask<bool> CloseCurrentWindowInternalAsync(bool invokeCallback)
        {
            if (_currentWindow == UIWindowType.None)
                return true;

            var onWindowClosed = _onCurrentWindowClosed;
            var closed = await CloseWindowAsync();
            if (!closed)
                return false;

            if (invokeCallback)
            {
                onWindowClosed?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// 현재 window와 연결된 공용 UI를 닫는다.
        /// prefab window는 비활성화만 하고, scene window는 additive scene을 언로드한다.
        /// </summary>
        private async UniTask<bool> CloseWindowAsync()
        {
            if (_currentWindow == UIWindowType.None)
            {
                return true;
            }

            var windowType = _currentWindow;

            if (IsPrefabWindow(windowType))
            {
                if (!HidePrefabWindowInstance(windowType))
                {
                    return false;
                }
            }
            else
            {
                if (!await TryUnloadTrackedSceneAsync(_currentSceneName, "WindowScene"))
                {
                    return false;
                }
            }

            _currentWindow = UIWindowType.None;
            _currentSceneName = null;
            ClearCurrentRoute(clearStack: true);
            _onCurrentWindowClosed = null;

            // 중략: TopBar / OutGameNav 숨김 처리
            return true;
        }

        /// <summary>
        /// OutGameNav 내부 전환처럼 최종 복귀 콜백을 유지해야 하는 경로에서 현재 window만 닫는다.
        /// </summary>
        private async UniTask<bool> CloseWindowOnlyAsync()
        {
            if (_currentWindow == UIWindowType.None)
            {
                return true;
            }

            var windowType = _currentWindow;

            if (IsPrefabWindow(windowType))
            {
                if (!HidePrefabWindowInstance(windowType))
                {
                    return false;
                }
            }
            else
            {
                if (!await TryUnloadTrackedSceneAsync(_currentSceneName, "WindowSceneOnly"))
                    return false;
            }

            _currentWindow = UIWindowType.None;
            _currentSceneName = null;
            ClearCurrentRoute(clearStack: false);

            return true;
        }

        #endregion

        #region window 준비 (scene / prefab 공용 경로)

        /// <summary>
        /// window를 연다. prefab window와 additive scene window 모두 이 경로를 사용한다.
        /// </summary>
        private async UniTask<bool> OpenWindowInternalAsync(UIWindowType windowType, Action onWindowClosed)
        {
            if (!TryBeginWindowOperation())
            {
                return false;
            }

            try
            {
                if (!await CloseCurrentWindowInternalAsync(invokeCallback: true))
                {
                    return false;
                }

                if (!await OpenPreparedWindowAsync(windowType))
                {
                    return false;
                }

                _onCurrentWindowClosed = onWindowClosed;
                return true;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[UIWindowService] window 열기 실패: {windowType}, {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);
                await CloseWindowAsync();
                return false;
            }
            finally
            {
                await EndWindowOperationAsync();
            }
        }

        /// <summary>
        /// window 로드, Presenter 준비, 현재 창 상태 저장을 한 번에 처리한다.
        /// </summary>
        private async UniTask<bool> OpenPreparedWindowAsync(UIWindowType windowType)
        {
            if (IsPrefabWindow(windowType))
            {
                return await OpenPreparedPrefabWindowAsync(windowType);
            }

            return await OpenPreparedSceneWindowAsync(windowType);
        }

        /// <summary>
        /// additive scene 기반 window를 준비하고 현재 창 상태를 저장한다.
        /// MailBox처럼 아직 scene으로 유지하는 window가 이 경로를 사용한다.
        /// </summary>
        private async UniTask<bool> OpenPreparedSceneWindowAsync(UIWindowType windowType)
        {
            var sceneName = GetWindowSceneName(windowType);
            if (string.IsNullOrEmpty(sceneName))
            {
                Log.LogMessage($"[UIWindowService] 창 타입에 해당하는 씬 이름이 없습니다: {windowType}", Log.LogLevel.Warning);
                return false;
            }

            var loaded = await SceneLoader.Instance.TryLoadAdditiveAsync(sceneName);
            if (!loaded)
            {
                return false;
            }

            ApplySceneCanvasSorting(sceneName, WindowSortingOrder);

            try
            {
                // 중략: scene에서 Presenter를 찾아 RefreshAsync 실행, TopBar 표시 정책 적용.
                //       실패 시 방금 로드한 scene을 언로드해 반쯤 열린 상태를 남기지 않는다.

                _currentWindow = windowType;
                _currentSceneName = sceneName;
                SetCurrentRoute(GetDefaultRoute(windowType));
                return true;
            }
            catch
            {
                await TryUnloadTrackedSceneAsync(sceneName, "OpenPreparedSceneWindow exception rollback");
                throw;
            }
        }

        /// <summary>
        /// prefab cache 기반 window를 준비하고 현재 창 상태를 저장한다.
        /// Shop, Gacha는 이 경로에서 instance를 재사용한다.
        /// </summary>
        private async UniTask<bool> OpenPreparedPrefabWindowAsync(UIWindowType windowType)
        {
            GameObject instance = null;
            try
            {
                instance = await GetOrCreateWindowInstanceAsync(windowType);
                if (instance == null)
                {
                    return false;
                }

                // prefab window는 활성화된 Canvas 상태에서 Refresh해야 Scroll/Layout 계산이 안정적이다.
                instance.SetActive(true);
                ApplyOwnerCanvasSorting(instance.transform, WindowSortingOrder);

                if (!await PreparePrefabWindowAsync(windowType, instance))
                {
                    instance.SetActive(false);
                    return false;
                }

                HideOtherPrefabWindowInstances(windowType);

                _currentWindow = windowType;
                _currentSceneName = null;
                SetCurrentRoute(GetDefaultRoute(windowType));
                return true;
            }
            catch (Exception e)
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                }

                Log.LogMessage($"[UIWindowService] prefab window 준비 실패: {windowType}, {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// window prefab instance를 cache에서 가져오거나 처음 생성한다.
        /// Shop, Gacha는 전환 중 파괴하지 않고 SetActive로 재사용한다.
        /// </summary>
        private async UniTask<GameObject> GetOrCreateWindowInstanceAsync(UIWindowType windowType)
        {
            if (_windowInstances.TryGetValue(windowType, out var cached) && cached != null)
            {
                return cached;
            }

            var address = GetWindowPrefabAddress(windowType);
            if (string.IsNullOrEmpty(address))
            {
                Log.LogMessage($"[UIWindowService] window prefab address가 없습니다: {windowType}", Log.LogLevel.Warning);
                return null;
            }

            var instance = await AddressablePrefabService.InstantiateAsync(address, transform);
            if (instance == null)
            {
                Log.LogMessage($"[UIWindowService] window prefab 생성 실패: {windowType}, {address}", Log.LogLevel.Warning);
                return null;
            }

            instance.SetActive(false);
            _windowInstances[windowType] = instance;

            return instance;
        }

        /// <summary>
        /// prefab window instance에서 presenter를 찾고 Refresh를 실행한다.
        /// </summary>
        private async UniTask<bool> PreparePrefabWindowAsync(UIWindowType windowType, GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            switch (windowType)
            {
                case UIWindowType.Shop:
                    {
                        var presenter = GetOrFindWindowPresenter<ShopPresenter>(windowType, instance);
                        if (presenter == null)
                        {
                            Log.LogMessage("[UIWindowService] Shop prefab에서 ShopPresenter를 찾지 못했습니다.", Log.LogLevel.Error);
                            return false;
                        }

                        await presenter.RefreshAsync();
                        return true;
                    }

                // 중략: Gacha 등 다른 prefab window도 같은 패턴으로 presenter를 준비한다.

                default:
                    return false;
            }
        }

        /// <summary>
        /// window prefab instance에서 presenter를 찾고 cache한다.
        /// </summary>
        private T GetOrFindWindowPresenter<T>(UIWindowType windowType, GameObject instance) where T : Component
        {
            if (_windowPresenters.TryGetValue(windowType, out var cached) && cached is T typedPresenter && typedPresenter != null)
            {
                return typedPresenter;
            }

            var presenter = instance != null ? instance.GetComponentInChildren<T>(true) : null;
            if (presenter != null)
            {
                _windowPresenters[windowType] = presenter;
            }

            return presenter;
        }

        #endregion

        #region prefab window cache 관리

        /// <summary>
        /// cache된 prefab window instance를 비활성화한다.
        /// prefab window는 전환 중 파괴하지 않고 CloseAll 같은 전체 정리 때만 해제한다.
        /// </summary>
        private bool HidePrefabWindowInstance(UIWindowType windowType)
        {
            if (!_windowInstances.TryGetValue(windowType, out var instance) || instance == null)
            {
                return true;
            }

            instance.SetActive(false);
            return true;
        }

        /// <summary>
        /// 지정 window를 제외한 모든 cached prefab window instance를 비활성화한다.
        /// prefab window cache에서는 동시에 하나의 window만 화면에 표시되어야 한다.
        /// </summary>
        private void HideOtherPrefabWindowInstances(UIWindowType exceptWindowType)
        {
            foreach (var pair in _windowInstances)
            {
                if (pair.Key == exceptWindowType)
                {
                    continue;
                }

                var instance = pair.Value;
                if (instance != null)
                {
                    instance.SetActive(false);
                }
            }
        }

        /// <summary>
        /// cache된 모든 prefab window instance를 Addressables에서 해제한다.
        /// 로그아웃, 전체 UI 정리처럼 window cache를 유지할 이유가 없는 흐름에서만 호출한다.
        /// </summary>
        private void ReleaseCachedWindowInstances()
        {
            foreach (var pair in _windowInstances)
            {
                var instance = pair.Value;
                if (instance == null)
                {
                    continue;
                }

                if (!AddressablePrefabService.ReleaseInstance(instance))
                {
                    Log.LogMessage($"[UIWindowService] prefab window 해제 실패: {pair.Key}", Log.LogLevel.Warning);
                }
            }

            _windowInstances.Clear();
            _windowPresenters.Clear();
        }

        #endregion

        #region route stack

        /// <summary>
        /// 지정한 route로 이동한다.
        /// 다른 window로 이동하면 현재 route를 history에 저장하고,
        /// 같은 Shop 안의 section 이동은 현재 window를 유지한다.
        /// </summary>
        /// <param name="route">이동할 route.</param>
        /// <param name="pushHistory">현재 route를 뒤로가기 history에 저장하면 true.</param>
        private async UniTask<bool> NavigateToRouteAsync(UIWindowRoute route, bool pushHistory)
        {
            if (_hasCurrentRoute && _currentRoute.Equals(route))
            {
                return true;
            }

            var previousRoute = _currentRoute;
            var hadPreviousRoute = _hasCurrentRoute;

            bool moved;
            if (route.windowType == UIWindowType.Shop && route.hasShopSection)
            {
                moved = await OpenShopSectionAsync(route.shopSectionType);
            }
            else
            {
                moved = await SwitchWindowInternalAsync(route.windowType);
            }

            if (moved && pushHistory && hadPreviousRoute)
            {
                _routeStack.Push(previousRoute);
            }

            return moved;
        }

        /// <summary>
        /// 외부 입력으로 들어온 route 이동 요청을 처리한다.
        /// 창 작업 busy 상태를 확보한 뒤 내부 route 이동을 실행한다.
        /// </summary>
        private async UniTask<bool> NavigateToRouteWithOperationAsync(UIWindowRoute route, bool pushHistory)
        {
            if (!TryBeginWindowOperation())
                return false;

            try
            {
                return await NavigateToRouteAsync(route, pushHistory);
            }
            finally
            {
                await EndWindowOperationAsync();
            }
        }

        /// <summary>
        /// 이미 창 작업 권한을 확보한 흐름 안에서 window를 전환한다.
        /// 전환 실패 시 이전 window 복구를 시도한다.
        /// </summary>
        private async UniTask<bool> SwitchWindowInternalAsync(UIWindowType windowType)
        {
            if (_currentWindow == windowType)
                return true;

            var prevWindow = _currentWindow;

            if (!await CloseWindowOnlyAsync())
                return false;

            var opened = await OpenPreparedWindowAsync(windowType);
            if (opened)
            {
                return true;
            }

            Log.LogMessage($"[UIWindowService] 창 전환 실패: {prevWindow} -> {windowType}", Log.LogLevel.Warning);

            if (prevWindow != UIWindowType.None && await OpenPreparedWindowAsync(prevWindow))
            {
                Log.LogMessage($"[UIWindowService] 이전 창 복구 성공: {prevWindow}", Log.LogLevel.Warning);
            }

            return false;
        }

        /// <summary>
        /// Shop window의 특정 section을 연다.
        /// 이미 Shop이 열려 있으면 window를 유지한 채 section만 전환한다.
        /// </summary>
        private async UniTask<bool> OpenShopSectionAsync(ShopSectionType sectionType)
        {
            if (_currentWindow != UIWindowType.Shop)
            {
                if (!await SwitchWindowInternalAsync(UIWindowType.Shop))
                {
                    return false;
                }
            }

            var presenter = GetCachedWindowPresenter<ShopPresenter>(UIWindowType.Shop);
            if (presenter == null)
            {
                return false;
            }

            presenter.ShowSection(sectionType);
            SetCurrentRoute(new UIWindowRoute(sectionType));
            return true;
        }

        /// <summary>
        /// cache된 window presenter를 반환한다. 없으면 null.
        /// </summary>
        private T GetCachedWindowPresenter<T>(UIWindowType windowType) where T : Component
        {
            return _windowPresenters.TryGetValue(windowType, out var cached) && cached is T typed ? typed : null;
        }

        /// <summary>
        /// window 이동 단위를 표현하는 route.
        /// window 타입만으로 부족한 Shop section 같은 내부 위치도 함께 기록해 뒤로가기 복귀에 사용한다.
        /// </summary>
        private readonly struct UIWindowRoute
        {
            public readonly UIWindowType windowType;
            public readonly ShopSectionType shopSectionType;
            public readonly bool hasShopSection;

            /// <summary>
            /// 일반 window route를 생성한다.
            /// </summary>
            public UIWindowRoute(UIWindowType type)
            {
                windowType = type;
                shopSectionType = default;
                hasShopSection = false;
            }

            /// <summary>
            /// Shop section route를 생성한다.
            /// </summary>
            public UIWindowRoute(ShopSectionType sectionType)
            {
                windowType = UIWindowType.Shop;
                shopSectionType = sectionType;
                hasShopSection = true;
            }

            public bool Equals(UIWindowRoute other)
            {
                return windowType == other.windowType &&
                       shopSectionType == other.shopSectionType &&
                       hasShopSection == other.hasShopSection;
            }
        }

        /// <summary>
        /// window 타입에 대응하는 기본 route를 만든다.
        /// Shop은 기본 진입 시 Featured section으로 취급한다.
        /// </summary>
        private static UIWindowRoute GetDefaultRoute(UIWindowType windowType)
        {
            switch (windowType)
            {
                case UIWindowType.Shop:
                    return new UIWindowRoute(ShopSectionType.Featured);

                default:
                    return new UIWindowRoute(windowType);
            }
        }

        /// <summary>
        /// 현재 route 상태를 갱신한다.
        /// </summary>
        private void SetCurrentRoute(UIWindowRoute route)
        {
            _currentRoute = route;
            _hasCurrentRoute = true;
        }

        /// <summary>
        /// 현재 route 상태를 초기화한다.
        /// </summary>
        private void ClearCurrentRoute(bool clearStack)
        {
            _currentRoute = default;
            _hasCurrentRoute = false;

            if (clearStack)
            {
                _routeStack.Clear();
            }
        }

        #endregion

        #region window 타입 매핑

        /// <summary>
        /// prefab cache 방식으로 관리할 window 타입인지 확인한다.
        /// Shop, Gacha는 전환이 잦아 additive scene 대신 prefab instance로 유지한다.
        /// </summary>
        private static bool IsPrefabWindow(UIWindowType windowType)
        {
            switch (windowType)
            {
                case UIWindowType.Shop:
                case UIWindowType.Gacha:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// scene window 타입에 대응하는 additive scene 이름을 반환한다.
        /// </summary>
        private static string GetWindowSceneName(UIWindowType windowType)
        {
            switch (windowType)
            {
                case UIWindowType.MailBox:
                    return SceneNames.MailBox;

                default:
                    return null;
            }
        }

        /// <summary>
        /// prefab window 타입에 대응하는 Addressables prefab address를 반환한다.
        /// </summary>
        private static string GetWindowPrefabAddress(UIWindowType windowType)
        {
            switch (windowType)
            {
                case UIWindowType.Shop:
                    return UIPrefabAddress.ShopWindow;

                case UIWindowType.Gacha:
                    return UIPrefabAddress.GachaWindow;

                default:
                    return null;
            }
        }

        /// <summary>
        /// 추적 중인 additive scene 언로드를 시도하고 실패 로그를 남긴다.
        /// </summary>
        private static async UniTask<bool> TryUnloadTrackedSceneAsync(string sceneName, string context)
        {
            if (string.IsNullOrEmpty(sceneName))
                return true;

            var unloaded = await SceneLoader.Instance.TryUnloadAdditiveAsync(sceneName);
            if (!unloaded)
            {
                Log.LogMessage($"[UIWindowService] {context} 언로드 실패: {sceneName}", Log.LogLevel.Error);
            }

            return unloaded;
        }

        #endregion
    }
}
