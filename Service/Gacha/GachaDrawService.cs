using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Service.Firebase;

namespace SampleClient.Service.Gacha
{
    /// <summary>
    /// 가챠 뽑기 서버 요청, 재시도 정책, Draw 결과의 런타임 캐시 반영을 담당한다.
    /// Presenter는 버튼 상태, 팝업, 연출 재생 같은 UI 흐름만 처리한다.
    /// Shop 구매와 같은 requestId 멱등성 계약을 사용하는 두 번째 도메인 예시로,
    /// 재시도 판단 자체는 RequestIdRetryPolicy 한 곳을 공유한다.
    /// </summary>
    internal static class GachaDrawService
    {
        /// <summary>
        /// 지정한 가챠를 서버에 Draw 요청한다.
        /// 일시적인 통신 오류나 서버 내부 오류는 같은 requestId로 재시도한다.
        /// </summary>
        /// <param name="gachaId">Draw할 가챠 Id.</param>
        /// <param name="requestId">멱등성 보장을 위해 같은 Draw 시도에서 유지할 요청 Id.</param>
        /// <param name="catalogVersion">클라이언트가 현재 사용 중인 가챠 카탈로그 버전.</param>
        /// <param name="cancellationToken">Presenter 파괴 시 재시도 대기를 중단하기 위한 취소 토큰.</param>
        /// <returns>서버가 반환한 Draw 결과.</returns>
        internal static async UniTask<GachaDrawResponse> RequestAsync(int gachaId, string requestId, string catalogVersion, CancellationToken cancellationToken)
        {
            for (var i = 0; i < RequestIdRetryPolicy.MaxAttemptCount; i++)
            {
                try
                {
                    var token = await FirebaseService.Instance.Api.Gacha.Draw(gachaId, requestId, catalogVersion);
                    return FirebaseUtil.ToObject<GachaDrawResponse>(token);
                }
                catch (PlayerApiException e)
                {
                    if (!ShouldPreserveRequestId(e) || i >= RequestIdRetryPolicy.MaxAttemptCount - 1)
                    {
                        throw;
                    }

                    await RequestIdRetryPolicy.DelayBeforeRetryAsync(
                        nameof(GachaDrawService),
                        e,
                        i,
                        cancellationToken);
                }
            }

            throw new InvalidOperationException("[GachaDrawService] 뽑기 요청 재시도 실패");
        }

        /// <summary>
        /// 예외 발생 후 같은 requestId를 유지해야 하는 오류인지 판단한다.
        /// 재시도 가능한 오류라면 requestId를 유지해야 서버 멱등성 정책과 맞는다.
        /// </summary>
        /// <param name="e">Draw 요청 중 발생한 예외.</param>
        /// <returns>같은 requestId를 유지해야 하면 true.</returns>
        internal static bool ShouldPreserveRequestId(Exception e)
        {
            return RequestIdRetryPolicy.ShouldPreserveRequestId(e, GachaCode.GACHA_IDEMPOTENCY_CONFLICT);
        }

        /// <summary>
        /// Draw 응답의 재화와 지급 아이템을 런타임 캐시에 반영한다.
        /// 재화가 바뀌었으면 Presenter가 재화 변경 알림을 발행할 수 있도록 true를 반환한다.
        /// </summary>
        /// <param name="drawResult">서버가 확정한 Draw 결과.</param>
        /// <returns>재화 캐시가 변경되었으면 true.</returns>
        internal static bool ApplyDrawState(GachaDrawResponse drawResult)
        {
            var currencyChanged = ApplyDrawBalance(drawResult);
            ApplyGrantedItem(drawResult);

            return currencyChanged;
        }

        /// <summary>
        /// 지급 보상의 assetId를 아이템 Id로 변환한다.
        /// 아이템 보상이 아니거나 숫자로 변환할 수 없으면 0을 반환한다.
        /// </summary>
        /// <param name="reward">서버가 확정한 지급 보상.</param>
        /// <returns>아이템 Id로 변환할 수 있으면 해당 Id, 아니면 0.</returns>
        internal static int GetRewardId(GachaDrawReward reward)
        {
            if (reward == null)
            {
                return 0;
            }

            return int.TryParse(reward.assetId, out var itemId) ? itemId : 0;
        }

        /// <summary>
        /// 지급 보상이 장착 가능한 아이템 타입인지 확인한다.
        /// </summary>
        /// <param name="reward">서버가 확정한 지급 보상.</param>
        /// <returns>아이템 보상이면 true.</returns>
        internal static bool IsItemReward(GachaDrawReward reward)
        {
            return reward != null && string.Equals(reward.kind, "item", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Draw 응답의 남은 재화를 런타임 캐시에 반영한다.
        /// </summary>
        /// <param name="drawResult">서버가 확정한 Draw 결과.</param>
        /// <returns>재화 캐시가 변경되었으면 true.</returns>
        private static bool ApplyDrawBalance(GachaDrawResponse drawResult)
        {
            if (drawResult == null || drawResult.remainingBalance == null)
            {
                return false;
            }

            GameBootstrapService.ApplyCurrencies(drawResult.remainingBalance);
            return true;
        }

        /// <summary>
        /// Draw 응답의 지급 아이템을 유저 인벤토리 런타임 캐시에 반영한다.
        /// </summary>
        /// <param name="drawResult">서버가 확정한 Draw 결과.</param>
        private static void ApplyGrantedItem(GachaDrawResponse drawResult)
        {
            var reward = drawResult != null ? drawResult.grantedReward : null;
            if (!IsItemReward(reward))
            {
                return;
            }

            var itemId = GetRewardId(reward);
            if (itemId == 0)
            {
                return;
            }

            GameBootstrapService.ApplyInventoryItem(itemId, reward.quantity);
        }
    }
}
