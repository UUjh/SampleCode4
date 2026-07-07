using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;
using SampleClient.Service.Shop;
using SampleClient.UI.Common.Currency;
using SampleClient.Utils;
using UnityEngine;

namespace SampleClient.UI.Shop
{
    /// <summary>
    /// 상점 카탈로그를 View에 표시하고 구매 요청을 처리하는 Presenter.
    /// 서버 요청과 캐시 반영은 ShopPurchaseService에 맡기고, Presenter는 UI 흐름만 담당한다.
    /// 실제 프로젝트의 탭 구성, 캐릭터 미리보기, 무한 스크롤 셀 구성은 샘플에서 중략했습니다.
    /// </summary>
    public sealed class ShopPresenter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _viewComponent;

        private IShopView _view;
        private readonly Dictionary<int, ShopOffer> _offerById = new Dictionary<int, ShopOffer>();

        private bool _purchasing;
        private string _catalogVersion;
        private ShopSectionType _sectionType = ShopSectionType.Featured;

        // 구매 시도 단위로 유지하는 멱등성 requestId.
        // 재시도 가능한 실패에서는 같은 값을 유지해 서버가 중복 구매를 방어할 수 있게 한다.
        private string _purchaseRequestId;

        /// <summary>
        /// Unity View 컴포넌트를 Presenter가 사용할 인터페이스로 연결한다.
        /// </summary>
        private void Awake()
        {
            _view = _viewComponent as IShopView;
            _view?.Bind(this);
        }

        private void OnDisable()
        {
            ClearPurchaseRequest();
        }

        /// <summary>
        /// Bootstrap 데이터를 기준으로 상점 목록과 상태를 갱신한다.
        /// </summary>
        public async UniTask RefreshAsync()
        {
            var data = GameBootstrapService.Data ?? await GameBootstrapService.LoadAsync();
            if (destroyCancellationToken.IsCancellationRequested || _view == null)
            {
                return;
            }

            if (data == null || data.shopCatalog == null)
            {
                _view.ShowError("상점 데이터를 불러오지 못했습니다.");
                return;
            }

            _catalogVersion = data.shopCatalog.catalogVersion;
            BuildItems(data.shopCatalog, data);
            ShowSection(_sectionType);
        }

        /// <summary>
        /// 현재 표시할 상점 섹션을 변경한다.
        /// UIWindowService가 route 복귀 시에도 이 메서드를 호출한다.
        /// </summary>
        public void ShowSection(ShopSectionType sectionType)
        {
            _sectionType = sectionType;
            _view?.ShowSection(sectionType);
        }

        /// <summary>
        /// View에서 상품을 선택했을 때 상세 정보를 갱신한다.
        /// </summary>
        /// <param name="offerId">선택한 상품 ID.</param>
        public void SelectOffer(int offerId)
        {
            if (!_offerById.TryGetValue(offerId, out var offer))
            {
                return;
            }

            // 선택 상품이 바뀌면 이전 상품 기준 requestId를 재사용하면 안 된다.
            ClearPurchaseRequest();
            _view?.SetSelectedItem(CreateViewModel(offer, GameBootstrapService.Data));
        }

        /// <summary>
        /// 구매 버튼 입력을 처리한다.
        /// </summary>
        /// <param name="offerId">구매할 상품 ID.</param>
        public void OnClickPurchase(int offerId)
        {
            OnClickPurchaseAsync(offerId).Forget();
        }

        /// <summary>
        /// 구매 버튼 입력 상태를 제어하고, 구매 서비스 결과를 UI에 반영한다.
        /// timeout/network/5xx 실패에서는 같은 requestId를 유지해 서버 멱등성 계약을 지킨다.
        /// </summary>
        /// <param name="offerId">구매할 상점 상품 Id.</param>
        private async UniTask OnClickPurchaseAsync(int offerId)
        {
            if (_purchasing)
            {
                return;
            }

            if (offerId == 0 || string.IsNullOrEmpty(_catalogVersion) || !_offerById.ContainsKey(offerId))
            {
                Log.LogMessage("[ShopPresenter] offerId 또는 catalogVersion이 비어 있습니다.", Log.LogLevel.Warning);
                return;
            }

            try
            {
                _purchasing = true;
                _view?.SetInputEnabled(false);

                var requestId = EnsurePurchaseRequestId();
                var purchase = await ShopPurchaseService.RequestAsync(offerId, requestId, _catalogVersion, destroyCancellationToken);

                // 서버 응답을 받았으므로 같은 requestId를 재사용할 필요가 없다. 다음 구매 시 새 requestId를 생성한다.
                ClearPurchaseRequest();

                if (GameBootstrapService.Data == null)
                    return;

                if (ShopPurchaseService.NeedsBootstrapAfterPurchase(offerId))
                {
                    // 릴레이 상품은 다음 단계나 다른 상품 상태가 바뀔 수 있으므로 서버 기준 전체 상태를 다시 받는다.
                    var currencyChanged = ShopPurchaseService.ApplyPurchaseUserState(purchase);
                    if (currencyChanged)
                    {
                        PublishCurrencyChanged();
                    }

                    await RefreshShopBootstrapAsync();
                }
                else
                {
                    var result = ShopPurchaseService.ApplyPurchaseResult(purchase);
                    if (!string.IsNullOrEmpty(result.catalogVersion))
                    {
                        _catalogVersion = result.catalogVersion;
                    }

                    if (result.currencyChanged)
                    {
                        PublishCurrencyChanged();
                    }
                }

                if (CanShowPurchaseResult())
                {
                    await RefreshAsync();
                }
            }
            catch (OperationCanceledException)
            {
                ClearPurchaseRequest();
            }
            catch (Exception e)
            {
                Log.LogMessage($"[ShopPresenter] 구매 실패: {e.Message}", Log.LogLevel.Error);

                // 카탈로그 버전 불일치는 상점 영역만 새 catalogVersion 기준으로 복구한다.
                if (await TryRefreshCatalogAsync(e))
                {
                    ClearPurchaseRequest();
                    return;
                }

                // 재시도 가능한 실패가 아니면 requestId를 버린다.
                // 유지되는 경우 다음 구매 시도에서 같은 requestId로 다시 요청한다.
                if (!ShopPurchaseService.ShouldPreserveRequestId(e))
                {
                    ClearPurchaseRequest();
                }

                if (CanShowPurchaseResult())
                {
                    _view.ShowError("구매에 실패했습니다.");
                }
            }
            finally
            {
                _purchasing = false;
                _view?.SetInputEnabled(true);
            }
        }

        /// <summary>
        /// 서버에서 상점 Bootstrap 상태를 다시 받아 캐시와 화면에 적용한다.
        /// </summary>
        private async UniTask RefreshShopBootstrapAsync()
        {
            if (!FirebaseService.IsApiReady)
            {
                return;
            }

            var bootstrap = await ShopBootstrapService.RequestBootstrapAsync(_catalogVersion);
            if (GameBootstrapService.Data == null)
            {
                Log.LogMessage("[ShopPresenter] Bootstrap 캐시가 없어 상점 상태를 반영하지 않습니다.", Log.LogLevel.Warning);
                return;
            }

            // 구매 직후 받은 최신 상점 상태이므로 캐시는 Presenter 생존과 무관하게 기록한다.
            GameBootstrapService.ApplyShopData(null, bootstrap);
        }

        /// <summary>
        /// 상점 카탈로그 버전 불일치 오류면 상점 카탈로그와 서버 Bootstrap 상태만 강제 갱신한다.
        /// 전체 GameBootstrap을 다시 돌리지 않고 Shop 영역만 새 catalogVersion 기준으로 맞춘다.
        /// </summary>
        /// <param name="e">구매 요청 중 발생한 예외.</param>
        /// <returns>카탈로그 복구에 성공했으면 true.</returns>
        private async UniTask<bool> TryRefreshCatalogAsync(Exception e)
        {
            if (!IsCatalogVersionMismatch(e))
            {
                return false;
            }

            var shopData = await ShopBootstrapService.LoadAsync(forceCatalogRefresh: true);
            if (shopData.catalog == null || shopData.bootstrap == null || GameBootstrapService.Data == null)
            {
                return false;
            }

            GameBootstrapService.ApplyShopData(shopData.catalog, shopData.bootstrap);

            if (!CanShowPurchaseResult())
            {
                return false;
            }

            _catalogVersion = shopData.catalog.catalogVersion;
            await RefreshAsync();
            return true;
        }

        /// <summary>
        /// 예외가 상점 카탈로그 버전 불일치인지 확인한다.
        /// </summary>
        private static bool IsCatalogVersionMismatch(Exception e)
        {
            if (e is PlayerApiException playerApiException)
            {
                return playerApiException.DomainCode == ShopCode.CATALOG_VERSION_MISMATCH;
            }

            return false;
        }

        /// <summary>
        /// 구매 결과를 현재 Shop UI에 표시해도 되는지 확인한다.
        /// </summary>
        /// <returns>Presenter와 View가 살아 있고 현재 열린 window가 Shop이면 true.</returns>
        private bool CanShowPurchaseResult()
        {
            return !destroyCancellationToken.IsCancellationRequested &&
                _view != null &&
                UIWindowService.HasInstance &&
                UIWindowService.Instance.CurrentWindow == UIWindowType.Shop;
        }

        /// <summary>
        /// 현재 구매 시도에 사용할 requestId를 반환한다.
        /// 재시도 가능한 실패로 requestId가 남아있으면 같은 값을 재사용한다.
        /// </summary>
        private string EnsurePurchaseRequestId()
        {
            if (string.IsNullOrEmpty(_purchaseRequestId))
            {
                _purchaseRequestId = Guid.NewGuid().ToString();
            }

            return _purchaseRequestId;
        }

        /// <summary>
        /// 현재 구매 시도 requestId를 초기화한다.
        /// </summary>
        private void ClearPurchaseRequest()
        {
            _purchaseRequestId = null;
        }

        /// <summary>
        /// 상점 카탈로그를 View 표시 모델로 구성한다.
        /// </summary>
        private void BuildItems(ShopCatalog catalog, GameBootstrapData data)
        {
            _offerById.Clear();

            var viewItems = new List<ShopItemViewModel>();
            var offers = catalog.offers ?? Array.Empty<ShopOffer>();
            for (var i = 0; i < offers.Length; i++)
            {
                var offer = offers[i];
                if (offer == null || !offer.isActive)
                {
                    continue;
                }

                _offerById[offer.offerId] = offer;
                viewItems.Add(CreateViewModel(offer, data));
            }

            _view?.SetItems(viewItems);
            _view?.SetWallet(data.wallet != null ? data.wallet.currencies : null);
        }

        /// <summary>
        /// 서버 카탈로그/Bootstrap 응답을 View 전용 모델로 변환한다.
        /// </summary>
        private static ShopItemViewModel CreateViewModel(ShopOffer offer, GameBootstrapData data)
        {
            var reward = offer.rewards != null && offer.rewards.Length > 0 ? offer.rewards[0] : null;
            var itemId = reward != null ? reward.itemId : 0;
            var itemCatalog = data != null ? data.item : null;
            var offerState = GetOfferState(data, offer.offerId);

            return new ShopItemViewModel
            {
                offerId = offer.offerId,
                itemId = itemId,
                title = offer.title,
                rarity = CatalogService.GetItemRarity(itemCatalog, itemId),
                priceAmount = offer.price != null ? offer.price.amount : 0,
                owned = data != null && data.inventory != null && data.inventory.HasItem(itemId),
                purchasable = offerState == null || offerState.isPurchasable
            };
        }

        /// <summary>
        /// 현재 Shop bootstrap 캐시에서 지정 상품의 서버 상태를 찾는다.
        /// </summary>
        private static ShopOfferState GetOfferState(GameBootstrapData data, int offerId)
        {
            var shop = data != null ? data.shop : null;
            if (shop == null || shop.offerStates == null || !shop.offerStates.TryGetValue(offerId, out var state))
            {
                return null;
            }

            return state;
        }

        /// <summary>
        /// 구매/뽑기/메일 수령처럼 재화가 바뀌는 기능들이 같은 방식으로 상단바를 갱신하도록 메시지를 발행한다.
        /// </summary>
        private static void PublishCurrencyChanged()
        {
            var currencies = GameBootstrapService.Data != null && GameBootstrapService.Data.wallet != null
                ? GameBootstrapService.Data.wallet.currencies
                : null;
            CurrencyMessages.Publish(currencies);
        }
    }

    /// <summary>
    /// Presenter가 의존하는 View 계약.
    /// Unity UI 상세 바인딩은 View 구현체에 숨긴다.
    /// </summary>
    public interface IShopView
    {
        void Bind(ShopPresenter presenter);
        void ShowSection(ShopSectionType sectionType);
        void SetItems(IReadOnlyList<ShopItemViewModel> items);
        void SetSelectedItem(ShopItemViewModel item);
        void SetWallet(IDictionary<string, Newtonsoft.Json.Linq.JToken> currencies);
        void SetInputEnabled(bool enabled);
        void ShowError(string message);
    }

    /// <summary>
    /// 서버 DTO가 아니라 View 표시를 위한 축약 모델.
    /// 실제 프로젝트는 무한 스크롤 셀 데이터로 확장되지만 샘플에서는 중략했습니다.
    /// </summary>
    public sealed class ShopItemViewModel
    {
        public int offerId;
        public int itemId;
        public string title;
        public string rarity;
        public int priceAmount;
        public bool owned;
        public bool purchasable;
    }
}
