using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRelease302Routine()
        {
            yield return null;
            var failures = new List<string>();

            var cosmetics = monetization.Products.Where(product =>
                product.Category is ShopCategory.Castle or ShopCategory.Unit or ShopCategory.MainMenu).ToArray();
            var castlePrices = cosmetics.Where(product => product.Category == ShopCategory.Castle).ToArray();
            var unitPrices = cosmetics.Where(product => product.Category == ShopCategory.Unit).ToArray();
            var menuPrices = cosmetics.Where(product => product.Category == ShopCategory.MainMenu).ToArray();
            if (castlePrices.Length != 2 || castlePrices.Any(product => product.GemPrice != 350))
                failures.Add("castle-price");
            if (unitPrices.Length != 20 || unitPrices.Any(product => product.GemPrice != 300))
                failures.Add("unit-price");
            if (menuPrices.Length != 2 || menuPrices.Any(product => product.GemPrice != 220))
                failures.Add("menu-price");
            if (shopCategory != ShopCategory.Supplies)
                failures.Add($"shop-default:{shopCategory}");

            var archer = definitions[UnitArchetype.Archer];
            if (!Mathf.Approximately(archer.AttackPower, 17f) ||
                !Mathf.Approximately(archer.AttackDelay, .86f))
                failures.Add($"archer-balance:{archer.AttackPower:0.00}/{archer.AttackDelay:0.00}");

            var promptPanel = new Rect(20f, 30f, 354f, 286f);
            var buttonWidth = (promptPanel.width - 54f) * .5f;
            var useRect = TacticalItemUseButtonRect(promptPanel, buttonWidth);
            var cancelRect = TacticalItemCancelButtonRect(promptPanel, buttonWidth);
            if (useRect.xMin >= cancelRect.xMin || useRect.Overlaps(cancelRect))
                failures.Add("item-button-order");

            var probeObject = new GameObject("QA 302 Hero HUD Probe");
            var probe = probeObject.AddComponent<PlayerUnit>();
            probe.Initialize(this, UnitArchetype.Archer, archer, NearestWalkable(new Vector2(0f, -5.5f), archer.Radius));
            probe.AddExperience(10000f);
            units.Add(probe);
            selectedUnits.Clear();
            selectedUnits.Add(probe);
            var statsRect = SelectedUnitStatsRect();
            var skillRect = SelectedUnitSkillRect();
            var ultimateRect = SelectedUnitUltimateRect();
            if (!probe.IsHero || skillRect.xMax >= ultimateRect.xMin ||
                ultimateRect.width <= skillRect.width || statsRect.xMax > skillRect.xMin)
                failures.Add($"hero-hud:{probe.IsHero}/{statsRect}/{skillRect}/{ultimateRect}");

            var roster = Enum.GetValues(typeof(UnitArchetype)).Cast<UnitArchetype>()
                .Where(archetype => archetype != UnitArchetype.None).ToArray();
            if (roster.Any(archetype => string.IsNullOrWhiteSpace(SelectedUnitAbilitySummary(archetype, false)) ||
                                       string.IsNullOrWhiteSpace(SelectedUnitAbilitySummary(archetype, true)) ||
                                       SelectedUnitAbilitySummary(archetype, false).Any(char.IsDigit) ||
                                       SelectedUnitAbilitySummary(archetype, true).Any(char.IsDigit)))
                failures.Add("ability-summary");
            var earlyMask = SelectedAbilityRadialMask(.2f);
            var lateMask = SelectedAbilityRadialMask(.8f);
            if (earlyMask == null || lateMask == null || earlyMask == lateMask ||
                selectedAbilityRadialMasks.Count < 2)
                failures.Add("radial-cooldown");

            selectedUnits.Clear();
            units.Remove(probe);
            Destroy(probeObject);
            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_302 passed={passed} prices={castlePrices.Length}/{unitPrices.Length}/{menuPrices.Length} " +
                      $"archer={archer.AttackPower:0.0}@{archer.AttackDelay:0.00} " +
                      $"buttons={useRect.xMin:0}<{cancelRect.xMin:0} hud={skillRect.width:0}/{ultimateRect.width:0} " +
                      $"failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 122);
        }
    }
}
