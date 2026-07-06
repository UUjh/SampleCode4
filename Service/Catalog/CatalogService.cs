using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SampleClient.Service.Catalog
{
    /// <summary>
    /// 원격 카탈로그 meta를 받고 hash가 같으면 로컬 캐시를 재사용하는 샘플 서비스.
    /// 샘플에서는 상점/가챠/아이템 카탈로그만 남겼습니다.
    /// </summary>
    public static class CatalogService
    {
        private const string CatalogHost = "https://example-catalog-host.invalid";
        private const int RequestTimeoutSeconds = 30;

        /// <summary>
        /// 현재 런타임에서 사용 중인 상점 카탈로그 캐시.
        /// </summary>
        public static ShopCatalog Shop { get; private set; }

        /// <summary>
        /// 현재 런타임에서 사용 중인 가챠 카탈로그 캐시.
        /// </summary>
        public static GachaCatalog Gacha { get; private set; }

        /// <summary>
        /// 현재 런타임에서 사용 중인 아이템 카탈로그 캐시.
        /// </summary>
        public static ItemCatalog Item { get; private set; }

        /// <summary>
        /// 상점 표시용 정적 카탈로그를 로드한다.
        /// </summary>
        /// <param name="forceRefresh">true이면 로컬 캐시 검증을 건너뛰고 원격 카탈로그를 다시 받는다.</param>
        /// <returns>역직렬화된 상점 카탈로그.</returns>
        public static async UniTask<ShopCatalog> LoadShopAsync(bool forceRefresh = false)
        {
            var json = await LoadCatalogJsonAsync("/shop/shop-client-meta.latest.json", "shop-client", forceRefresh);
            Shop = JsonConvert.DeserializeObject<ShopCatalog>(json);
            return Shop;
        }

        /// <summary>
        /// 가챠 표시용 정적 카탈로그를 로드한다.
        /// </summary>
        /// <param name="forceRefresh">true이면 로컬 캐시 검증을 건너뛰고 원격 카탈로그를 다시 받는다.</param>
        /// <returns>역직렬화된 가챠 카탈로그.</returns>
        public static async UniTask<GachaCatalog> LoadGachaAsync(bool forceRefresh = false)
        {
            var json = await LoadCatalogJsonAsync("/gacha/gacha-client-meta.latest.json", "gacha-client", forceRefresh);
            Gacha = JsonConvert.DeserializeObject<GachaCatalog>(json);
            return Gacha;
        }

        /// <summary>
        /// 아이템 이름, 등급, 표시 리소스 주소를 조회하기 위한 아이템 카탈로그를 로드한다.
        /// </summary>
        /// <param name="forceRefresh">true이면 로컬 캐시 검증을 건너뛰고 원격 카탈로그를 다시 받는다.</param>
        /// <returns>역직렬화된 아이템 카탈로그.</returns>
        public static async UniTask<ItemCatalog> LoadItemAsync(bool forceRefresh = false)
        {
            var json = await LoadCatalogJsonAsync("/catalog/items/items-client-meta.latest.json", "items-client", forceRefresh);
            Item = JsonConvert.DeserializeObject<ItemCatalog>(json);
            return Item;
        }

        /// <summary>
        /// 아이템 ID로 카탈로그의 표시 메타데이터를 찾는다.
        /// </summary>
        /// <param name="catalog">검색할 아이템 카탈로그.</param>
        /// <param name="itemId">검색할 아이템 ID.</param>
        /// <param name="item">검색 성공 시 반환되는 아이템 메타데이터.</param>
        /// <returns>아이템 메타데이터를 찾았으면 true.</returns>
        public static bool TryGetItem(ItemCatalog catalog, int itemId, out ItemInfo item)
        {
            item = null;
            return catalog != null &&
                   catalog.itemsById != null &&
                   itemId != 0 &&
                   catalog.itemsById.TryGetValue(itemId, out item) &&
                   item != null;
        }

        /// <summary>
        /// 아이템 ID에 대응하는 표시 이름을 반환한다.
        /// </summary>
        /// <param name="catalog">아이템 카탈로그.</param>
        /// <param name="itemId">조회할 아이템 ID.</param>
        /// <returns>카탈로그 이름이 있으면 이름, 없으면 ID 문자열.</returns>
        public static string GetItemName(ItemCatalog catalog, int? itemId)
        {
            if (!itemId.HasValue || !TryGetItem(catalog, itemId.Value, out var item) || string.IsNullOrEmpty(item.name))
            {
                return itemId.HasValue ? itemId.Value.ToString() : string.Empty;
            }

            return item.name;
        }

        /// <summary>
        /// 아이템 ID에 대응하는 등급 문자열을 반환한다.
        /// </summary>
        /// <param name="catalog">아이템 카탈로그.</param>
        /// <param name="itemId">조회할 아이템 ID.</param>
        /// <returns>normal, rare, epic, legend 등 일반화된 등급 문자열.</returns>
        public static string GetItemRarity(ItemCatalog catalog, int? itemId)
        {
            return itemId.HasValue && TryGetItem(catalog, itemId.Value, out var item) ? item.rarity : string.Empty;
        }

        /// <summary>
        /// 아이템 ID에 대응하는 카테고리 문자열을 반환한다.
        /// </summary>
        /// <param name="catalog">아이템 카탈로그.</param>
        /// <param name="itemId">조회할 아이템 ID.</param>
        /// <returns>카탈로그에 등록된 카테고리 문자열.</returns>
        public static string GetItemCategory(ItemCatalog catalog, int? itemId)
        {
            return itemId.HasValue && TryGetItem(catalog, itemId.Value, out var item) ? item.category : string.Empty;
        }

        /// <summary>
        /// 등급 문자열을 정렬 비교용 순위 값으로 변환한다.
        /// UI가 등급 문자열을 직접 비교하지 않고 카탈로그 계층의 기준 하나를 공유하게 한다.
        /// </summary>
        /// <param name="rarity">normal, rare, epic, legend 등 일반화된 등급 문자열.</param>
        /// <returns>높을수록 상위 등급. 알 수 없는 등급이면 -1.</returns>
        public static int GetRarityRank(string rarity)
        {
            switch (rarity)
            {
                case "normal": return 0;
                case "rare": return 1;
                case "epic": return 2;
                case "legend": return 3;
                default: return -1;
            }
        }

        /// <summary>
        /// meta JSON을 조회한 뒤 캐시 hash를 검증하고 실제 카탈로그 JSON을 반환한다.
        /// </summary>
        /// <param name="metaPath">원격 meta 파일 경로.</param>
        /// <param name="localName">persistentDataPath에 저장할 캐시 파일 이름.</param>
        /// <param name="forceRefresh">true이면 캐시를 사용하지 않고 원격 데이터를 받는다.</param>
        /// <returns>검증이 끝난 카탈로그 JSON 문자열.</returns>
        private static async UniTask<string> LoadCatalogJsonAsync(string metaPath, string localName, bool forceRefresh)
        {
            var meta = await DownloadJsonAsync<ClientCatalogMeta>(CatalogHost + metaPath);
            var cachePath = GetCachePath(localName);

            if (!forceRefresh && File.Exists(cachePath))
            {
                var cachedJson = File.ReadAllText(cachePath, Encoding.UTF8);
                if (Sha256(cachedJson) == meta.hash)
                {
                    return cachedJson;
                }
            }

            var json = await DownloadTextAsync(CatalogHost + "/" + meta.entry);
            if (Sha256(json) != meta.hash)
            {
                throw new InvalidOperationException("Catalog hash mismatch");
            }

            File.WriteAllText(cachePath, json, Encoding.UTF8);
            return json;
        }

        /// <summary>
        /// 원격 JSON 텍스트를 다운로드한 뒤 지정 타입으로 역직렬화한다.
        /// </summary>
        /// <typeparam name="T">역직렬화할 타입.</typeparam>
        /// <param name="url">다운로드할 URL.</param>
        /// <returns>역직렬화된 객체.</returns>
        private static async UniTask<T> DownloadJsonAsync<T>(string url)
        {
            var json = await DownloadTextAsync(url);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// UnityWebRequest로 원격 텍스트를 다운로드한다.
        /// </summary>
        /// <param name="url">다운로드할 URL.</param>
        /// <returns>응답 텍스트.</returns>
        private static async UniTask<string> DownloadTextAsync(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = RequestTimeoutSeconds;
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(request.error);
                }

                return request.downloadHandler.text;
            }
        }

        /// <summary>
        /// 카탈로그 캐시 파일 경로를 만든다.
        /// </summary>
        /// <param name="localName">캐시 파일 이름.</param>
        /// <returns>persistentDataPath 기준 캐시 파일 경로.</returns>
        private static string GetCachePath(string localName)
        {
            return Path.Combine(Application.persistentDataPath, localName + ".json");
        }

        /// <summary>
        /// 카탈로그 무결성 검증에 사용할 SHA-256 해시를 계산한다.
        /// </summary>
        /// <param name="text">해시를 계산할 원문.</param>
        /// <returns>소문자 hex 형식 SHA-256 문자열.</returns>
        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);

                for (var i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}


