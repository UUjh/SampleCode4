using System;
using Cysharp.Threading.Tasks;
using SampleClient.Core;
using SampleClient.Scene;
using SampleClient.UI.Store;
using SampleClient.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleClient.UI
{
    /// <summary>
    /// MainScene 위에 Additive UI Scene을 열고 닫는 샘플 서비스.
    /// 공개 샘플에서는 Store 창만 남기고, 동일 패턴의 다른 콘텐츠 UI는 중략했습니다.
    /// </summary>
    public class UIWindowService : PersistentSingleton<UIWindowService>
    {
        private UIWindowType _currentWindow = UIWindowType.None;
        private string _currentSceneName;
        private Action _onCurrentWindowClosed;
        private bool _isWindowLoading;

        /// <summary>
        /// 현재 열려 있는 UI 창 타입.
        /// </summary>
        public UIWindowType CurrentWindow => _currentWindow;

        /// <summary>
        /// 지정한 UI 창 Scene을 Additive로 로드하고 Presenter를 초기화한다.
        /// </summary>
        /// <param name="windowType">열 UI 창 타입.</param>
        /// <param name="onWindowClosed">창이 닫힐 때 호출할 콜백.</param>
        /// <returns>창을 정상적으로 열었으면 true.</returns>
        public async UniTask<bool> OpenWindowAsync(UIWindowType windowType, Action onWindowClosed = null)
        {
            if (_isWindowLoading)
            {
                return false;
            }

            var sceneName = ResolveSceneName(windowType);
            if (string.IsNullOrEmpty(sceneName))
            {
                Log.LogMessage($"[UIWindowService] 지원하지 않는 창 타입입니다: {windowType}", Log.LogLevel.Warning);
                return false;
            }

            _isWindowLoading = true;
            try
            {
                await CloseCurrentWindowAsync();

                var loaded = await SceneLoader.Instance.TryLoadAdditiveAsync(sceneName);
                if (!loaded)
                {
                    return false;
                }

                _currentWindow = windowType;
                _currentSceneName = sceneName;
                _onCurrentWindowClosed = onWindowClosed;

                BindPresenter(sceneName, windowType);
                return true;
            }
            finally
            {
                _isWindowLoading = false;
            }
        }

        /// <summary>
        /// 현재 콜백 상태를 유지한 채 다른 UI 창으로 전환한다.
        /// </summary>
        /// <param name="windowType">전환할 UI 창 타입.</param>
        /// <returns>전환에 성공했으면 true.</returns>
        public async UniTask<bool> SwitchWindowAsync(UIWindowType windowType)
        {
            return await OpenWindowAsync(windowType, _onCurrentWindowClosed);
        }

        /// <summary>
        /// 현재 열려 있는 Additive UI Scene을 닫고 종료 콜백을 호출한다.
        /// </summary>
        public async UniTask CloseCurrentWindowAsync()
        {
            if (string.IsNullOrEmpty(_currentSceneName))
            {
                return;
            }

            await SceneLoader.Instance.TryUnloadAdditiveAsync(_currentSceneName);
            _currentWindow = UIWindowType.None;
            _currentSceneName = null;
            _onCurrentWindowClosed?.Invoke();
            _onCurrentWindowClosed = null;
        }

        /// <summary>
        /// UI 창 타입에 대응하는 Scene 이름을 반환한다.
        /// </summary>
        /// <param name="windowType">UI 창 타입.</param>
        /// <returns>Scene 이름. 지원하지 않는 타입이면 null.</returns>
        private static string ResolveSceneName(UIWindowType windowType)
        {
            switch (windowType)
            {
                case UIWindowType.Store:
                    return SceneNames.Store;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 로드된 UI Scene에서 Presenter를 찾아 초기화한다.
        /// </summary>
        /// <param name="sceneName">Presenter를 찾을 Scene 이름.</param>
        /// <param name="windowType">초기화할 UI 창 타입.</param>
        private static void BindPresenter(string sceneName, UIWindowType windowType)
        {
            if (windowType != UIWindowType.Store)
            {
                return;
            }

            var presenter = FindSceneComponent<StorePresenter>(sceneName);
            if (presenter == null)
            {
                Log.LogMessage("[UIWindowService] StorePresenter를 찾지 못했습니다.", Log.LogLevel.Error);
                return;
            }

            presenter.Initialize().Forget();
        }

        /// <summary>
        /// 지정한 Scene의 루트 오브젝트에서 컴포넌트를 검색한다.
        /// </summary>
        /// <typeparam name="T">검색할 컴포넌트 타입.</typeparam>
        /// <param name="sceneName">검색할 Scene 이름.</param>
        /// <returns>찾은 컴포넌트. 없으면 null.</returns>
        private static T FindSceneComponent<T>(string sceneName) where T : Component
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var component = roots[i] != null ? roots[i].GetComponentInChildren<T>(true) : null;
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}

