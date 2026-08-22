using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRelease260Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Preparation;
            var failures = new List<string>();

            yield return PrewarmBossPresentations();
            var bossSprites = bossDirectionalAnimations.Values.SelectMany(AllDirectionalSprites260)
                .Where(sprite => sprite != null).Distinct().ToArray();
            var bossMarginFailures = bossSprites.Where(sprite =>
            {
                var margin = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 20f || margin.y < 20f || margin.z < 20f || margin.w < 20f;
            }).ToArray();
            var knownAncientEntSourceGap = BossFrameFallbackDetailsForQa.Length == 1 &&
                BossFrameFallbackDetailsForQa[0].StartsWith(
                    "boss-ancient-ent-attack-directions-v1-isolated-r4-c3=", StringComparison.Ordinal);
            var bossSourcePassed = bossDirectionalAnimations.Count == 10 && bossSprites.Length >= 850 &&
                                   bossMarginFailures.Length == 0 &&
                                   BossFrameIsolationFailuresForQa.Length == 0 &&
                                   (BossFrameFallbackRepairCountForQa == 0 || knownAncientEntSourceGap) &&
                                   (BossFrameRejectedCompositeCountForQa == 0 || knownAncientEntSourceGap);
            if (!bossSourcePassed)
                failures.Add($"boss-source={bossDirectionalAnimations.Count}/{bossSprites.Length}:" +
                             $"margin={bossMarginFailures.Length}:fail={BossFrameIsolationFailuresForQa.Length}:" +
                             $"reject/repair={BossFrameRejectedCompositeCountForQa}/{BossFrameFallbackRepairCountForQa}:" +
                             string.Join("|", BossFrameFallbackDetailsForQa.Take(2)));

            var regularAtlasSprites = enemyAnimationSets.Values
                .SelectMany(frames => frames ?? Array.Empty<Sprite>()).Where(sprite => sprite != null)
                .Concat(enemyVariantDirectionalAnimations.Values.SelectMany(AllDirectionalSprites260)
                    .Where(sprite => sprite != null)).Distinct().ToArray();
            EnemyUnit.PrimeOpaqueMetrics(regularAtlasSprites);
            var regularMarginFailures = regularAtlasSprites.Where(sprite =>
            {
                var margin = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 12f || margin.y < 12f || margin.z < 12f || margin.w < 12f;
            }).ToArray();
            if (regularAtlasSprites.Length < 60 || regularMarginFailures.Length > 0)
                failures.Add($"enemy-atlas={regularAtlasSprites.Length}:margin={regularMarginFailures.Length}:" +
                             string.Join("|", regularMarginFailures.Take(6).Select(sprite => sprite.name)));

            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var phases = new[] { .03f, .16f, .31f, .48f, .64f, .79f, .94f };
            var regularRuntimeFailures = new List<string>();
            foreach (var profile in EnemyVariantCatalog.AllProfiles)
            {
                var actor = new GameObject($"QA 260 Regular {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, 0, 900000f, false, 0, profile.CombatClass, profile);
                var heights = new List<float>();
                var scaleRatios = new List<float>();
                var contacts = new List<float>();
                var safe = actor.ActivePrimaryBodyChannelsForQa == 1;
                var channelsSafe = true;
                var marginsSafe = true;
                var correctionSafe = true;
                var isolationSafe = true;
                foreach (var direction in directions)
                for (var state = 0; state < 3; state++)
                foreach (var phase in phases)
                {
                    actor.PreviewMotionPoseForQa(direction, state, phase);
                    heights.Add(actor.VisualWorldHeight);
                    scaleRatios.Add(actor.CurrentVisualScaleHeightRatioForQa);
                    if (!actor.IsFlying && profile.CombatClass != EnemyClass.Wisp)
                        contacts.Add(actor.PreviewGroundContactForQa(direction, phase));
                    channelsSafe &= actor.ActivePrimaryBodyChannelsForQa == 1;
                    marginsSafe &= actor.CurrentSpriteHasSafeCellMarginForQa;
                    correctionSafe &= actor.CurrentSpriteHeightCorrectionForQa > .37f &&
                                      actor.CurrentSpriteHeightCorrectionForQa < 2.66f;
                    if (actor.CurrentSpriteHasIsolationAuditForQa)
                        isolationSafe &= actor.CurrentSpriteForeignComponentsForQa == 0;
                    safe &= channelsSafe && marginsSafe && correctionSafe && isolationSafe;
                }
                var minHeight = Mathf.Max(.001f, heights.Min());
                var heightRatio = heights.Max() / minHeight;
                var scaleRatio = scaleRatios.Max() / Mathf.Max(.001f, scaleRatios.Min());
                var groundSpread = contacts.Count == 0 ? 0f : contacts.Max() - contacts.Min();
                var groundAbsolute = contacts.Count == 0 ? 0f : contacts.Max(value => Mathf.Abs(value));
                var presentationStable = actor.HasAuthoredVariantDirectionalAnimationForQa
                    ? scaleRatio <= 1.045f
                    : heightRatio <= 1.075f;
                if (!safe || !presentationStable || groundSpread > .07f || groundAbsolute > .075f)
                    regularRuntimeFailures.Add($"{profile.Id}:{safe}:bounds={heightRatio:0.000}:" +
                                               $"scale={scaleRatio:0.000}:" +
                                               $"{groundSpread:0.000}/{groundAbsolute:0.000}:" +
                                               $"channel={channelsSafe}:margin={marginsSafe}:" +
                                               $"correction={correctionSafe}:isolation={isolationSafe}");
                Destroy(actor.gameObject);
            }
            if (regularRuntimeFailures.Count > 0)
                failures.Add("regular-runtime=" + string.Join("|", regularRuntimeFailures.Take(12)));

            var bossRuntimeFailures = new List<string>();
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var actor = new GameObject($"QA 260 Boss {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                var heights = new List<float>();
                var directionScaleRatios = new List<float>();
                var walkHeights = new List<float>();
                var safe = actor.ActiveBossArtworkChannelsForQa == 1 &&
                           actor.HasCompleteDirectionalAnimationForQa;
                foreach (var direction in directions)
                {
                var directionScales = new List<float>();
                for (var state = 0; state < 3; state++)
                foreach (var phase in phases)
                {
                    actor.PreviewMotionPoseForQa(direction, state, phase);
                    heights.Add(actor.VisualWorldHeight);
                    directionScales.Add(actor.CurrentVisualScaleHeightRatioForQa);
                    if (state == 0) walkHeights.Add(actor.VisualWorldHeight);
                    safe &= actor.ActiveBossArtworkChannelsForQa == 1 &&
                            actor.CurrentSpriteHasSafeCellMarginForQa &&
                            actor.CurrentSpriteHeightCorrectionForQa > .69f &&
                            actor.CurrentSpriteHeightCorrectionForQa < 1.47f;
                }
                directionScaleRatios.Add(directionScales.Max() /
                                         Mathf.Max(.001f, directionScales.Min()));
                }
                var ratio = heights.Max() / Mathf.Max(.001f, heights.Min());
                var scaleRatio = directionScaleRatios.Max();
                walkHeights.Sort();
                var relativeWalkHeight = walkHeights[walkHeights.Count / 2] /
                                         Mathf.Max(.01f, actor.Radius);
                if (!safe || scaleRatio > 1.025f ||
                    Mathf.Abs(relativeWalkHeight - actor.ExpectedBossVisualHeightPerRadiusForQa) > .13f)
                    bossRuntimeFailures.Add($"{profile.Id}:{safe}:bounds={ratio:0.000}:scale={scaleRatio:0.000}");
                Destroy(actor.gameObject);
            }
            if (bossRuntimeFailures.Count > 0)
                failures.Add("boss-runtime=" + string.Join("|", bossRuntimeFailures));

            var campaignFailures = new List<string>();
            for (var round = 1; round <= MaxRounds; round++)
            {
                var chapter = Mathf.Clamp((round - 1) / 5, 0, 9);
                var expectedFamily = EnemyVariantCatalog.ForChapterStage(chapter, 0).FamilyClass;
                var profiles = Enumerable.Range(0, 512)
                    .Select(index => EnemyVariantCatalog.ForWaveMember(round, index)).ToArray();
                if (profiles.Any(profile => profile.FamilyClass != expectedFamily))
                    campaignFailures.Add($"R{round}-family");
                if (profiles.Any(profile => profile.Id == EnemyVariantCatalog.ForChapterStage(chapter, 4).Id))
                    campaignFailures.Add($"R{round}-boss-leak");
                if (round >= 46 && profiles.Any(profile =>
                        profile.Id.StartsWith("jelly_", StringComparison.Ordinal) ||
                        profile.FamilyClass != EnemyClass.Mage))
                    campaignFailures.Add($"R{round}-finale-ooze");
            }
            var previousPressure = 0f;
            var minimumStep = float.MaxValue;
            var maximumStep = 0f;
            for (var round = 1; round <= MaxRounds; round++)
            {
                var pressure = WaveEnemyCount(round) * BaseEnemyHealth(round) *
                               EnemyRoundDamageMultiplierFor(round);
                if (previousPressure > 0f && round >= 6)
                {
                    var step = pressure / previousPressure;
                    minimumStep = Mathf.Min(minimumStep, step);
                    maximumStep = Mathf.Max(maximumStep, step);
                }
                previousPressure = pressure;
            }
            var campaignPassed = campaignFailures.Count == 0 && WaveEnemyCount(1) == 14 &&
                                 WaveEnemyCount(50) == 80 && minimumStep >= .76f &&
                                 maximumStep <= 1.18f;
            if (!campaignPassed)
                failures.Add($"campaign={string.Join("|", campaignFailures.Take(12))}:" +
                             $"count={WaveEnemyCount(1)}-{WaveEnemyCount(50)}:" +
                             $"step={minimumStep:0.000}-{maximumStep:0.000}");

            var augmentMeans = new float[5];
            var augmentStable = true;
            for (var tierIndex = 0; tierIndex < augmentMeans.Length; tierIndex++)
            {
                var tier = (AugmentTier)tierIndex;
                var cards = GetAugmentPool(tier).Concat(FixedRecruitAugments(tier))
                    .Where(card => !IsRecruitKey252(card.EffectKey)).ToArray();
                var seedMeans = new float[8];
                for (var seed = 0; seed < seedMeans.Length; seed++)
                {
                    var stats = SimulateAugmentTier252(tier, 26000 + tierIndex * 991 + seed * 7919,
                        12000, cards);
                    seedMeans[seed] = stats.Mean;
                    augmentStable &= TierImpactWithinBand252(tier, stats);
                }
                augmentMeans[tierIndex] = seedMeans.Average();
                augmentStable &= seedMeans.Max() - seedMeans.Min() <= .0025f;
                if (tierIndex > 0)
                    augmentStable &= augmentMeans[tierIndex] >= augmentMeans[tierIndex - 1] * 1.42f;
            }
            var random = new System.Random(2600811);
            var previousTier = AugmentTier.Bronze;
            var withoutHigh = 0;
            var rarityCounts = new int[5];
            const int raritySamples = 250000;
            for (var sample = 0; sample < raritySamples; sample++)
            {
                var tier = ResolveAugmentTier(previousTier, (sample + 1) % 5 == 0, withoutHigh,
                    (float)random.NextDouble());
                rarityCounts[(int)tier]++;
                withoutHigh = tier >= AugmentTier.Platinum ? 0 : withoutHigh + 1;
                previousTier = tier;
            }
            var diamondRate = rarityCounts[(int)AugmentTier.Diamond] / (float)raritySamples;
            var platinumRate = rarityCounts[(int)AugmentTier.Platinum] / (float)raritySamples;
            augmentStable &= diamondRate is >= .008f and <= .05f &&
                             platinumRate is >= .10f and <= .36f;
            if (!augmentStable)
                failures.Add($"augment={string.Join("/", augmentMeans.Select(value => value.ToString("0.0000")))}:" +
                             $"P={platinumRate:P2}:D={diamondRate:P2}");

            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(unit => unit != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();
            Phase = GamePhase.Battle;
            Round = 12;
            var archer = new GameObject("QA 260 Archer").AddComponent<PlayerUnit>();
            archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer],
                NearestWalkable(new Vector2(-.8f, -5.2f), .18f));
            archer.AddExperience(9999f);
            units.Add(archer);
            selectedUnits.Add(archer);
            archer.SetSelected(true);
            var targetProfile = EnemyVariantCatalog.ForChapterStage(2, 2);
            var target = new GameObject("QA 260 Attack Target").AddComponent<EnemyUnit>();
            target.Initialize(this, 0, 900000f, false, 0, targetProfile.CombatClass, targetProfile);
            target.ForcePositionForQa(NearestWalkable(new Vector2(.8f, -4.1f), .18f));
            enemies.Add(target);

            var statusRect = SelectedUnitStatusRect();
            var statusScreen = new Vector2(statusRect.center.x * UiScale,
                Screen.height - statusRect.center.y * UiScale);
            var statusBlocksWorld = IsHudPointer(statusScreen);
            archer.Stop();
            var holdBefore = archer.IsHoldingPosition;
            archer.PrepareForNextRound();
            var holdAfter = archer.IsHoldingPosition;
            var lancerRange = definitions[UnitArchetype.Lancer].Range;

            archer.TryCommandAttack(target);
            var hapticBefore = attackCommandHapticRequests;
            SetAttackOrderMarker(target);
            TriggerAttackCommandHaptic();
            StartCoroutine(FocusOrderLinkRoutine(archer, target));
            SpawnArcherSkillVolleyEffect(archer, target.Position, target.Position - archer.Position);
            yield return new WaitForSecondsRealtime(.08f);
            var volleyShafts = LastAuthoredArcherArrowCountForQa;
            var volleyHeads = LastAuthoredArcherCompletePartCountForQa;
            var arrowGeometry = volleyShafts >= 3 && volleyShafts <= 5 &&
                                volleyHeads == volleyShafts &&
                                LastAuthoredArcherArrowAspectForQa >= 10f;
            var commandFeedback = AttackOrderHasSilhouetteForQa &&
                                  AttackCommandHapticRequestsForQa == hapticBefore + 1 &&
                                  AttackCommandLineRequestsForQa > 0 &&
                                  LastAttackCommandLinePointCountForQa >= 8;
            if (!statusBlocksWorld || !holdBefore || !holdAfter || lancerRange > 1.20f ||
                !arrowGeometry || !commandFeedback)
                failures.Add($"control={statusBlocksWorld}:{holdBefore}/{holdAfter}:" +
                             $"lancer={lancerRange:0.00}:arrow={arrowGeometry}:{volleyShafts}/{volleyHeads}:" +
                             $"command={commandFeedback}/{AttackOrderHasSilhouetteForQa}/" +
                             $"{AttackCommandHapticRequestsForQa}/{AttackCommandLineRequestsForQa}");

            var guideTouch = VerifyGuideTouchScrollForQa();
            if (!guideTouch) failures.Add("guide-touch-scroll");

            ClearAttackOrderMarker();
            ClearTransientBattlePresentation();
            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(unit => unit != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_260 passed={passed} boss={bossSprites.Length}:" +
                      $"margin={bossMarginFailures.Length}:reject/repair=" +
                      $"{BossFrameRejectedCompositeCountForQa}/{BossFrameFallbackRepairCountForQa} " +
                      $"regular={regularAtlasSprites.Length}:margin={regularMarginFailures.Length}:" +
                      $"runtimeFail={regularRuntimeFailures.Count} bossRuntimeFail={bossRuntimeFailures.Count} " +
                      $"campaign={campaignPassed}:{minimumStep:0.000}-{maximumStep:0.000}:" +
                      $"finale={campaignFailures.Count == 0} augment={augmentStable}:" +
                      $"P={platinumRate:P2}:D={diamondRate:P2} control={statusBlocksWorld}/" +
                      $"{holdAfter}/{lancerRange:0.00}/{arrowGeometry}/{commandFeedback} " +
                      $"guide={guideTouch} fail={string.Join(",", failures.Take(24))}");
            Application.Quit(passed ? 0 : 120);
        }

        private static IEnumerable<Sprite> AllDirectionalSprites260(DirectionalAnimationSet set)
        {
            if (set == null) yield break;
            foreach (var sprite in set.Down) yield return sprite;
            foreach (var sprite in set.DownDiagonal) yield return sprite;
            foreach (var sprite in set.Side) yield return sprite;
            foreach (var sprite in set.UpDiagonal) yield return sprite;
            foreach (var sprite in set.Up) yield return sprite;
        }
    }
}
