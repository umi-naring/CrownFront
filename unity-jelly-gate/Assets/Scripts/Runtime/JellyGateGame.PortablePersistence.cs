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
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Portable progress backup could not be saved: {exception.GetType().Name}");
            }
        }
    }
}
