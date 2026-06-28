using Cysharp.Threading.Tasks;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;
using SampleClient.Utils;

namespace SampleClient.Service.Bootstrap
{
    internal static class ShopBootstrapService
    {
        /// <summary>
        /// 상점 카탈로그와 서버 Bootstrap 상태를 함께 로드한다.
        /// catalogVersion 불일치가 있으면 카탈로그를 강제 갱신한 뒤 서버 bootstrap을 다시 요청한다.
        /// </summary>
        /// <param name="forceCatalogRefresh">로컬 캐시와 meta 비교를 건너뛰고 카탈로그를 강제로 갱신할지 여부</param>
        /// <returns>상점 UI 구성용 정적 카탈로그와 유저별 서버 Bootstrap 상태.</returns>
        internal static async UniTask<(ShopCatalog catalog, ShopBootstrapResponse bootstrap)> LoadAsync(bool forceCatalogRefresh)
        {
            // 상점 UI 표시용 정적 카탈로그를 먼저 준비힌다.
            // 서버 Bootstrap 요청에는 catalogVersion을 전달해 서버 상태와 클라이언트 카탈로그 기준을 맞춘다.
            var catalog = await CatalogService.LoadShopAsync(forceCatalogRefresh);
            var bootstrap = await RequestBootstrapAsync(catalog != null ? catalog.catalogVersion : null);

            // 서버가 다른 catalogVersion을 내려주면 카탈로그를 강제 갱신한다.
            if (catalog != null && bootstrap != null && !string.IsNullOrEmpty(bootstrap.catalogVersion)
                && bootstrap.catalogVersion != catalog.catalogVersion)
            {
                Log.LogMessage($"[ShopBootstrapService] 상점 카탈로그 버전 불일치, 강제 로드 시작", Log.LogLevel.Debug);
                catalog = await CatalogService.LoadShopAsync(forceRefresh: true);
                bootstrap = await RequestBootstrapAsync(catalog != null ? catalog.catalogVersion : null);
            }

            return (catalog, bootstrap);
        }

        /// <summary>
        /// 서버에 클라이언트 상점 catalogVersion을 전달해 구매 가능 상태와 재화를 동기화한다.
        /// </summary>
        /// <param name="catalogVersion">클라이언트가 현재 보유한 상점 카탈로그 버전.</param>
        /// <returns>서버가 내려준 유저별 상점 Bootstrap 상태.</returns>
        internal static async UniTask<ShopBootstrapResponse> RequestBootstrapAsync(string catalogVersion)
        {
            var token = await FirebaseService.Instance.Api.Shop.Bootstrap(catalogVersion);
            return FirebaseUtil.ToObject<ShopBootstrapResponse>(token);
        }


    }
}



