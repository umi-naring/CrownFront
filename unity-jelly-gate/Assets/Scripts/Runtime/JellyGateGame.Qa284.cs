using System.Collections;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaChallengeScroll284Routine()
        {
            yield return null;
            var touchScroll = VerifyChallengeTouchScrollForQa();
            showMissionPanel = true;
            var modalBlocksBattlefieldInput = showMissionPanel;
            var passed = touchScroll && modalBlocksBattlefieldInput;
            Debug.Log($"QA_CHALLENGE_SCROLL_284 passed={passed} touchScroll={touchScroll} " +
                      $"items={BuildChallengeItems().Count} modalBlocksInput={modalBlocksBattlefieldInput}");
            Application.Quit(passed ? 0 : 84);
        }
    }
}
