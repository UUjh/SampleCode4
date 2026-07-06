using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SampleClient.Service.Catalog;

namespace SampleClient.Service.Bootstrap
{
    // 포트폴리오용 축약 모델입니다.
    // 실제 Response의 식별자, 운영 정책, 분석 필드, 상세 상태값은 중략했습니다.

    [Serializable]
    public class UserProfileResponse
    {
        public string nickName;
        public int exp;
        // 중략: 계정 식별자, 권한, 분석 카운터
    }

    [Serializable]
    public class UserBootstrapResponse
    {
        public UserProfileResponse profile;
        public UserWalletResponse wallet;
        public UserInventoryResponse inventory;
        public CurrentEquipmentResponse currentEquipment;
        // 중략: 서버 메타데이터
    }

    [Serializable]
    public class UserWalletResponse
    {
        public Dictionary<string, JToken> currencies;
    }

    [Serializable]
    public class UserInventoryResponse
    {
        public UserInventoryItem[] items;

        /// <summary>
        /// 지정 아이템 보유 여부를 확인한다.
        /// </summary>
        public bool HasItem(int itemId)
        {
            if (items == null || itemId == 0)
            {
                return false;
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].itemId == itemId && items[i].quantity > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public class UserInventoryItem
    {
        public int itemId;
        public int quantity;
    }

    [Serializable]
    public class CurrentEquipmentResponse
    {
        public Dictionary<string, int?> slots;
    }

    [Serializable]
    public class ShopBootstrapResponse
    {
        public string catalogVersion;
        public Dictionary<string, JToken> currencies;
        public Dictionary<int, ShopOfferState> offerStates;
        // 중략: 유저별 구매/잠금/노출 정책 상태
    }

    [Serializable]
    public class ShopOfferState
    {
        public bool isVisible;
        public bool isPurchased;
        public bool isPurchasable;
        public bool isCompleted;
        public int purchasedCount;
        public int? nextRelayStep; // 값이 있으면 구매 후 주변 상품 상태가 함께 바뀌는 릴레이 상품
        // 중략: 잠금 사유, 구매 제한, 기간 정책
    }

    [Serializable]
    public class ShopPurchaseResponse
    {
        public int offerId;
        public string catalogVersion;
        public Dictionary<string, JToken> remainingBalance;
        public ShopGrantedReward[] grantedRewards;
        // 중략: 구매 식별자, 감사 로그, 정책 상태
    }

    [Serializable]
    public class ShopGrantedReward
    {
        public int itemId;
        public int quantity;
    }

    [Serializable]
    public class GachaBootstrapResponse
    {
        public string catalogVersion;
        // 중략: 유저별 뽑기 가능 상태, 기간 정책
    }

    [Serializable]
    public class GachaDrawResponse
    {
        public int gachaId;
        public string catalogVersion;
        public Dictionary<string, JToken> remainingBalance;
        public GachaDrawReward grantedReward;
        // 중략: 뽑기 식별자, 연출용 메타데이터
    }

    [Serializable]
    public class GachaDrawReward
    {
        public string kind;    // item, currency 등 보상 종류
        public string assetId; // kind가 item이면 아이템 Id 문자열
        public int quantity;
        // 중략: 등급, 표시 정렬 필드
    }

    /// <summary>
    /// Main 진입 시 준비하는 아웃게임 런타임 캐시.
    /// Shop/Gacha 같은 콘텐츠 Presenter는 이 데이터로 초기화하고,
    /// 구매/뽑기 후에는 GameBootstrapService의 Apply 메서드로 필요한 부분만 갱신한다.
    /// </summary>
    public class GameBootstrapData
    {
        public UserProfileResponse userProfile;
        public UserWalletResponse wallet;
        public UserInventoryResponse inventory;
        public CurrentEquipmentResponse currentEquipment;
        public ShopCatalog shopCatalog;
        public ShopBootstrapResponse shop;
        public GachaCatalog gachaCatalog;
        public GachaBootstrapResponse gacha;
        public ItemCatalog item;
        // 중략: 메일, 퀴즈, 일일보상 등 동일 구조로 확장되는 다른 콘텐츠 모듈
    }
}
