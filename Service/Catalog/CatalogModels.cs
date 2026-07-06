using System;
using System.Collections.Generic;

namespace SampleClient.Service.Catalog
{
    // 포트폴리오용 샘플 모델입니다.
    // 정책 및 운영툴 전용 필드는 중략했습니다.

    [Serializable]
    public class ClientCatalogMeta
    {
        public string catalogVersion;
        public string entry;
        public string hash;
        // 중략: 스키마, 배포 메타데이터, 청크 데이터
    }

    [Serializable]
    public class ShopCatalog
    {
        public string catalogVersion;
        public ShopOffer[] offers;
        // 중략: 탭 구성, 스키마, 배포 메타데이터
    }

    [Serializable]
    public class ShopOffer
    {
        public int offerId;
        public string code;
        public string tabId;
        public string title;
        public string description;
        public bool isActive;
        public Price price;
        public ShopReward[] rewards;
        // 중략: 노출/완료 정책, 릴레이 힌트, 운영 기간
    }

    [Serializable]
    public class Price
    {
        public string type;
        public int amount;
    }

    [Serializable]
    public class ShopReward
    {
        public int itemId;
        public int quantity;
    }

    [Serializable]
    public class GachaCatalog
    {
        public string catalogVersion;
        public GachaInfo[] gachas;
        // 중략: 스키마 및 배포 메타데이터
    }

    [Serializable]
    public class GachaInfo
    {
        public int gachaId;
        public string code;
        public string name;
        public string description;
        public Price price;
        public bool isActive;
        public int sortOrder;
        // 중략: 노출 정책, 운영 기간, 등급별 확률/보상 구성
        //       실제 확률 계산과 결과 확정은 서버가 담당한다.
    }

    [Serializable]
    public class ItemCatalog
    {
        public string catalogVersion;
        public Dictionary<int, ItemInfo> itemsById;
        // 중략: 스키마 및 배포 메타데이터
    }

    [Serializable]
    public class ItemInfo
    {
        public int itemId;
        public string file;
        public string name;
        public string description;
        public string rarity; // normal, rare, epic, legend 등 일반화된 등급명
        public string category;
        public bool isActive;
        // 중략: 에셋 주소 변형 및 운영툴 메타데이터
    }
}

