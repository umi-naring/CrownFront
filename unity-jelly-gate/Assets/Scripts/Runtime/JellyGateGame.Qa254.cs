using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaBossArcherGoogle254Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Battle;
            Round = 5;
            var failures = new List<string>();

            // Load every source atlas first. This must produce 895 independently audited cells:
            // 10 bosses x 5 directions x (6 walk + 6 skill), plus nine 6-column and one
            // 5-column attack atlas.
            yield return PrewarmBossPresentations();
            if (LoadedBossDirectionalCountForQa != 10)
                failures.Add($"boss-cache:{LoadedBossDirectionalCountForQa}");
            if (BossFrameIsolationCheckCountForQa != 895)
                failures.Add($"boss-cell-count:{BossFrameIsolationCheckCountForQa}");
            if (BossFrameFallbackRepairCountForQa != 1)
                failures.Add($"boss-source-repairs:{BossFrameFallbackRepairCountForQa}");
            foreach (var failure in BossFrameIsolationFailuresForQa.Take(12))
                failures.Add($"boss-isolation:{failure}:{BossFrameIsolationDetailForQa(failure)}");

            var octants = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>().ToArray();
            var bossFrameSamples = 0;
            var bossPoseSamples = 0;
            var bossPortraits = new HashSet<int>();
            var worstGroundDrift = 0f;
            var worstPoseHeightRatio = 0f;
            var worstFormationOverlap = 0f;
            var worstSummonOverlap = 0f;

            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var set = GetBossDirectionalAnimation(profile);
                if (set == null || !set.SupportsEightDirections)
                {
                    failures.Add($"boss-set:{profile.Id}");
                    continue;
                }

                foreach (var octant in octants)
                {
                    var frames = set.FramesFor(octant);
                    if (frames.Length != 72)
                    {
                        failures.Add($"boss-timeline:{profile.Id}:{octant}:{frames.Length}");
                        continue;
                    }
                    for (var state = 0; state < 3; state++)
                    {
                        var stateFrames = frames.Skip(state * 24).Take(24).ToArray();
                        bossFrameSamples += stateFrames.Length;
                        var owned = stateFrames.All(sprite => sprite != null && sprite.texture != null &&
                            sprite.texture.name.Contains($"boss-{profile.Id.Replace('_', '-')}") &&
                            sprite.texture.name.Contains("-isolated-r"));
                        var distinct = stateFrames.Where(sprite => sprite != null)
                            .Select(sprite => sprite.GetInstanceID()).Distinct().Count();
                        if (!owned || distinct < 4)
                            failures.Add($"boss-frame:{profile.Id}:{octant}:{state}:{distinct}");
                    }
                }

                var boss = new GameObject($"QA 254 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, 0, 12000f, true, 0, profile.CombatClass, profile);
                if (boss.ActivePrimaryBodyChannelsForQa != 1 || boss.ActiveBossArtworkChannelsForQa != 1 ||
                    !boss.HasAuthoredBossDirectionalAnimationForQa ||
                    !boss.HasCompleteDirectionalAnimationForQa || boss.HasWorldHealthBar)
                    failures.Add($"boss-channel:{profile.Id}:{boss.ActivePrimaryBodyChannelsForQa}/{boss.ActiveBossArtworkChannelsForQa}");
                var poseHeights = new List<float>();

                foreach (var octant in octants)
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    for (var state = 0; state < 3; state++)
                    for (var phase = 0; phase < 8; phase++)
                    {
                        var normalized = (phase + .35f) / 8f;
                        var pose = boss.PreviewMotionPoseForQa(direction, state, normalized);
                        bossPoseSamples++;
                        if (boss.ActivePrimaryBodyChannelsForQa != 1 || boss.ActiveBossArtworkChannelsForQa != 1)
                            failures.Add($"boss-pose-channel:{profile.Id}:{octant}:{state}:{phase}:" +
                                         $"{boss.ActivePrimaryBodyChannelsForQa}/{boss.ActiveBossArtworkChannelsForQa}");
                        if (!boss.CurrentSpriteUsesStrictAtlasCellForQa)
                            failures.Add($"boss-pose-cell:{profile.Id}:{octant}:{state}:{phase}");
                        // Atlas cells carry very different transparent padding, so their internal
                        // corrective scale is not a visual-size metric. Audit the final opaque
                        // world height that the player actually sees.
                        poseHeights.Add(boss.VisualWorldHeight);
                    }

                    if (!boss.IsFlying)
                    {
                        var contacts = Enumerable.Range(0, 12)
                            .Select(sample => boss.PreviewGroundContactForQa(direction, sample / 12f)).ToArray();
                        var drift = contacts.Max() - contacts.Min();
                        worstGroundDrift = Mathf.Max(worstGroundDrift, drift);
                        if (drift > boss.Radius * .38f)
                            failures.Add($"boss-ground:{profile.Id}:{octant}:{drift:0.000}");
                    }
                }

                var minimumPoseHeight = poseHeights.Where(value => value > .01f).DefaultIfEmpty(0f).Min();
                var maximumPoseHeight = poseHeights.DefaultIfEmpty(0f).Max();
                var poseHeightRatio = minimumPoseHeight > .01f
                    ? maximumPoseHeight / minimumPoseHeight
                    : float.MaxValue;
                worstPoseHeightRatio = Mathf.Max(worstPoseHeightRatio, poseHeightRatio);
                if (poseHeightRatio > 1.72f)
                    failures.Add($"boss-height:{profile.Id}:{poseHeightRatio:0.00}");

                var portrait = GuideBossPortraitSprite(chapter);
                if (portrait == null || portrait.texture == null ||
                    portrait.texture.name.Contains("directions") ||
                    portrait.texture.name.Contains("-isolated-r"))
                    failures.Add($"boss-portrait:{profile.Id}");
                else bossPortraits.Add(portrait.GetInstanceID());
                Destroy(boss.gameObject);

                ClearEnemiesForQa254();
                Phase = GamePhase.Battle;
                var escort = EnemyVariantCatalog.ForChapterStage(chapter, 3);
                SpawnBossFormation(profile, escort, 12000f, 1600f);
                var formation = enemies.Where(enemy => enemy != null && enemy.IsAlive).ToArray();
                if (formation.Length != 6 || formation.Count(enemy => enemy.IsBoss) != 1 ||
                    formation.Any(enemy => enemy.ActivePrimaryBodyChannelsForQa != 1 ||
                                           enemy.IsBoss && enemy.ActiveBossArtworkChannelsForQa != 1))
                    failures.Add($"boss-formation-count:{profile.Id}:{formation.Length}");
                var formationOverlap = WorstPairOverlap254(formation);
                worstFormationOverlap = Mathf.Max(worstFormationOverlap, formationOverlap);
                if (formationOverlap > .08f)
                    failures.Add($"boss-formation-overlap:{profile.Id}:{formationOverlap:0.000}");

                var formationBoss = formation.FirstOrDefault(enemy => enemy.IsBoss);
                if (formationBoss != null)
                {
                    Phase = GamePhase.Battle;
                    var before = formation.Length;
                    var spawned = SpawnBossMinions(formationBoss, EnemyClass.Runner, 4);
                    var summons = enemies.Where(enemy => enemy != null && enemy.IsAlive)
                        .Skip(before).ToArray();
                    var summonGroup = summons.Concat(new[] { formationBoss }).ToArray();
                    var summonOverlap = WorstPairOverlap254(summonGroup);
                    worstSummonOverlap = Mathf.Max(worstSummonOverlap, summonOverlap);
                    if (spawned != 4 || summons.Length != 4 || summonOverlap > .08f)
                        failures.Add($"boss-summon-overlap:{profile.Id}:{spawned}:{summonOverlap:0.000}");
                }
                ClearEnemiesForQa254();
                yield return null;
            }

            if (bossPortraits.Count != 10) failures.Add($"boss-portrait-distinct:{bossPortraits.Count}");
            if (bossFrameSamples != 5760) failures.Add($"boss-frame-samples:{bossFrameSamples}");
            if (bossPoseSamples != 1920) failures.Add($"boss-pose-samples:{bossPoseSamples}");
            var bossPortraitLayout = GuideBossPortraitRect(new Rect(0f, 0f, 640f, 220f));
            if (bossPortraitLayout.width < 122f || bossPortraitLayout.height < 108f)
                failures.Add("boss-portrait-layout");

            // The archer hero ultimate owns exactly one visible, embedded arrow per legal enemy.
            var archer = new GameObject("QA 254 Global Archer").AddComponent<PlayerUnit>();
            archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer],
                NearestWalkable(new Vector2(0f, -4.8f), .18f));
            archer.AddExperience(9999f);
            units.Add(archer);
            var arrowTargets = new List<EnemyUnit>();
            for (var i = 0; i < 7; i++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(0, i % 4);
                var target = new GameObject($"QA 254 Arrow Target {i}").AddComponent<EnemyUnit>();
                target.Initialize(this, i, 9000f, false, i % Mathf.Max(1, LaneCount),
                    profile.CombatClass, profile);
                target.ForcePositionForQa(NearestWalkable(new Vector2(-2.4f + i * .8f,
                    -3.8f + i % 2 * .48f), .18f));
                enemies.Add(target);
                arrowTargets.Add(target);
            }
            StartCoroutine(RoyalArrowRainRoutine(archer, definitions[UnitArchetype.Archer].Color));
            yield return new WaitForSecondsRealtime(.18f);
            var arrowActors = GameObject.Find("Royal Arrow Rain Global Field")?
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("Global Arrow ", StringComparison.Ordinal))
                .OrderBy(renderer => int.Parse(renderer.name.Substring("Global Arrow ".Length))).ToArray() ??
                Array.Empty<SpriteRenderer>();
            var arrowsGlobal = arrowActors.Length == arrowTargets.Count &&
                arrowActors.Select((arrow, index) => arrow.transform.parent != null &&
                    Mathf.Abs(arrow.transform.parent.position.x - arrowTargets[index].HitPoint.x) < .34f &&
                    arrow.transform.parent.position.y > arrowTargets[index].HitPoint.y + 1.1f).All(value => value);
            if (!arrowsGlobal) failures.Add($"archer-global:{arrowActors.Length}");
            yield return new WaitForSecondsRealtime(.48f);
            var embedded = arrowActors.Select((arrow, index) => arrow != null && arrow.transform.parent != null &&
                Vector2.Distance(arrow.transform.parent.position, arrowTargets[index].HitPoint) < .26f).All(value => value);
            if (!embedded) failures.Add("archer-embedded");
            ClearTransientBattlePresentation();
            ClearEnemiesForQa254();
            units.Remove(archer);
            Destroy(archer.gameObject);

            // Both QA entitlement directions must be deterministic and visible to the settings UI.
            monetization.GrantAllProductsForTesting();
            var productsUnlocked = monetization.AllProductsOwnedForTesting &&
                                   monetization.Products.All(product => monetization.IsOwned(product.Id));
            monetization.ResetAllProductsForTesting();
            var productsLocked = !monetization.AllProductsOwnedForTesting &&
                                 monetization.Products.All(product => !monetization.IsOwned(product.Id)) &&
                                 string.IsNullOrEmpty(monetization.EquippedCastle) &&
                                 string.IsNullOrEmpty(monetization.EquippedMenu);
            if (!productsUnlocked || !productsLocked) failures.Add("product-lock-toggle");

            // Legacy Play Games events must no longer open UI or alter commerce state.
            var purchaseStatusBeforeLegacyLoginEvent = monetization.PurchaseStatusMessage;
            monetization.OnMonetizationEvent(
                "{\"type\":\"legacy_login_event\",\"productId\":\"\",\"price\":\"\",\"message\":\"LEGACY\"}");
            monetization.OnMonetizationEvent(
                "{\"type\":\"legacy_login_success\",\"productId\":\"\",\"price\":\"\",\"message\":\"\"}");
            var legacyLoginEventsIgnored =
                monetization.PurchaseStatusMessage == purchaseStatusBeforeLegacyLoginEvent;
            if (!legacyLoginEventsIgnored) failures.Add("legacy-login-event-visible");

            var passed = failures.Count == 0;
            Debug.Log($"QA_BOSS_ARCHER_GOOGLE_254 passed={passed} " +
                      $"bosses={LoadedBossDirectionalCountForQa} cells={BossFrameIsolationCheckCountForQa}/895 " +
                      $"sourceRepairs={BossFrameFallbackRepairCountForQa}/1 " +
                      $"frames={bossFrameSamples}/5760 poses={bossPoseSamples}/1920 portraits={bossPortraits.Count}/10 " +
                      $"groundDrift={worstGroundDrift:0.000} heightRatio={worstPoseHeightRatio:0.00} " +
                      $"formationOverlap={worstFormationOverlap:0.000} " +
                      $"summonOverlap={worstSummonOverlap:0.000} arrows={arrowActors.Length}/7 " +
                      $"products={productsUnlocked}/{productsLocked} loginRemoved={legacyLoginEventsIgnored} " +
                      $"fail={string.Join(",", failures.Take(24))}");
            Application.Quit(passed ? 0 : 114);
        }

        private void ClearEnemiesForQa254()
        {
            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray())
                Destroy(enemy.gameObject);
            enemies.Clear();
            bossFormationSpawnCount = 0;
        }

        private static float WorstPairOverlap254(IReadOnlyList<EnemyUnit> actors)
        {
            var worst = 0f;
            for (var first = 0; first < actors.Count; first++)
            for (var second = first + 1; second < actors.Count; second++)
            {
                if (actors[first] == null || actors[second] == null) continue;
                var required = (actors[first].Radius + actors[second].Radius) * .72f;
                worst = Mathf.Max(worst, required -
                    Vector2.Distance(actors[first].Position, actors[second].Position));
            }
            return worst;
        }
    }
}
