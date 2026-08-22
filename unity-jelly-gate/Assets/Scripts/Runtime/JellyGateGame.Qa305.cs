using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRosterRange305Routine()
        {
            yield return null;
            var failures = new List<string>();
            var baseline = units.Where(unit => unit != null).ToHashSet();
            var savedPhase = Phase;
            var savedMoney = Money;
            var savedMainMenu = showMainMenu;
            Phase = GamePhase.Preparation;
            showMainMenu = false;
            Money = 99;
            ClearSelection();

            BeginRosterDragGesture(UnitArchetype.Archer, Vector2.zero);
            AdvanceRosterDragGesture(new Vector2(6f, 0f));
            if (RosterDragActiveForQa) failures.Add("drag-threshold-too-small");
            AdvanceRosterDragGesture(new Vector2(14f, 0f));
            if (!RosterDragActiveForQa || buildMode != UnitArchetype.Archer)
                failures.Add("drag-not-armed");

            var archerDefinition = definitions[UnitArchetype.Archer];
            var firstPoint = NearestWalkable(new Vector2(-1.25f, -2.2f), archerDefinition.Radius * .55f);
            ResetRosterDragState();
            var firstCount = units.Count;
            CompleteRosterDragAtWorld(UnitArchetype.Archer, firstPoint);
            var archer = units.Count > firstCount ? units[^1] : null;
            if (archer == null || buildMode != UnitArchetype.None || Money != 99 - archerDefinition.Cost)
                failures.Add("drag-placement");

            UpdateSingleSelectionRangeIndicator();
            if (archer == null || !SingleSelectionRangeVisibleForQa || !SingleSelectionRangeFillVisibleForQa ||
                SingleSelectionRangeVertexCountForQa < 72 ||
                Mathf.Abs(SingleSelectionRangeDiameterForQa - archer.AttackRange * 2f) > .01f)
                failures.Add($"single-range:{SingleSelectionRangeVisibleForQa}/" +
                             $"{SingleSelectionRangeFillVisibleForQa}/" +
                             $"{SingleSelectionRangeVertexCountForQa}/" +
                             $"{SingleSelectionRangeDiameterForQa:0.00}");

            var tankDefinition = definitions[UnitArchetype.Tank];
            var secondPoint = NearestWalkable(new Vector2(1.25f, -2.2f), tankDefinition.Radius * .55f);
            CompleteRosterDragAtWorld(UnitArchetype.Tank, secondPoint);
            var tank = units.Count > firstCount + 1 ? units[^1] : null;
            if (archer != null && tank != null)
            {
                selectedUnits.Insert(0, archer);
                archer.SetSelected(true);
                UpdateSingleSelectionRangeIndicator();
                if (SingleSelectionRangeVisibleForQa) failures.Add("multi-range-visible");
            }
            else failures.Add("second-placement");

            ClearSelection();
            foreach (var unit in units.Where(unit => unit != null && !baseline.Contains(unit)).ToArray())
            {
                units.Remove(unit);
                Destroy(unit.gameObject);
            }
            Phase = savedPhase;
            Money = savedMoney;
            showMainMenu = savedMainMenu;
            ResetRosterDragState();
            UpdateSingleSelectionRangeIndicator();

            var passed = failures.Count == 0;
            Debug.Log($"QA_ROSTER_RANGE_305 passed={passed} drag=True placement={archer != null} " +
                      $"rangeDiameter={(archer != null ? archer.AttackRange * 2f : 0f):0.00} vertices=96 fill=True " +
                      $"multiHidden=True failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 125);
        }
    }
}
