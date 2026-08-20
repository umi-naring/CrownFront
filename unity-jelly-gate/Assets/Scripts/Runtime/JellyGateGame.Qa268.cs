using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaJellyIdentity268Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            foreach (var enemy in enemies.Where(item => item != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();

            var mageProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var bomberProfile = EnemyVariantCatalog.ForChapterStage(0, 3);
            var specs = new[]
            {
                ("MAGE FRONT", mageProfile, new Vector2(-1.35f, -3.35f), Vector2.down),
                ("MAGE REAR", mageProfile, new Vector2(1.35f, -3.35f), Vector2.up),
                ("BOMBER FRONT", bomberProfile, new Vector2(-1.35f, -5.05f), Vector2.down),
                ("BOMBER REAR", bomberProfile, new Vector2(1.35f, -5.05f), Vector2.up)
            };
            foreach (var spec in specs)
            {
                var actor = new GameObject($"QA 268 {spec.Item1}").AddComponent<EnemyUnit>();
                actor.Initialize(this, 0, 900f, false, 0, spec.Item2.CombatClass, spec.Item2);
                actor.ForcePositionForQa(spec.Item3);
                actor.PreviewMotionPoseForQa(spec.Item4, 0, .15f);
                enemies.Add(actor);
            }
            cameraMapCenter = new Vector2(0f, -4.2f);
            cameraZoom = cameraZoomTarget = Mathf.Min(cameraZoomMax, 4.2f);
            yield return null;
            Time.timeScale = 0f;
            UnityEngine.Debug.Log("QA_JELLY_IDENTITY_268 ready=True mage=front/rear bomber=front/rear");
            if (HasCommandLineArgument("-qaScreenshot"))
                yield return new WaitForSecondsRealtime(3f);
            Application.Quit(0);
        }

        private IEnumerator QaRelease268Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Preparation;
            var failures = new List<string>();

            // Device hitch regression: take a long, curved tank route that cannot use the direct
            // line shortcut, then repeat it. No clearance mask may be rebuilt after startup.
            var tankClearance = definitions[UnitArchetype.Tank].Radius * .55f;
            var route = paths.OrderByDescending(path => path.Count).First();
            var start = route[Mathf.Min(2, route.Count - 1)];
            var goal = route[Mathf.Max(0, route.Count - 18)];
            var cacheBuildsBefore = WalkGridCacheBuildCountForQa268;
            var pathClock = Stopwatch.StartNew();
            var expandedSamples = new List<int>();
            var successfulRoutes = 0;
            for (var iteration = 0; iteration < 96; iteration++)
            {
                var from = iteration % 2 == 0 ? start : goal;
                var to = iteration % 2 == 0 ? goal : start;
                var result = FindWalkPath(from, to, tankClearance);
                if (result.Count > 0) successfulRoutes++;
                expandedSamples.Add(LastWalkGridExpandedNodesForQa268);
            }
            pathClock.Stop();
            var cacheBuildsAfter = WalkGridCacheBuildCountForQa268;
            var averagePathMs = pathClock.Elapsed.TotalMilliseconds / 96d;
            if (successfulRoutes != 96) failures.Add($"tank-routes={successfulRoutes}/96");
            if (cacheBuildsAfter != cacheBuildsBefore)
                failures.Add($"cache-rebuild={cacheBuildsBefore}->{cacheBuildsAfter}");
            if (!expandedSamples.Any(value => value > 0)) failures.Add("no-obstacle-route");
            if (averagePathMs > 8d) failures.Add($"tank-path-ms={averagePathMs:0.00}");

            // Reproduce the reported jelly mage transition across every front walk frame, both
            // side octants and the complete rear hemisphere.
            var mageProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var mage = new GameObject("QA 268 Jelly Mage Front Rear").AddComponent<EnemyUnit>();
            mage.Initialize(this, 0, 400f, false, 0, mageProfile.CombatClass, mageProfile);
            var frontSafe = true;
            var rearIdentity = true;
            var heightSamples = new List<float>();
            var frontDirections = new[] { Vector2.down, Vector2.left, Vector2.right,
                new Vector2(-1f, -1f), new Vector2(1f, -1f) };
            foreach (var direction in frontDirections)
            for (var state = 0; state < 3; state++)
            for (var frame = 0; frame < 24; frame++)
            {
                mage.PreviewMotionPoseForQa(direction, state, (frame + .15f) / 24f);
                frontSafe &= mage.CurrentFrameTextureNameForQa.Contains(
                                 "enemy-jelly-animation-atlas-v2-isolated-1-") &&
                             mage.CurrentSpriteHasSafeCellMarginForQa &&
                             mage.CurrentSpriteForeignComponentsForQa <= 0;
                heightSamples.Add(mage.VisualWorldHeight);
            }
            var rearDirections = new[] { Vector2.up, new Vector2(-1f, 1f), new Vector2(1f, 1f) };
            foreach (var direction in rearDirections)
            for (var state = 0; state < 3; state++)
            for (var frame = 0; frame < 24; frame++)
            {
                mage.PreviewMotionPoseForQa(direction, state, (frame + .15f) / 24f);
                rearIdentity &= mage.CurrentFrameTextureNameForQa.Contains("enemy-jelly-mage-back-v2");
                heightSamples.Add(mage.VisualWorldHeight);
            }
            var minHeight = heightSamples.Where(value => value > .01f).DefaultIfEmpty(0f).Min();
            var maxHeight = heightSamples.DefaultIfEmpty(0f).Max();
            var directionalScaleStable = minHeight > .01f && maxHeight / minHeight <= 1.10f;
            if (!frontSafe) failures.Add("jelly-front-cell-leak");
            if (!rearIdentity) failures.Add("jelly-rear-identity");
            if (!mage.HasCompleteDirectionalAnimationForQa) failures.Add("jelly-8way-timeline");
            if (!directionalScaleStable) failures.Add($"jelly-height={minHeight:0.000}-{maxHeight:0.000}");
            Destroy(mage.gameObject);

            // jelly_bomber used to be the second apparent mage because the generic routing
            // treated every jelly with a Siege combat role as a Mage visual. Verify its complete
            // front/side action set stays on row zero and its rear stays the horned family back.
            var bomberProfile = EnemyVariantCatalog.ForChapterStage(0, 3);
            var bomber = new GameObject("QA 268 Jelly Bomber Identity").AddComponent<EnemyUnit>();
            bomber.Initialize(this, 0, 400f, false, 0, bomberProfile.CombatClass, bomberProfile);
            var bomberFrontIdentity = true;
            var bomberRearIdentity = true;
            foreach (var direction in frontDirections)
            for (var state = 0; state < 3; state++)
            for (var frame = 0; frame < 24; frame++)
            {
                bomber.PreviewMotionPoseForQa(direction, state, (frame + .15f) / 24f);
                bomberFrontIdentity &= bomber.CurrentFrameTextureNameForQa.Contains(
                    "enemy-jelly-animation-atlas-v2-isolated-0-");
            }
            foreach (var direction in rearDirections)
            for (var state = 0; state < 3; state++)
            for (var frame = 0; frame < 24; frame++)
            {
                bomber.PreviewMotionPoseForQa(direction, state, (frame + .15f) / 24f);
                bomberRearIdentity &= bomber.CurrentFrameTextureNameForQa.Contains(
                    "enemy-back-roster-v1");
            }
            if (!bomberFrontIdentity) failures.Add("jelly-bomber-front-borrowed-mage");
            if (!bomberRearIdentity) failures.Add("jelly-bomber-rear-identity");
            Destroy(bomber.gameObject);

            // Every boss must obey the requested travel octant. This exercises all ten authored
            // sets rather than accepting one representative boss as evidence for the roster.
            var bossFacingChecks = 0;
            var bossFacingPassed = 0;
            var bossGroundedChecks = 0;
            var bossGroundedPassed = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA 268 Boss Facing {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter % Mathf.Max(1, paths.Count), 2600f, true, chapter,
                    profile.CombatClass, profile);
                foreach (FacingOctant octant in Enum.GetValues(typeof(FacingOctant)))
                {
                    boss.PreviewMotionPoseForQa(EightWayFacing.VectorFor(octant), 0, .37f);
                    bossFacingChecks++;
                    if (boss.VisualOctant == octant) bossFacingPassed++;
                    var groundContact = Mathf.Abs(boss.PreviewGroundContactForQa(
                        EightWayFacing.VectorFor(octant), .37f));
                    bossGroundedChecks++;
                    if (groundContact <= .11f) bossGroundedPassed++;
                }
                Destroy(boss.gameObject);
            }
            if (bossFacingPassed != bossFacingChecks)
                failures.Add($"boss-facing={bossFacingPassed}/{bossFacingChecks}");
            if (bossGroundedPassed != bossGroundedChecks)
                failures.Add($"boss-ground={bossGroundedPassed}/{bossGroundedChecks}");

            // The hammer soldier must use only its authored weapon arc and ground cracks. The
            // shared red fire/spectacle was both visually unrelated and spawned away from the
            // hammer contact point, so its presence is a release blocker.
            ClearTransientBattlePresentation();
            Phase = GamePhase.Battle;
            var hammerTargetProfile = EnemyVariantCatalog.ForChapterStage(0, 0);
            var hammerTarget = new GameObject("QA 268 Hammer Target").AddComponent<EnemyUnit>();
            hammerTarget.Initialize(this, 0, 900000f, false, 0, hammerTargetProfile.CombatClass,
                hammerTargetProfile);
            hammerTarget.ForcePositionForQa(NearestWalkable(new Vector2(.62f, -4.15f), .18f));
            enemies.Add(hammerTarget);
            var hammer = new GameObject("QA 268 Hammer Soldier").AddComponent<PlayerUnit>();
            hammer.Initialize(this, UnitArchetype.Melee, definitions[UnitArchetype.Melee],
                NearestWalkable(new Vector2(-.42f, -4.18f), .18f));
            units.Add(hammer);
            StartCoroutine(PlayerSkillRoutine(hammer, hammerTarget, definitions[UnitArchetype.Melee]));
            yield return new WaitForSecondsRealtime(.38f);
            var hammerArc = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item != null && item.name.Contains("Grounding Arc Skin"));
            var straySharedFire = FindObjectsByType<TransientBattleEffect>(FindObjectsSortMode.None)
                .Any(item => item != null &&
                             (item.name.Contains("Hero Skill Spectacle") ||
                              item.name.Contains("Melee Skill Clarity Crest")));
            var hammerVfxClean = hammerArc != null &&
                                 hammerArc.GetComponentsInChildren<SpriteRenderer>(true).Length >= 25 &&
                                 !straySharedFire;
            if (!hammerVfxClean) failures.Add("hammer-vfx-channel");
            ClearTransientBattlePresentation();
            enemies.Remove(hammerTarget);
            units.Remove(hammer);
            Destroy(hammerTarget.gameObject);
            Destroy(hammer.gameObject);
            Phase = GamePhase.Preparation;

            var card = new Rect(4f, 0f, 640f, 248f);
            var unitFrame = GuideUnitPortraitFrameRect(card);
            var unitInner = GuideUnitPortraitRect(card);
            var framedUnitLayout = unitFrame.Contains(unitInner.min) && unitFrame.Contains(unitInner.max) &&
                                   unitFrame.width == GuideBossPortraitFrameRect(card).width;
            if (!framedUnitLayout) failures.Add("unit-portrait-frame");

            var passed = failures.Count == 0;
            UnityEngine.Debug.Log($"QA_RELEASE_268 passed={passed} tankRoutes={successfulRoutes}/96 " +
                                  $"pathMs={averagePathMs:0.000} expanded={expandedSamples.Max()} " +
                                  $"cache={cacheBuildsBefore}->{cacheBuildsAfter} " +
                                  $"jelly={frontSafe}/{rearIdentity}/{directionalScaleStable}:" +
                                  $"{minHeight:0.000}-{maxHeight:0.000} bossFacing={bossFacingPassed}/" +
                                  $"{bossFacingChecks} bossGround={bossGroundedPassed}/{bossGroundedChecks} " +
                                  $"bomber={bomberFrontIdentity}/{bomberRearIdentity} " +
                                  $"unitFrame={framedUnitLayout} hammerVfx={hammerVfxClean} " +
                                  $"fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 69);
        }
    }
}
