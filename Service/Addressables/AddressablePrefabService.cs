using System;
using Cysharp.Threading.Tasks;
using SampleClient.Utils;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SampleClient.Service.Addressables
{
    /// <summary>
    /// Addressables prefab instance 생성과 해제를 담당한다.
    /// UI window prefab은 전환 중 재사용할 수 있으므로 생성/해제 정책은 호출자가 결정한다.
    /// </summary>
    public static class AddressablePrefabService
    {
        /// <summary>
        /// 지정 address의 prefab instance를 생성한다.
        /// </summary>
        /// <param name="address">Addressables prefab address.</param>
        /// <param name="parent">생성된 instance를 붙일 부모 Transform.</param>
        /// <returns>생성된 prefab instance. 실패하면 null.</returns>
        public static async UniTask<GameObject> InstantiateAsync(string address, Transform parent)
        {
            if (string.IsNullOrEmpty(address))
            {
                Log.LogMessage("[AddressablePrefabService] prefab address가 비어 있습니다.", Log.LogLevel.Warning);
                return null;
            }

            try
            {
                var handle = UnityEngine.AddressableAssets.Addressables.InstantiateAsync(address, parent);
                var instance = await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded || instance == null)
                {
                    Log.LogMessage($"[AddressablePrefabService] prefab 생성 실패: {address}", Log.LogLevel.Warning);

                    if (handle.IsValid())
                    {
                        UnityEngine.AddressableAssets.Addressables.Release(handle);
                    }

                    return null;
                }

                return instance;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[AddressablePrefabService] prefab 생성 예외: {address}, {e.GetType().Name}: {e.Message}", Log.LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Addressables로 생성한 prefab instance를 해제한다.
        /// </summary>
        /// <param name="instance">해제할 prefab instance.</param>
        /// <returns>해제 요청이 성공하면 true, 대상이 없거나 실패하면 false.</returns>
        public static bool ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            return UnityEngine.AddressableAssets.Addressables.ReleaseInstance(instance);
        }
    }

    /// <summary>
    /// UI prefab Addressables address를 한 곳에서 관리한다.
    /// 실제 Addressables 등록명과 반드시 일치해야 한다.
    /// 샘플에서는 실제 에셋 경로 대신 일반화된 placeholder 경로를 사용합니다.
    /// </summary>
    public static class UIPrefabAddress
    {
        public const string ShopWindow = "SampleAssets/UI/Window/Shop_Canvas.prefab";
        public const string GachaWindow = "SampleAssets/UI/Window/Gacha_Canvas.prefab";

        public const string RewardOverlay = "SampleAssets/UI/Overlay/Reward_Canvas.prefab";
        // 중략: 동일한 방식으로 관리되는 다른 window/overlay prefab address
    }
}
