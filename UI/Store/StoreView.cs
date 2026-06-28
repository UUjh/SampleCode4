using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SampleClient.UI.Store
{
    /// <summary>
    /// 상점 화면의 Unity UI 바인딩 샘플.
    /// Presenter가 만든 표시 모델을 받아 목록, 선택 상품, 지갑 상태만 갱신한다.
    /// 실제 프로젝트의 탭 구성, 캐릭터 미리보기, 이벤트 배너 등은 공개 샘플에서 중략했다.
    /// </summary>
    public sealed class StoreView : UIView, IStoreView
    {
        [SerializeField] private Transform _itemRoot;
        [SerializeField] private StoreItemView _itemPrefab;
        [SerializeField] private TextMeshProUGUI _selectedTitleText;
        [SerializeField] private TextMeshProUGUI _selectedPriceText;
        [SerializeField] private TextMeshProUGUI _walletText;
        [SerializeField] private TextMeshProUGUI _errorText;
        [SerializeField] private Button _purchaseButton;

        private readonly List<StoreItemView> _items = new List<StoreItemView>();
        private StorePresenter _presenter;
        private int _selectedOfferId;

        public void Bind(StorePresenter presenter)
        {
            _presenter = presenter;
            SetButtonInteractable(_purchaseButton, false);
        }

        public void SetItems(IReadOnlyList<StoreItemViewModel> items)
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

        public void SetSelectedItem(StoreItemViewModel item)
        {
            _selectedOfferId = item != null ? item.offerId : 0;
            SetText(_selectedTitleText, item != null ? item.title : string.Empty);
            SetText(_selectedPriceText, item != null ? item.priceAmount.ToString() : string.Empty);
            SetButtonInteractable(_purchaseButton, item != null && !item.owned);

            if (_purchaseButton != null)
            {
                _purchaseButton.onClick.RemoveAllListeners();
                if (_selectedOfferId != 0)
                {
                    _purchaseButton.onClick.AddListener(() => _presenter.PurchaseAsync(_selectedOfferId).Forget());
                }
            }
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

            SetButtonInteractable(_purchaseButton, enabled && _selectedOfferId != 0);
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

