using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if UNITY_ANDROID || UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif
using UnityEngine;

namespace JellyGate
{
    [Serializable]
    public sealed class CloudItemCount
    {
        public string id = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class CloudEquippedUnit
    {
        public string unit = string.Empty;
        public string productId = string.Empty;
    }

    [Serializable]
    public sealed class CrownfrontEconomyCloudData
    {
        public int gold;
        public int gems;
        public List<CloudItemCount> items = new();
    }

    [Serializable]
    public sealed class CrownfrontCosmeticsCloudData
    {
        public List<string> ownedProductIds = new();
        public string equippedCastle = string.Empty;
        public string equippedMenu = string.Empty;
        public List<CloudEquippedUnit> equippedUnits = new();
    }

    [Serializable]
    public sealed class CrownfrontProgressCloudData
    {
        public long savedAtUtcTicks;
        public string runCheckpointJson = string.Empty;
        public long runCheckpointSavedAtUtcTicks;
        public int lifetimeRoundsCleared;
        public int lifetimeMonstersDefeated;
        public int lifetimeUnitsPlaced;
        public int lifetimeSkillsCast;
        public int lifetimeHeroesEvolved;
        public int lifetimeBossesDefeated;
        public int itemlessBest;
        public List<string> completedChallenges = new();
    }

    [Serializable]
    public sealed class CrownfrontCloudProfile
    {
        public int schemaVersion = 1;
        public long revision;
        public long savedAtUtcTicks;
        public CrownfrontEconomyCloudData economy = new();
        public CrownfrontCosmeticsCloudData cosmetics = new();
        public CrownfrontProgressCloudData progress = new();
    }

    /// <summary>
    /// Google Play Games v2 automatic sign-in and Saved Games synchronization.
    /// Paid Google Play ownership (notably permanent ad removal) remains authoritative in Billing;
    /// this profile stores gameplay progress and cosmetics bought with in-game gems.
    /// </summary>
    public sealed class CrownfrontPlayGamesCloud : MonoBehaviour
    {
        private const int SchemaVersion = 1;
        private const string SaveFileName = "crownfront_profile_v1";
        private const string RevisionKey = "Crownfront.Cloud.Revision.v1";
        private const string SavedTicksKey = "Crownfront.Cloud.SavedAtUtcTicks.v1";
        private const float SaveDebounceSeconds = 1.75f;

        private CrownfrontEconomy economy;
        private CrownfrontMonetization monetization;
        private JellyGateGame game;
        private bool initialized;
        private bool applyingRemote;
        private bool syncInFlight;
        private bool localDirty;
        private float nextSaveAt;
        private long localRevision;
        private long localSavedAtUtcTicks;

        public static CrownfrontPlayGamesCloud Instance { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsSyncing => syncInFlight;
        public string PlayerName { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;

        public void Initialize(JellyGateGame owner, CrownfrontEconomy wallet,
            CrownfrontMonetization cosmetics)
        {
            if (initialized) return;
            initialized = true;
            Instance = this;
            game = owner;
            economy = wallet;
            monetization = cosmetics;
            localRevision = Math.Max(0L, ReadLongPreference(RevisionKey));
            localSavedAtUtcTicks = Math.Max(0L, ReadLongPreference(SavedTicksKey));

            if (localRevision <= 0L && HasMeaningfulLocalProfile())
            {
                localRevision = 1L;
                localSavedAtUtcTicks = Math.Max(DateTime.UtcNow.Ticks,
                    game != null ? game.CloudProgressTimestampForMigration() : 0L);
                PersistClock();
            }

            if (economy != null) economy.Changed += MarkDirty;
            if (monetization != null) monetization.CosmeticsChanged += MarkDirty;

#if UNITY_ANDROID && !UNITY_EDITOR
            Status = GameLocalization.Text("Play Games 연결 중", "CONNECTING TO PLAY GAMES");
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(OnAuthenticated);
#else
            Status = GameLocalization.Text("Play Games는 Android 빌드에서 동작합니다.",
                "PLAY GAMES IS AVAILABLE IN ANDROID BUILDS.");
#endif
        }

        public void RetryAuthentication()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (syncInFlight) return;
            Status = GameLocalization.Text("Play Games 계정 선택 중", "CHOOSING A PLAY GAMES ACCOUNT");
            PlayGamesPlatform.Instance.ManuallyAuthenticate(OnAuthenticated);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnAuthenticated(SignInStatus status)
        {
            IsAuthenticated = status == SignInStatus.Success;
            if (!IsAuthenticated)
            {
                Status = GameLocalization.Text("Play Games 오프라인 · 기기에 저장 중",
                    "PLAY GAMES OFFLINE · SAVING ON DEVICE");
                return;
            }

            PlayerName = Social.localUser?.userName ?? string.Empty;
            Status = GameLocalization.Text("Play Games 기록 확인 중", "CHECKING PLAY GAMES SAVE");
            SynchronizeNow();
        }
#endif

        public static void MarkLocalProgressDirty()
        {
            if (Instance != null) Instance.MarkDirty();
        }

        private void MarkDirty()
        {
            if (!initialized || applyingRemote) return;
            localRevision = Math.Max(1L, localRevision + 1L);
            localSavedAtUtcTicks = DateTime.UtcNow.Ticks;
            PersistClock();
            localDirty = true;
            nextSaveAt = Time.unscaledTime + SaveDebounceSeconds;
        }

        private void Update()
        {
            if (!localDirty || syncInFlight || !IsAuthenticated ||
                Time.unscaledTime < nextSaveAt) return;
            SynchronizeNow();
        }

        public void SynchronizeNow()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!initialized || syncInFlight || !IsAuthenticated) return;
            syncInFlight = true;
            Status = GameLocalization.Text("Play Games 동기화 중", "SYNCING PLAY GAMES");
            PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
                SaveFileName, DataSource.ReadCacheOrNetwork,
                ConflictResolutionStrategy.UseMostRecentlySaved, OnSaveOpened);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnSaveOpened(SavedGameRequestStatus status, ISavedGameMetadata metadata)
        {
            if (status != SavedGameRequestStatus.Success || metadata == null)
            {
                FinishWithError("open", status);
                return;
            }

            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(metadata,
                (readStatus, bytes) => OnSaveRead(readStatus, bytes, metadata));
        }

        private void OnSaveRead(SavedGameRequestStatus status, byte[] bytes,
            ISavedGameMetadata metadata)
        {
            if (status != SavedGameRequestStatus.Success)
            {
                FinishWithError("read", status);
                return;
            }

            var local = CaptureLocalProfile();
            var remote = Deserialize(bytes);
            var merged = MergeProfiles(local, remote);
            merged.revision = Math.Max(local?.revision ?? 0L, remote?.revision ?? 0L) + 1L;
            merged.savedAtUtcTicks = DateTime.UtcNow.Ticks;

            applyingRemote = true;
            try
            {
                ApplyProfile(merged);
                localRevision = merged.revision;
                localSavedAtUtcTicks = merged.savedAtUtcTicks;
                PersistClock();
            }
            finally
            {
                applyingRemote = false;
            }

            var payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(CaptureLocalProfile()));
            var update = new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription("Crownfront profile " +
                                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"))
                .Build();
            PlayGamesPlatform.Instance.SavedGame.CommitUpdate(metadata, update, payload,
                OnSaveCommitted);
        }

        private void OnSaveCommitted(SavedGameRequestStatus status, ISavedGameMetadata metadata)
        {
            syncInFlight = false;
            if (status != SavedGameRequestStatus.Success)
            {
                localDirty = true;
                nextSaveAt = Time.unscaledTime + 5f;
                FinishWithError("commit", status);
                return;
            }

            localDirty = false;
            Status = string.IsNullOrWhiteSpace(PlayerName)
                ? GameLocalization.Text("Play Games 동기화 완료", "PLAY GAMES SYNCED")
                : GameLocalization.Text($"{PlayerName} · 동기화 완료", $"{PlayerName} · SYNCED");
        }

        private void FinishWithError(string stage, SavedGameRequestStatus status)
        {
            syncInFlight = false;
            localDirty = true;
            nextSaveAt = Time.unscaledTime + 8f;
            Status = GameLocalization.Text("클라우드 대기 · 기기에 안전하게 저장됨",
                "CLOUD PENDING · SAFELY SAVED ON DEVICE");
            Debug.LogWarning($"Play Games saved-game {stage} failed: {status}");
        }
#endif

        private CrownfrontCloudProfile CaptureLocalProfile()
        {
            return new CrownfrontCloudProfile
            {
                schemaVersion = SchemaVersion,
                revision = localRevision,
                savedAtUtcTicks = localSavedAtUtcTicks,
                economy = economy != null ? economy.ExportCloudData() : new CrownfrontEconomyCloudData(),
                cosmetics = monetization != null
                    ? monetization.ExportCloudCosmetics()
                    : new CrownfrontCosmeticsCloudData(),
                progress = game != null ? game.ExportCloudProgress() : new CrownfrontProgressCloudData()
            };
        }

        private void ApplyProfile(CrownfrontCloudProfile profile)
        {
            if (!IsValid(profile)) return;
            economy?.ApplyCloudData(profile.economy);
            monetization?.ApplyCloudCosmetics(profile.cosmetics);
            game?.ApplyCloudProgress(profile.progress);
        }

        private bool HasMeaningfulLocalProfile()
        {
            var progress = game != null ? game.ExportCloudProgress() : null;
            var cosmetics = monetization != null ? monetization.ExportCloudCosmetics() : null;
            return (economy != null && economy.HasMeaningfulCloudProgress()) ||
                   (progress != null && (progress.savedAtUtcTicks > 0L ||
                                         progress.lifetimeRoundsCleared > 0 ||
                                         !string.IsNullOrEmpty(progress.runCheckpointJson))) ||
                   (cosmetics?.ownedProductIds?.Count ?? 0) > 0;
        }

        private static CrownfrontCloudProfile Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                var profile = JsonUtility.FromJson<CrownfrontCloudProfile>(Encoding.UTF8.GetString(bytes));
                return IsValid(profile) ? profile : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValid(CrownfrontCloudProfile profile) =>
            profile != null && profile.schemaVersion == SchemaVersion;

        internal static CrownfrontCloudProfile MergeProfiles(CrownfrontCloudProfile local,
            CrownfrontCloudProfile remote)
        {
            if (!IsValid(local)) return Clone(remote) ?? NewProfile();
            if (!IsValid(remote)) return Clone(local) ?? NewProfile();

            var remoteLatest = remote.savedAtUtcTicks > local.savedAtUtcTicks;
            var latest = remoteLatest ? remote : local;
            var result = Clone(latest) ?? NewProfile();
            result.economy ??= new CrownfrontEconomyCloudData();
            result.cosmetics ??= new CrownfrontCosmeticsCloudData();
            result.progress ??= new CrownfrontProgressCloudData();
            result.schemaVersion = SchemaVersion;
            result.revision = Math.Max(local.revision, remote.revision);
            result.savedAtUtcTicks = Math.Max(local.savedAtUtcTicks, remote.savedAtUtcTicks);

            result.cosmetics.ownedProductIds = Union(local.cosmetics?.ownedProductIds,
                remote.cosmetics?.ownedProductIds);
            result.progress.completedChallenges = Union(local.progress?.completedChallenges,
                remote.progress?.completedChallenges);
            result.progress.lifetimeRoundsCleared = Math.Max(local.progress?.lifetimeRoundsCleared ?? 0,
                remote.progress?.lifetimeRoundsCleared ?? 0);
            result.progress.lifetimeMonstersDefeated = Math.Max(local.progress?.lifetimeMonstersDefeated ?? 0,
                remote.progress?.lifetimeMonstersDefeated ?? 0);
            result.progress.lifetimeUnitsPlaced = Math.Max(local.progress?.lifetimeUnitsPlaced ?? 0,
                remote.progress?.lifetimeUnitsPlaced ?? 0);
            result.progress.lifetimeSkillsCast = Math.Max(local.progress?.lifetimeSkillsCast ?? 0,
                remote.progress?.lifetimeSkillsCast ?? 0);
            result.progress.lifetimeHeroesEvolved = Math.Max(local.progress?.lifetimeHeroesEvolved ?? 0,
                remote.progress?.lifetimeHeroesEvolved ?? 0);
            result.progress.lifetimeBossesDefeated = Math.Max(local.progress?.lifetimeBossesDefeated ?? 0,
                remote.progress?.lifetimeBossesDefeated ?? 0);
            result.progress.itemlessBest = Math.Max(local.progress?.itemlessBest ?? 0,
                remote.progress?.itemlessBest ?? 0);

            var localCheckpointTicks = local.progress?.runCheckpointSavedAtUtcTicks ?? 0L;
            var remoteCheckpointTicks = remote.progress?.runCheckpointSavedAtUtcTicks ?? 0L;
            var checkpointSource = remoteCheckpointTicks > localCheckpointTicks ? remote.progress : local.progress;
            result.progress.runCheckpointJson = checkpointSource?.runCheckpointJson ?? string.Empty;
            result.progress.runCheckpointSavedAtUtcTicks = Math.Max(localCheckpointTicks, remoteCheckpointTicks);
            result.progress.savedAtUtcTicks = Math.Max(local.progress?.savedAtUtcTicks ?? 0L,
                remote.progress?.savedAtUtcTicks ?? 0L);
            return result;
        }

        private static CrownfrontCloudProfile NewProfile() => new()
        {
            schemaVersion = SchemaVersion,
            economy = new CrownfrontEconomyCloudData(),
            cosmetics = new CrownfrontCosmeticsCloudData(),
            progress = new CrownfrontProgressCloudData()
        };

        private static CrownfrontCloudProfile Clone(CrownfrontCloudProfile source)
        {
            if (source == null) return null;
            try
            {
                return JsonUtility.FromJson<CrownfrontCloudProfile>(JsonUtility.ToJson(source));
            }
            catch
            {
                return null;
            }
        }

        private static List<string> Union(IEnumerable<string> first, IEnumerable<string> second) =>
            (first ?? Array.Empty<string>()).Concat(second ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToList();

        private static long ReadLongPreference(string key)
        {
            var raw = PlayerPrefs.GetString(key, "0");
            return long.TryParse(raw, out var value) ? value : 0L;
        }

        private void PersistClock()
        {
            PlayerPrefs.SetString(RevisionKey, localRevision.ToString());
            PlayerPrefs.SetString(SavedTicksKey, localSavedAtUtcTicks.ToString());
            PlayerPrefs.Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && localDirty && IsAuthenticated && !syncInFlight) SynchronizeNow();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && IsAuthenticated && localDirty && !syncInFlight) SynchronizeNow();
        }

        private void OnApplicationQuit()
        {
            PersistClock();
        }

        private void OnDestroy()
        {
            if (economy != null) economy.Changed -= MarkDirty;
            if (monetization != null) monetization.CosmeticsChanged -= MarkDirty;
            if (Instance == this) Instance = null;
        }
    }
}
