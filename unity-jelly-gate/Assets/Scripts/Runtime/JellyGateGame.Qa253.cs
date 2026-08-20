using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        [Serializable]
        private sealed class QaGoogleServicesConfig253
        {
            public string playGamesProjectId = string.Empty;
            public string googleCloudProjectNumber = string.Empty;
            public string googleCloudProjectId = string.Empty;
        }

        private IEnumerator QaPresentation253Routine()
        {
            yield return null;
            Phase = GamePhase.Preparation;
            var originalLanguage = GameLocalization.Current;
            var failures = new List<string>();

            augmentPower.Clear();
            var archer = new GameObject("QA 253 Critical Archer").AddComponent<PlayerUnit>();
            archer.Initialize(this, UnitArchetype.Archer, definitions[UnitArchetype.Archer],
                new Vector2(0f, -4.8f));

            GameLocalization.Current = GameLanguage.Korean;
            var koreanStats = SelectedUnitPrimaryStats(archer) + " | " +
                              SelectedUnitDefenseAndPenetration(archer) + " | " +
                              SelectedUnitCombatRangesAndCritical(archer);
            var koreanLabels = CountToken253(koreanStats, "사거리") == 1 &&
                               koreanStats.Contains("탐지") && koreanStats.Contains("치명 25%") &&
                               koreanStats.Contains("치피 150%") && koreanStats.Contains("방어") &&
                               koreanStats.Contains("마저") && koreanStats.Contains("방관") &&
                               koreanStats.Contains("마관");
            GameLocalization.Current = GameLanguage.English;
            var englishStats = SelectedUnitPrimaryStats(archer) + " | " +
                               SelectedUnitDefenseAndPenetration(archer) + " | " +
                               SelectedUnitCombatRangesAndCritical(archer);
            var englishLabels = CountToken253(englishStats, "RNG") == 1 &&
                                englishStats.Contains("DET") && englishStats.Contains("CRIT 25%") &&
                                englishStats.Contains("C.DMG 150%") && englishStats.Contains("DEF") &&
                                englishStats.Contains("RES") && englishStats.Contains("ARM PEN") &&
                                englishStats.Contains("MAG PEN");

            var baseChance = GetCriticalChance(archer);
            var baseDamage = GetCriticalDamageMultiplier(archer);
            var firstPower = TierPower(AugmentTier.Gold);
            augmentPower["RangedCrit"] = firstPower;
            var firstChance = GetCriticalChance(archer);
            var firstDamage = GetCriticalDamageMultiplier(archer);
            var secondPower = firstPower + firstPower / (1f + firstPower * .55f);
            augmentPower["RangedCrit"] = secondPower;
            var secondChance = GetCriticalChance(archer);
            var secondDamage = GetCriticalDamageMultiplier(archer);
            augmentPower["RangedCrit"] = 99f;
            var cappedChance = GetCriticalChance(archer);
            var cappedDamage = GetCriticalDamageMultiplier(archer);
            var criticalRules = Mathf.Abs(baseChance - .25f) < .001f &&
                                Mathf.Abs(baseDamage - 1.5f) < .001f &&
                                firstChance > baseChance && firstDamage is > 1.64f and < 1.67f &&
                                secondChance > firstChance && secondDamage > firstDamage &&
                                cappedChance <= .5501f && cappedDamage <= 2.2501f;
            augmentPower.Clear();
            Destroy(archer.gameObject);
            if (!koreanLabels) failures.Add("stats-ko");
            if (!englishLabels) failures.Add("stats-en");
            if (!criticalRules) failures.Add("critical-stacking");

            var sampleCard = new Rect(4f, 0f, 640f, 248f);
            var unitPortrait = GuideUnitPortraitRect(sampleCard);
            var bossPortrait = GuideBossPortraitRect(new Rect(4f, 0f, 640f, 220f));
            var portraitLayout = unitPortrait.width >= 128f && unitPortrait.height >= 94f &&
                                 unitPortrait.width / sampleCard.width >= .19f &&
                                 bossPortrait.width >= 108f && bossPortrait.height >= 94f;
            if (!portraitLayout) failures.Add("portrait-layout");

            var bossPortraits = new List<Sprite>();
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var portrait = GuideBossPortraitSprite(chapter);
                bossPortraits.Add(portrait);
                if (portrait == null || portrait.texture == null ||
                    portrait.texture.name.Contains("directions") ||
                    portrait.texture.name.Contains("-isolated-r") ||
                    !(portrait.texture.name.Contains("boss-lineup-") ||
                      portrait.texture.name.Contains("boss-jelly-king-clean")))
                    failures.Add($"boss-guide-{profile.Id}");
            }
            var bossPortraitIdentity = bossPortraits.All(sprite => sprite != null) &&
                                       bossPortraits.Select(sprite => sprite.GetInstanceID()).Distinct().Count() == 10;
            if (!bossPortraitIdentity) failures.Add("boss-guide-distinct");

            var playerBodyChecks = 0;
            var roster = Enum.GetValues(typeof(UnitArchetype)).Cast<UnitArchetype>()
                .Where(unit => unit != UnitArchetype.None && definitions.ContainsKey(unit)).ToArray();
            foreach (var archetype in roster)
            {
                monetization.EquipDefault(ShopCategory.Unit, archetype);
                CheckPlayerPresentation253(archetype, "default", false, failures, ref playerBodyChecks);
                CheckPlayerPresentation253(archetype, "default", true, failures, ref playerBodyChecks);
                foreach (var product in monetization.Products.Where(product =>
                             product.Category == ShopCategory.Unit && product.TargetUnit == archetype))
                {
                    monetization.GrantForQa(product.Id);
                    monetization.Equip(product);
                    CheckPlayerPresentation253(archetype, product.Id, false, failures, ref playerBodyChecks);
                    CheckPlayerPresentation253(archetype, product.Id, true, failures, ref playerBodyChecks);
                }
                monetization.EquipDefault(ShopCategory.Unit, archetype);
            }

            var enemyBodyChecks = 0;
            foreach (var profile in EnemyVariantCatalog.AllProfiles)
            {
                var enemy = new GameObject($"QA 253 {profile.Id}").AddComponent<EnemyUnit>();
                enemy.Initialize(this, 0, 1000f, false, 0, profile.CombatClass, profile);
                enemyBodyChecks++;
                if (enemy.ActivePrimaryBodyChannelsForQa != 1)
                    failures.Add($"enemy-body-{profile.Id}:{enemy.ActivePrimaryBodyChannelsForQa}");
                if (enemy.HasAuthoredVariantDirectionalAnimationForQa)
                    CheckEnemyDirectionalCells253(enemy, profile.Id, failures);
                Destroy(enemy.gameObject);
            }
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA 253 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, 0, 10000f, true, 0, profile.CombatClass, profile);
                enemyBodyChecks++;
                if (boss.ActivePrimaryBodyChannelsForQa != 1)
                    failures.Add($"boss-body-{profile.Id}:{boss.ActivePrimaryBodyChannelsForQa}");
                CheckEnemyDirectionalCells253(boss, profile.Id, failures);
                Destroy(boss.gameObject);
            }

            var configAsset = Resources.Load<TextAsset>("crownfront-google-services");
            var config = configAsset != null
                ? JsonUtility.FromJson<QaGoogleServicesConfig253>(configAsset.text)
                : new QaGoogleServicesConfig253();
            var projectConfigured = config != null && config.playGamesProjectId == "228925673337" &&
                                    config.googleCloudProjectNumber == "228925673337" &&
                                    config.googleCloudProjectId == "project-4fef7106-3754-4175-9e8";
            if (!projectConfigured) failures.Add("google-project-config");

            GameLocalization.Current = originalLanguage;
            var passed = failures.Count == 0 && playerBodyChecks == roster.Length * 6 &&
                         enemyBodyChecks == EnemyVariantCatalog.AllProfiles.Length + 10;
            Debug.Log($"QA_PRESENTATION_253 passed={passed} labels={koreanLabels}/{englishLabels} " +
                      $"critical={criticalRules}:{baseDamage:0.00}/{firstDamage:0.00}/{secondDamage:0.00}/{cappedDamage:0.00} " +
                      $"portrait={portraitLayout} bosses={bossPortraitIdentity}:{bossPortraits.Count} " +
                      $"bodies={playerBodyChecks}/{enemyBodyChecks} google={projectConfigured} " +
                      $"fail={string.Join(",", failures.Take(16))}");
            Application.Quit(passed ? 0 : 111);
        }

        private void CheckPlayerPresentation253(UnitArchetype archetype, string skin, bool hero,
            ICollection<string> failures, ref int checks)
        {
            var actor = new GameObject($"QA 253 {archetype} {skin} {(hero ? "Hero" : "Normal")}")
                .AddComponent<PlayerUnit>();
            actor.Initialize(this, archetype, definitions[archetype], new Vector2(0f, -4.6f));
            if (hero) actor.AddExperience(9999f);
            actor.RefreshCosmeticPresentation();
            checks++;
            if (actor.ActivePrimaryBodyChannelsForQa != 1)
                failures.Add($"player-body-{archetype}-{skin}-{hero}:{actor.ActivePrimaryBodyChannelsForQa}");
            if (!actor.HasCompleteDirectionalAnimation)
                failures.Add($"player-direction-{archetype}-{skin}-{hero}");
            if (archetype == UnitArchetype.Lancer)
            {
                actor.PreviewMotionPoseForQa(Vector2.right, 0, .48f);
                var expectedAtlas = skin == "default" ? "lancer-direction" : "skin-rig-lancer";
                if (!actor.CurrentFrameTextureName.Contains(expectedAtlas) ||
                    !actor.CurrentFrameTextureName.Contains("isolated"))
                    failures.Add($"lancer-cell-{skin}-{hero}");
            }
            Destroy(actor.gameObject);
        }

        private static void CheckEnemyDirectionalCells253(EnemyUnit actor, string id,
            ICollection<string> failures)
        {
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            foreach (var direction in directions)
            for (var state = 0; state < 3; state++)
            {
                actor.PreviewMotionPoseForQa(direction, state, .48f);
                if (actor.CurrentSpriteUsesStrictAtlasCellForQa &&
                    actor.CurrentFrameTextureNameForQa.Contains("-isolated-r")) continue;
                failures.Add($"enemy-cell-{id}-{state}");
                return;
            }
        }

        private static int CountToken253(string source, string token)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token)) return 0;
            var count = 0;
            for (var index = 0; (index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0;
                 index += token.Length) count++;
            return count;
        }
    }
}
