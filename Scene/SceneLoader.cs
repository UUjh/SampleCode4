using System;
using Cysharp.Threading.Tasks;
using SampleClient.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using SampleClient.Core;

namespace SampleClient.Scene
{
    [DefaultExecutionOrder(-100)]
    public class SceneLoader : PersistentSingleton<SceneLoader>
    {
        private bool _mainLoaded;

        /// <summary>
        /// 지정 씬이 현재 로드되어 있는지 확인한다.
        /// </summary>
        private static bool IsSceneLoaded(string sceneName)
        {
            var s = SceneManager.GetSceneByName(sceneName);
            return s.IsValid() && s.isLoaded;
        }

        /// <summary>
        /// MainScene 로드 상태 캐시를 현재 씬 상태 기준으로 보정한다.
        /// </summary>
        private void RefreshMainLoadedState()
        {
            if (!_mainLoaded && IsSceneLoaded(SceneNames.Main))
            {
                _mainLoaded = true;
            }
        }

        /// <summary>
        /// Boot 직후: 이미 로그인되어 있으면 Main, 아니면 LogIn.
        /// </summary>
        public async UniTask BootAsync(bool isSignedIn)
        {
            try
            {
                if (isSignedIn)
                {
                    await LoadMainAsync();
                }
                else
                {
                    await LoadLogInAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] Boot 실패: {e}");
            }
        }

        /// <summary>
        /// 로그인 성공 후 메인 전환 
        /// MainScene을 로딩 UI와 함께 로드한다.
        /// </summary>
        public async UniTask LoadMainAsync()
        {
            try
            {
                // MainScene은 진입 직후 MainFlow가 bootstrap을 이어서 수행하므로,
                // 씬 로드 완료 시점이 아니라 bootstrap 완료 시점에 로딩을 닫는다.
                var ok = await LoadSingleWithLoadingAsync(SceneNames.Main, false);
                _mainLoaded = ok;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] Main 로드 실패: {e}");
                _mainLoaded = false;
            }
        }

        /// <summary>
        /// 로그인 씬으로 전환 
        /// LoginScene을 로딩 UI와 함께 로드한다.
        /// </summary>
        public async UniTask LoadLogInAsync()
        {
            try
            {
                var ok = await LoadSingleWithLoadingAsync(SceneNames.LogIn);
                _mainLoaded = false;
                if (!ok)
                {
                    Debug.LogError("[SceneLoader] LogIn 씬 로드에 실패했습니다. 씬 이름·Build Settings를 확인하세요.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] LogIn 로드 실패: {e}");
                _mainLoaded = false;
            }
        }

        /// <summary>
        /// MainScene이 로드된 상태에서 additive 씬을 로드한다.
        /// </summary>
        public async UniTask<bool> TryLoadAdditiveAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            RefreshMainLoadedState();
            if (!_mainLoaded || !IsSceneLoaded(SceneNames.Main))
            {
                Debug.LogWarning("[SceneLoader] Additive 로드는 Main이 로드된 뒤에만 가능합니다.");
                return false;
            }

            if (IsSceneLoaded(sceneName))
            {
                return true;
            }

            try
            {
                return await LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] Additive 로드 실패: {e}");
                return false;
            }
        }

        /// <summary>
        /// MainScene을 유지한 채 additive 씬을 언로드한다.
        /// </summary>
        public async UniTask<bool> TryUnloadAdditiveAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            RefreshMainLoadedState();
            if (!_mainLoaded)
            {
                Debug.LogWarning("[SceneLoader] Additive 언로드는 Main이 로드된 뒤에만 가능합니다.");
                return false;
            }

            if (sceneName == SceneNames.Main)
            {
                Debug.LogError("[SceneLoader] Main 씬은 TryUnloadAdditive로 내리면 안됨.");
                return false;
            }

            if (!IsSceneLoaded(sceneName))
            {
                return true;
            }

            try
            {
                var op = SceneManager.UnloadSceneAsync(sceneName);
                if (op == null)
                {
                    Debug.LogError($"[SceneLoader] 언로드 실패(씬 없음): {sceneName}");
                    return false;
                }

                while (!op.isDone)
                {
                    await UniTask.Yield();
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] Additive 언로드 실패: {e}");
                return false;
            }
        }

        /// <summary>
        /// 로딩 UI를 표시하고 지정 씬으로 단일 전환한다.
        /// </summary>
        public async UniTask<bool> LoadSingleWithLoadingAsync(string sceneName)
        {
            return await LoadSingleWithLoadingAsync(sceneName, true);
        }

        /// <summary>
        /// 기존 씬을 정리하면서 지정 씬을 로드하고 로딩 UI 종료 여부를 제어한다.
        /// </summary>
        private async UniTask<bool> LoadSingleWithLoadingAsync(string sceneName, bool hideLoadingOnComplete)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return false;
            }

            try
            {
                var unloadCount = SceneManager.sceneCount;
                var unloadScenes = new string[unloadCount];
                var count = 0;

                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.IsValid() &&
                        scene.isLoaded &&
                        scene.name != sceneName)
                    {
                        unloadScenes[count] = scene.name;
                        count++;
                    }
                }

                var ok = await LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (!ok)
                {
                    return false;
                }

                var targetScene = SceneManager.GetSceneByName(sceneName);
                if (targetScene.IsValid() && targetScene.isLoaded)
                {
                    SceneManager.SetActiveScene(targetScene);
                }

                for (var i = 0; i < count; i++)
                {
                    var unloadSceneName = unloadScenes[i];
                    if (!string.IsNullOrEmpty(unloadSceneName) && IsSceneLoaded(unloadSceneName))
                    {
                        var op = SceneManager.UnloadSceneAsync(unloadSceneName);
                        if (op == null)
                        {
                            Debug.LogError($"[SceneLoader] 전환 중 언로드 실패: {unloadSceneName}");
                            return false;
                        }

                        while (!op.isDone)
                        {
                            await UniTask.Yield();
                        }
                    }
                }

                _mainLoaded = sceneName == SceneNames.Main && IsSceneLoaded(SceneNames.Main);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneLoader] 씬 전환 실패: {e}");
                return false;
            }
            finally
            {
                if (hideLoadingOnComplete)
                {
                }
            }
        }

        /// <summary>
        /// 진행률 콜백 없이 Unity 씬 로드를 수행한다.
        /// </summary>
        private async UniTask<bool> LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            return await LoadSceneAsync(sceneName, mode, null);
        }

        /// <summary>
        /// Unity 씬 로드를 수행하고 진행률 콜백을 호출한다.
        /// </summary>
        private async UniTask<bool> LoadSceneAsync(string sceneName, LoadSceneMode mode, Action<float> onProgress)
        {
            if (IsSceneLoaded(sceneName) && mode == LoadSceneMode.Additive)
            {
                onProgress?.Invoke(1f);
                return true;
            }

            if (mode == LoadSceneMode.Single && IsSceneLoaded(sceneName))
            {
                if (sceneName == SceneNames.Main)
                {
                    _mainLoaded = true;
                }
                else
                {
                    _mainLoaded = false;
                }

                return true;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] LoadSceneAsync가 null입니다. 빌드 설정에 '{sceneName}'이 있는지 확인하세요.");
                return false;
            }

            while (!op.isDone)
            {
                onProgress?.Invoke(op.progress);
                await UniTask.Yield();
            }

            onProgress?.Invoke(1f);

            if (mode == LoadSceneMode.Single)
            {
                _mainLoaded = sceneName == SceneNames.Main && IsSceneLoaded(SceneNames.Main);
            }

            return true;
        }
    }
}





