using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaBattleDesign258Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            GameLocalization.Current = GameLanguage.Korean;
            var failures = new List<string>();

            var mixedRounds = 0;
            var familyPureRounds = 0;
            for (var round = 1; round <= MaxRounds; round++)
            {
                var profiles = Enumerable.Range(0, 24)
                    .Select(index => EnemyVariantCatalog.ForWaveMember(round, index)).ToArray();
                var expectedChapter = Mathf.Clamp((round - 1) / 5, 0, 9);
                var expectedFamily = EnemyVariantCatalog.ForChapterStage(expectedChapter, 0).FamilyClass;
                var distinctCore = profiles.Where(profile =>
                        !EnemyVariantCatalog.SpecialProfiles.Any(special => special.Id == profile.Id))
                    .Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count();
                if (distinctCore >= 3) mixedRounds++;
                else failures.Add($"wave-{round}-variety={distinctCore}");
                if (profiles.All(profile => profile.FamilyClass == expectedFamily)) familyPureRounds++;
                else failures.Add($"wave-{round}-family-mix");
                var bossId = EnemyVariantCatalog.ForChapterStage(expectedChapter, 4).Id;
                if (profiles.Any(profile => profile.Id == bossId)) failures.Add($"wave-{round}-boss-in-lineup");
            }

            var previousThreat = 0f;
            var maximumStep = 0f;
            var minimumStep = float.MaxValue;
            var smoothCurve = true;
            for (var round = 1; round <= MaxRounds; round++)
            {
                Round = round;
                var threat = WaveEnemyCount(round) * BaseEnemyHealth(round) * EnemyRoundDamageMultiplier;
                if (previousThreat > 0f)
                {
                    var ratio = threat / previousThreat;
                    maximumStep = Mathf.Max(maximumStep, ratio);
                    minimumStep = Mathf.Min(minimumStep, ratio);
                    // Round 1-4 intentionally teach by visibly adding pressure. Every later
                    // transition, including Wisp entry at R36, must remain a controlled ramp.
                    if (round >= 6 && (ratio < .985f || ratio > 1.22f))
                    {
                        smoothCurve = false;
                        failures.Add($"pressure-{round}={ratio:0.000}");
                    }
                }
                previousThreat = threat;
            }

            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray())
                Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(unit => unit != null).ToArray())
                Destroy(unit.gameObject);
            units.Clear();
            Phase = GamePhase.Battle;
            Round = 12;

            PlayerUnit Hero(UnitArchetype archetype, Vector2 position)
            {
                var actor = new GameObject($"QA 258 {archetype}").AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definitions[archetype], NearestWalkable(position, .18f));
                actor.AddExperience(9999f);
                units.Add(actor);
                return actor;
            }

            EnemyUnit Enemy(string name, Vector2 position, int index)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(2, index % 4);
                var target = new GameObject(name).AddComponent<EnemyUnit>();
                target.Initialize(this, index, 900000f, false, 0, profile.CombatClass, profile);
                target.ForcePositionForQa(NearestWalkable(position, .18f));
                enemies.Add(target);
                return target;
            }

            var tank = Hero(UnitArchetype.Tank, new Vector2(-2.1f, -5.2f));
            var archer = Hero(UnitArchetype.Archer, new Vector2(-1.0f, -5.2f));
            var druid = Hero(UnitArchetype.Druid, new Vector2(.2f, -5.2f));
            var mage = Hero(UnitArchetype.SingleMage, new Vector2(1.4f, -5.2f));
            var lancer = Hero(UnitArchetype.Lancer, new Vector2(2.3f, -5.2f));
            var ally = Hero(UnitArchetype.Melee, new Vector2(.45f, -5.0f));

            var emptyTankBlocked = !HasValidUltimateContext(tank);
            var emptyArcherBlocked = !HasValidUltimateContext(archer);
            var emptyDruidBlocked = !HasValidUltimateContext(druid);
            var emptyMageBlocked = !HasValidUltimateContext(mage);
            tank.TakeDamage(24f, DamageType.Pure);
            var woundedTankReady = HasValidUltimateContext(tank);
            ally.TakeDamage(24f, DamageType.Pure);
            var protectionReady = HasValidUltimateContext(druid);
            var far = Enemy("QA 258 Global Target", new Vector2(0f, 4.4f), 0);
            var globalArcherReady = HasValidUltimateContext(archer);
            var rangedMageBlocked = !HasValidUltimateContext(mage);
            far.ForcePositionForQa(NearestWalkable(new Vector2(1.45f, -2.9f), .18f));
            var localMageReady = HasValidUltimateContext(mage);
            var cluster = new[]
            {
                Enemy("QA 258 Charge Target A", new Vector2(2.05f, -2.7f), 1),
                Enemy("QA 258 Charge Target B", new Vector2(2.35f, -2.75f), 2),
                Enemy("QA 258 Charge Target C", new Vector2(2.55f, -2.55f), 3)
            };
            var lancerReady = HasValidUltimateContext(lancer);
            var contextsPassed = emptyTankBlocked && emptyArcherBlocked && emptyDruidBlocked &&
                                 emptyMageBlocked && woundedTankReady && protectionReady &&
                                 globalArcherReady && rangedMageBlocked && localMageReady && lancerReady;
            if (!contextsPassed)
                failures.Add($"contexts={emptyTankBlocked}/{emptyArcherBlocked}/{emptyDruidBlocked}/" +
                             $"{emptyMageBlocked}/{woundedTankReady}/{protectionReady}/" +
                             $"{globalArcherReady}/{rangedMageBlocked}/{localMageReady}/{lancerReady}");

            var originalPosition = lancer.Position;
            var recoverySerial = lancer.UltimateRecoverySerialForQa;
            StartCoroutine(HeroUltimateRoutine(lancer, cluster[1]));
            for (var waitStep = 0; waitStep < 40 &&
                 lancer != null && lancer.UltimateRecoverySerialForQa == recoverySerial; waitStep++)
            {
                Time.timeScale = 1f;
                yield return new WaitForSecondsRealtime(.05f);
            }
            var lancerRecovery = lancer != null && lancer.IsAlive && !lancer.IsMoving &&
                                  lancer.UltimateRecoveryActiveForQa &&
                                  lancer.UltimateRecoverySerialForQa == recoverySerial + 1 &&
                                  Vector2.Distance(lancer.Position, originalPosition) < .08f;
            if (!lancerRecovery) failures.Add($"lancer-recovery:alive={lancer != null && lancer.IsAlive}:" +
                $"moving={lancer != null && lancer.IsMoving}:active={lancer != null && lancer.UltimateRecoveryActiveForQa}:" +
                $"serial={lancer?.UltimateRecoverySerialForQa - recoverySerial}:" +
                $"distance={(lancer == null ? -1f : Vector2.Distance(lancer.Position, originalPosition)):0.000}");

            var activeNameUpdated = GetAugmentPool(AugmentTier.Gold).Any(template =>
                template.EffectKey == "ActiveVolley" && template.Name.Contains("화살 포화"));
            if (!activeNameUpdated) failures.Add("active-barrage-name");
            var scrollReady = augmentSummaryScroll == Vector2.zero &&
                              typeof(JellyGateGame).GetMethod("HandleAugmentSummaryTouchDrag",
                                  System.Reflection.BindingFlags.Instance |
                                  System.Reflection.BindingFlags.NonPublic) != null;
            if (!scrollReady) failures.Add("augment-scroll");

            ClearTransientBattlePresentation();
            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray())
                Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(unit => unit != null).ToArray())
                Destroy(unit.gameObject);
            units.Clear();

            var passed = failures.Count == 0;
            Debug.Log($"QA_BATTLE_DESIGN_258 passed={passed} mixed={mixedRounds}/50 " +
                      $"families={familyPureRounds}/50 curve={smoothCurve}:{minimumStep:0.000}-" +
                      $"{maximumStep:0.000} contexts={contextsPassed} lancer={lancerRecovery} " +
                      $"barrage={activeNameUpdated} scroll={scrollReady} fail={string.Join(",", failures.Take(20))}");
            Application.Quit(passed ? 0 : 118);
        }
    }
}
