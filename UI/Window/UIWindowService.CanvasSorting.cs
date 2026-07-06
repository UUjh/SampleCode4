using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleClient.UI
{
    /// <summary>
    /// UIWindowService의 Canvas 정렬 보정 partial.
    /// additive scene과 prefab window가 섞이면 로드 순서에 따라 표시 순서가 달라질 수 있으므로,
    /// window/overlay 계층마다 명시한 sortingOrder를 적용해 표시 순서를 고정한다.
    /// </summary>
    public partial class UIWindowService
    {
        /// <summary>
        /// 지정한 scene의 root Canvas 정렬값을 적용한다.
        /// 일반 window scene은 Presenter를 찾기 전에 Canvas 순서를 먼저 맞춰도 되므로,
        /// scene root 아래에서 다른 Canvas를 부모로 갖지 않는 기준 Canvas만 대상으로 삼는다.
        /// </summary>
        /// <param name="sceneName">정렬값을 적용할 additive scene 이름.</param>
        /// <param name="sortingOrder">
        /// 적용할 Canvas sortingOrder 값.
        /// MainScene의 기준값 0보다 큰 값을 사용해 additive UI가 Main 위에 표시되게 한다.
        /// </param>
        private static void ApplySceneCanvasSorting(string sceneName, int sortingOrder)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                ApplyRootCanvasSorting(roots[i], sortingOrder);
            }
        }

        /// <summary>
        /// scene root 아래의 기준 Canvas 정렬값을 적용한다.
        /// 자식 Canvas가 별도 내부 레이어로 쓰이는 경우를 깨지 않기 위해,
        /// 부모 Canvas가 없는 Canvas만 sortingOrder 변경 대상으로 삼는다.
        /// </summary>
        /// <param name="root">additive scene의 root GameObject.</param>
        /// <param name="sortingOrder">root 기준 Canvas에 적용할 sortingOrder 값.</param>
        private static void ApplyRootCanvasSorting(GameObject root, int sortingOrder)
        {
            if (root == null)
            {
                return;
            }

            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || HasParentCanvas(canvas))
                {
                    continue;
                }

                ApplyCanvasSorting(canvas, sortingOrder);
            }
        }

        /// <summary>
        /// owner 본인 또는 부모에 있는 Canvas 정렬값을 적용한다.
        /// prefab window/overlay처럼 instance 기준으로 처리할 수 있는 경로에서 사용한다.
        /// </summary>
        /// <param name="owner">Canvas를 찾기 위한 기준 컴포넌트.</param>
        /// <param name="sortingOrder">적용할 Canvas sortingOrder 값.</param>
        private static void ApplyOwnerCanvasSorting(Component owner, int sortingOrder)
        {
            if (owner == null)
            {
                return;
            }

            var canvas = owner.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = owner.GetComponentInParent<Canvas>();
            }

            ApplyCanvasSorting(canvas, sortingOrder);
        }

        /// <summary>
        /// Canvas가 다른 Canvas의 하위에 있는지 확인한다.
        /// 내부 팝업, 버튼 이펙트, 부분 레이어용 자식 Canvas의 기존 정렬을 보존하기 위한 필터다.
        /// </summary>
        /// <param name="canvas">부모 Canvas 존재 여부를 검사할 Canvas.</param>
        /// <returns>canvas의 부모 계층에 다른 Canvas가 있으면 true.</returns>
        private static bool HasParentCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return false;
            }

            var parent = canvas.transform.parent;
            while (parent != null)
            {
                if (parent.GetComponent<Canvas>() != null)
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        /// <summary>
        /// Canvas overrideSorting과 sortingOrder를 적용한다.
        /// overrideSorting을 켜야 additive scene끼리 로드 순서가 달라도 명시한 sortingOrder 기준으로 표시된다.
        /// </summary>
        /// <param name="canvas">정렬값을 적용할 Canvas.</param>
        /// <param name="sortingOrder">Canvas에 적용할 sortingOrder 값.</param>
        private static void ApplyCanvasSorting(Canvas canvas, int sortingOrder)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
    }
}
