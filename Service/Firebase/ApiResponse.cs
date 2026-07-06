using Newtonsoft.Json.Linq;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Player API의 공통 응답 포맷.
    /// code 값은 아래 도메인 코드 상수와 같은 서버 계약을 따른다.
    /// </summary>
    public class ApiResponse
    {
        public bool isSuccess;
        public int code;
        public string message;
        public JToken data;
    }

    /// <summary>
    /// 공통 응답 코드.
    /// HTTP 상태 코드와 같은 값 공간을 쓰는 서버 계약이므로 매직 넘버 대신 이 상수로 판단한다.
    /// </summary>
    public static class CommonCode
    {
        public const int SUCCESS = 200;
        public const int BAD_REQUEST = 400;
        public const int UNAUTHORIZED = 401;
        public const int FORBIDDEN = 403;
        public const int CONFLICT = 409;
        public const int RATE_LIMIT = 429;
        public const int APP_CHECK_REQUIRED = 440;
        public const int APP_CHECK_INVALID = 441;
        public const int INTERNAL = 500;
        public const int MAINTENANCE = 503;
    }

    /// <summary>
    /// 관리자/제재 관련 응답 코드.
    /// 샘플에서는 클라이언트 재시도 판단에 쓰는 코드만 남겼다.
    /// </summary>
    public static class AdminCode
    {
        public const int USER_BANNED = 3011;
        // 중략: 운영툴 전용 관리자 도메인 코드
    }

    /// <summary>
    /// 상점 응답 코드.
    /// 샘플에서는 구매 흐름에서 분기하는 대표 코드만 남겼다.
    /// </summary>
    public static class ShopCode
    {
        public const int OFFER_NOT_FOUND = 5000;
        public const int CATALOG_VERSION_MISMATCH = 5001;
        public const int NOT_PURCHASABLE = 5002;
        public const int INSUFFICIENT_BALANCE = 5003;
        public const int IDEMPOTENCY_CONFLICT = 5006;
        // 중략: 선행 조건, 릴레이, 트랜잭션 관련 상세 코드
    }

    /// <summary>
    /// 가챠 응답 코드.
    /// 샘플에서는 뽑기 재시도 판단에 쓰는 대표 코드만 남겼다.
    /// </summary>
    public static class GachaCode
    {
        public const int GACHA_NOT_FOUND = 6000;
        public const int GACHA_CATALOG_VERSION_MISMATCH = 6001;
        public const int GACHA_INSUFFICIENT_BALANCE = 6003;
        public const int GACHA_IDEMPOTENCY_CONFLICT = 6005;
        // 중략: 뽑기 정책, 트랜잭션 관련 상세 코드
    }

    /// <summary>
    /// 응답 코드 이름.
    /// 재시도/실패 로그에 코드 숫자 대신 사람이 읽을 수 있는 이름을 남기기 위해 사용한다.
    /// </summary>
    public static class ApiCodeNames
    {
        /// <summary>
        /// API 도메인 코드를 사람이 읽을 수 있는 이름으로 변환한다.
        /// </summary>
        public static string Describe(int code)
        {
            switch (code)
            {
                case CommonCode.SUCCESS: return "SUCCESS";
                case CommonCode.BAD_REQUEST: return "BAD_REQUEST";
                case CommonCode.UNAUTHORIZED: return "UNAUTHORIZED";
                case CommonCode.FORBIDDEN: return "FORBIDDEN";
                case CommonCode.CONFLICT: return "CONFLICT";
                case CommonCode.RATE_LIMIT: return "RATE_LIMIT";
                case CommonCode.APP_CHECK_REQUIRED: return "APP_CHECK_REQUIRED";
                case CommonCode.APP_CHECK_INVALID: return "APP_CHECK_INVALID";
                case CommonCode.INTERNAL: return "INTERNAL";
                case CommonCode.MAINTENANCE: return "MAINTENANCE";

                case AdminCode.USER_BANNED: return "USER_BANNED";

                case ShopCode.OFFER_NOT_FOUND: return "OFFER_NOT_FOUND";
                case ShopCode.CATALOG_VERSION_MISMATCH: return "CATALOG_VERSION_MISMATCH";
                case ShopCode.NOT_PURCHASABLE: return "NOT_PURCHASABLE";
                case ShopCode.INSUFFICIENT_BALANCE: return "INSUFFICIENT_BALANCE";
                case ShopCode.IDEMPOTENCY_CONFLICT: return "IDEMPOTENCY_CONFLICT";

                case GachaCode.GACHA_NOT_FOUND: return "GACHA_NOT_FOUND";
                case GachaCode.GACHA_CATALOG_VERSION_MISMATCH: return "GACHA_CATALOG_VERSION_MISMATCH";
                case GachaCode.GACHA_INSUFFICIENT_BALANCE: return "GACHA_INSUFFICIENT_BALANCE";
                case GachaCode.GACHA_IDEMPOTENCY_CONFLICT: return "GACHA_IDEMPOTENCY_CONFLICT";

                // 중략: 메일, 장비, 일일보상 등 다른 도메인 코드 이름
                default: return "UNKNOWN";
            }
        }
    }
}
