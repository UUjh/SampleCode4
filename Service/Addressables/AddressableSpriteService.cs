using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using SampleClient.Utils;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SampleClient.Service.Addressables
{
    /// <summary>
    /// Addressables Sprite를 address 단위로 공유하고 참조 수를 관리한다.
    /// 모든 호출은 Unity 메인 스레드에서 사용한다.
    /// </summary>
    public static class AddressableSpriteService
    {
        /// <summary>
        /// address 하나에 대응하는 실제 Addressables handle과 사용 수를 보관한다.
        /// </summary>
        internal sealed class SpriteEntry
        {
            public string address;
            public AsyncOperationHandle<Sprite> handle;
            public int referenceCount;
            public bool released;
        }

        /// <summary>
        /// address 별로 하나만 유지되는 Sprite 로드 항목.
        /// 같은 address를 여러 View가 요청해도 handle은 하나만 만든다.
        /// </summary>
        private static readonly Dictionary<string, SpriteEntry> Entries = new Dictionary<string, SpriteEntry>();

        /// <summary>
        /// View가 보유하는 Sprite 사용권
        /// View는 자신의 lease만 Dispose하며 Addressables.Release를 직접 호출하지 않는다.
        /// </summary>
        public sealed class SpriteLease : IDisposable
        {
            private SpriteEntry _entry;

            internal SpriteLease(SpriteEntry entry)
            {
                _entry = entry;
            }

            /// <summary>
            /// 현재 lease가 사용하는 address.
            /// 같은 address를 다시 받았는지 비교할 때 사용한다.
            /// </summary>
            public string Address => _entry != null ? _entry.address : string.Empty;

            /// <summary>
            /// Sprite 로드 완료를 기다린다.
            /// Dispose된 lease는 null을 반환한다.
            /// </summary>
            /// <returns></returns>
            public UniTask<Sprite> LoadAsync()
            {
                var entry = _entry;
                return entry != null ? WaitForLoadAsync(entry) : UniTask.FromResult<Sprite>(null);
            }

            /// <summary>
            /// 이 View의 Sprite 사용을 종료한다.
            /// 같은 lease를 두 번 Dispose해도 참조 수가 중복 감소하지 않는다.
            /// </summary>
            public void Dispose()
            {
                if (_entry == null)
                    return;

                Release(_entry);
                _entry = null;
            }
        }

        /// <summary>
        /// 지정 address의 Sprite 사용권을 얻는다.
        /// 같은 address가 이미 로드 중이거나 로드되어 있으면 기존 handle을 공유한다.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public static SpriteLease Acquire(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            if (!Entries.TryGetValue(address, out var entry))
            {
                entry = new SpriteEntry
                {
                    address = address,
                    handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(address)
                };

                Entries.Add(address, entry);
            }

            entry.referenceCount++;
            return new SpriteLease(entry);
        }

        /// <summary>
        /// Addressables 로드 완료를 기다리고 성공한 Sprite를 반환한다.
        /// 로드 도중 모든 View가 Release해도 로드 완료 뒤 handle을 안전하게 정리한다.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private static async UniTask<Sprite> WaitForLoadAsync(SpriteEntry entry)
        {
            try
            {
                var sprite = await entry.handle.Task;
                if (entry.handle.Status != AsyncOperationStatus.Succeeded)
                {
                    Log.LogMessage($"[AddressableSpriteService] Sprite 로드 실패: {entry.address}", Log.LogLevel.Warning);
                    ReleaseEntry(entry);
                    return null;
                }

                return sprite;
            }
            catch (Exception e)
            {
                Log.LogMessage($"[AddressableSpriteService] Sprite 로드 예외: {entry.address}, {e.Message}", Log.LogLevel.Warning);
                ReleaseEntry(entry);
                return null;
            }
            finally
            {
                // 로드 중 이미 모든 View가 Release했다면 완료 직후 handle도 해제한다.
                if (!entry.released && entry.referenceCount == 0)
                {
                    ReleaseEntry(entry);
                }
            }
        }

        /// <summary>
        /// lease 하나의 사용을 종료한다.
        /// 마지막 사용자가 사라졌고 로드가 끝난 경우에만 실제 Addressables handle을 해제한다.
        /// </summary>
        /// <param name="entry"></param>
        private static void Release(SpriteEntry entry)
        {
            if (entry == null || entry.released || entry.referenceCount == 0)
            {
                return;
            }

            entry.referenceCount--;

            if (entry.referenceCount != 0)
            {
                return;
            }

            if (entry.handle.IsDone)
            {
                ReleaseEntry(entry);
                return;
            }

            // LoadAsync를 호출하지 않은 lease도 로드 완료 후 정리한다.
            entry.handle.Completed += _ =>
            {
                if (!entry.released && entry.referenceCount == 0)
                {
                    ReleaseEntry(entry);
                }
            };
        }

        /// <summary>
        /// Dictionary 항목과 Addressables handle을 함께 정리한다.
        /// 이 메서드는 같은 entry에 대해 한 번만 실행된다.
        /// </summary>
        /// <param name="entry"></param>
        private static void ReleaseEntry(SpriteEntry entry)
        {
            if (entry == null || entry.released)
                return;

            entry.released = true;

            if (Entries.TryGetValue(entry.address, out var current) && ReferenceEquals(current, entry))
            {
                Entries.Remove(entry.address);
            }

            if (entry.handle.IsValid())
            {
                UnityEngine.AddressableAssets.Addressables.Release(entry.handle);
            }
        }
    }

    /// <summary>
    /// ItemCatalog file을 프로젝트의 item Sprite address로 변환한다.
    /// 카탈로그에는 공통 파일명만 두고, 사용처가 어느 이미지인지만 이 규칙으로 구분한다.
    /// </summary>
    public static class ItemSpriteAddress
    {
        private const string ItemViewFolder = "Assets/SampleAssets/items/";
        private const string CharacterItemFolder = "Assets/SampleAssets/character_items/";

        /// <summary>
        /// 상점, 컬렉션, 가챠 프라이즈 보상 아이템 카드용 address를 만든다.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetItemView(string file)
        {
            return GetAddress(ItemViewFolder, file);
        }

        /// <summary>
        /// 캐릭터 장착 미리보기용 address를 만든다.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetCharacterItem(string file)
        {
            return GetAddress(CharacterItemFolder, file);
        }

        private static string GetAddress(string folder, string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return string.Empty;
            }

            return folder + file + ".png";
        }
    }

    public static class CharacterSpriteAddress
    {
        // Addressables에 등록한 기본 캐릭터 색상 Sprite 폴더.
        private const string CharacterFolder = "Assets/SampleAssets/characters/";

        // 00.png부터 14.png까지 총 15개 색상을 사용한다.
        public const int ColorCount = 15;

        /// <summary>
        /// 로컬 색상 인덱스를 기본 캐릭터 Sprite Address로 변환한다.
        /// color=0은 00.png, color=14는 14.png를 뜻한다.
        /// </summary>
        public static string Get(int color)
        {
            if (color < 0 || color >= ColorCount)
            {
                return string.Empty;
            }

            return CharacterFolder + color.ToString("00") + ".png";
        }
    }
}





