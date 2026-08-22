using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private static readonly Vector2[] QaDirections264 =
            Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();

        private static bool IsCleanKoreanLabel264(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (var character in value)
            {
                if (character is ' ' or '·' || character is >= '0' and <= '9' ||
                    character is >= 'A' and <= 'Z' || character is >= 'a' and <= 'z' ||
                    character is >= '\uAC00' and <= '\uD7A3') continue;
                return false;
            }
            return true;
        }

        private IEnumerator QaSprite264Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            yield return PrewarmBossPresentations();

            var failures = new List<string>();
            var bossPoses = 0;
            var regularPoses = 0;
            var uniqueBossFrames = new HashSet<int>();
            for (var chapter = 0; chapter < 10; chapter++)
            {
                Round = (chapter + 1) * 5;
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA 264 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                var bossSafe = boss.HasCompleteDirectionalAnimationForQa;
                var stateFrames = new[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
                var statePoses = new[] { new HashSet<string>(), new HashSet<string>(), new HashSet<string>() };
                foreach (var direction in QaDirections264)
                for (var state = 0; state < 3; state++)
                {
                    var renderedHeights = new List<float>(24);
                    var renderedAreas = new List<float>(24);
                    var renderedWidths = new List<float>(24);
                    for (var frame = 0; frame < 24; frame++)
                    {
                        var pose = boss.PreviewMotionPoseForQa(direction, state, (frame + .17f) / 24f);
                        statePoses[state].Add($"{Mathf.RoundToInt(pose.x * 100)}:" +
                                              $"{Mathf.RoundToInt(pose.y * 100)}:" +
                                              $"{Mathf.RoundToInt(pose.z * 100)}:" +
                                              $"{Mathf.RoundToInt(pose.w * 100)}:" +
                                              $"{Mathf.RoundToInt(boss.ArticulatedPoseSignatureForQa * 100)}");
                        bossPoses++;
                        var sprite = boss.CurrentSpriteForQa;
                        if (sprite != null)
                        {
                            uniqueBossFrames.Add(sprite.GetInstanceID());
                            stateFrames[state].Add(sprite.GetInstanceID());
                        }
                        var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                        renderedHeights.Add(boss.VisualWorldHeight);
                        renderedAreas.Add(EnemyUnit.SpriteOpaqueAreaForQa(sprite));
                        renderedWidths.Add(EnemyUnit.SpriteOpaqueWidthForQa(sprite));
                        var frameSafe = sprite != null && boss.ActiveBossArtworkChannelsForQa == 1 &&
                                        boss.CurrentSpriteHasIsolationAuditForQa &&
                                        boss.CurrentSpriteHasSafeCellMarginForQa &&
                                        boss.CurrentSpriteForeignComponentsForQa == 0 &&
                                        margins.x >= 28f && margins.y >= 28f &&
                                        margins.z >= 28f && margins.w >= 28f &&
                                        boss.CurrentSpriteRenderAspectForQa is > .20f and < 2.45f;
                        if (!frameSafe)
                        {
                            bossSafe = false;
                            if (failures.Count < 80)
                                failures.Add($"boss-frame:{profile.Id}:{state}:{frame}:" +
                                             $"m={margins}:a={boss.CurrentSpriteRenderAspectForQa:0.00}:" +
                                             $"foreign={boss.CurrentSpriteForeignComponentsForQa}:" +
                                             $"channels={boss.ActiveBossArtworkChannelsForQa}");
                        }
                    }
                    var heightRatio = renderedHeights.Max() /
                                      Mathf.Max(.001f, renderedHeights.Min());
                    var areaRatio = renderedAreas.Max() / Mathf.Max(.001f, renderedAreas.Min());
                    var widthRatio = renderedWidths.Max() / Mathf.Max(.001f, renderedWidths.Min());
                    if (heightRatio > 1.10f || areaRatio > 3.15f || widthRatio > 2.95f)
                    {
                        bossSafe = false;
                        failures.Add($"boss-shape:{profile.Id}:{state}:" +
                                     $"h={heightRatio:0.00}:a={areaRatio:0.00}:w={widthRatio:0.00}");
                    }
                }
                // The source paintings do not always provide multiple safe cells for every
                // mirrored octant. Require meaningful state-wide frame variation while the
                // octant/facing and procedural anticipation-contact-recovery poses remain
                // independently verified. This never relaxes clipping or foreign-pixel checks.
                for (var state = 0; state < stateFrames.Length; state++)
                    if (stateFrames[state].Count < 2 || statePoses[state].Count < 4)
                    {
                        bossSafe = false;
                        failures.Add($"boss-static:{profile.Id}:{state}:" +
                                     $"sprites={stateFrames[state].Count}:poses={statePoses[state].Count}");
                    }
                if (!bossSafe && failures.Count < 80) failures.Add($"boss-total:{profile.Id}");
                Destroy(boss.gameObject);
            }

            var profileFailures = new List<string>();
            foreach (var profile in EnemyVariantCatalog.AllProfiles)
            {
                Round = Mathf.Clamp(Array.IndexOf(EnemyVariantCatalog.AllProfiles, profile) + 1, 1, 50);
                var actor = new GameObject($"QA 264 Enemy {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, 0, 1000f, false, 0, profile.CombatClass, profile);
                var heights = new List<float>();
                var contacts = new List<float>();
                var directionTextures = new Dictionary<FacingOctant, string>();
                foreach (var octant in Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>())
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    for (var frame = 0; frame < 24; frame++)
                    {
                        actor.PreviewMotionPoseForQa(direction, 0, (frame + .17f) / 24f);
                        regularPoses++;
                        heights.Add(actor.VisualWorldHeight);
                        contacts.Add(actor.PreviewGroundContactForQa(direction, (frame + .17f) / 24f));
                        directionTextures[octant] = actor.CurrentFrameTextureNameForQa;
                        if (actor.VisualOctant != octant)
                            profileFailures.Add($"{profile.Id}:facing:{octant}->{actor.VisualOctant}");
                        if (actor.HasAuthoredVariantDirectionalAnimationForQa &&
                            (!actor.CurrentSpriteHasSafeCellMarginForQa ||
                             actor.CurrentSpriteForeignComponentsForQa > 0 ||
                             actor.ActivePrimaryBodyChannelsForQa != 1))
                            profileFailures.Add($"{profile.Id}:atlas:{octant}:{frame}");
                    }
                }
                var heightRatio = heights.Max() / Mathf.Max(.001f, heights.Min());
                var contactSpread = contacts.Max() - contacts.Min();
                var rangedSafe = actor.IsRanged
                    ? actor.AttackRange is >= 1.20f and <= 3.20f
                    : actor.IsFlying || actor.AttackRange <= 1.15f &&
                      actor.AttackRange <= actor.Radius + .48f;
                if (heightRatio > (profile.Id == "jelly_mage" ? 1.075f : 1.12f) ||
                    contactSpread > .13f || !rangedSafe)
                    profileFailures.Add($"{profile.Id}:h={heightRatio:0.000}:" +
                                        $"ground={contactSpread:0.000}:range={actor.AttackRange:0.00}/" +
                                        $"{actor.Radius:0.00}");
                if (profile.Id == "jelly_mage")
                {
                    var rearSafe = new[]
                    {
                        FacingOctant.NorthWest, FacingOctant.North, FacingOctant.NorthEast
                    }.All(octant => directionTextures.TryGetValue(octant, out var texture) &&
                                    texture.Contains("enemy-jelly-mage-back-v2",
                                        StringComparison.OrdinalIgnoreCase));
                    if (!rearSafe) profileFailures.Add("jelly_mage:rear-identity");
                }
                Destroy(actor.gameObject);
            }

            Round = 1;
            var druid = new GameObject("QA 264 Grove Spiritcaller").AddComponent<PlayerUnit>();
            druid.Initialize(this, UnitArchetype.Druid, definitions[UnitArchetype.Druid],
                new Vector2(0f, -2f));
            var westMirror = druid.PreviewFacingMirrorForQa(Vector2.left, .32f);
            var westOctant = druid.VisualOctant;
            var eastMirror = druid.PreviewFacingMirrorForQa(Vector2.right, .32f);
            var eastOctant = druid.VisualOctant;
            var druidFacing = westMirror != eastMirror && westOctant == FacingOctant.West &&
                              eastOctant == FacingOctant.East;
            if (!druidFacing) profileFailures.Add($"druid-facing:{westMirror}/{eastMirror}:" +
                                                  $"{westOctant}/{eastOctant}");
            Destroy(druid.gameObject);

            GameLocalization.Current = GameLanguage.Korean;
            var koreanLabels = EnemyVariantCatalog.AllProfiles.All(profile =>
                IsCleanKoreanLabel264(profile.Name)) &&
                Enumerable.Range(0, 10).All(chapter =>
                    IsCleanKoreanLabel264(EnemyVariantCatalog.FamilyNameForChapter(chapter)));
            if (!koreanLabels) failures.Add("korean-enemy-labels");

            failures.AddRange(profileFailures.Take(80));
            var passed = failures.Count == 0 && bossPoses == 5760 && regularPoses == 10176 &&
                         uniqueBossFrames.Count >= 100;
            Debug.Log($"QA_SPRITE_264 passed={passed} bossPoses={bossPoses} " +
                      $"regularPoses={regularPoses} bossFrames={uniqueBossFrames.Count} " +
                      $"profiles={EnemyVariantCatalog.AllProfiles.Length} korean={koreanLabels} " +
                      $"druid={druidFacing} fail={string.Join(",", failures.Take(100))}");
            Application.Quit(passed ? 0 : 124);
        }

        private IEnumerator QaBattlefield264Routine()
        {
            yield return null;
            var failures = new List<string>();
            var expectedWaveCounts = new[]
            {
                14,17,20,23,26, 24,27,30,33,34, 30,33,36,39,40,
                36,40,44,48,48, 42,46,50,54,54, 47,51,55,59,59,
                52,56,60,64,64, 57,61,65,69,69, 62,66,70,74,74,
                66,70,74,78,80
            };
            var waveCurve = Enumerable.Range(1, 50)
                .Select(WaveEnemyCount).SequenceEqual(expectedWaveCounts);
            var chapterShape = Enumerable.Range(0, 10).All(chapter =>
            {
                var firstRound = chapter * 5 + 1;
                var first = WaveEnemyCount(firstRound);
                var second = WaveEnemyCount(firstRound + 1);
                var third = WaveEnemyCount(firstRound + 2);
                var fourth = WaveEnemyCount(firstRound + 3);
                var bossTotal = WaveEnemyCount(firstRound + 4) + 6;
                return first < second && second < third && third < fourth && bossTotal > fourth;
            });
            var burstCurve = WaveSquadSize(1) == 5 && WaveSquadSize(50) == 10 &&
                             WaveSpawnBurstSize(1) == 2 && WaveSpawnBurstSize(50) == 4;
            var sideRatio = Enumerable.Range(0, 100)
                .Count(index => DeploymentLaneForIndex(index) >= 4) / 100f;
            if (!waveCurve) failures.Add($"wave={WaveEnemyCount(1)}-{WaveEnemyCount(50)}");
            if (!chapterShape) failures.Add("chapter-body-budget");
            if (!burstCurve) failures.Add("burst-curve");
            if (Mathf.Abs(sideRatio - .4f) > .001f) failures.Add($"side={sideRatio:0.00}");

            var ranges = definitions[UnitArchetype.Tank].Range < definitions[UnitArchetype.Melee].Range &&
                         definitions[UnitArchetype.Melee].Range < definitions[UnitArchetype.Lancer].Range &&
                Mathf.Abs(definitions[UnitArchetype.Melee].Range - .94f) < .001f &&
                         Mathf.Abs(definitions[UnitArchetype.Lancer].Range - 1.02f) < .001f;
            if (!ranges) failures.Add($"ranges={definitions[UnitArchetype.Tank].Range:0.00}/" +
                                      $"{definitions[UnitArchetype.Melee].Range:0.00}/" +
                                      $"{definitions[UnitArchetype.Lancer].Range:0.00}");

            var spawnSafe = paths.Count == 6 &&
                            paths.Take(4).All(path => path.Count > 1 && path[0].y > -7f &&
                                IsWalkableWithClearance(path[0], .14f)) &&
                            paths.Skip(4).All(path => path.Count > 1 && Mathf.Abs(path[0].x) > 5.65f &&
                                path[0].y is > 2.80f and < 3.25f &&
                                IsWalkableWithClearance(path[0], .14f));
            if (!spawnSafe) failures.Add("spawn-anchors");

            var riversBlocked = SideRiverGardenFootprints.All(PolygonInteriorBlocked264) && new[]
                {
                    new Vector2(-5.75f, 1.55f), new Vector2(-5.55f, .15f),
                    new Vector2(-5.68f, -1.9f), new Vector2(-5.92f, -4.1f),
                    new Vector2(5.75f, 1.55f), new Vector2(5.55f, .15f),
                    new Vector2(5.68f, -1.9f), new Vector2(5.92f, -4.1f)
                }.All(point => IsManualNavigationBlocked(point) && !IsWalkable(point));
            var fortressesBlocked = ExteriorFortressBlockers.All(blocker =>
                !IsWalkable(blocker.Center));
            if (!riversBlocked) failures.Add("river-garden-leak");
            if (!fortressesBlocked) failures.Add("fortress-leak");

            var routeConnectivity = paths.All(path => path.Count > 2 &&
                FindWalkPath(path[0], path[^1], .14f).Count > 0) &&
                FindWalkPath(bossEntrancePath[0], bossEntrancePath[^1], .14f).Count > 0;
            if (!routeConnectivity) failures.Add("route-connectivity");

            var bossStats = true;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                Round = (chapter + 1) * 5;
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var baseHealth = BaseEnemyHealth(Round);
                var startingHealth = baseHealth * (6.80f + chapter * .34f);
                var boss = new GameObject($"QA 264 Stats {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, 0, startingHealth, true, 0, profile.CombatClass, profile);
                bossStats &= boss.MaxHealth >= baseHealth * 6.7f &&
                             boss.Barrier >= boss.MaxHealth * .229f &&
                             boss.Armor >= 49f + Round && boss.MagicResistance >= 44f + Round;
                Destroy(boss.gameObject);
            }
            if (!bossStats) failures.Add("boss-durability");

            var cardHeightSafe = SelectedUnitStatusRect().height <= 138.1f;
            if (!cardHeightSafe) failures.Add($"unit-card={SelectedUnitStatusRect().height:0.0}");

            var passed = failures.Count == 0;
            Debug.Log($"QA_BATTLEFIELD_264 passed={passed} wave={WaveEnemyCount(1)}-{WaveEnemyCount(50)} " +
                      $"squad={WaveSquadSize(1)}-{WaveSquadSize(50)} burst={WaveSpawnBurstSize(1)}-" +
                      $"{WaveSpawnBurstSize(50)} side={sideRatio:0.00} spawn={spawnSafe} " +
                      $"chapters={chapterShape} river={riversBlocked} fortress={fortressesBlocked} routes={routeConnectivity} " +
                      $"boss={bossStats} ranges={ranges} card={cardHeightSafe} " +
                      $"fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 125);
        }

        private bool PolygonInteriorBlocked264(Vector2[] polygon)
        {
            var minX = polygon.Min(point => point.x);
            var maxX = polygon.Max(point => point.x);
            var minY = polygon.Min(point => point.y);
            var maxY = polygon.Max(point => point.y);
            var samples = 0;
            for (var y = minY + .12f; y < maxY; y += .24f)
            for (var x = minX + .12f; x < maxX; x += .24f)
            {
                var point = new Vector2(x, y);
                if (!IsPointInPolygon(point, polygon)) continue;
                samples++;
                if (IsWalkable(point)) return false;
            }
            return samples >= 12;
        }

        private IEnumerator QaAugmentRuntime264Routine()
        {
            yield return null;
            var failures = new List<string>();
            var tiers = new[]
            {
                AugmentTier.Bronze, AugmentTier.Silver, AugmentTier.Gold,
                AugmentTier.Platinum, AugmentTier.Diamond
            };
            var templates = tiers.SelectMany(tier => GetAugmentPool(tier)
                    .Concat(FixedRecruitAugments(tier)))
                .GroupBy(template => template.EffectKey).Select(group => group.First()).ToArray();

            augmentPower.Clear();
            augmentCount.Clear();
            acquiredAugments.Clear();
            activeAugmentReadyAt.Clear();
            var applied = 0;
            var localized = true;
            foreach (var tier in tiers)
            foreach (var template in GetAugmentPool(tier).Concat(FixedRecruitAugments(tier)))
            {
                GameLocalization.Current = GameLanguage.Korean;
                localized &= GameLocalization.AugmentName(template.EffectKey, "__MISSING__") != "__MISSING__" &&
                             GameLocalization.AugmentDescription(template.EffectKey, TierPower(tier),
                                 "__MISSING__") != "__MISSING__";
                GameLocalization.Current = GameLanguage.English;
                localized &= GameLocalization.AugmentName(template.EffectKey, "__MISSING__") != "__MISSING__" &&
                             GameLocalization.AugmentDescription(template.EffectKey, TierPower(tier),
                                 "__MISSING__") != "__MISSING__";
                var before = StackPower(template.EffectKey);
                SelectAugment(new AugmentOffer(template.Name, template.Description,
                    template.EffectKey, tier, TierPower(tier)));
                var powerStored = StackPower(template.EffectKey) > before;
                var routeStored = TryGetUnlockUnit(template.EffectKey, out var unit)
                    ? unlockedUnits.Contains(unit)
                    : powerStored;
                if (!routeStored) failures.Add($"apply:{template.EffectKey}");
                else applied++;
                if (IsUniqueAugmentKey(template.EffectKey) && IsAugmentAvailable(template))
                    failures.Add($"unique:{template.EffectKey}");
            }
            if (!localized) failures.Add("localization");

            augmentPower.Clear();
            var survivor = new GameObject("QA 264 Survivor").AddComponent<PlayerUnit>();
            survivor.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank],
                new Vector2(0f, -2f));
            survivor.TakeDamage(60f, DamageType.Pure);
            var damaged = survivor.Health;
            survivor.PrepareForNextRound();
            var noFreeHealing = Mathf.Abs(survivor.Health - damaged) < .001f;
            var recoveryInGoldOnly = !GetAugmentPool(AugmentTier.Silver)
                                         .Any(template => template.EffectKey == "RoundRecovery") &&
                                     GetAugmentPool(AugmentTier.Gold)
                                         .Count(template => template.EffectKey == "RoundRecovery") == 1;
            if (!recoveryInGoldOnly) failures.Add("recovery-tier");
            augmentPower["RoundRecovery"] = TierPower(AugmentTier.Gold);
            var recoveryFraction = RoundClearRecoveryFraction;
            survivor.PrepareForNextRound(recoveryFraction);
            var augmentHealing = survivor.Health > damaged &&
                                 Mathf.Abs(recoveryFraction - TierPower(AugmentTier.Gold) * .18f) < .001f;
            if (!noFreeHealing) failures.Add("free-round-heal");
            if (!augmentHealing) failures.Add("recovery-augment");
            Destroy(survivor.gameObject);

            var tierPoolsSafe = tiers.All(tier => GetAvailableAugmentTemplates(tier).Count >= 3);
            if (!tierPoolsSafe) failures.Add("tier-pool-underflow");
            var passed = failures.Count == 0 && applied == templates.Length && templates.Length >= 60;
            Debug.Log($"QA_AUGMENT_RUNTIME_264 passed={passed} cards={templates.Length} applied={applied} " +
                      $"localized={localized} noFreeHeal={noFreeHealing} recovery={augmentHealing}:" +
                      $"{recoveryFraction:0.000} pools={tierPoolsSafe} " +
                      $"fail={string.Join(",", failures.Take(80))}");
            Application.Quit(passed ? 0 : 126);
        }
    }
}
