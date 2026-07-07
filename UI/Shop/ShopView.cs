using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SampleClient.UI.Shop
{
    /// <summary>
    /// 상점 화면의 Unity UI 바인딩 샘플.
    /// Presenter가 만든 표시 모델을 받아 섹션, 목록, 선택 상품, 지갑 상태만 갱신한다.
    /// 실제 프로젝트의 무한 스크롤(GPM InfiniteScroll), 캐릭터 미리보기, 이벤트 배너 등은 샘플에서 중략했습니다.
    /// </summary>
    public sealed class ShopView : UIView, IShopView
    {
        [Header("Section")]
        [SerializeField] private GameObject _featuredSection;
        [SerializeField] private GameObject _characterShopSection;
        [SerializeField] private GameObject _resourcesSection;

        [Header("Items")]
        [SerializeField] private Transform _itemRoot;
        [SerializeField] private ShopItemView _itemPrefab;

        [Header("Selected / Wallet")]
        [SerializeField] private TextMeshProUGUI _selectedTitleText;
        [SerializeField] private TextMeshProUGUI _selectedPriceText;
        [SerializeField] private TextMeshProUGUI _walletText;
        [SerializeField] private TextMeshProUGUI _errorText;
        [SerializeField] private Button _purchaseButton;

        private readonly List<ShopItemView> _items = new List<ShopItemView>();
        private ShopPresenter _presenter;
        private int _selectedOfferId;
        private bool _selectedPurchasable;

        public void Bind(ShopPresenter presenter)
        {
            _presenter = presenter;
            ClearSubscriptions();
            BindPurchaseButton();
            SetButtonInteractable(_purchaseButton, false);
        }

        public void ShowSection(ShopSectionType sectionType)
        {
            SetActive(_featuredSection, sectionType == ShopSectionType.Featured);
            SetActive(_characterShopSection, sectionType == ShopSectionType.CharacterShop);
            SetActive(_resourcesSection, sectionType == ShopSectionType.Resources);
        }

        public void SetItems(IReadOnlyList<ShopItemViewModel> items)
        {
            ClearItems();

            if (items == null || _itemRoot == null || _itemPrefab == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var view = Instantiate(_itemPrefab, _itemRoot);
                view.Bind(items[i], OnSelectItem);
                _items.Add(view);
            }
        }

        public void SetSelectedItem(ShopItemViewModel item)
        {
            _selectedOfferId = item != null ? item.offerId : 0;
            _selectedPurchasable = item != null && !item.owned && item.purchasable;
            SetText(_selectedTitleText, item != null ? item.title : string.Empty);
            SetText(_selectedPriceText, item != null ? item.priceAmount.ToString() : string.Empty);
            SetButtonInteractable(_purchaseButton, _selectedPurchasable);
        }

        private void BindPurchaseButton()
        {
            if (_purchaseButton == null)
            {
                return;
            }

            AddSubscription(_purchaseButton
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    if (_selectedOfferId != 0 && _selectedPurchasable)
                    {
                        _presenter?.OnClickPurchase(_selectedOfferId);
                    }
                }));
        }

        public void SetWallet(IDictionary<string, JToken> currencies)
        {
            if (currencies == null || currencies.Count == 0)
            {
                SetText(_walletText, string.Empty);
                return;
            }

            foreach (var pair in currencies)
            {
                SetText(_walletText, $"{pair.Key}: {pair.Value}");
                return;
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].SetInputEnabled(enabled);
            }

            SetButtonInteractable(_purchaseButton, enabled && _selectedOfferId != 0 && _selectedPurchasable);
        }

        public void ShowError(string message)
        {
            SetText(_errorText, message);
        }

        private void OnSelectItem(int offerId)
        {
            _presenter?.SelectOffer(offerId);
        }

        private void ClearItems()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                {
                    Destroy(_items[i].gameObject);
                }
            }

            _items.Clear();
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
