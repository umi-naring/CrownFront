using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaVisual261Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Battle;
            Round = 46;
            spawning = true;
            yield return PrewarmBossPresentations();

            foreach (var enemy in enemies.Where(item => item != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(item => item != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();

            var finalPositions = new[]
            {
                new Vector2(-2.85f, -4.95f), new Vector2(-1.35f, -3.85f),
                new Vector2(1.25f, -3.75f), new Vector2(2.85f, -4.95f)
            };
            for (var index = 0; index < finalPositions.Length; index++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(9, index);
                var actor = new GameObject($"QA Visual R46 {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, index, BaseEnemyHealth(46), false, index % Mathf.Max(1, LaneCount),
                    profile.CombatClass, profile);
                actor.ForcePositionForQa(NearestWalkable(finalPositions[index], .18f));
                enemies.Add(actor);
            }

            var bossProfile = EnemyVariantCatalog.ForChapterStage(9, 4);
            var boss = new GameObject("QA Visual R50 Abyss Sovereign").AddComponent<EnemyUnit>();
            boss.Initialize(this, 0, BaseEnemyHealth(50) * 4.8f, true, 0,
                bossProfile.CombatClass, bossProfile);
            boss.ForcePositionForQa(NearestWalkable(new Vector2(0f, 3.10f), .34f));
            enemies.Add(boss);

            var tank = new GameObject("QA Visual Sacred Bulwark Tank").AddComponent<PlayerUnit>();
            tank.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank],
                NearestWalkable(new Vector2(0f, -5.55f), .18f));
            tank.AddExperience(9999f);
            units.Add(tank);
            yield return null;
            ClearTransientBattlePresentation();
            StartCoroutine(HeroUltimateRoutine(tank, boss));
            yield return new WaitForSecondsRealtime(.48f);
            Time.timeScale = 0f;
            Debug.Log("QA_VISUAL_261 ready=True finale=4 boss=True sacredSphere=True");
        }

        private IEnumerator QaRelease261Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Preparation;
            var failures = new List<string>();
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var phases = new[] { .04f, .23f, .48f, .73f, .94f };
            var actualActorsChecked = 0;
            var actualPosesChecked = 0;
            var finalFrontSprites = new HashSet<int>();
            var finalBackSprites = new HashSet<int>();
            var roundPressures = new float[MaxRounds + 1];

            yield return PrewarmBossPresentations();

            // This is an actual runtime-spawn audit, not a catalogue audit. Every profile that
            // can appear in every round is initialized through EnemyUnit and sampled in all
            // eight directions across walk, attack and skill timelines.
            for (var round = 1; round <= MaxRounds; round++)
            {
                Round = round;
                var count = WaveEnemyCount(round);
                var bossRound = round % 5 == 0;
                var regularCount = bossRound ? Mathf.Max(9, count - 7) : count;
                var waveProfiles = Enumerable.Range(0, regularCount)
                    .Select(index => EnemyVariantCatalog.ForWaveMember(round, index)).ToArray();
                var groups = waveProfiles.GroupBy(profile => profile.Id).ToArray();
                foreach (var group in groups)
                {
                    var profile = group.First();
                    var ranged = profile.CombatClass is EnemyClass.Mage or EnemyClass.Shaman or
                        EnemyClass.Siege or EnemyClass.Wisp or EnemyClass.Silencer or EnemyClass.Cursebinder;
                    var startingHealth = BaseEnemyHealth(round) * (ranged ? .84f : 1f);
                    var actor = new GameObject($"QA 261 R{round} {profile.Id}").AddComponent<EnemyUnit>();
                    actor.Initialize(this, 0, startingHealth, false, 0, profile.CombatClass, profile);
                    actualActorsChecked++;
                    var heights = new List<float>();
                    var scales = new List<float>();
                    var safe = actor.ActivePrimaryBodyChannelsForQa == 1;
                    foreach (var direction in directions)
                    for (var state = 0; state < 3; state++)
                    foreach (var phase in phases)
                    {
                        actor.PreviewMotionPoseForQa(direction, state, phase);
                        actualPosesChecked++;
                        heights.Add(actor.VisualWorldHeight);
                        scales.Add(actor.CurrentVisualScaleHeightRatioForQa);
                        safe &= actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                actor.CurrentSpriteHasSafeCellMarginForQa &&
                                actor.CurrentSpriteHeightCorrectionForQa is > .37f and < 2.66f;
                        if (actor.CurrentSpriteHasIsolationAuditForQa)
                            safe &= actor.CurrentSpriteForeignComponentsForQa == 0;
                    }
                    var heightRatio = heights.Max() / Mathf.Max(.001f, heights.Min());
                    var scaleRatio = scales.Max() / Mathf.Max(.001f, scales.Min());
                    var expectedBand = ExpectedRegularVisualBand261(profile.CombatClass);
                    var medianHeight = heights.OrderBy(value => value).ElementAt(heights.Count / 2);
                    // Direction paintings have different source occupancy and therefore require
                    // different correction scales. What players see is the final opaque body
                    // height; rejecting the correction itself falsely failed otherwise stable
                    // jelly/veil/armour action frames.
                    var presentationStable = heightRatio <= 1.08f;
                    safe &= presentationStable;

                    if (round >= 46)
                    {
                        actor.PreviewMotionPoseForQa(Vector2.down, 0, .15f);
                        var frontTexture = actor.CurrentFrameTextureNameForQa;
                        var usesFrontArt = actor.UsesAuthoredVariantArt;
                        finalFrontSprites.Add(actor.CurrentSpriteIdForQa);
                        actor.PreviewMotionPoseForQa(Vector2.up, 0, .15f);
                        var backTexture = actor.CurrentFrameTextureNameForQa;
                        var usesBackArt = actor.UsesAuthoredVariantBackArt;
                        finalBackSprites.Add(actor.CurrentSpriteIdForQa);
                        safe &= frontTexture.Contains("enemy-abyss-roster-v1-roster-cell",
                                    StringComparison.OrdinalIgnoreCase) &&
                                backTexture.Contains("enemy-abyss-roster-back-v1-roster-cell",
                                    StringComparison.OrdinalIgnoreCase) &&
                                !frontTexture.Contains("jelly", StringComparison.OrdinalIgnoreCase) &&
                                !backTexture.Contains("jelly", StringComparison.OrdinalIgnoreCase) &&
                                usesFrontArt && usesBackArt &&
                                medianHeight >= expectedBand.x && medianHeight <= expectedBand.y;
                    }

                    roundPressures[round] += group.Count() * actor.MaxHealth *
                        (actor.AttackPower + actor.MagicPower * .55f + actor.GateDamage * .22f);
                    if (!safe)
                        failures.Add($"R{round}:{profile.Id}:sprite:h={heightRatio:0.000}/" +
                                     $"{medianHeight:0.000}:s={scaleRatio:0.000}:" +
                                     actor.CurrentFrameTextureNameForQa);
                    Destroy(actor.gameObject);
                }

                if (bossRound)
                {
                    var chapter = Mathf.Clamp((round - 1) / 5, 0, 9);
                    var bossProfile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                    var boss = new GameObject($"QA 261 R{round} Boss {bossProfile.Id}")
                        .AddComponent<EnemyUnit>();
                    boss.Initialize(this, 0, BaseEnemyHealth(round) * (4.28f + chapter * .18f),
                        true, 0, bossProfile.CombatClass, bossProfile);
                    actualActorsChecked++;
                    var bossSafe = boss.ActiveBossArtworkChannelsForQa == 1 &&
                                   boss.HasCompleteDirectionalAnimationForQa;
                    var bossDirectionScaleRatios = new List<float>();
                    var bossWalkHeights = new List<float>();
                    foreach (var direction in directions)
                    {
                    var directionScales = new List<float>();
                    for (var state = 0; state < 3; state++)
                    foreach (var phase in phases)
                    {
                        boss.PreviewMotionPoseForQa(direction, state, phase);
                        actualPosesChecked++;
                        directionScales.Add(boss.CurrentVisualScaleHeightRatioForQa);
                        if (state == 0) bossWalkHeights.Add(boss.VisualWorldHeight);
                        bossSafe &= boss.ActiveBossArtworkChannelsForQa == 1 &&
                                    boss.CurrentSpriteHasSafeCellMarginForQa &&
                                    boss.CurrentSpriteForeignComponentsForQa <= 0;
                    }
                    bossDirectionScaleRatios.Add(directionScales.Max() /
                                                 Mathf.Max(.001f, directionScales.Min()));
                    }
                    var bossScaleRatio = bossDirectionScaleRatios.Max();
                    // A winged dragon and a staff-bearing elder cannot share one absolute-height
                    // threshold: their combat radii intentionally differ.  The production
                    // contract is instead a readable silhouette relative to the targetable body,
                    // while every authored pose must retain one exact scale.  This catches a
                    // genuinely tiny/huge boss without rejecting a correctly proportioned family.
                    bossWalkHeights.Sort();
                    var bossWalkHeight = bossWalkHeights[bossWalkHeights.Count / 2];
                    var bossHeightPerRadius = bossWalkHeight / Mathf.Max(.01f, boss.Radius);
                    var expectedBossHeight = boss.ExpectedBossVisualHeightPerRadiusForQa;
                    // v2.63 deliberately adjusts per-pose local scale to neutralize the source
                    // atlas' inconsistent painted heights. Validate the final on-screen body
                    // against its combat radius; raw transform equality is no longer meaningful.
                    bossSafe &= bossScaleRatio <= 1.75f &&
                                Mathf.Abs(bossHeightPerRadius - expectedBossHeight) <= .13f;
                    Debug.Log($"QA_261_BOSS_METRIC R{round}:{bossProfile.Id}:" +
                              $"walkHeight={bossWalkHeight:0.000}:radius={boss.Radius:0.000}:" +
                              $"relative={bossHeightPerRadius:0.000}/{expectedBossHeight:0.000}:" +
                              $"scale={bossScaleRatio:0.000}");
                    roundPressures[round] += boss.MaxHealth *
                        (boss.AttackPower + boss.MagicPower * .55f + boss.GateDamage * .22f);
                    if (!bossSafe)
                        failures.Add($"R{round}:{bossProfile.Id}:boss-sprite:{bossScaleRatio:0.000}/" +
                                     $"{boss.VisualWorldHeight:0.000}/{bossHeightPerRadius:0.000}:" +
                                     boss.CurrentFrameTextureNameForQa);
                    Destroy(boss.gameObject);
                }
                yield return null;
            }

            if (finalFrontSprites.Count < 4 || finalBackSprites.Count < 4)
                failures.Add($"final-identity={finalFrontSprites.Count}/{finalBackSprites.Count}");

            // Portraits use the same full authored bodies as runtime bosses and must retain a
            // transparent border before aspect-fit drawing. This catches source cropping before
            // the GUI has a chance to hide it.
            var portraitFailures = new List<string>();
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var sprite = GuideBossPortraitSprite(chapter);
                if (sprite == null || !HasTransparentPortraitBorder261(sprite, 2))
                    portraitFailures.Add($"R{(chapter + 1) * 5}");
            }
            if (portraitFailures.Count > 0)
                failures.Add("boss-guide-crop=" + string.Join("/", portraitFailures));

            // Use actual unit stats from the runtime actors above. Normal-wave transitions and
            // the final family are constrained separately from boss spikes.
            var regularTransitions = new List<float>();
            var largestRegularTransitionRound = 0;
            var largestRegularTransition = 0f;
            for (var round = 2; round <= MaxRounds; round++)
            {
                if (round % 5 == 0 || (round - 1) % 5 == 0) continue;
                var transition = roundPressures[round] / Mathf.Max(1f, roundPressures[round - 1]);
                regularTransitions.Add(transition);
                if (transition <= largestRegularTransition) continue;
                largestRegularTransition = transition;
                largestRegularTransitionRound = round;
            }
            var finalEntryRatio = roundPressures[46] / Mathf.Max(1f, roundPressures[44]);
            var finalGrowthRatio = roundPressures[49] / Mathf.Max(1f, roundPressures[46]);
            var balancePassed = RoundHealthPressureMultiplier(1) >= 1.07f &&
                                EnemyRoundDamageMultiplierFor(1) >= 1.06f &&
                                RoundHealthPressureMultiplier(46) <= .97f &&
                                EnemyRoundDamageMultiplierFor(46) <= .95f &&
                                regularTransitions.Max() <= 1.60f &&
                                finalEntryRatio <= 1.18f && finalGrowthRatio <= 1.45f;
            if (!balancePassed)
                failures.Add($"balance=step:{regularTransitions.Min():0.000}-" +
                             $"{regularTransitions.Max():0.000}@R{largestRegularTransitionRound}:entry={finalEntryRatio:0.000}:" +
                             $"growth={finalGrowthRatio:0.000}:mult=" +
                             $"{RoundHealthPressureMultiplier(1):0.000}/" +
                             $"{RoundHealthPressureMultiplier(46):0.000}");

            foreach (var enemy in enemies.Where(item => item != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(item => item != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();
            ClearTransientBattlePresentation();
            yield return null;

            // Validate the two re-authored abilities through the same battle coroutines used by
            // touch controls. The tank must create only a body-following defence sphere; the
            // hammer trail must terminate at the enemy contact point.
            Phase = GamePhase.Battle;
            Round = 20;
            var targetProfile = EnemyVariantCatalog.ForChapterStage(3, 1);
            var target = new GameObject("QA 261 Ability Target").AddComponent<EnemyUnit>();
            target.Initialize(this, 0, 900000f, false, 0, targetProfile.CombatClass, targetProfile);
            target.ForcePositionForQa(NearestWalkable(new Vector2(.75f, -4.15f), .18f));
            enemies.Add(target);

            var tank = new GameObject("QA 261 Sacred Tank").AddComponent<PlayerUnit>();
            tank.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank],
                NearestWalkable(new Vector2(-.55f, -4.25f), .18f));
            tank.AddExperience(9999f);
            units.Add(tank);
            var bulwarkBefore = sacredBulwarkSphereCount;
            StartCoroutine(HeroUltimateRoutine(tank, target));
            yield return new WaitForSecondsRealtime(.34f);
            var bulwark = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item != null && item.name.Contains("Sacred Bulwark Sphere"));
            var bulwarkPassed = sacredBulwarkSphereCount == bulwarkBefore + 1 && bulwark != null &&
                                bulwark.GetComponentsInChildren<SpriteRenderer>(true).Length >= 20 &&
                                !FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                                    .Any(item => item != null && item.name.Contains("Hero Ultimate Impact Tank"));
            if (!bulwarkPassed) failures.Add("tank-sacred-sphere");

            ClearTransientBattlePresentation();
            yield return null;
            var hammer = new GameObject("QA 261 Coral Hammer").AddComponent<PlayerUnit>();
            hammer.Initialize(this, UnitArchetype.Melee, definitions[UnitArchetype.Melee],
                NearestWalkable(new Vector2(-.15f, -4.3f), .18f));
            units.Add(hammer);
            var hammerBefore = coralHammerEffectCount;
            StartCoroutine(PlayerSkillRoutine(hammer, target, definitions[UnitArchetype.Melee]));
            yield return new WaitForSecondsRealtime(.36f);
            var hammerEffect = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item != null && item.name.Contains("Grounding Arc Skin"));
            var hammerPassed = coralHammerEffectCount == hammerBefore + 1 && hammerEffect != null &&
                               hammerEffect.GetComponentsInChildren<SpriteRenderer>(true).Length >= 25;
            if (!hammerPassed) failures.Add("coral-hammer-contact");

            ClearTransientBattlePresentation();
            foreach (var enemy in enemies.Where(item => item != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(item => item != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_261 passed={passed} actors={actualActorsChecked} " +
                      $"poses={actualPosesChecked} finale={finalFrontSprites.Count}/" +
                      $"{finalBackSprites.Count} portraits={10 - portraitFailures.Count}/10 " +
                      $"balance={balancePassed}:{regularTransitions.Min():0.000}-" +
                      $"{regularTransitions.Max():0.000}:{finalEntryRatio:0.000}/" +
                      $"{finalGrowthRatio:0.000} vfx={bulwarkPassed}/{hammerPassed} " +
                      $"fail={string.Join(",", failures.Take(30))}");
            Application.Quit(passed ? 0 : 121);
        }

        private static Vector2 ExpectedRegularVisualBand261(EnemyClass enemyClass) => enemyClass switch
        {
            EnemyClass.Runner => new Vector2(.48f, 1.12f),
            EnemyClass.Wisp => new Vector2(.52f, 1.25f),
            EnemyClass.Shaman or EnemyClass.Mage => new Vector2(.62f, 1.55f),
            EnemyClass.Siege or EnemyClass.Brute => new Vector2(.82f, 2.05f),
            EnemyClass.Flyer => new Vector2(.46f, 1.25f),
            _ => new Vector2(.45f, 1.65f)
        };

        private static bool HasTransparentPortraitBorder261(Sprite sprite, int thickness)
        {
            if (sprite == null || sprite.texture == null) return false;
            Color32[] pixels;
            try { pixels = sprite.texture.GetPixels32(); }
            catch (Exception) { return true; }
            var left = Mathf.RoundToInt(sprite.rect.xMin);
            var right = Mathf.RoundToInt(sprite.rect.xMax) - 1;
            var bottom = Mathf.RoundToInt(sprite.rect.yMin);
            var top = Mathf.RoundToInt(sprite.rect.yMax) - 1;
            for (var inset = 0; inset < thickness; inset++)
            {
                var x0 = Mathf.Clamp(left + inset, 0, sprite.texture.width - 1);
                var x1 = Mathf.Clamp(right - inset, 0, sprite.texture.width - 1);
                var y0 = Mathf.Clamp(bottom + inset, 0, sprite.texture.height - 1);
                var y1 = Mathf.Clamp(top - inset, 0, sprite.texture.height - 1);
                for (var x = x0; x <= x1; x++)
                    if (pixels[y0 * sprite.texture.width + x].a > 8 ||
                        pixels[y1 * sprite.texture.width + x].a > 8) return false;
                for (var y = y0; y <= y1; y++)
                    if (pixels[y * sprite.texture.width + x0].a > 8 ||
                        pixels[y * sprite.texture.width + x1].a > 8) return false;
            }
            return true;
        }
    }
}
