using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SampleClient.Utils;

namespace SampleClient.Service.Firebase
{
    /// <summary>
    /// requestId 멱등성을 사용하는 쓰기 요청의 공통 재시도 정책.
    /// Shop 구매와 Gacha 뽑기처럼 같은 requestId 재호출을 서버가 중복 처리로 방어하는 API에서만 사용한다.
    /// </summary>
    internal static class RequestIdRetryPolicy
    {
        internal const int MaxAttemptCount = 3;
        private const int RetryDelayMs = 500;

        /// <summary>
        /// 실패 후 같은 requestId를 유지해야 하는 오류인지 판단한다.
        /// timeout, network error, 5xx는 서버 처리 여부가 미확정이므로 같은 requestId를 유지한다.
        /// </summary>
        /// <param name="e">요청 중 발생한 예외.</param>
        /// <param name="idempotencyConflictCode">도메인별 idempotency conflict 코드.</param>
        /// <returns>같은 requestId를 유지해야 하면 true.</returns>
        internal static bool ShouldPreserveRequestId(Exception e, int idempotencyConflictCode)
        {
            if (e is PlayerApiException playerApiException)
            {
                return ShouldPreserveRequestId(playerApiException, idempotencyConflictCode);
            }

            return false;
        }

        /// <summary>
        /// 실패 후 같은 requestId를 유지해야 하는 Player API 오류인지 판단한다.
        /// </summary>
        /// <param name="e">Player API 예외.</param>
        /// <param name="idempotencyConflictCode">도메인별 idempotency conflict 코드.</param>
        /// <returns>같은 requestId를 유지해야 하면 true.</returns>
        internal static bool ShouldPreserveRequestId(PlayerApiException e, int idempotencyConflictCode)
        {
            if (e == null || IsBusinessFailure(e.HttpStatus, e.DomainCode, idempotencyConflictCode))
            {
                return false;
            }

            return e.HttpStatus == 0 || e.HttpStatus >= CommonCode.INTERNAL;
        }

        /// <summary>
        /// 같은 requestId 재시도 전 짧게 대기한다.
        /// </summary>
        /// <param name="owner">로그에 표시할 호출 서비스 이름.</param>
        /// <param name="e">재시도를 유발한 Player API 예외.</param>
        /// <param name="attemptIndex">0부터 시작하는 현재 시도 인덱스.</param>
        /// <param name="cancellationToken">재시도 대기 취소 토큰.</param>
        /// <returns>재시도 전 대기 작업.</returns>
        internal static async UniTask DelayBeforeRetryAsync(
            string owner,
            PlayerApiException e,
            int attemptIndex,
            CancellationToken cancellationToken)
        {
            Log.LogMessage(
                $"[{owner}] 같은 requestId로 재시도: http={e.HttpStatus} code={e.DomainCode}({ApiCodeNames.Describe(e.DomainCode)}) attempt={attemptIndex + 1}",
                Log.LogLevel.Warning);

            await UniTask.Delay(RetryDelayMs, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 자동 재시도하면 안 되는 확정 비즈니스 실패인지 확인한다.
        /// 잘못된 요청, 권한 거부, 충돌, idempotency conflict는 다시 보내도 결과가 같으므로 재시도하지 않는다.
        /// </summary>
        /// <param name="httpStatus">HTTP 상태 코드.</param>
        /// <param name="domainCode">서버 도메인 오류 코드.</param>
        /// <param name="idempotencyConflictCode">도메인별 idempotency conflict 코드.</param>
        /// <returns>비즈니스 실패면 true.</returns>
        private static bool IsBusinessFailure(long httpStatus, int domainCode, int idempotencyConflictCode)
        {
            return httpStatus == CommonCode.BAD_REQUEST ||
                   httpStatus == CommonCode.FORBIDDEN ||
                   httpStatus == CommonCode.CONFLICT ||
                   domainCode == idempotencyConflictCode;
        }
    }
}
