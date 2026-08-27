using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaChallengeRewards322Routine()
        {
            yield return null;
            var failures = new List<string>();
            var items = BuildChallengeItems();

            Check322(items.Count == 51 && ChallengeKeys.Length == 51,
                "challenge-count", failures);
            Check322(items.All(item => item.RewardGold > 0 && item.RewardGold <= 28),
                "gold-every-completion-bounded", failures);
            var gemRewards = items.Where(item => item.RewardGems > 0).ToArray();
            Check322(gemRewards.Length == 7 && gemRewards.All(item => item.RewardGems == 2) &&
                     gemRewards.Sum(item => item.RewardGems) == 14,
                "rare-gem-budget", failures);

            foreach (var family in items.GroupBy(item => item.Key.Split('_')[0]))
            {
                var gemEntries = family.Where(item => item.RewardGems > 0).ToArray();
                Check322(gemEntries.Length == 1 && gemEntries[0].Goal == family.Max(item => item.Goal),
                    $"family-gem-only-at-cap:{family.Key}", failures);
            }

            var local = CloudProfile321(1, 100, 10, 1, "castle.local", "round_10", 10,
                string.Empty, 100);
            var remote = CloudProfile321(2, 200, 20, 2, "castle.remote", "kills_100", 20,
                string.Empty, 200);
            local.progress.rewardedChallenges = new List<string> { "round_10" };
            remote.progress.rewardedChallenges = new List<string> { "kills_100" };
            var merged = CrownfrontPlayGamesCloud.MergeProfiles(local, remote);
            Check322(merged.progress.rewardedChallenges.SequenceEqual(
                    new[] { "kills_100", "round_10" }),
                "reward-ledger-cloud-union", failures);

            var tokenA = CrownfrontMonetization.StableTokenFingerprintForQa("token-a");
            var tokenARepeat = CrownfrontMonetization.StableTokenFingerprintForQa("token-a");
            var tokenB = CrownfrontMonetization.StableTokenFingerprintForQa("token-b");
            Check322(tokenA == tokenARepeat && tokenA != tokenB && tokenA.Length == 16,
                "durable-purchase-token-key", failures);
            Check322(CrownfrontEconomy.GemsAfterRefundForQa(100, 30) == 70 &&
                     CrownfrontEconomy.GemsAfterRefundForQa(10, 30) == 0 &&
                     CrownfrontEconomy.GemsAfterRefundForQa(10, -2) == 10,
                "refund-gems-clamped", failures);
            Check322(CrownfrontEconomy.GemDebtAfterRefundForQa(100, 0, 30) == 0 &&
                     CrownfrontEconomy.GemDebtAfterRefundForQa(20, 4, 30) == 14,
                "refund-gem-debt-preserved", failures);

            var passed = failures.Count == 0;
            Debug.Log($"QA_CHALLENGE_REWARDS_322 passed={passed} challenges={items.Count} " +
                      $"gemRewards={gemRewards.Length} totalGemBudget={gemRewards.Sum(item => item.RewardGems)} " +
                      $"failures={string.Join("|", failures)}");
            Application.Quit(passed ? 0 : 134);
        }

        private static void Check322(bool condition, string name, ICollection<string> failures)
        {
            if (!condition) failures.Add(name);
        }
    }
}
