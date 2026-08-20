using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaUltimateSpriteAudit256Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Battle;
            Round = 5;
            var failures = new List<string>();

            yield return PrewarmBossPresentations();
            var bossSprites = bossDirectionalAnimations.Values
                .SelectMany(AllDirectionalSprites256).Where(sprite => sprite != null)
                .Distinct().ToArray();
            var bossUnsafe = bossSprites.Where(sprite =>
            {
                var margin = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 10f || margin.y < 10f || margin.z < 10f || margin.w < 10f;
            }).ToArray();
            var sanitizedSourceRepairs = BossFrameFallbackRepairCountForQa >=
                                         BossFrameRejectedCompositeCountForQa &&
                                         BossFrameFallbackRepairCountForQa <= 895;
            if (bossDirectionalAnimations.Count != 10 ||
                BossFrameIsolationCheckCountForQa is < 894 or > 895 ||
                // Invalid authored cells are now replaced by a nearby clean action pose. The
                // final timeline therefore contains fewer unique Sprite instances by design;
                // it must still retain hundreds of authored poses with zero unsafe margins.
                bossSprites.Length is < 450 or > 895 || bossUnsafe.Length > 0 ||
                BossFrameIsolationFailuresForQa.Length > 0 ||
                !sanitizedSourceRepairs)
                failures.Add($"boss-frames:{bossDirectionalAnimations.Count}:{bossSprites.Length}:unsafe={bossUnsafe.Length}");
            if (BossFrameIsolationFailuresForQa.Length > 0)
                failures.Add($"boss-isolation:{BossFrameIsolationFailuresForQa.Length}:" +
                             string.Join("|", BossFrameIsolationFailuresForQa.Take(4)));

            var defaultSprites = unitDirectionalAnimations.Values.Concat(heroDirectionalAnimations.Values)
                .SelectMany(AllDirectionalSprites256).Where(sprite => sprite != null)
                .Distinct().ToArray();
            var skinSprites = authoredSkinAnimations.Values.SelectMany(AllDirectionalSprites256)
                .Where(sprite => sprite != null).Distinct().ToArray();
            var playerUnsafe = defaultSprites.Concat(skinSprites).Distinct().Where(sprite =>
            {
                var margin = PlayerUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 2f || margin.y < 2f || margin.z < 2f || margin.w < 2f;
            }).ToArray();
            var lancerSprites = defaultSprites.Concat(skinSprites).Distinct().Where(sprite =>
                    sprite.texture != null && sprite.texture.name.IndexOf("lancer-direction",
                        StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            var lancerUnsafe = lancerSprites.Where(sprite =>
            {
                var margin = PlayerUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 8f || margin.y < 6f || margin.z < 8f || margin.w < 6f;
            }).ToArray();
            if (unitDirectionalAnimations.Count != 10 || heroDirectionalAnimations.Count != 10 ||
                authoredSkinAnimations.Count != 40 || playerUnsafe.Length > 0 ||
                lancerSprites.Length == 0 || lancerUnsafe.Length > 0)
                failures.Add($"player-frames:sets={unitDirectionalAnimations.Count}/" +
                             $"{heroDirectionalAnimations.Count}/{authoredSkinAnimations.Count}:" +
                             $"sprites={defaultSprites.Length}/{skinSprites.Length}:" +
                             $"unsafe={playerUnsafe.Length}:lancer={lancerSprites.Length}/{lancerUnsafe.Length}:" +
                             string.Join("|", playerUnsafe.Take(8).Select(sprite =>
                             {
                                 var margin = PlayerUnit.SpriteOpaqueMarginsForQa(sprite);
                                 return $"{sprite.texture.name}[{margin.x:0},{margin.y:0}," +
                                        $"{margin.z:0},{margin.w:0}]";
                             })));

            var exclusionProfile = EnemyVariantCatalog.ForChapterStage(0, 4);
            var exclusionBoss = new GameObject("QA 256 Boss Exclusion").AddComponent<EnemyUnit>();
            exclusionBoss.Initialize(this, 0, 900000f, true, 0,
                exclusionProfile.CombatClass, exclusionProfile);
            exclusionBoss.ForcePositionForQa(NearestWalkable(new Vector2(0f, -3.7f), .18f));
            enemies.Add(exclusionBoss);
            RegisterBossSilhouetteExclusionForQa(exclusionBoss);
            var minionProfile = EnemyVariantCatalog.ForChapterStage(0, 0);
            var exclusionMinion = new GameObject("QA 256 Boss Exclusion Minion").AddComponent<EnemyUnit>();
            exclusionMinion.Initialize(this, 1, 9000f, false, 0,
                minionProfile.CombatClass, minionProfile);
            exclusionMinion.SetSummonedPosition(exclusionBoss.Position, 0);
            exclusionMinion.ForcePositionForQa(exclusionBoss.Position);
            enemies.Add(exclusionMinion);
            var excludedPosition = ResolveBossSilhouetteExclusion(exclusionMinion, exclusionBoss.Position);
            var requiredExclusion = exclusionBoss.Radius * 1.08f + exclusionMinion.Radius * .62f;
            var bossExclusionPassed = Vector2.Distance(excludedPosition, exclusionBoss.Position) >=
                                      requiredExclusion - .02f;
            if (!bossExclusionPassed) failures.Add("boss-live-minion-exclusion");
            enemies.Remove(exclusionBoss);
            enemies.Remove(exclusionMinion);
            Destroy(exclusionBoss.gameObject);
            Destroy(exclusionMinion.gameObject);
            RegisterBossSilhouetteExclusionForQa(null);

            var targetProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var target = new GameObject("QA 256 Ultimate Target").AddComponent<EnemyUnit>();
            target.Initialize(this, 0, 900000f, false, 0, targetProfile.CombatClass, targetProfile);
            target.ForcePositionForQa(NearestWalkable(new Vector2(.9f, -4.1f), .18f));
            enemies.Add(target);

            var tank = new GameObject("QA 256 Defensive Tank").AddComponent<PlayerUnit>();
            tank.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank],
                NearestWalkable(new Vector2(0f, -4.2f), .18f));
            tank.AddExperience(9999f);
            units.Add(tank);
            yield return new WaitForSecondsRealtime(1.35f);
            var targetHealth = target.Health;
            StartCoroutine(HeroUltimateRoutine(tank, target));
            yield return new WaitForSecondsRealtime(.42f);
            var tankOffense = target.Health < targetHealth - .01f ||
                              FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                                  .Any(effect => effect != null &&
                                      (effect.name == "Hero Ultimate Impact Tank" ||
                                       effect.name == "Tank Ultimate VFX Animation"));
            tank.ApplyArmorShred(99f, 5f);
            tank.ApplyResistanceCurse(99f, 5f);
            var tankDefense = tank.HasDefensiveStanceForQa &&
                              tank.DefensiveDamageReductionForQa >= .67f &&
                              tank.HasDefensiveDebuffImmunityForQa &&
                              tank.ActiveArmorShredForQa <= .01f &&
                              tank.ActiveResistanceCurseForQa <= .01f &&
                              FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                                  .Any(effect => effect != null &&
                                      effect.name.StartsWith("Sacred Bulwark Sphere Skin ",
                                          StringComparison.Ordinal) &&
                                      effect.GetComponentsInChildren<SpriteRenderer>(true).Length >= 20);
            if (tankOffense || !tankDefense)
                failures.Add($"tank-ultimate:offense={tankOffense}:defense={tankDefense}");
            ClearTransientBattlePresentation();

            var archer = new GameObject("QA 256 Arrow Only Archer").AddComponent<PlayerUnit>();
            archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer],
                NearestWalkable(new Vector2(-.9f, -4.2f), .18f));
            archer.AddExperience(9999f);
            units.Add(archer);
            yield return new WaitForSecondsRealtime(1.35f);
            var extraTargets = new List<EnemyUnit>();
            for (var index = 0; index < 4; index++)
            {
                var enemy = new GameObject($"QA 256 Arrow Target {index}").AddComponent<EnemyUnit>();
                enemy.Initialize(this, index + 1, 900000f, false, 0,
                    targetProfile.CombatClass, targetProfile);
                enemy.ForcePositionForQa(NearestWalkable(new Vector2(-1.6f + index * .8f,
                    -3.25f + index % 2 * .32f), .18f));
                enemies.Add(enemy);
                extraTargets.Add(enemy);
            }
            StartCoroutine(HeroUltimateRoutine(archer, target));
            yield return new WaitForSecondsRealtime(.40f);
            var arrowField = GameObject.Find("Royal Arrow Rain Global Field");
            var arrowRenderers = arrowField == null ? Array.Empty<SpriteRenderer>() :
                arrowField.GetComponentsInChildren<SpriteRenderer>(true);
            var expectedArrowTargets = enemies.Count;
            var arrows = arrowField == null ? 0 : arrowField.transform.Cast<Transform>()
                .Count(item => item.name.StartsWith("Royal Arrow Actor ", StringComparison.Ordinal));
            var completeArrows = arrowRenderers.Count(renderer =>
                renderer.name.StartsWith("Royal Arrow Shaft ", StringComparison.Ordinal)) == expectedArrowTargets &&
                arrowRenderers.Count(renderer =>
                    renderer.name.StartsWith("Royal Arrowhead ", StringComparison.Ordinal)) == expectedArrowTargets &&
                arrowRenderers.Count(renderer =>
                    renderer.name.StartsWith("Royal Fletching ", StringComparison.Ordinal)) == expectedArrowTargets;
            var forbiddenArrowBursts = arrowRenderers.Count(renderer =>
                renderer.name.Contains("Contact", StringComparison.Ordinal) ||
                renderer.name.Contains("Impact", StringComparison.Ordinal) ||
                renderer.name.Contains("Beam", StringComparison.Ordinal) ||
                renderer.name.Contains("Explosion", StringComparison.Ordinal));
            var commonArcherImpact = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                .Any(effect => effect != null &&
                    (effect.name == "Hero Ultimate Impact Archer" ||
                     effect.name == "Hero Ultimate Spectacle"));
            if (arrows != expectedArrowTargets || !completeArrows ||
                forbiddenArrowBursts != 0 || commonArcherImpact)
                failures.Add($"archer-arrow-only:{arrows}/{expectedArrowTargets}:" +
                             $"burst={forbiddenArrowBursts}:common={commonArcherImpact}");

            ClearTransientBattlePresentation();
            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray())
                Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(unit => unit != null).ToArray())
                Destroy(unit.gameObject);
            units.Clear();

            var passed = failures.Count == 0;
            Debug.Log($"QA_ULTIMATE_SPRITE_AUDIT_256 passed={passed} " +
                      $"bosses={bossDirectionalAnimations.Count}:sprites={bossSprites.Length}:unsafe={bossUnsafe.Length} " +
                      $"playerSets={unitDirectionalAnimations.Count}/{heroDirectionalAnimations.Count}/" +
                      $"{authoredSkinAnimations.Count}:sprites={defaultSprites.Length}/{skinSprites.Length}:" +
                      $"unsafe={playerUnsafe.Length}:lancer={lancerSprites.Length}/{lancerUnsafe.Length} " +
                      $"bossExclusion={bossExclusionPassed} tank={tankDefense}/{!tankOffense} " +
                      $"archer={arrows}/{expectedArrowTargets}:" +
                      $"burst={forbiddenArrowBursts} fail={string.Join(",", failures.Take(20))}");
            Application.Quit(passed ? 0 : 116);
        }

        private static IEnumerable<Sprite> AllDirectionalSprites256(DirectionalAnimationSet set)
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
