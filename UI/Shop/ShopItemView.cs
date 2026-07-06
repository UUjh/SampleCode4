using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SampleClient.UI.Shop
{
    /// <summary>
    /// 상점 목록의 단일 셀 View 샘플.
    /// 이미지 로딩과 등급별 스타일 적용은 Addressables/ScriptableObject 예시로 분리하고 여기서는 중략했다.
    /// </summary>
    public sealed class ShopItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private GameObject _ownedBadge;

        private int _offerId;
        private Action<int> _onSelected;

        public void Bind(ShopItemViewModel model, Action<int> onSelected)
        {
            _offerId = model != null ? model.offerId : 0;
            _onSelected = onSelected;

            SetText(_titleText, model != null ? model.title : string.Empty);
            SetText(_priceText, model != null ? model.priceAmount.ToString() : string.Empty);
            SetActive(_ownedBadge, model != null && model.owned);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _onSelected?.Invoke(_offerId));
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            if (_button != null)
            {
                _button.interactable = enabled;
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject obj, bool active)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }
}
