namespace SampleClient.UI.Shop
{
    /// <summary>
    /// ShopPresenter에서 관리하는 섹션 타입.
    /// 같은 Shop window 안에서 섹션 전환은 route로 기록되어 뒤로가기 복귀에 사용된다.
    /// 샘플에서는 일반화된 섹션 이름을 사용합니다.
    /// </summary>
    public enum ShopSectionType
    {
        Featured,
        CharacterShop,
        Resources
    }
}
