namespace SampleClient.Scene
{
    public static class SceneNames
    {
        public const string Boot = "BootScene";
        public const string LogIn = "LogInScene";
        public const string Main = "MainScene";

        // additive scene 방식으로 유지하는 UI window 씬.
        // Shop/Gacha window는 prefab 방식으로 전환되어 씬 이름 대신 UIPrefabAddress를 사용한다.
        public const string MailBox = "MailBoxScene";

        // 중략: 동일한 방식으로 관리되는 기타 Scene 이름
    }
}
