using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaStatusLayout273CaptureRoutine(bool hero = false, bool magic = false)
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            showMissionPanel = false;
            showGuidePanel = false;
            Phase = GamePhase.Preparation;
            GameLocalization.Current = GameLanguage.Korean;
            var archetype = magic ? UnitArchetype.Oracle : UnitArchetype.Archer;
            var actor = new GameObject($"QA276 Status {archetype}").AddComponent<PlayerUnit>();
            actor.Initialize(this, archetype, definitions[archetype], new Vector2(0f, -2f));
            if (hero) actor.RestoreCheckpointState(5, 635f, actor.MaxHealth, false, 6.1f, 9.4f);
            else actor.RestoreCheckpointState(1, 38f, actor.MaxHealth, false, 7.3f, 0f);
            units.Add(actor);
            SelectOnly(actor);
            yield return null;
            Debug.Log($"QA_STATUS_LAYOUT_276_CAPTURE hero={hero} magic={magic} rect={SelectedUnitStatusRect()} " +
                      $"stats={SelectedUnitStatsRect()} skill={SelectedUnitSkillRect()} " +
                      $"ultimate={SelectedUnitUltimateRect()}");
        }

        private IEnumerator QaMenuSaveFlow272Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            var previous = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
            var hadPrevious = PlayerPrefs.HasKey(RunCheckpointKey);
            var failures = new List<string>();
            try
            {
                ClearRunCheckpoint();
                showMainMenu = false;
                RestartGame(false);
                Phase = GamePhase.Preparation;
                Round = 12;
                Money = 9;
                gateHealth = 451f;
                var tank = new GameObject("QA272 Saved Tank").AddComponent<PlayerUnit>();
                tank.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank], new Vector2(0f, -5f));
                units.Add(tank);
                SaveRunCheckpoint(true);
                if (!HasRunCheckpoint()) failures.Add("seed-save");

                showMainMenu = true;
                showResumePrompt = true;
                InitializeRunCheckpointPrompt();
                var noAutomaticPrompt = !showResumePrompt;
                if (!noAutomaticPrompt) failures.Add("automatic-resume-prompt");

                RequestFrontStart();
                var deployOnlyPrompt = showMainMenu && showResumePrompt;
                if (!deployOnlyPrompt) failures.Add("deploy-did-not-prompt");
                showResumePrompt = false;

                showMainMenu = false;
                showSystemMenu = true;
                RequestReturnToMainMenu();
                var returnPrompt = showReturnToMainMenuSavePrompt && !showSystemMenu &&
                                   Mathf.Approximately(Time.timeScale, 0f);
                if (!returnPrompt) failures.Add("return-prompt");
                CancelReturnToMainMenu();
                var cancelSafe = !showReturnToMainMenuSavePrompt && showSystemMenu &&
                                 Mathf.Approximately(Time.timeScale, 0f);
                if (!cancelSafe) failures.Add("cancel-stack");

                RequestReturnToMainMenu();
                ReturnToMainMenu(true);
                var savedReturn = showMainMenu && !showReturnToMainMenuSavePrompt && HasRunCheckpoint() &&
                                  Mathf.Approximately(Time.timeScale, 1f);
                if (!savedReturn) failures.Add("save-return");

                showMainMenu = false;
                RestartGame(false);
                Phase = GamePhase.Preparation;
                Round = 13;
                var archer = new GameObject("QA272 Discarded Archer").AddComponent<PlayerUnit>();
                archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer], new Vector2(0f, -4f));
                units.Add(archer);
                SaveRunCheckpoint(true);
                RequestReturnToMainMenu();
                ReturnToMainMenu(false);
                var discardedReturn = showMainMenu && !showReturnToMainMenuSavePrompt &&
                                      !HasRunCheckpoint() && Mathf.Approximately(Time.timeScale, 1f);
                if (!discardedReturn) failures.Add("discard-return");

                var passed = failures.Count == 0;
                Debug.Log($"QA_MENU_SAVE_FLOW_272 passed={passed} auto={noAutomaticPrompt} " +
                          $"deploy={deployOnlyPrompt} prompt={returnPrompt} cancel={cancelSafe} " +
                          $"save={savedReturn} discard={discardedReturn} fail={string.Join(",", failures)}");
                Application.Quit(passed ? 0 : 132);
            }
            finally
            {
                if (hadPrevious) PlayerPrefs.SetString(RunCheckpointKey, previous);
                else PlayerPrefs.DeleteKey(RunCheckpointKey);
                PlayerPrefs.Save();
                Time.timeScale = 1f;
            }
        }

        private IEnumerator QaCombatStats270Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            var failures = new List<string>();

            var roster = Enum.GetValues(typeof(UnitArchetype)).Cast<UnitArchetype>()
                .Where(value => value != UnitArchetype.None && definitions.ContainsKey(value)).ToArray();
            var playerStats = 0;
            foreach (var archetype in roster)
            {
                var actor = new GameObject($"QA270 {archetype}").AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definitions[archetype], Vector2.zero);
                var complete = actor.Armor >= 0f && actor.MagicResistance >= 0f &&
                               actor.PhysicalPenetration >= 0f && actor.MagicPenetration >= 0f &&
                               (actor.PhysicalPenetration > 0f || actor.MagicPenetration > 0f);
                if (!complete) failures.Add($"player-stat:{archetype}");
                playerStats++;
                Destroy(actor.gameObject);
            }

            var enemyStats = 0;
            var classes = Enum.GetValues(typeof(EnemyClass)).Cast<EnemyClass>()
                .Where(value => value != EnemyClass.Boss).ToArray();
            foreach (var round in new[] { 1, 25, 50 })
            foreach (var enemyClass in classes)
            {
                var physical = EnemyUnit.EnemyPhysicalPenetration(enemyClass, round, false);
                var magic = EnemyUnit.EnemyMagicPenetration(enemyClass, round, false);
                if (physical < 0f || magic < 0f || physical + magic <= 0f)
                    failures.Add($"enemy-stat:{enemyClass}:r{round}:{physical:0.0}/{magic:0.0}");
                if (round > 1 &&
                    (physical < EnemyUnit.EnemyPhysicalPenetration(enemyClass, 1, false) ||
                     magic < EnemyUnit.EnemyMagicPenetration(enemyClass, 1, false)))
                    failures.Add($"enemy-regress:{enemyClass}:r{round}");
                enemyStats++;
            }

            var physicalBase = CombatMath.MitigatedDamage(100f, DamageType.Physical, 50f, 70f);
            var physicalPartial = CombatMath.MitigatedDamage(100f, DamageType.Physical, 50f, 70f, 20f);
            var physicalCapped = CombatMath.MitigatedDamage(100f, DamageType.Physical, 50f, 70f, 999f);
            var magicBase = CombatMath.MitigatedDamage(100f, DamageType.Magic, 50f, 70f);
            var magicPartial = CombatMath.MitigatedDamage(100f, DamageType.Magic, 50f, 70f, 0f, 25f);
            var magicCapped = CombatMath.MitigatedDamage(100f, DamageType.Magic, 50f, 70f, 0f, 999f);
            var pure = CombatMath.MitigatedDamage(100f, DamageType.Pure, 999f, 999f, 999f, 999f);
            var formula = physicalPartial > physicalBase && physicalCapped <= 100.001f &&
                          Mathf.Abs(physicalCapped - 100f) < .001f && magicPartial > magicBase &&
                          Mathf.Abs(magicCapped - 100f) < .001f && Mathf.Abs(pure - 100f) < .001f;
            if (!formula) failures.Add($"formula:{physicalBase:0.00}/{physicalPartial:0.00}/" +
                                       $"{physicalCapped:0.00}:{magicBase:0.00}/{magicPartial:0.00}/" +
                                       $"{magicCapped:0.00}:{pure:0.00}");

            var uiActor = new GameObject("QA270 UI Unit").AddComponent<PlayerUnit>();
            uiActor.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer], Vector2.zero);
            selectedUnits.Add(uiActor);
            uiActor.SetSelected(true);
            var originalLanguage = GameLocalization.Current;
            GameLocalization.Current = GameLanguage.Korean;
            var korean = SelectedUnitPrimaryStats(uiActor) + " | " +
                         SelectedUnitDefenseAndPenetration(uiActor) + " | " +
                         SelectedUnitCombatRangesAndCritical(uiActor);
            GameLocalization.Current = GameLanguage.English;
            var english = SelectedUnitPrimaryStats(uiActor) + " | " +
                          SelectedUnitDefenseAndPenetration(uiActor) + " | " +
                          SelectedUnitCombatRangesAndCritical(uiActor);
            GameLocalization.Current = GameLanguage.Korean;
            var fullKoreanCells = SelectedUnitStatCells(uiActor);
            GameLocalization.Current = GameLanguage.English;
            var fullEnglishCells = SelectedUnitStatCells(uiActor);
            var magicActor = new GameObject("QA276 UI Magic Unit").AddComponent<PlayerUnit>();
            magicActor.Initialize(this, UnitArchetype.Oracle, definitions[UnitArchetype.Oracle], Vector2.zero);
            GameLocalization.Current = GameLanguage.Korean;
            var magicKoreanCells = SelectedUnitStatCells(magicActor);
            GameLocalization.Current = GameLanguage.English;
            var magicEnglishCells = SelectedUnitStatCells(magicActor);
            GameLocalization.Current = originalLanguage;
            var labels = korean.Contains("방어") && korean.Contains("마저") && korean.Contains("방관") &&
                         korean.Contains("마관") && english.Contains("DEF") && english.Contains("RES") &&
                         english.Contains("ARM PEN") && english.Contains("MAG PEN") &&
                         fullKoreanCells.Length == 8 && magicKoreanCells.Length == 8 &&
                         fullKoreanCells.Contains($"마법 저항 {uiActor.MagicResistance:0}") &&
                         fullKoreanCells.Contains($"방어력 관통 {uiActor.PhysicalPenetration:0}") &&
                         fullKoreanCells.Any(value => value.StartsWith("공격력 ")) &&
                         !fullKoreanCells.Any(value => value.StartsWith("마력 ") || value.StartsWith("마법 관통") ||
                                                              value.StartsWith("탐지 범위")) &&
                         magicKoreanCells.Contains($"마법 관통 {magicActor.MagicPenetration:0}") &&
                         magicKoreanCells.Any(value => value.StartsWith("마력 ")) &&
                         !magicKoreanCells.Any(value => value.StartsWith("공격력 ") || value.StartsWith("방어력 관통") ||
                                                               value.StartsWith("탐지 범위")) &&
                         fullKoreanCells.Any(value => value.StartsWith("치명타 ") && value.Contains("피해")) &&
                         fullEnglishCells.Any(value => value.StartsWith("MAGIC RESIST")) &&
                         !fullEnglishCells.Contains($"MAGIC {uiActor.MagicPower:0}") &&
                         !fullEnglishCells.Any(value => value.StartsWith("MAGIC PENETRATION") ||
                                                               value.StartsWith("DETECTION RANGE")) &&
                         magicEnglishCells.Contains($"MAGIC {magicActor.MagicPower:0}") &&
                         magicEnglishCells.Any(value => value.StartsWith("MAGIC PENETRATION")) &&
                         !magicEnglishCells.Contains($"ATTACK {magicActor.AttackPower:0}") &&
                         !magicEnglishCells.Any(value => value.StartsWith("ARMOR PENETRATION") ||
                                                                value.StartsWith("DETECTION RANGE"));
            if (!labels) failures.Add("ui-labels");

            var labelStyle = new GUIStyle(statStyle)
            {
                fontSize = 14, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip
            };
            foreach (var statusWidth in new[] { 326f, 371f, 440f })
            {
                var statsWidth = statusWidth - 20f;
                var cellWidth = (statsWidth - 18f) * .25f;
                foreach (var value in fullKoreanCells)
                    if (!(statusWidth < 370f || value.StartsWith("치명타 ")
                            ? FittedWrappedLabelFitsForQa(new Rect(0f, 0f, cellWidth,
                                    statusWidth < 370f ? 30f : 23f), value,
                                labelStyle, 8)
                            : FittedLabelFitsForQa(new Rect(0f, 0f, cellWidth, 23f), value,
                                labelStyle, 10)))
                        failures.Add($"stat-label-clip:ko:{statusWidth:0}:{cellWidth:0.0}:{value}");
                foreach (var value in magicKoreanCells)
                    if (!(statusWidth < 370f || value.StartsWith("치명타 ")
                            ? FittedWrappedLabelFitsForQa(new Rect(0f, 0f, cellWidth,
                                    statusWidth < 370f ? 30f : 23f), value,
                                labelStyle, 8)
                            : FittedLabelFitsForQa(new Rect(0f, 0f, cellWidth, 23f), value,
                                labelStyle, 10)))
                        failures.Add($"stat-label-clip:ko-magic:{statusWidth:0}:{cellWidth:0.0}:{value}");
                foreach (var value in fullEnglishCells)
                    if (!FittedWrappedLabelFitsForQa(new Rect(0f, 0f, cellWidth, 28f), value,
                            labelStyle, 8))
                        failures.Add($"stat-label-clip:en:{statusWidth:0}:{cellWidth:0.0}:{value}");
                foreach (var value in magicEnglishCells)
                    if (!FittedWrappedLabelFitsForQa(new Rect(0f, 0f, cellWidth, 28f), value,
                            labelStyle, 8))
                        failures.Add($"stat-label-clip:en-magic:{statusWidth:0}:{cellWidth:0.0}:{value}");
            }

            uiActor.RestoreCheckpointState(2, 100f, uiActor.MaxHealth, false, 7.3f, 0f);
            if (Mathf.Abs(uiActor.ExperienceWithinCurrentLevel() - 28f) > .01f ||
                Mathf.Abs(uiActor.ExperienceRequiredForCurrentLevel() - 110f) > .01f)
                failures.Add($"experience-current-level:{uiActor.ExperienceWithinCurrentLevel():0}/" +
                             $"{uiActor.ExperienceRequiredForCurrentLevel():0}");
            uiActor.RestoreCheckpointState(1, 0f, uiActor.MaxHealth, false, 7.3f, 0f);

            GameLocalization.Current = GameLanguage.Korean;
            var normalRect = SelectedUnitStatusRect();
            var normalStatsRect = SelectedUnitStatsRect();
            var normalSkillRect = SelectedUnitSkillRect();
            var bottomBoundary = SafeGuiRect.yMax - BottomHudHeight;
            var statusScreen = new Vector2(normalRect.center.x * UiScale,
                Screen.height - normalRect.center.y * UiScale);
            var reclaimedGui = new Vector2(normalRect.center.x, normalRect.y - 6f);
            var reclaimedScreen = new Vector2(reclaimedGui.x * UiScale,
                Screen.height - reclaimedGui.y * UiScale);
            var reclaimedInsideBattlefield = reclaimedGui.y >= SafeGuiRect.y + TopHudHeight;
            var reclaimedMovementArea = !normalRect.Contains(reclaimedGui) &&
                                        (!reclaimedInsideBattlefield || !IsHudPointer(reclaimedScreen));
            var normalLayout = Mathf.Abs(normalRect.height - SelectedUnitStatusHeight()) < .01f &&
                               normalRect.yMax <= bottomBoundary - 4.9f &&
                               Mathf.Abs(normalRect.width - (SafeGuiRect.width - 12f)) < .01f &&
                               normalStatsRect.yMax + 1f <= normalSkillRect.yMin &&
                               normalSkillRect.xMin >= normalRect.xMin + 9.9f &&
                               normalSkillRect.xMax <= normalRect.xMax - 9.9f &&
                               normalSkillRect.yMax <= normalRect.yMax - 4f &&
                               IsHudPointer(statusScreen) && reclaimedMovementArea;
            uiActor.RestoreCheckpointState(5, 635f, uiActor.MaxHealth, false, 0f, 0f);
            var heroRect = SelectedUnitStatusRect();
            var ultimateRect = SelectedUnitUltimateRect();
            var heroSkillRect = SelectedUnitSkillRect();
            var heroLayout = Mathf.Abs(heroRect.height - SelectedUnitStatusHeight()) < .01f &&
                             heroRect.yMax <= bottomBoundary - 4.9f &&
                             heroSkillRect.xMax + 5f <= ultimateRect.xMin &&
                             Mathf.Abs(heroSkillRect.yMin - ultimateRect.yMin) < .01f &&
                             ultimateRect.width >= 148f &&
                             ultimateRect.yMax <= heroRect.yMax;
            if (!normalLayout || !heroLayout)
                failures.Add($"layout:{normalRect}:{heroRect}:{ultimateRect}:bottom={bottomBoundary:0.0}");
            selectedUnits.Clear();
            Destroy(uiActor.gameObject);
            Destroy(magicActor.gameObject);
            GameLocalization.Current = originalLanguage;

            foreach (var rowWidth in new[] { 306f, 380f })
            {
                var row = new Rect(0f, 0f, rowWidth, ChallengeRowHeight);
                var title = ChallengeTitleRectForQa(row);
                var description = ChallengeDescriptionRectForQa(row);
                var status = ChallengeStatusRectForQa(row);
                var challengeLayout = title.xMax <= status.xMin && description.xMax <= status.xMin &&
                                      title.yMax <= description.yMin && description.yMax <= row.yMax &&
                                      status.xMax <= row.xMax && status.yMax <= row.yMax &&
                                      ChallengeRowStride > ChallengeRowHeight;
                if (!challengeLayout) failures.Add($"challenge-layout:{rowWidth}:{title}:{description}:{status}");
            }

            var oracle = new GameObject("QA270 Oracle Support").AddComponent<PlayerUnit>();
            oracle.Initialize(this, UnitArchetype.Oracle, definitions[UnitArchetype.Oracle], Vector2.zero);
            var supportedAlly = new GameObject("QA270 Supported Ally").AddComponent<PlayerUnit>();
            supportedAlly.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank], new Vector2(1f, 0f));
            supportedAlly.RestoreCheckpointState(1, 0f, supportedAlly.MaxHealth - 50f, false, 5f, 0f);
            units.Add(oracle);
            units.Add(supportedAlly);
            var healthBeforeSupport = supportedAlly.Health;
            var cooldownBeforeSupport = supportedAlly.SkillCooldownRemaining;
            var expectedSupportHealth = Mathf.Min(supportedAlly.MaxHealth,
                healthBeforeSupport + oracle.MagicPower * .12f);
            ApplyOracleMoonlightSupport(oracle, oracle.MagicPower, false);
            var oracleSupport = Mathf.Abs(supportedAlly.Health - expectedSupportHealth) < .02f &&
                                Mathf.Abs(supportedAlly.SkillCooldownRemaining -
                                          Mathf.Max(0f, cooldownBeforeSupport - .55f)) < .02f;
            if (!oracleSupport)
                failures.Add($"oracle-support:hp={healthBeforeSupport:0.00}->{supportedAlly.Health:0.00}:" +
                             $"cd={cooldownBeforeSupport:0.00}->{supportedAlly.SkillCooldownRemaining:0.00}");
            units.Remove(oracle);
            units.Remove(supportedAlly);
            Destroy(oracle.gameObject);
            Destroy(supportedAlly.gameObject);

            var passed = failures.Count == 0 && playerStats == roster.Length &&
                         enemyStats == classes.Length * 3;
            Debug.Log($"QA_COMBAT_STATS_270 passed={passed} players={playerStats}/{roster.Length} " +
                      $"enemies={enemyStats}/{classes.Length * 3} formula={formula} labels={labels} " +
                      $"layout={normalLayout}/{heroLayout} reclaimed={reclaimedMovementArea} oracleSupport={oracleSupport} " +
                      $"fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 130);
        }
    }
}
