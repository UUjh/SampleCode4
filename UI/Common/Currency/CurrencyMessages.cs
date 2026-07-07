using System;
using System.Collections.Generic;
using MessagePipe;
using Newtonsoft.Json.Linq;

namespace SampleClient.UI.Common.Currency
{
    /// <summary>
    /// 재화 변경을 여러 UI에 전파하기 위한 MessagePipe 메시지.
    /// 상점/가챠/메일 수령처럼 서로 다른 기능에서 같은 상단바 UI를 갱신할 때 사용한다.
    /// </summary>
    public sealed class CurrencyChangedMessage
    {
        public CurrencyChangedMessage(Dictionary<string, JToken> currencies)
        {
            Currencies = currencies;
        }

        public Dictionary<string, JToken> Currencies { get; }
    }

    /// <summary>
    /// MessagePipe 접근 지점을 UI 도메인 이름으로 감싼다.
    /// Presenter와 View가 MessagePipe 전역 API에 직접 흩어져 의존하지 않게 하기 위한 얇은 wrapper다.
    /// </summary>
    public static class CurrencyMessages
    {
        public static void Publish(Dictionary<string, JToken> currencies)
        {
            GlobalMessagePipe.GetPublisher<CurrencyChangedMessage>()
                .Publish(new CurrencyChangedMessage(CloneCurrencies(currencies)));
        }

        private static Dictionary<string, JToken> CloneCurrencies(Dictionary<string, JToken> currencies)
        {
            if (currencies == null)
                return null;

            var clone = new Dictionary<string, JToken>(currencies.Count);
            foreach (var pair in currencies)
            {
                clone[pair.Key] = pair.Value != null ? pair.Value.DeepClone() : null;
            }

            return clone;
        }

        public static IDisposable Subscribe(Action<CurrencyChangedMessage> onChanged)
        {
            return GlobalMessagePipe.GetSubscriber<CurrencyChangedMessage>()
                .Subscribe(onChanged);
        }
    }
}
