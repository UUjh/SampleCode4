using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;

namespace SampleClient.Service.Bootstrap
{
    /// <summary>
    /// Main 진입 전에 유저 상태, 상점 Bootstrap, 카탈로그를 준비하는 샘플 서비스.
    /// 공개 샘플에서는 상점 흐름에 필요한 캐시와 갱신 지점만 남겼습니다.
    /// </summary>
    public static class AppBootstrapService
    {
        private static readonly object LoadLock = new object();
        private static UniTaskCompletionSource<AppBootstrapData> _loadingSource;

        /// <summary>
        /// UI Presenter가 참조하는 런타임 Bootstrap 캐시.
        /// </summary>
        public static AppBootstrapData Data { get; private set; }

        /// <summary>
        /// 로그아웃 또는 세션 초기화 시 런타임 캐시와 진행 중인 로딩 상태를 비운다.
        /// </summary>
        public static void Clear()
        {
            Data = null;
            lock (LoadLock)
            {
                _loadingSource = null;
            }
        }

        /// <summary>
        /// 유저 Bootstrap, 상점 Bootstrap, 아이템 카탈로그를 병렬로 준비한다.
        /// 같은 시점에 여러 Presenter가 호출해도 실제 요청은 한 번만 수행한다.
        /// </summary>
        /// <param name="forceCatalogRefresh">true이면 카탈로그 캐시를 무시하고 원격 데이터를 다시 받는다.</param>
        /// <returns>초기화된 런타임 Bootstrap 데이터.</returns>
        public static UniTask<AppBootstrapData> LoadAsync(bool forceCatalogRefresh = false)
        {
            lock (LoadLock)
            {
                if (_loadingSource != null)
                {
                    return _loadingSource.Task;
                }

                _loadingSource = new UniTaskCompletionSource<AppBootstrapData>();
            }

            LoadInternalAsync(forceCatalogRefresh).Forget();
            return _loadingSource.Task;
        }

        /// <summary>
        /// 실제 Bootstrap 로딩을 수행하고 완료/실패 결과를 공유 Task에 반영한다.
        /// </summary>
        /// <param name="forceCatalogRefresh">카탈로그 강제 갱신 여부.</param>
        private static async UniTaskVoid LoadInternalAsync(bool forceCatalogRefresh)
        {
            try
            {
                var userTask = RequestUserBootstrapAsync();
                var shopTask = ShopBootstrapService.LoadAsync(forceCatalogRefresh);
                var itemTask = CatalogService.LoadItemAsync(forceCatalogRefresh);

                var user = await userTask;
                var shop = await shopTask;
                var item = await itemTask;

                Data = new AppBootstrapData
                {
                    userProfile = user != null ? user.profile : null,
                    wallet = NormalizeWallet(user != null ? user.wallet : null),
                    inventory = NormalizeInventory(user != null ? user.inventory : null),
                    currentEquipment = NormalizeEquipment(user != null ? user.currentEquipment : null),
                    shopCatalog = shop.catalog,
                    shop = shop.bootstrap,
                    item = item
                    // 중략: 동일한 Bootstrap 패턴으로 확장되는 다른 콘텐츠 모듈
                };

                _loadingSource.TrySetResult(Data);
            }
            catch (System.Exception e)
            {
                _loadingSource.TrySetException(e);
            }
            finally
            {
                lock (LoadLock)
                {
                    _loadingSource = null;
                }
            }
        }

        /// <summary>
        /// 상점 카탈로그와 서버 Bootstrap 상태를 런타임 캐시에 반영한다.
        /// </summary>
        /// <param name="catalog">새로 적용할 상점 카탈로그.</param>
        /// <param name="bootstrap">새로 적용할 상점 서버 Bootstrap 상태.</param>
        internal static void ApplyShopData(ShopCatalog catalog, ShopBootstrapResponse bootstrap)
        {
            if (Data == null)
            {
                return;
            }

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
        /// 구매 응답으로 받은 재화 상태를 유저 Wallet 캐시에 반영한다.
        /// </summary>
        /// <param name="currencies">서버가 내려준 최신 재화 딕셔너리.</param>
        internal static void ApplyCurrencies(Dictionary<string, JToken> currencies)
        {
            if (Data == null || currencies == null)
            {
                return;
            }

            Data.wallet = Data.wallet ?? new UserWalletResponse();
            Data.wallet.currencies = currencies;
        }

        /// <summary>
        /// 현재 유저 Wallet 기준 재화 상태를 반환한다.
        /// </summary>
        /// <returns>재화 ID와 수량 토큰 딕셔너리.</returns>
        internal static Dictionary<string, JToken> GetCurrencies()
        {
            return Data != null && Data.wallet != null ? Data.wallet.currencies : null;
        }

        /// <summary>
        /// Bootstrap 캐시에 있는 아이템 카탈로그를 반환한다.
        /// </summary>
        /// <returns>현재 아이템 카탈로그.</returns>
        internal static ItemCatalog GetItemCatalog()
        {
            return Data != null && Data.item != null ? Data.item : CatalogService.Item;
        }

        /// <summary>
        /// 구매로 지급된 아이템을 유저 인벤토리 캐시에 반영한다.
        /// </summary>
        /// <param name="itemId">지급된 아이템 ID.</param>
        /// <param name="quantity">지급 수량.</param>
        internal static void ApplyInventoryItem(int itemId, int quantity)
        {
            if (Data == null || itemId == 0)
            {
                return;
            }

            Data.inventory = NormalizeInventory(Data.inventory);
            var amount = quantity > 0 ? quantity : 1;

            for (var i = 0; i < Data.inventory.items.Length; i++)
            {
                var item = Data.inventory.items[i];
                if (item != null && item.itemId == itemId)
                {
                    item.quantity += amount;
                    return;
                }
            }

            var items = new List<UserInventoryItem>(Data.inventory.items)
            {
                new UserInventoryItem { itemId = itemId, quantity = amount }
            };
            Data.inventory.items = items.ToArray();
        }

        /// <summary>
        /// 서버에서 로그인 유저의 기본 상태 묶음을 조회한다.
        /// </summary>
        /// <returns>유저 프로필, 재화, 인벤토리, 장착 상태 응답.</returns>
        private static async UniTask<UserBootstrapResponse> RequestUserBootstrapAsync()
        {
            var token = await FirebaseService.Instance.Api.User.Bootstrap();
            return FirebaseUtil.ToObject<UserBootstrapResponse>(token);
        }

        /// <summary>
        /// Wallet 응답이 null이어도 UI가 안전하게 접근할 수 있도록 기본값을 채운다.
        /// </summary>
        /// <param name="wallet">서버 Wallet 응답.</param>
        /// <returns>정규화된 Wallet 응답.</returns>
        private static UserWalletResponse NormalizeWallet(UserWalletResponse wallet)
        {
            wallet = wallet ?? new UserWalletResponse();
            wallet.currencies = wallet.currencies ?? new Dictionary<string, JToken>();
            return wallet;
        }

        /// <summary>
        /// Inventory 응답이 null이어도 UI가 안전하게 순회할 수 있도록 기본값을 채운다.
        /// </summary>
        /// <param name="inventory">서버 Inventory 응답.</param>
        /// <returns>정규화된 Inventory 응답.</returns>
        private static UserInventoryResponse NormalizeInventory(UserInventoryResponse inventory)
        {
            inventory = inventory ?? new UserInventoryResponse();
            inventory.items = inventory.items ?? new UserInventoryItem[0];
            return inventory;
        }

        /// <summary>
        /// 장착 슬롯 응답이 null이어도 UI가 안전하게 접근할 수 있도록 기본값을 채운다.
        /// </summary>
        /// <param name="equipment">서버 장착 슬롯 응답.</param>
        /// <returns>정규화된 장착 슬롯 응답.</returns>
        private static CurrentEquipmentResponse NormalizeEquipment(CurrentEquipmentResponse equipment)
        {
            equipment = equipment ?? new CurrentEquipmentResponse();
            equipment.slots = equipment.slots ?? new Dictionary<string, int?>();
            return equipment;
        }
    }
}

