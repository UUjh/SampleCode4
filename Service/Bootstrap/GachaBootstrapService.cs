using Cysharp.Threading.Tasks;
using SampleClient.Service.Catalog;
using SampleClient.Service.Firebase;
using SampleClient.Utils;

namespace SampleClient.Service.Bootstrap
{
    internal static class GachaBootstrapService
    {
        /// <summary>
        /// 가챠 카탈로그와 서버 부트스트랩 상태를 함께 로드한다.
        /// catalogVersion 불일치가 있으면 카탈로그를 강제 갱신한 뒤 서버 bootstrap을 다시 요청한다.
        /// </summary>
        /// <param name="forceCatalogRefresh">로컬 캐시와 meta 비교를 건너뛰고 카탈로그를 강제로 갱신할지 여부.</param>
        /// <returns>가챠 UI 구성용 정적 카탈로그와 유저별 서버 Bootstrap 상태.</returns>
        internal static async UniTask<(GachaCatalog catalog, GachaBootstrapResponse bootstrap)> LoadAsync(bool forceCatalogRefresh)
        {
            var catalog = await CatalogService.LoadGachaAsync(forceCatalogRefresh);
            var bootstrap = await RequestBootstrapAsync(catalog != null ? catalog.catalogVersion : null);

            if (catalog != null && bootstrap != null &&
                !string.IsNullOrEmpty(bootstrap.catalogVersion) &&
                bootstrap.catalogVersion != catalog.catalogVersion)
            {
                Log.LogMessage("[GachaBootstrapService] 가챠 카탈로그 버전 불일치, 강제 로드 시작", Log.LogLevel.Debug);

                catalog = await CatalogService.LoadGachaAsync(forceRefresh: true);
                bootstrap = await RequestBootstrapAsync(catalog != null ? catalog.catalogVersion : null);
            }

            return (catalog, bootstrap);
        }

        /// <summary>
        /// 서버에 클라이언트 가챠 catalogVersion을 전달해 뽑기 가능 상태를 동기화한다.
        /// </summary>
        /// <param name="catalogVersion">클라이언트가 현재 보유한 가챠 카탈로그 버전.</param>
        /// <returns>서버가 내려준 유저별 가챠 Bootstrap 상태.</returns>
        internal static async UniTask<GachaBootstrapResponse> RequestBootstrapAsync(string catalogVersion)
        {
            var token = await FirebaseService.Instance.Api.Gacha.Bootstrap(catalogVersion);
            return FirebaseUtil.ToObject<GachaBootstrapResponse>(token);
        }
    }
}
