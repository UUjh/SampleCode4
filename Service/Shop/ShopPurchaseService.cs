using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SampleClient.Service.Bootstrap;
using SampleClient.Service.Firebase;

namespace SampleClient.Service.Shop
{
    /// <summary>
    /// 상점 구매 요청과 구매 결과의 런타임 캐시 반영을 담당한다.
    /// ShopPresenter는 구매 버튼, 팝업, 선택 상품 갱신 같은 UI 흐름만 처리하고,
    /// 서버 요청과 구매 결과 캐시 반영은 이 서비스로 모은다.
    /// </summary>
    internal static class ShopPurchaseService
    {
        /// <summary>
        /// 지정한 상품을 서버에 구매 요청한다.
        /// requestId는 Presenter가 구매 시도 단위로 생성하고, 재시도 가능한 실패에서는 같은 값을 유지한다.
        /// </summary>
        /// <param name="offerId">구매할 상점 상품 Id.</param>
        /// <param name="requestId">멱등성 보장을 위해 같은 구매 시도에서 유지할 요청 Id.</param>
        /// <param name="catalogVersion">클라이언트가 현재 사용 중인 상점 카탈로그 버전.</param>
        /// <param name="cancellationToken">Presenter 파괴 시 재시도 대기를 중단하기 위한 취소 토큰.</param>
        /// <returns>서버가 확정한 구매 결과.</returns>
        internal static async UniTask<ShopPurchaseResponse> RequestAsync(int offerId, string requestId, string catalogVersion, CancellationToken cancellationToken)
        {
            for (var i = 0; i < RequestIdRetryPolicy.MaxAttemptCount; i++)
            {
                try
                {
                    var token = await FirebaseService.Instance.Api.Shop.Purchase(offerId, requestId, catalogVersion);
                    return FirebaseUtil.ToObject<ShopPurchaseResponse>(token);
                }
                catch (PlayerApiException e)
                {
                    if (!ShouldPreserveRequestId(e) || i >= RequestIdRetryPolicy.MaxAttemptCount - 1)
                    {
                        throw;
                    }

                    await RequestIdRetryPolicy.DelayBeforeRetryAsync(
                        nameof(ShopPurchaseService),
                        e,
                        i,
                        cancellationToken);
                }
            }

            throw new InvalidOperationException("[ShopPurchaseService] 구매 요청 재시도 실패");
        }

        /// <summary>
        /// 구매 실패 후 같은 requestId를 유지해야 하는 오류인지 판단한다.
        /// timeout, network error, 5xx는 서버 처리 여부가 미확정이므로 같은 requestId를 유지한다.
        /// </summary>
        /// <param name="e">구매 요청 중 발생한 예외.</param>
        /// <returns>같은 requestId를 유지해야 하는 오류면 true.</returns>
        internal static bool ShouldPreserveRequestId(Exception e)
        {
            return RequestIdRetryPolicy.ShouldPreserveRequestId(e, ShopCode.IDEMPOTENCY_CONFLICT);
        }

        /// <summary>
        /// 구매 응답을 Bootstrap 캐시에 반영한다.
        /// 단일 구매 결과만으로 갱신 가능한 일반 상품에 사용한다.
        /// 반환값은 Presenter가 UI 갱신과 재화 변경 알림에 사용한다.
        /// </summary>
        /// <param name="purchase">서버가 반환한 구매 결과.</param>
        /// <returns>
        /// 갱신된 상점 Bootstrap 캐시, 새 catalogVersion, 재화 변경 여부.
        /// Bootstrap 캐시가 없거나 구매 결과가 없으면 shop은 null일 수 있다.
        /// </returns>
        internal static (ShopBootstrapResponse shop, string catalogVersion, bool currencyChanged) ApplyPurchaseResult(ShopPurchaseResponse purchase)
        {
            if (purchase == null)
            {
                return (GameBootstrapService.Data != null ? GameBootstrapService.Data.shop : null, null, false);
            }

            var shop = GameBootstrapService.Data != null ? GameBootstrapService.Data.shop : null;
            if (shop == null)
            {
                return (null, null, false);
            }

            if (!string.IsNullOrEmpty(purchase.catalogVersion))
            {
                shop.catalogVersion = purchase.catalogVersion;
            }

            var currencyChanged = ApplyPurchaseUserState(purchase);
            ApplyPurchasedOfferState(shop, purchase.offerId);

            return (shop, purchase.catalogVersion, currencyChanged);
        }

        /// <summary>
        /// 구매 응답의 유저 재화와 지급 아이템을 런타임 캐시에 반영한다.
        /// 재화가 바뀌었으면 Presenter가 재화 변경 알림을 발행할 수 있도록 true를 반환한다.
        /// </summary>
        /// <param name="purchase">서버가 반환한 구매 결과.</param>
        /// <returns>재화 캐시가 변경되었으면 true.</returns>
        internal static bool ApplyPurchaseUserState(ShopPurchaseResponse purchase)
        {
            if (purchase == null)
            {
                return false;
            }

            var currencyChanged = purchase.remainingBalance != null;
            if (currencyChanged)
            {
                GameBootstrapService.ApplyCurrencies(purchase.remainingBalance);
            }

            ApplyGrantedRewards(purchase.grantedRewards);
            return currencyChanged;
        }

        /// <summary>
        /// 구매 후 전체 Shop bootstrap 재조회가 필요한 상품인지 확인한다.
        /// 릴레이 상품은 다음 단계나 주변 상품 상태가 바뀔 수 있으므로 서버 상태를 다시 받는다.
        /// </summary>
        /// <param name="offerId">구매한 상점 상품 Id.</param>
        /// <returns>구매 후 Shop bootstrap을 다시 요청해야 하면 true.</returns>
        internal static bool NeedsBootstrapAfterPurchase(int offerId)
        {
            var shop = GameBootstrapService.Data != null ? GameBootstrapService.Data.shop : null;
            var offerState = GetOfferState(shop, offerId);

            return offerState != null && offerState.nextRelayStep.HasValue;
        }

        /// <summary>
        /// 단일 구매 응답만으로 확정 가능한 상품 상태를 로컬 Shop bootstrap 캐시에 반영한다.
        /// 릴레이 상품처럼 주변 상품 상태가 함께 바뀔 수 있는 경우에는 이 메서드 대신 bootstrap 재조회가 필요하다.
        /// </summary>
        /// <param name="shop">현재 런타임에 저장된 Shop bootstrap 캐시.</param>
        /// <param name="offerId">구매 완료 처리할 상점 상품 Id.</param>
        private static void ApplyPurchasedOfferState(ShopBootstrapResponse shop, int offerId)
        {
            var offerState = GetOfferState(shop, offerId);
            if (offerState == null)
                return;

            offerState.purchasedCount += 1;
            offerState.isCompleted = true;
            offerState.isPurchased = true;
            offerState.isPurchasable = false;
        }

        /// <summary>
        /// 구매 응답에 포함된 지급 아이템을 유저 인벤토리 런타임 캐시에 반영한다.
        /// </summary>
        /// <param name="rewards">구매로 지급된 보상 목록.</param>
        private static void ApplyGrantedRewards(ShopGrantedReward[] rewards)
        {
            if (rewards == null)
                return;

            for (var i = 0; i < rewards.Length; i++)
            {
                var reward = rewards[i];
                if (reward == null || reward.itemId == 0)
                {
                    continue;
                }

                GameBootstrapService.ApplyInventoryItem(reward.itemId, reward.quantity);
            }
        }

        /// <summary>
        /// 현재 Shop bootstrap 캐시에서 지정 상품의 서버 상태를 찾는다.
        /// </summary>
        /// <param name="shop">조회할 Shop bootstrap 응답.</param>
        /// <param name="offerId">상태를 찾을 상점 상품 Id.</param>
        /// <returns>상품 상태가 있으면 해당 상태, 없으면 null.</returns>
        private static ShopOfferState GetOfferState(ShopBootstrapResponse shop, int offerId)
        {
            if (shop == null || shop.offerStates == null || offerId == 0 || !shop.offerStates.TryGetValue(offerId, out var offerState))
                return null;

            return offerState;
        }
    }
}
