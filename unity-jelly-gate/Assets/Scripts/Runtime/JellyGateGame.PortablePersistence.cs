using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private const int PortableProgressVersion = 1;
        private const string PortableProgressFileName = "crownfront_portable_progress_v1.json";
        private const string CloudCheckpointChangedTicksKey =
            "Crownfront.Cloud.CheckpointChangedUtcTicks.v1";

        [Serializable]
        private sealed class PortableProgressData
        {
            public int version;
            public long savedAtUtcTicks;
            public string runCheckpointJson = string.Empty;
            public int lifetimeRoundsCleared;
            public int lifetimeMonstersDefeated;
            public int lifetimeUnitsPlaced;
            public int lifetimeSkillsCast;
            public int lifetimeHeroesEvolved;
            public int lifetimeBossesDefeated;
            public List<string> completedChallenges = new();
        }

        private static string PortableProgressPath =>
            Path.Combine(Application.persistentDataPath, PortableProgressFileName);

        internal long CloudProgressTimestampForMigration()
        {
            try
            {
                if (!File.Exists(PortableProgressPath)) return 0L;
                var data = JsonUtility.FromJson<PortableProgressData>(File.ReadAllText(PortableProgressPath));
                return data?.savedAtUtcTicks ?? File.GetLastWriteTimeUtc(PortableProgressPath).Ticks;
            }
            catch
            {
                return 0L;
            }
        }

        internal CrownfrontProgressCloudData ExportCloudProgress()
        {
            var checkpointJson = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
            TryReadCheckpointTimestamp(checkpointJson, out var checkpointTicks);
            var checkpointChangedTicks = ReadLongPlayerPreference(CloudCheckpointChangedTicksKey);
            return new CrownfrontProgressCloudData
            {
                savedAtUtcTicks = CloudProgressTimestampForMigration(),
                runCheckpointJson = checkpointJson,
                runCheckpointSavedAtUtcTicks = Math.Max(checkpointTicks, checkpointChangedTicks),
                lifetimeRoundsCleared = lifetimeRoundsCleared,
                lifetimeMonstersDefeated = lifetimeMonstersDefeated,
                lifetimeUnitsPlaced = lifetimeUnitsPlaced,
                lifetimeSkillsCast = lifetimeSkillsCast,
                lifetimeHeroesEvolved = lifetimeHeroesEvolved,
                lifetimeBossesDefeated = lifetimeBossesDefeated,
                itemlessBest = PlayerPrefs.GetInt("Crownfront.Challenge.ItemlessBest", 0),
                completedChallenges = new List<string>(completedMissionKeys)
            };
        }

        internal void ApplyCloudProgress(CrownfrontProgressCloudData data)
        {
            if (data == null) return;
            lifetimeRoundsCleared = Math.Max(0, data.lifetimeRoundsCleared);
            lifetimeMonstersDefeated = Math.Max(0, data.lifetimeMonstersDefeated);
            lifetimeUnitsPlaced = Math.Max(0, data.lifetimeUnitsPlaced);
            lifetimeSkillsCast = Math.Max(0, data.lifetimeSkillsCast);
            lifetimeHeroesEvolved = Math.Max(0, data.lifetimeHeroesEvolved);
            lifetimeBossesDefeated = Math.Max(0, data.lifetimeBossesDefeated);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Rounds", lifetimeRoundsCleared);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Kills", lifetimeMonstersDefeated);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Placements", lifetimeUnitsPlaced);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Skills", lifetimeSkillsCast);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Heroes", lifetimeHeroesEvolved);
            PlayerPrefs.SetInt("Crownfront.Lifetime.Bosses", lifetimeBossesDefeated);
            PlayerPrefs.SetInt("Crownfront.Challenge.ItemlessBest", Math.Max(0, data.itemlessBest));

            completedMissionKeys.Clear();
            foreach (var challengeKey in ChallengeKeys)
                PlayerPrefs.DeleteKey("Crownfront.Challenge." + challengeKey);
            foreach (var key in data.completedChallenges ?? new List<string>())
            {
                if (Array.IndexOf(ChallengeKeys, key) < 0) continue;
                completedMissionKeys.Add(key);
                PlayerPrefs.SetInt("Crownfront.Challenge." + key, 1);
            }

            if (TryReadCheckpointTimestamp(data.runCheckpointJson, out _))
                PlayerPrefs.SetString(RunCheckpointKey, data.runCheckpointJson);
            else
                PlayerPrefs.DeleteKey(RunCheckpointKey);
            PlayerPrefs.SetString(CloudCheckpointChangedTicksKey,
                Math.Max(0L, data.runCheckpointSavedAtUtcTicks).ToString());
            PlayerPrefs.Save();
            SavePortableProgressBackup();
            InitializeRunCheckpointPrompt();
        }

        private static long ReadLongPlayerPreference(string key)
        {
            var raw = PlayerPrefs.GetString(key, "0");
            return long.TryParse(raw, out var value) ? value : 0L;
        }

        /// <summary>
        /// Restores only challenge history and an unfinished front. Paid ownership is deliberately
        /// excluded: Google Play Billing remains the authority for non-consumable purchases.
        /// Android restores this small file before the first launch on a transferred/new device.
        /// </summary>
        private void RestorePortableProgressBackup()
        {
            var path = PortableProgressPath;
            if (!File.Exists(path)) return;
            try
            {
                var data = JsonUtility.FromJson<PortableProgressData>(File.ReadAllText(path));
                if (data == null || data.version != PortableProgressVersion) return;

                PlayerPrefs.SetInt("Crownfront.Lifetime.Rounds",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Rounds", 0), data.lifetimeRoundsCleared));
                PlayerPrefs.SetInt("Crownfront.Lifetime.Kills",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Kills", 0), data.lifetimeMonstersDefeated));
                PlayerPrefs.SetInt("Crownfront.Lifetime.Placements",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Placements", 0), data.lifetimeUnitsPlaced));
                PlayerPrefs.SetInt("Crownfront.Lifetime.Skills",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Skills", 0), data.lifetimeSkillsCast));
                PlayerPrefs.SetInt("Crownfront.Lifetime.Heroes",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Heroes", 0), data.lifetimeHeroesEvolved));
                PlayerPrefs.SetInt("Crownfront.Lifetime.Bosses",
                    Mathf.Max(PlayerPrefs.GetInt("Crownfront.Lifetime.Bosses", 0), data.lifetimeBossesDefeated));

                foreach (var key in data.completedChallenges ?? new List<string>())
                {
                    if (Array.IndexOf(ChallengeKeys, key) >= 0)
                        PlayerPrefs.SetInt("Crownfront.Challenge." + key, 1);
                }

                if (TryReadCheckpointTimestamp(data.runCheckpointJson, out var backupTicks))
                {
                    var localJson = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
                    if (!TryReadCheckpointTimestamp(localJson, out var localTicks) || backupTicks > localTicks)
                        PlayerPrefs.SetString(RunCheckpointKey, data.runCheckpointJson);
                }
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Portable progress backup could not be restored: {exception.GetType().Name}");
            }
        }

        private static bool TryReadCheckpointTimestamp(string json, out long savedAtUtcTicks)
        {
            savedAtUtcTicks = 0L;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var checkpoint = JsonUtility.FromJson<RunCheckpointData>(json);
                if (checkpoint == null || checkpoint.version != RunCheckpointVersion ||
                    checkpoint.round is < 1 or > MaxRounds || checkpoint.units == null ||
                    checkpoint.units.Count == 0) return false;
                savedAtUtcTicks = checkpoint.savedAtUtcTicks;
                return savedAtUtcTicks > 0L;
            }
            catch
            {
                return false;
            }
        }

        private void SavePortableProgressBackup()
        {
            try
            {
                var data = new PortableProgressData
                {
                    version = PortableProgressVersion,
                    savedAtUtcTicks = DateTime.UtcNow.Ticks,
                    runCheckpointJson = PlayerPrefs.GetString(RunCheckpointKey, string.Empty),
                    lifetimeRoundsCleared = lifetimeRoundsCleared,
                    lifetimeMonstersDefeated = lifetimeMonstersDefeated,
                    lifetimeUnitsPlaced = lifetimeUnitsPlaced,
                    lifetimeSkillsCast = lifetimeSkillsCast,
                    lifetimeHeroesEvolved = lifetimeHeroesEvolved,
                    lifetimeBossesDefeated = lifetimeBossesDefeated,
                    completedChallenges = new List<string>(completedMissionKeys)
                };
                var path = PortableProgressPath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temporaryPath = path + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data));
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
                CrownfrontPlayGamesCloud.MarkLocalProgressDirty();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Portable progress backup could not be saved: {exception.GetType().Name}");
            }
        }
    }
}
