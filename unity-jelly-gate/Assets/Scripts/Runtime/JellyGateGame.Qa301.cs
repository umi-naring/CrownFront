using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRelease301Routine()
        {
            yield return null;
            var failures = new List<string>();

            var twelveRoundReward = CalculateRunGoldReward(29, 12, 1f, false);
            if (twelveRoundReward is < 30 or > 36)
                failures.Add($"round12-gold:{twelveRoundReward}");
            if (CalculateRunGoldReward(29, 12, .5f, true) >= twelveRoundReward)
                failures.Add("gold-quality-order");
            if (IsRunSettlementEligible(GamePhase.Preparation, 12) ||
                IsRunSettlementEligible(GamePhase.Battle, 12) ||
                IsRunSettlementEligible(GamePhase.Augment, 12))
                failures.Add("mid-run-settlement-enabled");
            if (!IsRunSettlementEligible(GamePhase.Defeat, 12) ||
                !IsRunSettlementEligible(GamePhase.Victory, MaxRounds) ||
                !IsRunSettlementEligible(GamePhase.Battle, MaxRounds))
                failures.Add("terminal-settlement-disabled");

            var allAugments = Enum.GetValues(typeof(AugmentTier)).Cast<AugmentTier>()
                .SelectMany(GetAugmentPool).ToArray();
            if (allAugments.Any(template =>
                    template.EffectKey.Contains("Crit", StringComparison.OrdinalIgnoreCase) ||
                    template.Name.Contains("치명", StringComparison.OrdinalIgnoreCase) ||
                    template.Description.Contains("치명", StringComparison.OrdinalIgnoreCase) ||
                    template.Name.Contains("critical hit", StringComparison.OrdinalIgnoreCase) ||
                    template.Description.Contains("critical hit", StringComparison.OrdinalIgnoreCase) ||
                    template.Description.Contains("critical chance", StringComparison.OrdinalIgnoreCase) ||
                    template.Description.Contains("critical damage", StringComparison.OrdinalIgnoreCase)))
                failures.Add("critical-augment-remains");
            if (!allAugments.Any(template => template.EffectKey == "RangedPrecision") ||
                !allAugments.Any(template => template.EffectKey == "RangedSuppress"))
                failures.Add("ranged-replacements-missing");

            var tankDefinition = definitions[UnitArchetype.Tank];
            var testPosition = NearestWalkable(new Vector2(0f, -5.6f), tankDefinition.Radius * .55f);
            TryPlaceUnit(UnitArchetype.Tank, testPosition);
            var probe = units.LastOrDefault(unit => unit != null && unit.Archetype == UnitArchetype.Tank);
            if (probe == null)
                failures.Add("selected-hud-probe");
            else
            {
                var models = SelectedUnitStatCellModels(probe);
                if (GetCriticalChance(probe) != 0f || GetCriticalDamageMultiplier(probe) != 1f)
                    failures.Add("critical-combat-remains");
                if (models.Length != 6 || models.Any(model =>
                        model.Label.Contains("치명", StringComparison.OrdinalIgnoreCase) ||
                        model.Label.Contains("critical", StringComparison.OrdinalIgnoreCase)))
                    failures.Add("selected-hud-stat-model");
            }

            var confirmationCountBefore = economy.Count(TacticalItemId.FieldAid);
            economy.GrantPurchasedItem(TacticalItemId.FieldAid, 1);
            RequestTacticalItemUse(TacticalItemId.FieldAid);
            if (!TacticalItemUsePromptVisible || Time.timeScale != 0f ||
                economy.Count(TacticalItemId.FieldAid) != confirmationCountBefore + 1)
                failures.Add("item-confirmation-modal");
            CancelTacticalItemUse();
            if (TacticalItemUsePromptVisible)
                failures.Add("item-confirmation-cancel");

            var musketeerSprites = new List<Sprite>();
            if (unitDirectionalAnimations.TryGetValue(UnitArchetype.Musketeer, out var normalMusketeer))
                musketeerSprites.AddRange(AllDirectionalSprites301(normalMusketeer));
            if (heroDirectionalAnimations.TryGetValue(UnitArchetype.Musketeer, out var heroMusketeer))
                musketeerSprites.AddRange(AllDirectionalSprites301(heroMusketeer));
            var unaudited = musketeerSprites.Where(sprite => sprite == null ||
                !SpriteFrameIsolationRegistry.HasAudit(sprite)).ToArray();
            var contaminated = musketeerSprites.Where(sprite => sprite != null &&
                SpriteFrameIsolationRegistry.For(sprite).RemainingForeignComponents != 0).ToArray();
            if (musketeerSprites.Count < 128 || unaudited.Length > 0 || contaminated.Length > 0)
                failures.Add($"musketeer-frame-isolation:{musketeerSprites.Count}/" +
                             $"{unaudited.Length}/{contaminated.Length}");

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_301 passed={passed} gold12={twelveRoundReward} settlement=terminal-only " +
                      $"augments={allAugments.Length} musketeerFrames={musketeerSprites.Count} " +
                      $"failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 121);
        }

        private static IEnumerable<Sprite> AllDirectionalSprites301(DirectionalAnimationSet set)
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
