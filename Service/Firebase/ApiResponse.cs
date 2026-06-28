namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// 서버 공통 응답 코드 샘플.
    /// 공개 샘플에서는 상점 구매 흐름에서 처리하는 대표 코드만 남겼습니다.
    /// </summary>
    public static class CommonCode
    {
        public const string UNAUTHORIZED = "UNAUTHORIZED";
        public const string VALIDATION_ERROR = "VALIDATION_ERROR";
        public const string INTERNAL = "INTERNAL";
    }

    public static class ShopCode
    {
        public const string CATALOG_VERSION_MISMATCH = "CATALOG_VERSION_MISMATCH";
        public const string INSUFFICIENT_BALANCE = "INSUFFICIENT_BALANCE";
        public const string OFFER_NOT_FOUND = "OFFER_NOT_FOUND";
        public const string ALREADY_PURCHASED = "ALREADY_PURCHASED";
        // 중략: 운영 정책에 따라 추가되는 상세 도메인 코드
    }

    public static class ApiCodeNames
    {
        public static string Describe(string code)
        {
            return string.IsNullOrEmpty(code) ? "UNKNOWN" : code;
        }
    }
}

