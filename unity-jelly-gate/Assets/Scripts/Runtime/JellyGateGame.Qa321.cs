using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaPlayGamesCloud321Routine()
        {
            yield return null;
            var failures = new List<string>();

            var local = CloudProfile321(4, 200, 40, 2, "castle.local", "challenge.local", 10,
                string.Empty, 300);
            var remote = CloudProfile321(9, 100, 500, 7, "castle.remote", "challenge.remote", 20,
                "remote-checkpoint", 200);
            var merged = CrownfrontPlayGamesCloud.MergeProfiles(local, remote);
            Check321(merged.economy.gold == 40 && merged.economy.gems == 2,
                "latest-local-economy", failures);
            Check321(merged.cosmetics.equippedCastle == "castle.local",
                "latest-local-equipment", failures);
            Check321(merged.cosmetics.ownedProductIds.SequenceEqual(new[] { "castle.local", "castle.remote" }),
                "cosmetic-union", failures);
            Check321(merged.progress.completedChallenges.SequenceEqual(
                    new[] { "challenge.local", "challenge.remote" }),
                "challenge-union", failures);
            Check321(merged.progress.lifetimeMonstersDefeated == 20,
                "progress-maximum", failures);
            Check321(string.IsNullOrEmpty(merged.progress.runCheckpointJson) &&
                     merged.progress.runCheckpointSavedAtUtcTicks == 300,
                "checkpoint-tombstone", failures);

            remote.savedAtUtcTicks = 400;
            remote.progress.runCheckpointSavedAtUtcTicks = 500;
            remote.progress.runCheckpointJson = "newer-remote-checkpoint";
            merged = CrownfrontPlayGamesCloud.MergeProfiles(local, remote);
            Check321(merged.economy.gold == 500 && merged.economy.gems == 7,
                "latest-remote-economy", failures);
            Check321(merged.cosmetics.equippedCastle == "castle.remote",
                "latest-remote-equipment", failures);
            Check321(merged.progress.runCheckpointJson == "newer-remote-checkpoint",
                "newer-remote-checkpoint", failures);

            var sparse = new CrownfrontCloudProfile
            {
                schemaVersion = 1,
                savedAtUtcTicks = 800,
                economy = null,
                cosmetics = null,
                progress = null
            };
            merged = CrownfrontPlayGamesCloud.MergeProfiles(local, sparse);
            Check321(merged.economy != null && merged.cosmetics != null && merged.progress != null,
                "null-section-normalization", failures);

            var invalid = CloudProfile321(99, 900, 999, 999, "invalid", "invalid", 999,
                "invalid", 900);
            invalid.schemaVersion = 99;
            merged = CrownfrontPlayGamesCloud.MergeProfiles(local, invalid);
            Check321(merged.economy.gold == 40 && merged.schemaVersion == 1,
                "invalid-schema-fallback", failures);

            var passed = failures.Count == 0;
            Debug.Log($"QA_PLAY_GAMES_CLOUD_321 passed={passed} failures={string.Join("|", failures)}");
            Application.Quit(passed ? 0 : 133);
        }

        private static CrownfrontCloudProfile CloudProfile321(long revision, long savedAt,
            int gold, int gems, string cosmetic, string challenge, int kills,
            string checkpoint, long checkpointSavedAt) => new()
        {
            schemaVersion = 1,
            revision = revision,
            savedAtUtcTicks = savedAt,
            economy = new CrownfrontEconomyCloudData
            {
                gold = gold,
                gems = gems,
                items = new List<CloudItemCount> { new() { id = "FrontlineReturn", count = 2 } }
            },
            cosmetics = new CrownfrontCosmeticsCloudData
            {
                ownedProductIds = new List<string> { cosmetic },
                equippedCastle = cosmetic,
                equippedMenu = string.Empty,
                equippedUnits = new List<CloudEquippedUnit>()
            },
            progress = new CrownfrontProgressCloudData
            {
                savedAtUtcTicks = savedAt,
                runCheckpointJson = checkpoint,
                runCheckpointSavedAtUtcTicks = checkpointSavedAt,
                lifetimeMonstersDefeated = kills,
                completedChallenges = new List<string> { challenge }
            }
        };

        private static void Check321(bool condition, string name, ICollection<string> failures)
        {
            if (!condition) failures.Add(name);
        }
    }
}
