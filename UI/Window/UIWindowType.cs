namespace SampleClient.UI
{
    /// <summary>
    /// UIWindowService에서 관리하는 UI 창 종류 enum.
    /// Shop/Gacha는 prefab window, MailBox는 additive scene window로 열린다.
    /// </summary>
    public enum UIWindowType
    {
        None,
        Shop,
        Gacha,
        MailBox
        // 중략: 동일한 패턴으로 열리는 다른 콘텐츠 UI
    }
}
