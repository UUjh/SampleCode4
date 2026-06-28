using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SampleClient.Core
{
    /// <summary>
    /// 단독 씬 테스트 시 필요한 최소 런타임 오브젝트를 보정한다.
    /// 프로젝트별 서비스 컨테이너, 메시지 버스, 분석 SDK 초기화는 샘플에서 중략했다.
    /// </summary>
    public static class SampleRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneObject()
        {
            EnsureEventSystem();
        }

        /// <summary>
        /// EventSystem이 없으면 InputSystem UI 모듈과 함께 생성한다.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystem = new GameObject(nameof(EventSystem));
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
