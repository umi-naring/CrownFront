using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaSkinCombatVfx255Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Battle;
            Round = 5;
            var failures = new List<string>();
            var roster = new[]
            {
                UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
                UnitArchetype.AreaMage, UnitArchetype.SingleMage, UnitArchetype.Bombardier,
                UnitArchetype.Lancer, UnitArchetype.Druid, UnitArchetype.Musketeer,
                UnitArchetype.Oracle
            };
            var tiers = new[] { CombatVfxTier.Basic, CombatVfxTier.Skill, CombatVfxTier.Ultimate };
            var styleSamples = 0;
            var tierSamples = 0;
            var cueSamples = 0;
            var projectileSamples = 0;

            var targetProfile = EnemyVariantCatalog.ForChapterStage(0, 1);
            var target = new GameObject("QA 255 Skin VFX Target").AddComponent<EnemyUnit>();
            target.Initialize(this, 0, 900000f, false, 0, targetProfile.CombatClass, targetProfile);
            target.ForcePositionForQa(NearestWalkable(new Vector2(1.5f, -3.8f), .18f));
            enemies.Add(target);

            foreach (var archetype in roster)
            {
                var products = monetization.Products.Where(product => product.Category == ShopCategory.Unit &&
                                                                      product.TargetUnit == archetype)
                    .OrderBy(product => product.Id).ToArray();
                if (products.Length != 2)
                {
                    failures.Add($"skin-products:{archetype}:{products.Length}");
                    continue;
                }

                foreach (var product in products)
                {
                    monetization.GrantForQa(product.Id);
                    monetization.Equip(product);
                    var source = new GameObject($"QA 255 {archetype} {product.Id}").AddComponent<PlayerUnit>();
                    source.Initialize(this, archetype, definitions[archetype],
                        NearestWalkable(new Vector2(-1.5f, -4.5f), .18f));
                    units.Add(source);
                    var style = ResolveCombatSkinVfxStyle(source, archetype, definitions[archetype].Color);
                    styleSamples++;
                    if (!style.Skinned || style.Variant is < 1 or > 2 ||
                        Vector4.Distance(style.Primary, GetUnitSkinAccent(archetype)) > .01f ||
                        Vector4.Distance(style.Secondary, GetUnitSkinSecondary(archetype)) > .01f ||
                        Vector4.Distance(style.Primary, style.Secondary) < .035f ||
                        Vector4.Distance(style.Core, style.Primary) < .025f)
                        failures.Add($"skin-style:{archetype}:v{style.Variant}");

                    foreach (var tier in tiers)
                    {
                        SpawnCombatImpact(Vector2.zero, archetype, definitions[archetype].Color,
                            tier == CombatVfxTier.Basic ? .62f : tier == CombatVfxTier.Skill ? .88f : 1.12f,
                            Vector2.right, tier, source);
                        var root = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                            .LastOrDefault(effect => effect != null &&
                                effect.name == $"{archetype} {tier} VFX Animation");
                        var signatures = root == null ? 0 : root.GetComponentsInChildren<SpriteRenderer>(true)
                            .Count(renderer => renderer.name.StartsWith(
                                $"Skin VFX {tier} V{style.Variant} Signature ", StringComparison.Ordinal));
                        var minimum = tier == CombatVfxTier.Basic ? 3 : tier == CombatVfxTier.Skill ? 6 : 10;
                        if (signatures < minimum)
                            failures.Add($"tier-signature:{archetype}:v{style.Variant}:{tier}:{signatures}");
                        tierSamples++;
                    }

                    SpawnAttackCue(source.AttackOriginFor(target.Position), archetype,
                        definitions[archetype].Color, source);
                    var cue = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                        .LastOrDefault(effect => effect != null && effect.name == $"{archetype} Attack Cue");
                    if (cue == null || !cue.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer =>
                            renderer.name.StartsWith($"Skin {style.Variant} Windup Signature ",
                                StringComparison.Ordinal)))
                        failures.Add($"attack-cue:{archetype}:v{style.Variant}");
                    else cueSamples++;

                    if (archetype is not (UnitArchetype.Tank or UnitArchetype.Melee or UnitArchetype.Lancer))
                    {
                        var projectileRoot = TransientBattleEffect.Create(
                            $"QA 255 {archetype} Skin Projectile");
                        var projectile = projectileRoot.AddComponent<ProjectileShot>();
                        projectile.Initialize(this, source, source.AttackOriginFor(target.Position), target,
                            1f, 0f, style.Primary);
                        var projectileSignatures = projectileRoot.GetComponentsInChildren<SpriteRenderer>(true)
                            .Count(renderer => renderer.name.StartsWith(
                                $"Skin VFX Basic V{style.Variant} Projectile Signature ",
                                StringComparison.Ordinal));
                        if (projectileSignatures < (style.Variant == 1 ? 3 : 5))
                            failures.Add($"projectile-signature:{archetype}:v{style.Variant}:{projectileSignatures}");
                        else projectileSamples++;
                    }

                    SpawnHeroSkillSpectacle(source, target.Position, false);
                    SpawnHeroSkillSpectacle(source, target.Position, true);
                    SpawnUltimateImpactFlash(target.Position, style.Primary, archetype, source);
                    var effects = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None);
                    var skillRoot = effects.LastOrDefault(effect => effect != null &&
                        effect.name == "Hero Skill Spectacle");
                    var ultimateRoot = effects.LastOrDefault(effect => effect != null &&
                        effect.name == "Hero Ultimate Spectacle");
                    var impactRoot = effects.LastOrDefault(effect => effect != null &&
                        effect.name == $"Hero Ultimate Impact {archetype}");
                    if (skillRoot == null || ultimateRoot == null || impactRoot == null ||
                        !skillRoot.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer =>
                            renderer.name == $"Skill Skin {style.Variant} Signature") ||
                        !ultimateRoot.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer =>
                            renderer.name == $"Ultimate Skin {style.Variant} Signature") ||
                        !impactRoot.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer =>
                            renderer.name == $"Ultimate Skin {style.Variant} Signature"))
                        failures.Add($"skill-ultimate-signature:{archetype}:v{style.Variant}");

                    ClearTransientBattlePresentation();
                    units.Remove(source);
                    Destroy(source.gameObject);
                    yield return null;
                }
                monetization.EquipDefault(ShopCategory.Unit, archetype);
            }

            var archerProducts = monetization.Products.Where(product => product.Category == ShopCategory.Unit &&
                                                                         product.TargetUnit == UnitArchetype.Archer)
                .OrderBy(product => product.Id).ToArray();
            var arrowTargets = new List<EnemyUnit>();
            for (var i = 0; i < 6; i++)
            {
                var arrowTarget = new GameObject($"QA 255 Arrow Target {i}").AddComponent<EnemyUnit>();
                arrowTarget.Initialize(this, i, 900000f, false, i % Mathf.Max(1, LaneCount),
                    targetProfile.CombatClass, targetProfile);
                arrowTarget.ForcePositionForQa(NearestWalkable(new Vector2(-2.2f + i * .82f,
                    -2.9f + i % 2 * .42f), .18f));
                enemies.Add(arrowTarget);
                arrowTargets.Add(arrowTarget);
            }
            foreach (var product in archerProducts)
            {
                monetization.GrantForQa(product.Id);
                monetization.Equip(product);
                var archer = new GameObject($"QA 255 Archer Ultimate {product.Id}").AddComponent<PlayerUnit>();
                archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer],
                    NearestWalkable(new Vector2(0f, -4.9f), .18f));
                units.Add(archer);
                StartCoroutine(RoyalArrowRainRoutine(archer, definitions[UnitArchetype.Archer].Color));
                yield return new WaitForSecondsRealtime(.12f);
                var field = GameObject.Find("Royal Arrow Rain Global Field");
                var actors = field == null ? Array.Empty<Transform>() :
                    field.transform.Cast<Transform>().Where(item =>
                        item.name.StartsWith("Royal Arrow Actor ", StringComparison.Ordinal)).ToArray();
                var shafts = field == null ? Array.Empty<SpriteRenderer>() :
                    field.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer =>
                        renderer.name.StartsWith("Royal Arrow Shaft ", StringComparison.Ordinal)).ToArray();
                var heads = field == null ? Array.Empty<SpriteRenderer>() :
                    field.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer =>
                        renderer.name.StartsWith("Royal Arrowhead ", StringComparison.Ordinal)).ToArray();
                var fletchings = field == null ? Array.Empty<SpriteRenderer>() :
                    field.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer =>
                        renderer.name.StartsWith("Royal Fletching ", StringComparison.Ordinal)).ToArray();
                var trails = field == null ? Array.Empty<SpriteRenderer>() :
                    field.GetComponentsInChildren<SpriteRenderer>(true).Where(renderer =>
                        renderer.name.StartsWith($"Archer Skin {archer.SkinVariant} Razor Trail ",
                            StringComparison.Ordinal)).ToArray();
                var forbiddenLandingBursts = field == null ? 0 :
                    field.GetComponentsInChildren<SpriteRenderer>(true).Count(renderer =>
                        renderer.name.Contains("Contact", StringComparison.Ordinal) ||
                        renderer.name.Contains("Impact", StringComparison.Ordinal) ||
                        renderer.name.Contains("Beam", StringComparison.Ordinal) ||
                        renderer.name.Contains("Explosion", StringComparison.Ordinal));
                if (actors.Length != arrowTargets.Count + 1 || shafts.Length != arrowTargets.Count + 1 ||
                    heads.Length != arrowTargets.Count + 1 || fletchings.Length != arrowTargets.Count + 1 ||
                    trails.Length != arrowTargets.Count + 1 || forbiddenLandingBursts != 0)
                    failures.Add($"archer-one-per-enemy:v{archer.SkinVariant}:" +
                                 $"{actors.Length}/{shafts.Length}/{heads.Length}/{fletchings.Length}/" +
                                 $"{trails.Length}:" +
                                 $"burst={forbiddenLandingBursts}:{arrowTargets.Count + 1}");
                ClearTransientBattlePresentation();
                units.Remove(archer);
                Destroy(archer.gameObject);
                yield return null;
            }
            monetization.EquipDefault(ShopCategory.Unit, UnitArchetype.Archer);

            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray())
                Destroy(enemy.gameObject);
            enemies.Clear();
            var passed = failures.Count == 0 && styleSamples == 20 && tierSamples == 60 &&
                         cueSamples == 20 && projectileSamples == 14;
            Debug.Log($"QA_SKIN_COMBAT_VFX_255 passed={passed} styles={styleSamples}/20 " +
                      $"tiers={tierSamples}/60 cues={cueSamples}/20 projectiles={projectileSamples}/14 " +
                      $"archerTargets={arrowTargets.Count + 1} fail={string.Join(",", failures.Take(24))}");
            Application.Quit(passed ? 0 : 115);
        }
    }
}
