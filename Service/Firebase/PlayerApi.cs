using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Firebase Cloud Functions REST API를 도메인별 클래스로 분리한 샘플.
    /// 샘플에서는 User/Shop/Gacha API만 코드로 남기고 나머지 도메인은 중략했습니다.
    /// </summary>
    public sealed class PlayerApi
    {
        private readonly UserApi _user;
        private readonly ShopApi _shop;
        private readonly GachaApi _gacha;

        /// <summary>
        /// 공통 HTTP 클라이언트를 주입받아 도메인별 API 래퍼를 생성한다.
        /// </summary>
        /// <param name="client">Firebase 토큰과 AppCheck 토큰을 붙여 요청하는 공통 클라이언트.</param>
        public PlayerApi(PlayerApiClient client)
        {
            _user = new UserApi(client);
            _shop = new ShopApi(client);
            _gacha = new GachaApi(client);
        }

        /// <summary>
        /// 유저 상태 조회 API.
        /// </summary>
        public UserApi User => _user;

        /// <summary>
        /// 상점 상태 조회 및 구매 API.
        /// </summary>
        public ShopApi Shop => _shop;

        /// <summary>
        /// 가챠 상태 조회 및 뽑기 API.
        /// </summary>
        public GachaApi Gacha => _gacha;
        // 중략: Mail, Equipment, DailyReward 등 동일한 방식으로 분리되는 다른 도메인 API
    }

    /// <summary>
    /// 유저 API 래퍼.
    /// </summary>
    public sealed class UserApi
    {
        private readonly PlayerApiClient _client;

        /// <summary>
        /// 유저 API 래퍼를 생성한다.
        /// </summary>
        /// <param name="client">공통 Player API 클라이언트.</param>
        public UserApi(PlayerApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 로그인 직후 화면 초기화에 필요한 유저 상태 묶음을 조회한다.
        /// </summary>
        /// <returns>서버 User Bootstrap 응답 토큰.</returns>
        public UniTask<JToken> Bootstrap()
        {
            return _client.PostAsync("/user/bootstrap");
        }

        /// <summary>
        /// 유저 프로필만 다시 조회한다.
        /// </summary>
        /// <returns>서버 프로필 응답 토큰.</returns>
        public UniTask<JToken> GetProfile()
        {
            return _client.GetAsync("/user/profile");
        }

        /// <summary>
        /// 익명 로그인 직후 서버에 유저 문서 생성을 보장한다.
        /// 앱 시작마다 호출하면 no-op이어도 rate limit 대상이 되므로 로그인 흐름에서만 호출한다.
        /// </summary>
        /// <returns>서버 유저 보장 응답 토큰.</returns>
        public UniTask<JToken> Ensure()
        {
            return _client.PostAsync("/user/ensure");
        }
    }

    /// <summary>
    /// 상점 API 래퍼.
    /// </summary>
    public sealed class ShopApi
    {
        private readonly PlayerApiClient _client;

        /// <summary>
        /// 상점 API 래퍼를 생성한다.
        /// </summary>
        /// <param name="client">공통 Player API 클라이언트.</param>
        public ShopApi(PlayerApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 클라이언트 카탈로그 버전을 서버에 전달하고 유저별 상점 상태를 조회한다.
        /// </summary>
        /// <param name="catalogVersion">클라이언트가 현재 사용하는 상점 카탈로그 버전.</param>
        /// <returns>서버 상점 Bootstrap 응답 토큰.</returns>
        public UniTask<JToken> Bootstrap(string catalogVersion)
        {
            var body = string.IsNullOrEmpty(catalogVersion)
                ? null
                : new Dictionary<string, object> { { "catalogVersion", catalogVersion } };

            return _client.PostAsync("/shop/bootstrap", body);
        }

        /// <summary>
        /// 상점 상품 구매를 요청한다.
        /// requestId를 함께 보내 재시도 상황에서도 서버가 멱등성을 판단할 수 있게 한다.
        /// </summary>
        /// <param name="offerId">구매할 상품 ID.</param>
        /// <param name="requestId">클라이언트가 생성한 멱등성 요청 ID.</param>
        /// <param name="catalogVersion">구매 기준이 되는 상점 카탈로그 버전.</param>
        /// <returns>서버 구매 응답 토큰.</returns>
        public UniTask<JToken> Purchase(int offerId, string requestId, string catalogVersion)
        {
            return _client.PostAsync("/shop/purchase", new Dictionary<string, object>
            {
                { "offerId", offerId },
                { "requestId", requestId },
                { "catalogVersion", catalogVersion }
            });
        }
    }

    /// <summary>
    /// 가챠 API 래퍼.
    /// 구매와 같은 requestId 멱등성 계약을 사용하는 두 번째 쓰기 도메인 예시다.
    /// </summary>
    public sealed class GachaApi
    {
        private readonly PlayerApiClient _client;

        /// <summary>
        /// 가챠 API 래퍼를 생성한다.
        /// </summary>
        /// <param name="client">공통 Player API 클라이언트.</param>
        public GachaApi(PlayerApiClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 클라이언트 가챠 카탈로그 버전을 서버에 전달하고 유저별 뽑기 가능 상태를 조회한다.
        /// </summary>
        /// <param name="catalogVersion">클라이언트가 현재 사용하는 가챠 카탈로그 버전.</param>
        /// <returns>서버 가챠 Bootstrap 응답 토큰.</returns>
        public UniTask<JToken> Bootstrap(string catalogVersion)
        {
            var body = string.IsNullOrEmpty(catalogVersion)
                ? null
                : new Dictionary<string, object> { { "catalogVersion", catalogVersion } };

            return _client.PostAsync("/gacha/bootstrap", body);
        }

        /// <summary>
        /// 가챠 뽑기를 요청한다.
        /// 결과 확정은 전적으로 서버가 담당하고, 클라이언트는 requestId로 중복 지급만 방지한다.
        /// </summary>
        /// <param name="gachaId">뽑을 가챠 ID.</param>
        /// <param name="requestId">클라이언트가 생성한 멱등성 요청 ID.</param>
        /// <param name="catalogVersion">뽑기 기준이 되는 가챠 카탈로그 버전.</param>
        /// <returns>서버 뽑기 응답 토큰.</returns>
        public UniTask<JToken> Draw(int gachaId, string requestId, string catalogVersion)
        {
            return _client.PostAsync("/gacha/draw", new Dictionary<string, object>
            {
                { "gachaId", gachaId },
                { "requestId", requestId },
                { "catalogVersion", catalogVersion }
            });
        }
    }
}
