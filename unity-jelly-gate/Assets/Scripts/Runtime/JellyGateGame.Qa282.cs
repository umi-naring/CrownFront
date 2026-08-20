using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaAdPresentation282Routine()
        {
            yield return null;
            var failures = new List<string>();
            var originalToast = "QA_AD_TOAST_SENTINEL";
            toastText = originalToast;
            toastUntil = Time.unscaledTime + 30f;
            var purchaseStatus = monetization.PurchaseStatusMessage;
            var closedCount = 0;
            void MarkClosed() => closedCount++;
            monetization.InterstitialClosed += MarkClosed;

            monetization.OnMonetizationEvent(
                "{\"type\":\"ad_error\",\"message\":\"App not approved yet. https://support.google.com/admob/answer/9905175\"}");
            if (toastText != originalToast) failures.Add("raw-error-visible");
            if (monetization.PurchaseStatusMessage != purchaseStatus) failures.Add("purchase-status-polluted");
            if (closedCount != 1) failures.Add($"close-signal:{closedCount}");
            if (monetization.AdsReady) failures.Add("error-left-ready");

            monetization.OnMonetizationEvent("{\"type\":\"ads_initialized\"}");
            monetization.OnMonetizationEvent("{\"type\":\"ad_loaded\"}");
            if (!monetization.ConsentStatusKnown || !monetization.AdsReady)
                failures.Add("ready-state");
            if (toastText != originalToast) failures.Add("ready-toast-visible");

            monetization.InterstitialClosed -= MarkClosed;
            var passed = failures.Count == 0;
            Debug.Log($"QA_AD_PRESENTATION_282 passed={passed} rawHidden={toastText == originalToast} " +
                      $"closeSignal={closedCount} ready={monetization.AdsReady} " +
                      $"fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 122);
        }
    }
}
