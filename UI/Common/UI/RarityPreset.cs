using System;
using UnityEngine;

namespace SampleClient.UI.Common
{
    [CreateAssetMenu(menuName = "UI/Rarity Preset")]
    public class RarityPreset : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string rarity;

            [Header("Item Card")]
            [Tooltip("아이템 대표 색상")]
            public Color itemColor = Color.white;
            public Sprite itemFrameSprite;
            public Sprite itemRarityFrameSprite;

            [Header("Detail Popup / Result UI")]
            public Sprite backgroundSprite;
            public Sprite frameSprite;
            public Color frameColor = Color.white;
            public Color frameArtColor = Color.white;
            public Color lightColor = Color.white;
            public bool frameArtVisible = true;
            public Color textColor = Color.white;
        }

        [SerializeField] private Entry[] _entries;

        /// <summary>
        /// 서버/카탈로그에서 받은 rarity 문자열과 같은 등급의 스타일을 찾는다.
        /// </summary>
        /// <param name="rarity"></param>
        /// <returns></returns>
        public Entry Find(string rarity)
        {
            if (_entries == null || string.IsNullOrEmpty(rarity))
            {
                return null;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                if (entry != null && string.Equals(entry.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}



