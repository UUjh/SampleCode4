using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;
using SampleClient.Utils;

namespace SampleClient.Service.Bootstrap
{
    /// <summary>
    /// Main 진입 전에 유저 상태, 상점/가챠 상태, 로컬 카탈로그를 준비하는 샘플 서비스.
    /// UI는 결과를 받아 Shop/Gacha 패널을 구성한다.
    /// 샘플에서는 메일, 퀴즈, 일일보상 등 같은 패턴으로 병렬 로드되는 모듈은 중략했습니다.
    /// </summary>
    public static class GameBootstrapService
    {
        // MainFlow와 각 Presenter가 같은 시점에 LoadAsync를 호출해도 bootstrap 요청은 한 번만 실행.
        private static readonly object LoadLock = new object();
        private static UniTaskCompletionSource<GameBootstrapData> _loadingSource;
        // force 갱신 중인지 기록해 일반 로딩 대기와 catalog mismatch 복구 로딩을 구분.
        private static bool _loadingForceRefresh;

        /// <summary>
        /// Main에서 시작할 때 서버/로컬에서 받은 데이터 저장.
        /// Shop/Gacha 패널은 이 데이터로 초기화하고, 구매/뽑기 후에는 필요한 부분만 갱신한다.
        /// </summary>
        public static GameBootstrapData Data { get; private set; }

        /// <summary>
        /// 로그아웃 또는 세션 초기화 시 현재 런타임 캐시를 비운다.
        /// </summary>
        public static void Clear()
        {
            Data = null;
            // 중략: Mail/Quiz/Equipment 등 세션 캐시를 가진 서비스 정리

            lock (LoadLock)
            {
                _loadingSource = null;
                _loadingForceRefresh = false;
            }
        }

        /// <summary>
        /// Bootstrap Data를 로드한다.
        /// 이미 로드된 데이터가 있으면 재사용하고, 동시에 호출된 로딩은 하나의 작업으로 합친다.
        /// forceCatalogRefresh가 true면 기존 캐시를 무시하고 다시 로드한다.
        /// </summary>
        public static async UniTask<GameBootstrapData> LoadAsync(bool forceCatalogRefresh = false, Action<float> onProgress = null)
        {
            if (!forceCatalogRefresh && Data != null)
            {
                return Data;
            }

            while (true)
            {
                UniTask<GameBootstrapData> loadingTask = default;
                UniTaskCompletionSource<GameBootstrapData> loadingSource = null;
                bool loadingForceRefresh = false;

                lock (LoadLock)
                {
                    if (_loadingSource == null)
                    {
                        _loadingSource = new UniTaskCompletionSource<GameBootstrapData>();
                        _loadingForceRefresh = forceCatalogRefresh;
                        loadingSource = _loadingSource;
                    }
                    else
                    {
                        loadingTask = _loadingSource.Task;
                        loadingForceRefresh = _loadingForceRefresh;
                    }
                }

                if (loadingSource != null)
                {
                    return await LoadAndPublishAsync(forceCatalogRefresh, loadingSource, onProgress);
                }

                // 이미 로딩 중이면 MainFlow/Presenter의 중복 bootstrap 요청을 막고 같은 Task를 기다린다.
                if (!forceCatalogRefresh || loadingForceRefresh)
                {
                    return await loadingTask;
                }

                // 일반 로딩 중에 강제 갱신 요청이 들어온 경우에는 기존 로딩 완료 후 새 force load를 시작한다.
                // 기존 로딩이 실패해도 force 요청은 복구 목적일 수 있으므로 새 로딩으로 진입한다.
                try
                {
                    await loadingTask;
                }
                catch (Exception e)
                {
                    Log.LogMessage($"[GameBootstrapService] 기존 bootstrap 실패 후 강제 갱신을 다시 시도합니다. {e.Message}", Log.LogLevel.Warning);
                }
            }
        }

        /// <summary>
        /// 실제 Bootstrap 로딩을 실행하고 대기 중인 호출자에게 결과를 전달한다.
        /// </summary>
        private static async UniTask<GameBootstrapData> LoadAndPublishAsync(bool forceCatalogRefresh,
            UniTaskCompletionSource<GameBootstrapData> loadingSource, Action<float> onProgress)
        {
            try
            {
                var data = await LoadBootstrapAsync(forceCatalogRefresh, onProgress);
                loadingSource.TrySetResult(data);
                return data;
            }
            catch (Exception e)
            {
                loadingSource.TrySetException(e);
                throw;
            }
            finally
            {
                lock (LoadLock)
                {
                    if (_loadingSource == loadingSource)
                    {
                        _loadingSource = null;
                        _loadingForceRefresh = false;
                    }
                }
            }
        }

        /// <summary>
        /// 서버 API와 카탈로그 캐시를 사용해 전체 Bootstrap 데이터를 구성한다.
        /// </summary>
        private static async UniTask<GameBootstrapData> LoadBootstrapAsync(bool forceCatalogRefresh, Action<float> onProgress = null)
        {
            if (!FirebaseService.IsApiReady)
            {
                // 중략: 에디터 전용이면 LocalBootstrapService 가짜 데이터로 fallback 후 진입, 그 외에는 아래 throw
                throw new InvalidOperationException("[GameBootstrapService] Firebase API가 준비되지 않아 Bootstrap을 로드할 수 없습니다.");
            }

            try
            {
                onProgress?.Invoke(0.35f);

                // 서로 의존하지 않는 bootstrap/read 작업은 동시에 시작한다.
                // shop/gacha는 catalogVersion 의존성이 있으므로 각 helper 안에서 catalog -> server bootstrap 순서를 유지한다.
                var userBootstrapTask = RequestUserBootstrapAsync();
                var shopDataTask = ShopBootstrapService.LoadAsync(forceCatalogRefresh);
                var gachaDataTask = GachaBootstrapService.LoadAsync(forceCatalogRefresh);
                var itemCatalogTask = CatalogService.LoadItemAsync(forceCatalogRefresh);
                // 중략: 메일함, 템플릿 메일, 퀴즈, 일일보상 병렬 로드

                // 병렬 작업을 한 번에 await해 이미 시작된 작업들의 완료/예외를 함께 회수한다.
                // 순차 await 중 앞 작업이 실패해 뒤 작업 예외가 방치되는 상황을 줄이기 위해 사용.
                // 이 단계의 서버 요청 중 하나라도 실패하면 부분 데이터로 Main에 진입하지 않고 bootstrap 전체를 실패 처리한다.
                var (userBootstrap, shopData, gachaData, itemCatalog) =
                    await UniTask.WhenAll(
                        userBootstrapTask,
                        shopDataTask,
                        gachaDataTask,
                        itemCatalogTask
                    );

                onProgress?.Invoke(0.8f);

                Data = new GameBootstrapData
                {
                    userProfile = userBootstrap != null ? userBootstrap.profile : null,
                    wallet = NormalizeWallet(userBootstrap != null ? userBootstrap.wallet : null),
                    inventory = NormalizeInventory(userBootstrap != null ? userBootstrap.inventory : null),
                    currentEquipment = NormalizeCurrentEquipment(userBootstrap != null ? userBootstrap.currentEquipment : null),
                    shopCatalog = shopData.catalog,
                    shop = shopData.bootstrap,
                    gachaCatalog = gachaData.catalog,
                    gacha = gachaData.bootstrap,
                    item = itemCatalog
                };

                onProgress?.Invoke(0.9f);
            }
            catch (Exception e)
            {
                Log.LogMessage($"[GameBootstrapService] Bootstrap 실패, error={e.Message}", Log.LogLevel.Error);
                throw;
            }

            Log.LogMessage("[GameBootstrapService] Bootstrap 완료.", Log.LogLevel.Debug);
            return Data;
        }

        /// <summary>
        /// 서버 응답의 재화 상태를 유저 Wallet 캐시에 반영한다.
        /// remainingBalance는 일부 재화만 내려올 수 있으므로 기존 지갑을 교체하지 않고 키 단위로 갱신한다.
        /// </summary>
        /// <param name="currencies">서버가 반환한 변경 재화 잔액.</param>
        internal static void ApplyCurrencies(Dictionary<string, JToken> currencies)
        {
            if (Data == null || currencies == null)
            {
                return;
            }

            if (Data.wallet == null)
            {
                Data.wallet = new UserWalletResponse();
            }

            if (Data.wallet.currencies == null)
            {
                Data.wallet.currencies = new Dictionary<string, JToken>();
            }

            foreach (var pair in currencies)
            {
                Data.wallet.currencies[pair.Key] = pair.Value != null ? pair.Value.DeepClone() : null;
            }
        }

        /// <summary>
        /// 현재 유저 Wallet 기준 재화 상태를 반환한다.
        /// </summary>
        internal static Dictionary<string, JToken> GetCurrencies()
        {
            return Data != null && Data.wallet != null
                ? Data.wallet.currencies
                : null;
        }

        /// <summary>
        /// 상점 카탈로그와 서버 Bootstrap 상태를 런타임 캐시에 반영한다.
        /// ShopPresenter나 구매 복구 경로가 직접 GameBootstrapData 필드를 수정하지 않도록 갱신 지점을 모은다.
        /// </summary>
        /// <param name="catalog">새로 적용할 상점 카탈로그.</param>
        /// <param name="bootstrap">새로 적용할 상점 서버 Bootstrap 상태.</param>
        internal static void ApplyShopData(ShopCatalog catalog, ShopBootstrapResponse bootstrap)
        {
            if (Data == null)
                return;

            if (catalog != null)
            {
                Data.shopCatalog = catalog;
            }

            if (bootstrap != null)
            {
                Data.shop = bootstrap;
            }
        }

        /// <summary>
        /// 가챠 카탈로그와 서버 Bootstrap 상태를 런타임 캐시에 반영한다.
        /// GachaPresenter나 draw 복구 경로가 직접 GameBootstrapData 필드를 수정하지 않도록 갱신 지점을 모은다.
        /// </summary>
        /// <param name="catalog">새로 적용할 가챠 카탈로그.</param>
        /// <param name="bootstrap">새로 적용할 가챠 서버 Bootstrap 상태.</param>
        internal static void ApplyGachaData(GachaCatalog catalog, GachaBootstrapResponse bootstrap)
        {
            if (Data == null)
                return;

            if (catalog != null)
            {
                Data.gachaCatalog = catalog;
            }

            if (bootstrap != null)
            {
                Data.gacha = bootstrap;
            }
        }

        /// <summary>
        /// 지급된 아이템을 유저 인벤토리 캐시에 반영한다.
        /// </summary>
        internal static void ApplyInventoryItem(int itemId, int quantity)
        {
            if (Data == null || itemId == 0)
            {
                return;
            }

            if (Data.inventory == null)
            {
                Data.inventory = new UserInventoryResponse();
            }

            var amount = quantity > 0 ? quantity : 1;
            var items = Data.inventory.items;
            if (items == null)
            {
                Data.inventory.items = new[]
                {
                    new UserInventoryItem
                    {
                        itemId = itemId,
                        quantity = amount
                    }
                };
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null || item.itemId != itemId)
                {
                    continue;
                }

                item.quantity += amount;
                return;
            }

            var nextItems = new UserInventoryItem[items.Length + 1];
            Array.Copy(items, nextItems, items.Length);
            nextItems[nextItems.Length - 1] = new UserInventoryItem
            {
                itemId = itemId,
                quantity = amount
            };
            Data.inventory.items = nextItems;
        }

        // 중략: ApplyEquipmentSlots, ApplyMailData, ApplyDailyReward 등
        //       동일한 방식으로 도메인별 부분 갱신 지점을 이 서비스에 모은다.

        /// <summary>
        /// 유저 재화 응답을 빈 값 없는 상태로 보정한다.
        /// </summary>
        private static UserWalletResponse NormalizeWallet(UserWalletResponse wallet)
        {
            if (wallet == null)
            {
                wallet = new UserWalletResponse();
            }

            if (wallet.currencies == null)
            {
                wallet.currencies = new Dictionary<string, JToken>();
            }

            return wallet;
        }

        /// <summary>
        /// 유저 인벤토리 응답을 빈 값 없는 상태로 보정한다.
        /// </summary>
        private static UserInventoryResponse NormalizeInventory(UserInventoryResponse inventory)
        {
            if (inventory == null)
            {
                inventory = new UserInventoryResponse();
            }

            if (inventory.items == null)
            {
                inventory.items = new UserInventoryItem[0];
            }

            return inventory;
        }

        /// <summary>
        /// 유저 장착 응답을 빈 값 없는 상태로 보정한다.
        /// </summary>
        private static CurrentEquipmentResponse NormalizeCurrentEquipment(CurrentEquipmentResponse currentEquipment)
        {
            if (currentEquipment == null)
            {
                currentEquipment = new CurrentEquipmentResponse();
            }

            if (currentEquipment.slots == null)
            {
                currentEquipment.slots = new Dictionary<string, int?>();
            }

            return currentEquipment;
        }

        /// <summary>
        /// 유저 홈 Bootstrap 요청.
        /// </summary>
        private static async UniTask<UserBootstrapResponse> RequestUserBootstrapAsync()
        {
            var token = await FirebaseService.Instance.Api.User.Bootstrap();
            return FirebaseUtil.ToObject<UserBootstrapResponse>(token);
        }
    }
}
