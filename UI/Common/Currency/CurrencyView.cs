using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using SampleClient.Service.Bootstrap;
using TMPro;
using UnityEngine;

namespace SampleClient.UI.Common.Currency
{
    /// <summary>
    /// 상단바 재화 표시 View 샘플.
    /// MessagePipe로 발행된 재화 변경 메시지를 R3 생명주기 구독 목록에 묶어 수신한다.
    /// </summary>
    public sealed class CurrencyView : UIView
    {
        private const string DefaultGemKey = "701";
        private const string DefaultCoinKey = "702";

        [SerializeField] private TextMeshProUGUI _gemText;
        [SerializeField] private TextMeshProUGUI _coinText;
        [SerializeField] private string _gemKey = DefaultGemKey;
        [SerializeField] private string _coinKey = DefaultCoinKey;

        private void OnEnable()
        {
            ClearSubscriptions();
            AddSubscription(CurrencyMessages.Subscribe(OnCurrencyChanged));

            var currencies = GameBootstrapService.Data != null && GameBootstrapService.Data.wallet != null
                ? GameBootstrapService.Data.wallet.currencies
                : null;
            SetCurrencies(currencies);
        }

        private void OnDisable()
        {
            ClearSubscriptions();
        }

        private void OnCurrencyChanged(CurrencyChangedMessage message)
        {
            SetCurrencies(message.Currencies);
        }

        private void SetCurrencies(IReadOnlyDictionary<string, JToken> currencies)
        {
            SetText(_gemText, GetAmountText(currencies, _gemKey));
            SetText(_coinText, GetAmountText(currencies, _coinKey));
        }

        private static string GetAmountText(IReadOnlyDictionary<string, JToken> currencies, string key)
        {
            if (currencies == null || string.IsNullOrEmpty(key) || !currencies.TryGetValue(key, out var value))
            {
                return "0";
            }

            return value != null ? value.ToString() : "0";
        }
    }
}
