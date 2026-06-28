using System;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// Player API 실패 정보를 담는 예외.
    /// Body는 원문 응답이므로 UI나 운영 로그에 그대로 출력하지 않는다.
    /// </summary>
    public class PlayerApiException : Exception
    {
        public long HttpStatus { get; }
        public int DomainCode { get; }
        public string Body { get; }

        public PlayerApiException(long httpStatus, int domainCode, string message, string body)
            : base(message)
        {
            HttpStatus = httpStatus;
            DomainCode = domainCode;
            Body = body;
        }
    }
}




