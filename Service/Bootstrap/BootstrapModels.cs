using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SampleClient.Service.Catalog;

namespace SampleClient.Service.Bootstrap
{
    // 공개 포트폴리오용 축약 모델입니다.
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

    public class AppBootstrapData
    {
        public UserProfileResponse userProfile;
        public UserWalletResponse wallet;
        public UserInventoryResponse inventory;
        public CurrentEquipmentResponse currentEquipment;
        public ShopCatalog shopCatalog;
        public ShopBootstrapResponse shop;
        public ItemCatalog item;
        // 중략: 동일 구조로 확장되는 다른 콘텐츠 모듈
    }
}

