using System;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private Action pendingInterstitialTransition;
        private bool interstitialTransitionPending;
        private bool interstitialTransitionLostFocus;
        private float interstitialTransitionStartedAt;
        private int pendingMainMenuGoldNotice;
        private float mainMenuGoldNoticeUntil;

        private void RequestInterstitialThen(Action transition)
        {
            if (transition == null || interstitialTransitionPending) return;
            if (monetization != null && monetization.NotifyRunEnded())
            {
                interstitialTransitionPending = true;
                interstitialTransitionLostFocus = false;
                interstitialTransitionStartedAt = Time.unscaledTime;
                pendingInterstitialTransition = transition;
                monetization.InterstitialClosed -= FinishInterstitialTransition;
                monetization.InterstitialClosed += FinishInterstitialTransition;
                Time.timeScale = 0f;
                return;
            }
            transition();
        }

        private void FinishInterstitialTransition()
        {
            if (monetization != null) monetization.InterstitialClosed -= FinishInterstitialTransition;
            var transition = pendingInterstitialTransition;
            pendingInterstitialTransition = null;
            interstitialTransitionPending = false;
            Time.timeScale = 1f;
            transition?.Invoke();
        }

        private void UpdateInterstitialTransition()
        {
            if (!interstitialTransitionPending) return;
            var elapsed = Time.unscaledTime - interstitialTransitionStartedAt;
            // A failed/no-fill request can leave the Android bridge waiting for a load callback.
            // Do not trap the player on a black transition screen when no ad became ready.
            if (elapsed >= 8f && (monetization == null || !monetization.AdsReady))
                FinishInterstitialTransition();
            else if (elapsed >= 45f)
                FinishInterstitialTransition();
        }

        private void OnInterstitialApplicationFocus(bool focused)
        {
            if (!interstitialTransitionPending) return;
            if (!focused)
            {
                interstitialTransitionLostFocus = true;
                return;
            }
            // Full-screen ad activities commonly pause/focus the Unity activity. If an adapter
            // fails to forward onAdDismissed, focus restoration is the reliable final signal.
            if (interstitialTransitionLostFocus && Time.unscaledTime - interstitialTransitionStartedAt >= .35f)
                FinishInterstitialTransition();
        }

        private void QueueMainMenuGoldNotice(int awarded)
        {
            if (awarded <= 0) return;
            pendingMainMenuGoldNotice = awarded;
            // The interstitial can remain open longer than the notice. Start the five-second
            // display only after the menu is actually visible.
            mainMenuGoldNoticeUntil = float.PositiveInfinity;
        }

        private void ActivateMainMenuGoldNotice()
        {
            if (pendingMainMenuGoldNotice > 0)
                mainMenuGoldNoticeUntil = Time.unscaledTime + 5f;
        }

        private void DrawMainMenuGoldRewardNotice()
        {
            if (pendingMainMenuGoldNotice <= 0) return;
            if (Time.unscaledTime > mainMenuGoldNoticeUntil)
            {
                pendingMainMenuGoldNotice = 0;
                return;
            }
            var safe = SafeGuiRect;
            var width = Mathf.Min(286f, safe.width - 24f);
            var rect = new Rect(safe.center.x - width * .5f, safe.y + 47f, width, 43f);
            DrawOrnatePanel(rect, new Color(.12f, .075f, .018f, .97f), new Color(1f, .79f, .28f), 2f);
            DrawFittedLabel(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, rect.height - 8f),
                L($"전선 정산 · 골드 +{pendingMainMenuGoldNotice:N0}",
                    $"FRONT REWARD · +{pendingMainMenuGoldNotice:N0} GOLD"),
                new GUIStyle(centeredStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, .9f, .52f) }
                }, 12);
        }

        private void RestartDefeatedRunWithPreparation()
        {
            RequestInterstitialThen(() =>
            {
                ClearRevivalSnapshots();
                ClearRunCheckpoint();
                RestartGame(false);
                showMainMenu = true;
                OpenPregameLoadout();
                mainMenuInputReadyAt = Time.unscaledTime + .42f;
                voiceBarks?.SetBattleMusic(false, true);
            });
        }
    }
}
