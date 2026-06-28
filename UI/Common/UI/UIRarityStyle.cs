using UnityEngine;
using UnityEngine.UI;

namespace SampleClient.UI.Common
{
    public class UIRarityStyle : MonoBehaviour
    {
        [SerializeField] private RarityPreset _preset;
        [SerializeField] private Image _frameImg;

        /// <summary>
        /// rarity에 맞는 등급표 프레임 스타일을 적용한다.
        /// </summary>
        public void Apply(string rarity)
        {
            var style = _preset != null ? _preset.Find(rarity) : null;
            if (style == null)
            {
                Clear();
                return;
            }

            SetImage(_frameImg, style.itemRarityFrameSprite);
        }

        public void Clear()
        {
            SetImage(_frameImg, null);
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            var active = sprite != null;
            image.gameObject.SetActive(active);

            if (active)
            {
                image.sprite = sprite;
            }
        }
    }
}



