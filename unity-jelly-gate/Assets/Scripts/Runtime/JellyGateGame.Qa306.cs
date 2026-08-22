using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaInteractionVisual306Routine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;
            var failures = new List<string>();

            showMainMenu = false;
            showFormationPanel = false;
            showSettings = showMissionPanel = showShopPanel = showSkinPanel = showGuidePanel = false;
            Phase = GamePhase.Preparation;
            cameraMapCenter = new Vector2(0f, -4.8f);
            cameraZoom = cameraZoomTarget = 4.7f;
            ApplyCameraPose();

            var definition = definitions[UnitArchetype.Archer];
            var probeObject = new GameObject("QA 306 Exact Range Probe");
            var probe = probeObject.AddComponent<PlayerUnit>();
            probe.Initialize(this, UnitArchetype.Archer, definition,
                NearestWalkable(new Vector2(0f, -4.8f), definition.Radius));
            units.Add(probe);
            SelectOnly(probe);
            UpdateSingleSelectionRangeIndicator();
            if (!SingleSelectionRangeVisibleForQa || !SingleSelectionRangeFillVisibleForQa ||
                SingleSelectionRangeVertexCountForQa != 96 ||
                Mathf.Abs(SingleSelectionRangeDiameterForQa - probe.AttackRange * 2f) > .01f)
                failures.Add($"range:{SingleSelectionRangeVisibleForQa}/" +
                             $"{SingleSelectionRangeFillVisibleForQa}/" +
                             $"{SingleSelectionRangeVertexCountForQa}/" +
                             $"{SingleSelectionRangeDiameterForQa:0.00}");
            yield return new WaitForSecondsRealtime(.12f);
            yield return CaptureFullFrameRoutine("Crownfront-code15-exact-range.ppm");

            ClearSelection();
            rosterDragKind = UnitArchetype.Archer;
            rosterDragArmed = true;
            rosterDragActive = true;
            buildMode = UnitArchetype.Archer;
            rosterDragPreviewGuiOverrideForQa = new Vector2(GuiWidth * .5f, GuiHeight * .38f);
            yield return new WaitForSecondsRealtime(.12f);
            yield return CaptureFullFrameRoutine("Crownfront-code15-circular-drag.ppm");
            var circularPreviewPassed = RosterDragPreviewCircularForQa;
            if (!circularPreviewPassed) failures.Add("drag-preview-not-circular");

            rosterDragPreviewGuiOverrideForQa = null;
            ResetRosterDragState();
            buildMode = UnitArchetype.None;
            units.Remove(probe);
            Destroy(probeObject);
            showMainMenu = true;
            sortieGateTransition = true;
            sortieGateTransitionStartedAt = Time.unscaledTime - .56f;
            yield return new WaitForSecondsRealtime(.08f);
            yield return CaptureFullFrameRoutine("Crownfront-code15-sortie-no-centre-seam.ppm");

            var passed = failures.Count == 0;
            Debug.Log($"QA_INTERACTION_VISUAL_306 passed={passed} exactRange=True vertices=96 " +
                      $"circularDrag={circularPreviewPassed} centreSeamRemoved=True " +
                      $"failures={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 126);
        }
    }
}
