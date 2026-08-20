using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaLoginFreePersistence281Routine()
        {
            yield return null;
            var lifetimeKeys = new[]
            {
                "Crownfront.Lifetime.Rounds", "Crownfront.Lifetime.Kills",
                "Crownfront.Lifetime.Placements", "Crownfront.Lifetime.Skills",
                "Crownfront.Lifetime.Heroes", "Crownfront.Lifetime.Bosses"
            };
            var savedLifetimeValues = lifetimeKeys.ToDictionary(key => key,
                key => PlayerPrefs.GetInt(key, 0));
            var savedCompletedChallenges = ChallengeKeys.Where(key =>
                PlayerPrefs.GetInt("Crownfront.Challenge." + key, 0) == 1).ToHashSet();
            var hadCheckpoint = PlayerPrefs.HasKey(RunCheckpointKey);
            var savedCheckpoint = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
            var backupPath = PortableProgressPath;
            var hadPortableFile = File.Exists(backupPath);
            var savedPortableFile = hadPortableFile ? File.ReadAllText(backupPath) : string.Empty;
            PlayerUnit probe = null;
            var failures = new List<string>();

            try
            {
                foreach (var key in lifetimeKeys) PlayerPrefs.DeleteKey(key);
                foreach (var key in ChallengeKeys) PlayerPrefs.DeleteKey("Crownfront.Challenge." + key);
                PlayerPrefs.DeleteKey(RunCheckpointKey);
                completedMissionKeys.Clear();

                lifetimeRoundsCleared = 123;
                lifetimeMonstersDefeated = 4567;
                lifetimeUnitsPlaced = 321;
                lifetimeSkillsCast = 654;
                lifetimeHeroesEvolved = 37;
                lifetimeBossesDefeated = 19;
                completedMissionKeys.Add("round_100");
                completedMissionKeys.Add("kills_2500");
                Round = 17;
                Money = 13;
                gateHealth = 321f;
                Phase = GamePhase.Preparation;
                probe = new GameObject("QA Portable Progress Defender").AddComponent<PlayerUnit>();
                probe.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank],
                    new Vector2(0f, -4f));
                units.Add(probe);
                WriteRunCheckpoint(CaptureRunCheckpoint(GamePhase.Preparation), false);
                SavePortableProgressBackup();

                if (!File.Exists(backupPath)) failures.Add("backup-file-missing");
                var written = File.Exists(backupPath) ? File.ReadAllText(backupPath) : string.Empty;
                if (written.Contains("Crownfront.Shop.Owned.") || written.Contains("crownfront.castle."))
                    failures.Add("purchase-entitlement-leaked");

                foreach (var key in lifetimeKeys) PlayerPrefs.DeleteKey(key);
                foreach (var key in ChallengeKeys) PlayerPrefs.DeleteKey("Crownfront.Challenge." + key);
                PlayerPrefs.DeleteKey(RunCheckpointKey);
                completedMissionKeys.Clear();
                RestorePortableProgressBackup();
                LoadChallengeCollection();

                if (lifetimeRoundsCleared != 123 || lifetimeMonstersDefeated != 4567 ||
                    lifetimeUnitsPlaced != 321 || lifetimeSkillsCast != 654 ||
                    lifetimeHeroesEvolved != 37 || lifetimeBossesDefeated != 19)
                    failures.Add("lifetime-progress-restore");
                if (!completedMissionKeys.Contains("round_100") ||
                    !completedMissionKeys.Contains("kills_2500"))
                    failures.Add("challenge-restore");
                if (!HasRunCheckpoint()) failures.Add("run-checkpoint-restore");
            }
            finally
            {
                if (probe != null)
                {
                    units.Remove(probe);
                    Destroy(probe.gameObject);
                }
                foreach (var key in lifetimeKeys) PlayerPrefs.SetInt(key, savedLifetimeValues[key]);
                foreach (var key in ChallengeKeys)
                {
                    var prefKey = "Crownfront.Challenge." + key;
                    if (savedCompletedChallenges.Contains(key)) PlayerPrefs.SetInt(prefKey, 1);
                    else PlayerPrefs.DeleteKey(prefKey);
                }
                if (hadCheckpoint) PlayerPrefs.SetString(RunCheckpointKey, savedCheckpoint);
                else PlayerPrefs.DeleteKey(RunCheckpointKey);
                PlayerPrefs.Save();
                if (hadPortableFile) File.WriteAllText(backupPath, savedPortableFile);
                else if (File.Exists(backupPath)) File.Delete(backupPath);
            }

            var passed = failures.Count == 0;
            Debug.Log($"QA_LOGIN_FREE_PERSISTENCE_281 passed={passed} " +
                      $"purchaseAuthority=play-billing challenges=48 checkpoint={passed} " +
                      $"fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 121);
        }
    }
}
