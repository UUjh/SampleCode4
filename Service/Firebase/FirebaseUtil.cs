using Newtonsoft.Json.Linq;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Player API 응답 data 토큰을 DTO로 변환하는 작은 유틸리티.
    /// </summary>
    public static class FirebaseUtil
    {
        public static T ToObject<T>(JToken token)
        {
            return token != null ? token.ToObject<T>() : default;
        }
    }
}
