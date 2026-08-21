using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private const string RunCheckpointKey = "Crownfront.RunCheckpoint.v1";
        private const int RunCheckpointVersion = 1;
        private const float RunCheckpointAutosaveInterval = 6f;

        [Serializable]
        private sealed class RunCheckpointData
        {
            public int version;
            public long savedAtUtcTicks;
            public int round;
            public int money;
            public float gateHealth;
            public int phase;
            public int lastTier;
            public int augmentOffersWithoutHighTier;
            public int lastStandCharges;
            public int monstersDefeated;
            public int skillsCast;
            public int roundsCleared;
            public int unitsPlaced;
            public List<CheckpointFloat> augmentPower = new();
            public List<CheckpointInt> augmentCount = new();
            public List<CheckpointOffer> acquiredAugments = new();
            public List<CheckpointFloat> activeAugmentCooldowns = new();
            public List<int> unlockedUnits = new();
            public List<CheckpointUnit> units = new();
            public List<CheckpointOffer> currentOffers = new();
            public List<int> activeTacticalItems = new();
            public bool usedAnyTacticalItem;
            public bool revivalUsed;
            public int fieldAidUses;
            public int runGoldScore;
            public int perfectRounds;
            public bool runGoldAwarded;
        }

        [Serializable]
        private sealed class CheckpointFloat
        {
            public string key;
            public float value;
        }

        [Serializable]
        private sealed class CheckpointInt
        {
            public string key;
            public int value;
        }

        [Serializable]
        private sealed class CheckpointOffer
        {
            public string name;
            public string description;
            public string effectKey;
            public int tier;
            public float power;
        }

        [Serializable]
        private sealed class CheckpointUnit
        {
            public int archetype;
            public float x;
            public float y;
            public int level;
            public float experience;
            public float health;
            public bool holding;
            public float skillCooldown;
            public float ultimateCooldown;
            public int placementRound;
            public int placementBatchId;
            public int placementRefundCost;
        }

        private bool showResumePrompt;
        private bool showReturnToMainMenuSavePrompt;
        private float nextRunCheckpointAutosaveAt;
        private string preparedBattleCheckpointJson = string.Empty;

        public bool HasRunCheckpointForQa => HasRunCheckpoint();
        public bool ResumePromptVisibleForQa => showResumePrompt;
        public bool ReturnToMainMenuSavePromptVisibleForQa => showReturnToMainMenuSavePrompt;

        private void InitializeRunCheckpointPrompt()
        {
            // A stored run is offered only after the player deliberately presses Front Deploy.
            // Showing this modal on arrival at the main menu made a normal menu visit feel like
            // an unfinished transaction and conflicted with the Google account prompt.
            showResumePrompt = false;
        }

        private bool HasRunCheckpoint()
        {
            if (!PlayerPrefs.HasKey(RunCheckpointKey)) return false;
            var json = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var data = JsonUtility.FromJson<RunCheckpointData>(json);
                return data != null && data.version == RunCheckpointVersion &&
                       data.round is >= 1 and <= MaxRounds && data.units != null && data.units.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private RunCheckpointData ReadRunCheckpoint()
        {
            if (!PlayerPrefs.HasKey(RunCheckpointKey)) return null;
            try
            {
                var data = JsonUtility.FromJson<RunCheckpointData>(
                    PlayerPrefs.GetString(RunCheckpointKey, string.Empty));
                return data != null && data.version == RunCheckpointVersion ? data : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Run checkpoint could not be read: {exception.GetType().Name}");
                return null;
            }
        }

        private void ClearRunCheckpoint()
        {
            preparedBattleCheckpointJson = string.Empty;
            showResumePrompt = false;
            PlayerPrefs.DeleteKey(RunCheckpointKey);
            ClearRevivalSnapshots();
            PlayerPrefs.Save();
            SavePortableProgressBackup();
        }

        private RunCheckpointData CaptureRunCheckpoint(GamePhase storedPhase)
        {
            var data = new RunCheckpointData
            {
                version = RunCheckpointVersion,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                round = Mathf.Clamp(Round, 1, MaxRounds),
                money = Mathf.Max(0, Money),
                gateHealth = Mathf.Clamp(gateHealth, 1f, GateMaxHealth),
                phase = (int)storedPhase,
                lastTier = (int)lastTier,
                augmentOffersWithoutHighTier = augmentOffersWithoutHighTier,
                lastStandCharges = lastStandCharges,
                monstersDefeated = monstersDefeated,
                skillsCast = skillsCast,
                roundsCleared = roundsCleared,
                unitsPlaced = unitsPlaced
            };
            data.activeTacticalItems.AddRange(activeRunItems.Select(item => (int)item));
            data.usedAnyTacticalItem = usedAnyTacticalItemThisRun;
            data.revivalUsed = revivalUsedThisRun;
            data.fieldAidUses = fieldAidUsesThisRun;
            data.runGoldScore = runGoldScore;
            data.perfectRounds = perfectRoundsThisRun;
            data.runGoldAwarded = runGoldAwarded;

            data.augmentPower.AddRange(augmentPower.Select(pair =>
                new CheckpointFloat { key = pair.Key, value = pair.Value }));
            data.augmentCount.AddRange(augmentCount.Select(pair =>
                new CheckpointInt { key = pair.Key, value = pair.Value }));
            data.acquiredAugments.AddRange(acquiredAugments.Values.Select(ToCheckpointOffer));
            data.activeAugmentCooldowns.AddRange(activeAugmentReadyAt.Select(pair =>
                new CheckpointFloat { key = pair.Key, value = Mathf.Max(0f, pair.Value - Time.time) }));
            data.unlockedUnits.AddRange(unlockedUnits.Select(unit => (int)unit));
            data.currentOffers.AddRange(currentOffers.Select(ToCheckpointOffer));
            foreach (var unit in units.Where(unit => unit != null && unit.IsAlive))
            {
                data.units.Add(new CheckpointUnit
                {
                    archetype = (int)unit.Archetype,
                    x = unit.Position.x,
                    y = unit.Position.y,
                    level = unit.Level,
                    experience = unit.Experience,
                    health = unit.Health,
                    holding = unit.IsHoldingPosition,
                    skillCooldown = unit.SkillCooldownRemaining,
                    ultimateCooldown = unit.UltimateCooldownRemaining,
                    placementRound = unit.PlacementRound,
                    placementBatchId = unit.PlacementBatchId,
                    placementRefundCost = unit.PlacementRefundCost
                });
            }
            return data;
        }

        private static CheckpointOffer ToCheckpointOffer(AugmentOffer offer) => new()
        {
            name = offer.Name,
            description = offer.Description,
            effectKey = offer.EffectKey,
            tier = (int)offer.Tier,
            power = offer.Power
        };

        private static AugmentOffer FromCheckpointOffer(CheckpointOffer offer) =>
            new(offer.name ?? string.Empty, offer.description ?? string.Empty,
                offer.effectKey ?? string.Empty, (AugmentTier)Mathf.Clamp(offer.tier, 0, 4), offer.power);

        private void WriteRunCheckpoint(RunCheckpointData data, bool cacheAsPreparedBattle)
        {
            if (data == null || data.units == null || data.units.Count == 0) return;
            var json = JsonUtility.ToJson(data);
            if (string.IsNullOrWhiteSpace(json)) return;
            PlayerPrefs.SetString(RunCheckpointKey, json);
            PlayerPrefs.Save();
            SavePortableProgressBackup();
            if (cacheAsPreparedBattle) preparedBattleCheckpointJson = json;
            nextRunCheckpointAutosaveAt = Time.unscaledTime + RunCheckpointAutosaveInterval;
        }

        private void SaveRunCheckpoint(bool preparingBattle = false)
        {
            if (IsQaMode() && !HasCommandLineArgument("-qaCheckpoint266") &&
                !HasCommandLineArgument("-qaMenuSave272")) return;
            if (showMainMenu || Phase is GamePhase.Defeat or GamePhase.Victory || units.Count == 0) return;

            // A combat checkpoint is deliberately the formation immediately before the wave.
            // It avoids serialising half-resolved projectiles/enemies and prevents a forced
            // shutdown from producing an impossible combat scene on the next launch.
            if (Phase == GamePhase.Battle && !preparingBattle)
            {
                if (!string.IsNullOrEmpty(preparedBattleCheckpointJson) &&
                    !PlayerPrefs.HasKey(RunCheckpointKey))
                {
                    PlayerPrefs.SetString(RunCheckpointKey, preparedBattleCheckpointJson);
                    PlayerPrefs.Save();
                    SavePortableProgressBackup();
                }
                return;
            }

            var storedPhase = Phase == GamePhase.Augment ? GamePhase.Augment : GamePhase.Preparation;
            WriteRunCheckpoint(CaptureRunCheckpoint(storedPhase), preparingBattle);
        }

        private void UpdateRunCheckpointAutosave()
        {
            if (showMainMenu || Time.unscaledTime < nextRunCheckpointAutosaveAt) return;
            nextRunCheckpointAutosaveAt = Time.unscaledTime + RunCheckpointAutosaveInterval;
            SaveRunCheckpoint();
        }

        private void RequestFrontStart()
        {
            if (HasRunCheckpoint())
            {
                showResumePrompt = true;
                return;
            }
            OpenPregameLoadout();
        }

        private void RequestReturnToMainMenu()
        {
            // There is nothing meaningful to preserve after the run has already ended. Those
            // overlays retain their existing direct main-menu route and clear their checkpoint.
            if (Phase is GamePhase.Defeat or GamePhase.Victory || units.Count == 0)
            {
                ReturnToMainMenu(false);
                return;
            }

            showSystemMenu = false;
            showReturnToMainMenuSavePrompt = true;
            Time.timeScale = 0f;
        }

        private void CancelReturnToMainMenu()
        {
            showReturnToMainMenuSavePrompt = false;
            showSystemMenu = true;
            Time.timeScale = 0f;
        }

        private void DrawReturnToMainMenuSavePrompt()
        {
            var screen = new Rect(0f, 0f, GuiWidth, GuiHeight);
            DrawPanel(screen, new Color(0f, .006f, .018f, .78f));
            var safe = SafeGuiRect;
            var width = Mathf.Min(382f, safe.width * .94f);
            var height = Mathf.Min(382f, safe.height * .72f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - height * .5f, width, height);
            DrawOrnatePanel(panel, new Color(.018f, .04f, .082f, .997f),
                new Color(.58f, .82f, .96f), 4f);

            DrawFittedLabel(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 48f),
                L("현재 전선을 저장할까요?", "SAVE THIS FRONT?"), modalTitleStyle, 15);
            DrawFittedLabel(new Rect(panel.x + 26f, panel.y + 74f, panel.width - 52f, 82f),
                L("저장하면 다음 전선 출전에서 이어갈 수 있습니다.\n" +
                  "전투 중에는 현재 라운드 시작 직전 상태로 저장됩니다.",
                  "A SAVED FRONT CAN BE CONTINUED THE NEXT TIME YOU DEPLOY.\n" +
                  "DURING COMBAT, THE SAFE PRE-WAVE FORMATION IS SAVED."),
                new GUIStyle(statStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = true }, 9);

            if (DrawPremiumButton(new Rect(panel.x + 22f, panel.y + 172f, panel.width - 44f, 54f),
                    L("저장하고 메인 메뉴", "SAVE & MAIN MENU"),
                    new Color(.035f, .15f, .125f, .995f), new Color(.42f, 1f, .78f), true))
                ReturnToMainMenu(true);
            if (DrawPremiumButton(new Rect(panel.x + 22f, panel.y + 238f, panel.width - 44f, 52f),
                    L("저장하지 않고 메인 메뉴", "DON'T SAVE"),
                    new Color(.15f, .055f, .052f, .995f), new Color(1f, .56f, .42f), true))
                ReturnToMainMenu(false);
            if (DrawPremiumButton(new Rect(panel.x + 22f, panel.yMax - 60f, panel.width - 44f, 42f),
                    L("취소", "CANCEL"), new Color(.045f, .065f, .105f, .995f),
                    new Color(.5f, .66f, .82f), true))
                CancelReturnToMainMenu();
        }

        private void StartNewFront()
        {
            ClearRunCheckpoint();
            RestartGame(false);
            showMainMenu = false;
            voiceBarks?.SetBattleMusic(true, true);
            ShowToast(L("편성을 마치고 전투를 시작하세요.", "Prepare your formation."));
        }

        private bool RestoreRunCheckpoint(RunCheckpointData data)
        {
            if (data == null || data.units == null || data.units.Count == 0) return false;
            RestartGame(false);
            Round = Mathf.Clamp(data.round, 1, MaxRounds);
            Money = Mathf.Max(0, data.money);
            gateHealth = Mathf.Clamp(data.gateHealth, 1f, GateMaxHealth);
            lastTier = (AugmentTier)Mathf.Clamp(data.lastTier, 0, 4);
            augmentOffersWithoutHighTier = Mathf.Max(0, data.augmentOffersWithoutHighTier);
            lastStandCharges = Mathf.Max(0, data.lastStandCharges);
            monstersDefeated = Mathf.Max(0, data.monstersDefeated);
            skillsCast = Mathf.Max(0, data.skillsCast);
            roundsCleared = Mathf.Max(0, data.roundsCleared);
            unitsPlaced = Mathf.Max(0, data.unitsPlaced);
            activeRunItems.Clear();
            foreach (var value in data.activeTacticalItems ?? new List<int>())
                if (Enum.IsDefined(typeof(TacticalItemId), value)) activeRunItems.Add((TacticalItemId)value);
            usedAnyTacticalItemThisRun = data.usedAnyTacticalItem;
            revivalUsedThisRun = data.revivalUsed;
            fieldAidUsesThisRun = Mathf.Clamp(data.fieldAidUses, 0, 2);
            runGoldScore = Mathf.Max(0, data.runGoldScore);
            perfectRoundsThisRun = Mathf.Max(0, data.perfectRounds);
            runGoldAwarded = data.runGoldAwarded;

            foreach (var pair in data.augmentPower ?? new List<CheckpointFloat>())
                if (!string.IsNullOrEmpty(pair.key)) augmentPower[pair.key] = pair.value;
            foreach (var pair in data.augmentCount ?? new List<CheckpointInt>())
                if (!string.IsNullOrEmpty(pair.key)) augmentCount[pair.key] = pair.value;
            foreach (var saved in data.acquiredAugments ?? new List<CheckpointOffer>())
            {
                var offer = FromCheckpointOffer(saved);
                if (!string.IsNullOrEmpty(offer.EffectKey)) acquiredAugments[offer.EffectKey] = offer;
            }
            foreach (var pair in data.activeAugmentCooldowns ?? new List<CheckpointFloat>())
                if (!string.IsNullOrEmpty(pair.key)) activeAugmentReadyAt[pair.key] = Time.time + Mathf.Max(0f, pair.value);
            foreach (var value in data.unlockedUnits ?? new List<int>())
            {
                var archetype = (UnitArchetype)Mathf.Clamp(value, (int)UnitArchetype.Tank, (int)UnitArchetype.Oracle);
                unlockedUnits.Add(archetype);
            }

            foreach (var saved in data.units)
            {
                var archetype = (UnitArchetype)Mathf.Clamp(saved.archetype,
                    (int)UnitArchetype.Tank, (int)UnitArchetype.Oracle);
                if (!definitions.TryGetValue(archetype, out var baseDefinition)) continue;
                var definition = ApplyUnitAugments(archetype, baseDefinition);
                var position = new Vector2(saved.x, saved.y);
                var actor = new GameObject($"{definition.Name} (Resumed)").AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definition, position);
                actor.RestoreCheckpointState(saved.level, saved.experience, saved.health, saved.holding,
                    saved.skillCooldown, saved.ultimateCooldown);
                actor.MarkPlacementForUndo(saved.placementRound, saved.placementBatchId,
                    saved.placementRefundCost);
                units.Add(actor);
            }
            if (units.Count == 0) return false;
            nextPlacementBatchId = Mathf.Max(1, units.Max(unit => unit.PlacementBatchId) + 1);

            var resumeAugment = data.phase == (int)GamePhase.Augment;
            Phase = resumeAugment ? GamePhase.Augment : GamePhase.Preparation;
            currentOffers = (data.currentOffers ?? new List<CheckpointOffer>())
                .Select(FromCheckpointOffer).Where(offer => !string.IsNullOrEmpty(offer.EffectKey)).ToArray();
            if (resumeAugment && currentOffers.Length != 3) currentOffers = GenerateOffers();
            if (!resumeAugment) currentOffers = Array.Empty<AugmentOffer>();
            showFormationPanel = !resumeAugment;
            showAugmentSummary = false;
            augmentOverlayHidden = false;
            runInProgress = true;
            preparedBattleCheckpointJson = JsonUtility.ToJson(CaptureRunCheckpoint(
                resumeAugment ? GamePhase.Augment : GamePhase.Preparation));
            ApplyBattlefieldMood(MonsterClassForRound(Round), true);
            return true;
        }

        private void ContinueSavedFront()
        {
            var data = ReadRunCheckpoint();
            showResumePrompt = false;
            if (!RestoreRunCheckpoint(data))
            {
                ClearRunCheckpoint();
                StartNewFront();
                ShowToast(L("저장 데이터를 복구할 수 없어 새 전선을 시작합니다.",
                    "THE SAVE COULD NOT BE RESTORED. A NEW FRONT HAS STARTED."));
                return;
            }
            showMainMenu = false;
            voiceBarks?.SetBattleMusic(true, true);
            ShowToast(L($"ROUND {Round} 저장 지점에서 이어갑니다.",
                $"RESUMED FROM ROUND {Round}."));
        }

        private void DrawResumeRunPrompt()
        {
            var data = ReadRunCheckpoint();
            if (data == null)
            {
                showResumePrompt = false;
                return;
            }
            var screen = new Rect(0f, 0f, GuiWidth, GuiHeight);
            DrawPanel(screen, new Color(0f, .008f, .025f, .76f));
            var safe = SafeGuiRect;
            var width = Mathf.Min(374f, safe.width * .92f);
            var panel = new Rect(safe.center.x - width * .5f, safe.center.y - 164f, width, 328f);
            DrawOrnatePanel(panel, new Color(.018f, .042f, .085f, .995f),
                new Color(.48f, .9f, .82f), 4f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 42f),
                L("저장된 전선", "SAVED FRONT"), modalTitleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 73f, panel.width - 48f, 62f),
                L($"ROUND {data.round} · 성문 {Mathf.CeilToInt(data.gateHealth)} / {GateMaxHealth:0}\n" +
                  $"생존 유닛 {data.units.Count}명 · 코인 {data.money}",
                  $"ROUND {data.round} · GATE {Mathf.CeilToInt(data.gateHealth)} / {GateMaxHealth:0}\n" +
                  $"{data.units.Count} SURVIVORS · {data.money} COINS"),
                new GUIStyle(centeredStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(panel.x + 24f, panel.y + 143f, panel.width - 48f, 42f),
                L("강제 종료 전의 스테이지 준비 상태로 복구합니다.",
                    "RESTORE THE STAGE AT ITS SAFE PRE-WAVE CHECKPOINT."),
                new GUIStyle(statStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 11 });

            if (DrawPremiumButton(new Rect(panel.x + 22f, panel.y + 202f, panel.width - 44f, 52f),
                    L("이어하기", "CONTINUE"), new Color(.035f, .16f, .14f, .99f),
                    new Color(.45f, 1f, .82f), true))
                ContinueSavedFront();
            if (DrawPremiumButton(new Rect(panel.x + 22f, panel.y + 264f, panel.width - 44f, 44f),
                    L("새 전선 시작", "START NEW FRONT"), new Color(.14f, .055f, .045f, .99f),
                    new Color(1f, .58f, .4f), true))
            {
                showResumePrompt = false;
                ClearRunCheckpoint();
                OpenPregameLoadout();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveRunCheckpoint();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveRunCheckpoint();
        }

        private void OnApplicationQuit()
        {
            SaveRunCheckpoint();
        }
    }
}
