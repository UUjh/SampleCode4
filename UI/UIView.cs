using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SampleClient.UI
{
    /// <summary>
    /// R3 구독을 View 생명주기에 맞춰 정리하는 기본 View.
    /// 버튼 OnClickAsObservable 같은 UI 구독은 AddSubscription(disposable)로 등록한다.
    /// </summary>
    public abstract class UIView : MonoBehaviour
    {
        private CompositeDisposable _subscriptions;

        /// <summary>
        /// 중복 구독 방지를 위해 구독 목록을 초기화한다.
        /// </summary>
        protected void ClearSubscriptions()
        {
            _subscriptions?.Dispose();
            _subscriptions = new CompositeDisposable();
            _subscriptions.AddTo(this);
        }

        /// <summary>
        /// View 생명주기에 맞춰 IDisposable 구독을 등록한다.
        /// </summary>
        protected void AddSubscription(IDisposable disposable)
        {
            if (_subscriptions == null)
            {
                ClearSubscriptions();
            }

            disposable?.AddTo(_subscriptions);
        }

        /// <summary>
        /// 대상 오브젝트가 있을 때만 활성 상태를 변경한다.
        /// </summary>
        protected static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        /// <summary>
        /// 텍스트 컴포넌트가 있을 때만 문자열을 설정한다.
        /// </summary>
        protected static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        protected static void SetImage(Image img, Sprite sprite)
        {
            if (img != null)
            {
                img.sprite = sprite;
            }
        }

        /// <summary>
        /// 버튼 컴포넌트가 있을 때만 색상을 변경한다.
        /// </summary>
        /// <param name="button"></param>
        /// <param name="color"></param>
        protected static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.image != null)
            {
                button.image.color = color;
            }
        }
    }
}




