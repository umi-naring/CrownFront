using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaPlacementUndo279Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            Phase = GamePhase.Preparation;
            Round = 7;
            Money = 40;
            foreach (var unit in units.Where(unit => unit != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();
            ClearSelection();
            nextPlacementBatchId = 1;

            var veteranDefinition = ApplyUnitAugments(UnitArchetype.Tank, definitions[UnitArchetype.Tank]);
            var veteran = new GameObject("QA279 Previous Round Veteran").AddComponent<PlayerUnit>();
            veteran.Initialize(this, UnitArchetype.Tank, veteranDefinition, new Vector2(-1.2f, -5.4f));
            veteran.MarkPlacementForUndo(Round - 1, 88, veteranDefinition.Cost);
            units.Add(veteran);
            var veteranPosition = veteran.Position;
            var moneyBefore = Money;

            TryPlaceUnit(UnitArchetype.Archer, new Vector2(0f, -5.5f));
            var firstBatch = units.Where(unit => unit != veteran && unit.PlacementRound == Round).ToArray();
            var firstPlaced = firstBatch.Length == 1 && firstBatch[0].PlacementRefundCost ==
                definitions[UnitArchetype.Archer].Cost;
            var firstRefund = firstBatch.Sum(unit => unit.PlacementRefundCost);
            var undoFirst = UndoLastCurrentRoundPlacement();
            var previousUntouched = veteran != null && veteran.IsAlive && units.Contains(veteran) &&
                                    Vector2.Distance(veteran.Position, veteranPosition) < .001f;
            var firstRefundCorrect = Money == moneyBefore && firstRefund == definitions[UnitArchetype.Archer].Cost;

            augmentPower["Duplicate"] = 100f;
            var moneyBeforeDuplicate = Money;
            TryPlaceUnit(UnitArchetype.Melee, new Vector2(0f, -5.5f));
            var current = units.Where(unit => unit != veteran && unit.PlacementRound == Round).ToArray();
            var duplicateGrouped = current.Length == 2 && current.Select(unit => unit.PlacementBatchId).Distinct().Count() == 1 &&
                                   current.Count(unit => unit.PlacementRefundCost > 0) == 1;
            var undoDuplicate = UndoLastCurrentRoundPlacement();
            var duplicateRefundCorrect = Money == moneyBeforeDuplicate;
            augmentPower.Remove("Duplicate");

            TryPlaceUnit(UnitArchetype.SingleMage, new Vector2(0f, -5.35f));
            var battleBatch = units.Where(unit => unit != veteran && unit.PlacementRound == Round).ToArray();
            Phase = GamePhase.Battle;
            var battleLocked = !UndoLastCurrentRoundPlacement() && battleBatch.All(unit => units.Contains(unit));
            Phase = GamePhase.Preparation;
            var cleanupAfterBattleLock = UndoLastCurrentRoundPlacement();
            var noCurrentRoundPlacement = !CanUndoCurrentRoundPlacement;
            var iconLoaded = placementUndoIcon != null && placementUndoIcon.width >= 256 &&
                             placementUndoIcon.height >= 256 &&
                             placementUndoIcon.name.Contains("small-v2");
            GetBottomActionRects(SafeGuiRect.yMax - 66f, out _, out _, out var undoRect, out _);
            var iconDrawSize = Mathf.Min(undoRect.width - 8f, undoRect.height - 8f);
            var smallUiReadable = undoRect.width >= 48f && iconDrawSize >= 40f;

            var passed = firstPlaced && undoFirst && previousUntouched && firstRefundCorrect && duplicateGrouped &&
                         undoDuplicate && duplicateRefundCorrect && battleLocked && cleanupAfterBattleLock &&
                         noCurrentRoundPlacement && iconLoaded && smallUiReadable;
            Debug.Log($"QA_PLACEMENT_UNDO_279 passed={passed} firstPlaced={firstPlaced} undoFirst={undoFirst} " +
                      $"previousUntouched={previousUntouched} firstRefund={firstRefundCorrect} " +
                      $"duplicateGrouped={duplicateGrouped} undoDuplicate={undoDuplicate} " +
                      $"duplicateRefund={duplicateRefundCorrect} battleLocked={battleLocked} cleanup={cleanupAfterBattleLock} " +
                      $"noCurrent={noCurrentRoundPlacement} icon={iconLoaded} readable={smallUiReadable}:{iconDrawSize:0.0}px");
            Application.Quit(passed ? 0 : 139);
        }
    }
}
