using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;
using UnityEngine;

namespace SampleClient.UI.Store
{
    /// <summary>
    /// MVP 패턴에서 UI 입력과 데이터 갱신 흐름을 담당하는 Presenter 샘플.
    /// 공개 샘플에서는 상점 화면을 예시로 사용하지만, 같은 구조를 다른 아웃게임 UI에도 적용할 수 있다.
    /// </summary>
    public sealed class StorePresenter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _viewComponent;

        private IStoreView _view;
        private readonly Dictionary<int, ShopOffer> _offerById = new Dictionary<int, ShopOffer>();
        private string _catalogVersion;
        private bool _isPurchasing;

        /// <summary>
        /// Unity View 컴포넌트를 Presenter가 사용할 인터페이스로 연결한다.
        /// </summary>
        private void Awake()
        {
            _view = _viewComponent as IStoreView;
            _view?.Bind(this);
        }

        /// <summary>
        /// UI Scene이 열린 뒤 Bootstrap 데이터 기준으로 화면을 초기화한다.
        /// </summary>
        public async UniTask Initialize()
        {
            await RefreshAsync();
        }

        /// <summary>
        /// 서버 Bootstrap과 원격 카탈로그를 조합해 View 표시 모델을 만든다.
        /// </summary>
        public async UniTask RefreshAsync()
        {
            var data = AppBootstrapService.Data ?? await AppBootstrapService.LoadAsync();
            if (data == null || data.shopCatalog == null)
            {
                _view?.ShowError("데이터를 불러오지 못했습니다.");
                return;
            }

            _catalogVersion = data.shopCatalog.catalogVersion;
            _offerById.Clear();

            var viewItems = new List<StoreItemViewModel>();
            var offers = data.shopCatalog.offers ?? Array.Empty<ShopOffer>();
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
        /// View에서 상품을 선택했을 때 상세 정보와 미리보기를 갱신한다.
        /// </summary>
        /// <param name="offerId">선택한 상품 ID.</param>
        public void SelectOffer(int offerId)
        {
            if (!_offerById.TryGetValue(offerId, out var offer))
            {
                return;
            }

            _view?.SetSelectedItem(CreateViewModel(offer, AppBootstrapService.Data));
        }

        /// <summary>
        /// 서버에 구매를 요청하고 응답 결과로 로컬 Bootstrap 캐시와 View를 갱신한다.
        /// </summary>
        /// <param name="offerId">구매할 상품 ID.</param>
        public async UniTask PurchaseAsync(int offerId)
        {
            if (_isPurchasing || !_offerById.ContainsKey(offerId))
            {
                return;
            }

            _isPurchasing = true;
            _view?.SetInputEnabled(false);

            try
            {
                var requestId = Guid.NewGuid().ToString("N");
                var token = await FirebaseService.Instance.Api.Shop.Purchase(offerId, requestId, _catalogVersion);
                var purchase = FirebaseUtil.ToObject<ShopPurchaseResponse>(token);

                ApplyPurchaseResult(purchase);
                await RefreshAsync();
            }
            catch (Exception e)
            {
                _view?.ShowError(e.Message);
            }
            finally
            {
                _isPurchasing = false;
                _view?.SetInputEnabled(true);
            }
        }

        /// <summary>
        /// 서버 응답을 런타임 캐시에 반영한다.
        /// 실제 프로젝트의 상세 정책 필드는 공개 샘플에서 중략했다.
        /// </summary>
        private static void ApplyPurchaseResult(ShopPurchaseResponse purchase)
        {
            if (purchase == null)
            {
                return;
            }

            AppBootstrapService.ApplyCurrencies(purchase.remainingBalance);
            for (var i = 0; purchase.grantedRewards != null && i < purchase.grantedRewards.Length; i++)
            {
                var reward = purchase.grantedRewards[i];
                if (reward != null)
                {
                    AppBootstrapService.ApplyInventoryItem(reward.itemId, reward.quantity);
                }
            }

            // 중략: 구매 완료 상태, 잠금 상태, catalogVersion mismatch 복구 정책
        }

        /// <summary>
        /// 서버 카탈로그/Bootstrap 응답을 View 전용 모델로 변환한다.
        /// </summary>
        private static StoreItemViewModel CreateViewModel(ShopOffer offer, AppBootstrapData data)
        {
            var reward = offer.rewards != null && offer.rewards.Length > 0 ? offer.rewards[0] : null;
            var itemId = reward != null ? reward.itemId : 0;
            var itemCatalog = data != null ? data.item : null;

            return new StoreItemViewModel
            {
                offerId = offer.offerId,
                itemId = itemId,
                title = offer.title,
                rarity = CatalogService.GetItemRarity(itemCatalog, itemId),
                priceAmount = offer.price != null ? offer.price.amount : 0,
                spriteAddress = CatalogService.GetItemSpriteAddress(itemCatalog, itemId),
                owned = data != null && data.inventory != null && data.inventory.HasItem(itemId)
            };
        }
    }

    /// <summary>
    /// Presenter가 의존하는 View 계약.
    /// Unity UI 상세 바인딩은 View 구현체에 숨긴다.
    /// </summary>
    public interface IStoreView
    {
        void Bind(StorePresenter presenter);
        void SetItems(IReadOnlyList<StoreItemViewModel> items);
        void SetSelectedItem(StoreItemViewModel item);
        void SetWallet(IDictionary<string, Newtonsoft.Json.Linq.JToken> currencies);
        void SetInputEnabled(bool enabled);
        void ShowError(string message);
    }

    /// <summary>
    /// 서버 DTO가 아니라 View 표시를 위한 축약 모델.
    /// 반복되는 셀 상태와 구체적인 UI 필드는 공개 샘플에서 중략했다.
    /// </summary>
    public sealed class StoreItemViewModel
    {
        public int offerId;
        public int itemId;
        public string title;
        public string rarity;
        public int priceAmount;
        public string spriteAddress;
        public bool owned;
    }
}

