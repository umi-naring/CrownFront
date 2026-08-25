using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaAllUnitPoses320Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            Time.timeScale = 1f;
            yield return PrewarmBossPresentations();

            var failures = new List<string>();
            var csv = new StringBuilder(24 * 1024 * 1024);
            csv.AppendLine("category,id,variant,hero,direction,state,phase,sprite,source,left,bottom,right,top,foreign,seam,channels,height,scale,ground,opaqueArea,centreX,centreY,pass");
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>().ToArray();
            var phases = Enumerable.Range(0, 24).Select(index => (index + .5f) / 24f).ToArray();
            var poseCount = 0;
            var uniqueSprites = new HashSet<Sprite>();
            var silhouetteCache = new Dictionary<Sprite, Vector3>();
            var exportedFailures = new HashSet<Sprite>();
            var exportedArcherAudit = new HashSet<Sprite>();
            var archerNeighbourFragmentsRemoved = 0;
            var archerAimChecks = 0;
            var archerAimFailures = 0;
            var artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "qa-artifacts", "Crownfront-QA-320"));
            Directory.CreateDirectory(artifactRoot);
            var failureFrameRoot = Path.Combine(artifactRoot, "failure-frames");
            if (Directory.Exists(failureFrameRoot)) Directory.Delete(failureFrameRoot, true);
            Directory.CreateDirectory(failureFrameRoot);
            var archerFrameRoot = Path.Combine(artifactRoot, "hero-archer-live-frames");
            if (Directory.Exists(archerFrameRoot)) Directory.Delete(archerFrameRoot, true);
            Directory.CreateDirectory(archerFrameRoot);

            var roster = new[]
            {
                UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
                UnitArchetype.AreaMage, UnitArchetype.SingleMage, UnitArchetype.Bombardier,
                UnitArchetype.Lancer, UnitArchetype.Druid, UnitArchetype.Musketeer,
                UnitArchetype.Oracle
            };

            var playerPresentations = 0;
            foreach (var archetype in roster)
            for (var variant = 0; variant <= 2; variant++)
            foreach (var hero in new[] { false, true })
            {
                DirectionalAnimationSet presentation;
                if (variant == 0)
                {
                    var source = hero ? heroDirectionalAnimations : unitDirectionalAnimations;
                    source.TryGetValue(archetype, out presentation);
                }
                else presentation = GetAuthoredSkinAnimation(archetype, variant, hero);
                if (presentation == null)
                {
                    failures.Add($"player-missing:{archetype}:v{variant}:hero={hero}");
                    continue;
                }

                playerPresentations++;
                var actor = new GameObject($"QA320 Player {archetype} v{variant} h{hero}")
                    .AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definitions[archetype],
                    NearestWalkable(new Vector2(0f, -5.4f), .18f));
                if (hero) actor.AddExperience(99999f);
                actor.UseDirectionalAnimationForQa(presentation);
                var heights = new List<float>();
                var opaqueAreasByDirection = directions.ToDictionary(direction => direction,
                    _ => new List<float>());
                var opaqueCentresByDirection = directions.ToDictionary(direction => direction,
                    _ => new List<Vector2>());
                var silhouettePosesByDirection = directions.ToDictionary(direction => direction,
                    _ => new List<(Sprite sprite, float area, Vector2 centre, int state, int frame)>());
                var mirrorSignatures = new Dictionary<FacingOctant, string>();
                var presentationPassed = actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                         actor.HasCompleteDirectionalAnimation;

                foreach (var octant in directions)
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    for (var state = 0; state < 4; state++)
                    for (var frame = 0; frame < phases.Length; frame++)
                    {
                        var pose = actor.PreviewMotionPoseForQa(direction, state, phases[frame]);
                        poseCount++;
                        var sprite = actor.PortraitSprite;
                        var margins = PlayerUnit.SpriteOpaqueMarginsForQa(sprite);
                        var audit = SpriteFrameIsolationRegistry.For(sprite);
                        if (archetype == UnitArchetype.Archer &&
                            (audit.Source ?? string.Empty).IndexOf("archer",
                                System.StringComparison.OrdinalIgnoreCase) >= 0)
                            archerNeighbourFragmentsRemoved = Mathf.Max(archerNeighbourFragmentsRemoved,
                                audit.RemovedForeignComponents);
                        // Measure the character body, not a raised spear/staff/projectile. The
                        // latter is expected to change reach during an authored attack pose.
                        var height = actor.VisualBodyWorldHeightForQa;
                        var rawSilhouette = SpriteSilhouette320(sprite, silhouetteCache);
                        var visualScale = actor.VisualScaleForQa;
                        var silhouette = new Vector3(
                            rawSilhouette.x * Mathf.Abs(visualScale.x * visualScale.y),
                            rawSilhouette.y * visualScale.x,
                            rawSilhouette.z * visualScale.y);
                        var groundDelta = state == 0
                            ? Mathf.Abs(actor.PreviewGroundContactForQa(direction, phases[frame]) -
                                        actor.GroundPlaneLocalYForQa)
                            : 0f;
                        var grounded = state != 0 || groundDelta <= actor.Radius * .008f;
                        if (state == 0) actor.PreviewMotionPoseForQa(direction, state, phases[frame]);
                        var horizontalDirection = octant is FacingOctant.SouthWest or FacingOctant.West or
                            FacingOctant.NorthWest or FacingOctant.NorthEast or FacingOctant.East or
                            FacingOctant.SouthEast;
                        var expectedFlip = horizontalDirection && (presentation.SideFacesRight
                            ? !EightWayFacing.IsRight(octant)
                            : EightWayFacing.IsRight(octant));
                        var facingCorrect = actor.VisualSpriteFlipped == expectedFlip;
                        // Archer bows are intentionally disconnected from the painted torso in
                        // several authored frames.  The exact-cell crop and registered isolation
                        // audit below reject neighbouring atlas actors; do not misclassify the
                        // archer's own bow as an adjacent sprite fragment.
                        var archerFrameClean = archetype != UnitArchetype.Archer ||
                                               audit.RemainingForeignComponents == 0;
                        var rendererCentred = state != 0 ||
                                              (Mathf.Abs(pose.x) <= .0025f && Mathf.Abs(pose.z) <= .0025f);
                        var fixedRangedActionCentre = state == 0 ||
                            archetype is not (UnitArchetype.Archer or UnitArchetype.AreaMage or
                                UnitArchetype.SingleMage or UnitArchetype.Musketeer or
                                UnitArchetype.Druid or UnitArchetype.Oracle) ||
                            Mathf.Abs(pose.x) <= .0025f;
                        var passed = sprite != null &&
                                     actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                     actor.CurrentSpriteMetricsReadyForQa &&
                                     actor.VisualOctant == octant &&
                                     margins.x >= 18f && margins.y >= 18f &&
                                     margins.z >= 18f && margins.w >= 18f &&
                                     SpriteFrameIsolationRegistry.HasAudit(sprite) &&
                                     audit.RemainingForeignComponents == 0 &&
                                     audit.BodySeamClosed &&
                                     grounded &&
                                     facingCorrect &&
                                     archerFrameClean &&
                                     rendererCentred &&
                                     fixedRangedActionCentre &&
                                     Mathf.Abs(pose.w) is > .20f and < 2.50f;
                        presentationPassed &= passed;
                        if (sprite != null)
                        {
                            uniqueSprites.Add(sprite);
                            heights.Add(height);
                            opaqueAreasByDirection[octant].Add(silhouette.x);
                            opaqueCentresByDirection[octant].Add(new Vector2(silhouette.y, silhouette.z));
                            silhouettePosesByDirection[octant].Add((sprite, silhouette.x,
                                new Vector2(silhouette.y, silhouette.z), state, frame));
                            if (state == 0 && frame == 10)
                                mirrorSignatures[octant] = $"{sprite.GetInstanceID()}:{actor.VisualSpriteFlipped}";
                            if (!passed) ExportFailureFrame320(sprite, failureFrameRoot, exportedFailures,
                                $"player-{archetype}-v{variant}-h{hero}-{octant}-s{state}-f{frame}");
                            if (archetype == UnitArchetype.Archer && hero)
                                ExportFailureFrame320(sprite, archerFrameRoot, exportedArcherAudit,
                                    $"hero-archer-v{variant}-{octant}-s{state}-f{frame}");
                        }
                        AppendPose320(csv, "player", archetype.ToString(), variant, hero, octant,
                            state, frame, sprite, audit, margins, actor.ActivePrimaryBodyChannelsForQa,
                            height, pose.w, groundDelta, silhouette, passed);
                    }
                }

                if (archetype == UnitArchetype.Archer && hero)
                {
                    var obliqueAimProbes = new[]
                    {
                        (new Vector2(-.14f, 1f), FacingOctant.NorthWest),
                        (new Vector2(.14f, 1f), FacingOctant.NorthEast),
                        (new Vector2(-.14f, -1f), FacingOctant.SouthWest),
                        (new Vector2(.14f, -1f), FacingOctant.SouthEast)
                    };
                    foreach (var probe in obliqueAimProbes)
                    {
                        archerAimChecks++;
                        var actual = actor.PreviewCombatAimForQa(probe.Item1);
                        var originProjection = Vector2.Dot(
                            actor.AttackOriginFor(actor.Position + probe.Item1.normalized * 4f) - actor.Position,
                            EightWayFacing.VectorFor(probe.Item2));
                        var aimPassed = actual == probe.Item2 && originProjection > actor.Radius * .35f;
                        presentationPassed &= aimPassed;
                        if (!aimPassed)
                        {
                            archerAimFailures++;
                            failures.Add($"archer-aim:v{variant}:expected={probe.Item2}:actual={actual}:" +
                                         $"origin={originProjection:0.000}");
                        }
                    }
                }

                var heightRatio = heights.Count == 0
                    ? float.PositiveInfinity
                    : heights.Max() / Mathf.Max(.001f, heights.Min());
                presentationPassed &= heightRatio <= 1.16f;
                var silhouetteStable = true;
                foreach (var octant in directions)
                {
                    var areas = opaqueAreasByDirection[octant];
                    var centres = opaqueCentresByDirection[octant];
                    if (areas.Count == 0 || centres.Count == 0)
                    {
                        silhouetteStable = false;
                        continue;
                    }
                    var sorted = areas.OrderBy(value => value).ToArray();
                    var median = sorted[sorted.Length / 2];
                    var minimum = sorted[0];
                    var centreSpreadX = centres.Max(value => value.x) - centres.Min(value => value.x);
                    var centreSpreadY = centres.Max(value => value.y) - centres.Min(value => value.y);
                    // Horizontal centroid travel is intentionally not bounded: a lance, bow or
                    // staff can swing far across the frame. A cropped body still loses roughly
                    // half its opaque mass and/or jumps vertically, which are the reliable faults.
                    var directionStable = median > .001f && minimum / median >= .52f &&
                                          centreSpreadY <= actor.Radius * 1.08f;
                    silhouetteStable &= directionStable;
                    if (!directionStable)
                    {
                        var poseSamples = silhouettePosesByDirection[octant];
                        var samples = new[]
                        {
                            poseSamples.OrderBy(value => value.area).First(),
                            poseSamples.OrderBy(value => value.centre.x).First(),
                            poseSamples.OrderByDescending(value => value.centre.x).First(),
                            poseSamples.OrderBy(value => value.centre.y).First(),
                            poseSamples.OrderByDescending(value => value.centre.y).First()
                        };
                        foreach (var sample in samples)
                            ExportFailureFrame320(sample.sprite, failureFrameRoot, exportedFailures,
                                $"silhouette-{archetype}-v{variant}-h{hero}-{octant}-" +
                                $"s{sample.state}-f{sample.frame}-a{sample.area:0.000}");
                    }
                }
                presentationPassed &= silhouetteStable;
                var westEastDistinct = OppositeFacingPairIsDistinct320(mirrorSignatures,
                    FacingOctant.West, FacingOctant.East) &&
                    OppositeFacingPairIsDistinct320(mirrorSignatures,
                        FacingOctant.NorthWest, FacingOctant.NorthEast) &&
                    OppositeFacingPairIsDistinct320(mirrorSignatures,
                        FacingOctant.SouthWest, FacingOctant.SouthEast);
                presentationPassed &= westEastDistinct;
                if (!presentationPassed)
                    failures.Add($"player:{archetype}:v{variant}:hero={hero}:height={heightRatio:0.000}:" +
                                 $"silhouette={silhouetteStable}:opposite={westEastDistinct}:" +
                                 string.Join("/", mirrorSignatures.OrderBy(pair => pair.Key)
                                     .Select(pair => $"{pair.Key}={pair.Value}")));
                Destroy(actor.gameObject);
                if (playerPresentations % 5 == 0) yield return null;
            }

            var enemyPresentations = 0;
            foreach (var profile in EnemyVariantCatalog.AllProfiles)
            {
                enemyPresentations++;
                var actor = new GameObject($"QA320 Enemy {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, 0, 900000f, false, 0, profile.CombatClass, profile);
                // Most ordinary monsters intentionally use a front/back body pair plus
                // procedural side articulation instead of a heavyweight 72-cell atlas. Audit
                // the final rendered poses below; requiring an authored atlas would reject that
                // valid presentation path without detecting any visible defect.
                var presentationPassed = actor.ActivePrimaryBodyChannelsForQa == 1;
                var heightsByState = Enumerable.Range(0, 6).Select(_ => new List<float>()).ToArray();
                var scaleByState = Enumerable.Range(0, 6).Select(_ => new List<float>()).ToArray();
                var walkGroundContacts = new List<float>();
                var mirrorSignatures = new Dictionary<FacingOctant, string>();
                foreach (var octant in directions)
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    for (var state = 0; state < 6; state++)
                    for (var frame = 0; frame < phases.Length; frame++)
                    {
                        var pose = actor.PreviewPresentationStateForQa(direction, state, phases[frame]);
                        poseCount++;
                        var sprite = actor.CurrentSpriteForQa;
                        var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                        var audit = SpriteFrameIsolationRegistry.For(sprite);
                        var groundDelta = 0f;
                        if (!actor.IsFlying && profile.CombatClass != EnemyClass.Wisp && state == 1)
                        {
                            var groundContact = actor.PreviewGroundContactForQa(direction, phases[frame]);
                            walkGroundContacts.Add(groundContact);
                            groundDelta = Mathf.Abs(groundContact);
                            pose = actor.PreviewPresentationStateForQa(direction, state, phases[frame]);
                        }
                        var facingCorrect = actor.CurrentBodyFlipXForQa ==
                                            actor.ExpectedBodyFlipForQa(direction);
                        var height = actor.VisualWorldHeight;
                        var passed = sprite != null &&
                                     actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                     actor.VisualOctant == octant &&
                                     actor.CurrentSpriteHasSafeCellMarginForQa &&
                                     actor.CurrentSpriteHasIsolationAuditForQa &&
                                     actor.CurrentSpriteForeignComponentsForQa == 0 &&
                                     !actor.CurrentSpriteHasInternalHorizontalBodySeamForQa &&
                                     facingCorrect &&
                                     actor.CurrentSpriteRenderAspectForQa is > .16f and < 3.4f &&
                                     actor.CurrentSpriteOpaqueAreaForQa > .001f &&
                                     Mathf.Abs(pose.w) is > .20f and < 2.50f;
                        presentationPassed &= passed;
                        if (sprite != null)
                        {
                            uniqueSprites.Add(sprite);
                            heightsByState[state].Add(height);
                            scaleByState[state].Add(Mathf.Abs(pose.w));
                            if (state == 1 && frame == 10)
                                mirrorSignatures[octant] =
                                    $"{sprite.GetInstanceID()}:{actor.VisualSpriteFlippedForQa}";
                            if (!passed) ExportFailureFrame320(sprite, failureFrameRoot, exportedFailures,
                                $"enemy-{profile.Id}-{octant}-s{state}-f{frame}");
                        }
                        AppendPose320(csv, "enemy", profile.Id, 0, false, octant, state, frame,
                            sprite, audit, margins, actor.ActivePrimaryBodyChannelsForQa, height,
                            pose.w, groundDelta, Vector3.zero, passed);
                    }
                }
                var worstHeightRatio = heightsByState.Where(values => values.Count > 0)
                    .Max(values => values.Max() / Mathf.Max(.001f, values.Min()));
                var worstScaleRatio = scaleByState.Where(values => values.Count > 0)
                    .Max(values => values.Max() / Mathf.Max(.001f, values.Min()));
                // A source sheet can need a different internal scale per direction because its
                // transparent canvas and weapon reach differ. The player sees the final opaque
                // world height, which is the stable quantity checked above. Per-pose scale still
                // remains bounded to stop accidental blow-ups.
                presentationPassed &= worstHeightRatio <= 1.36f;
                var groundSpread = walkGroundContacts.Count == 0
                    ? 0f
                    : walkGroundContacts.Max() - walkGroundContacts.Min();
                presentationPassed &= actor.IsFlying || profile.CombatClass == EnemyClass.Wisp ||
                                      groundSpread <= actor.Radius * .006f;
                if (!presentationPassed)
                    failures.Add($"enemy:{profile.Id}:height={worstHeightRatio:0.000}:" +
                                 $"scale={worstScaleRatio:0.000}:ground={groundSpread:0.0000}");
                Destroy(actor.gameObject);
                if (enemyPresentations % 4 == 0) yield return null;
            }

            var bossPresentations = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                bossPresentations++;
                var actor = new GameObject($"QA320 Boss {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                var presentationPassed = actor.ActiveBossArtworkChannelsForQa == 1 &&
                                         actor.HasCompleteDirectionalAnimationForQa &&
                                         actor.HasAuthoredBossDirectionalAnimationForQa;
                var heightsByState = Enumerable.Range(0, 6).Select(_ => new List<float>()).ToArray();
                var walkGroundContacts = new List<float>();
                var mirrorSignatures = new Dictionary<FacingOctant, string>();
                foreach (var octant in directions)
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    for (var state = 0; state < 6; state++)
                    for (var frame = 0; frame < phases.Length; frame++)
                    {
                        var pose = actor.PreviewPresentationStateForQa(direction, state, phases[frame]);
                        poseCount++;
                        var sprite = actor.CurrentSpriteForQa;
                        var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                        var audit = SpriteFrameIsolationRegistry.For(sprite);
                        var height = actor.VisualWorldHeight;
                        var groundDelta = 0f;
                        if (!actor.IsFlying && profile.CombatClass != EnemyClass.Wisp && state == 1)
                        {
                            var groundContact = actor.PreviewGroundContactForQa(direction, phases[frame]);
                            walkGroundContacts.Add(groundContact);
                            groundDelta = Mathf.Abs(groundContact);
                            pose = actor.PreviewPresentationStateForQa(direction, state, phases[frame]);
                        }
                        var facingCorrect = actor.CurrentBodyFlipXForQa ==
                                            actor.ExpectedBodyFlipForQa(direction);
                        var passed = sprite != null &&
                                     actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                     actor.ActiveBossArtworkChannelsForQa == 1 &&
                                     actor.VisualOctant == octant &&
                                     margins.x >= 20f && margins.y >= 20f &&
                                     margins.z >= 20f && margins.w >= 20f &&
                                     actor.CurrentSpriteHasIsolationAuditForQa &&
                                     actor.CurrentSpriteForeignComponentsForQa == 0 &&
                                     !actor.CurrentSpriteHasInternalHorizontalBodySeamForQa &&
                                     facingCorrect &&
                                     actor.CurrentSpriteRenderAspectForQa is > .16f and < 3.4f &&
                                     actor.CurrentSpriteOpaqueAreaForQa > .001f &&
                                     Mathf.Abs(pose.w) is > .20f and < 2.50f;
                        presentationPassed &= passed;
                        if (sprite != null)
                        {
                            uniqueSprites.Add(sprite);
                            heightsByState[state].Add(height);
                            if (state == 1 && frame == 10)
                                mirrorSignatures[octant] =
                                    $"{sprite.GetInstanceID()}:{actor.VisualSpriteFlippedForQa}";
                            if (!passed) ExportFailureFrame320(sprite, failureFrameRoot, exportedFailures,
                                $"boss-{profile.Id}-{octant}-s{state}-f{frame}");
                        }
                        AppendPose320(csv, "boss", profile.Id, 0, false, octant, state, frame,
                            sprite, audit, margins, actor.ActiveBossArtworkChannelsForQa, height,
                            pose.w, groundDelta, Vector3.zero, passed);
                    }
                }
                var worstHeightRatio = heightsByState.Where(values => values.Count > 0)
                    .Max(values => values.Max() / Mathf.Max(.001f, values.Min()));
                presentationPassed &= worstHeightRatio <= 1.48f;
                var groundSpread = walkGroundContacts.Count == 0
                    ? 0f
                    : walkGroundContacts.Max() - walkGroundContacts.Min();
                presentationPassed &= actor.IsFlying || profile.CombatClass == EnemyClass.Wisp ||
                                      groundSpread <= actor.Radius * .006f;
                if (!presentationPassed)
                    failures.Add($"boss:{profile.Id}:height={worstHeightRatio:0.000}:" +
                                 $"ground={groundSpread:0.0000}");
                Destroy(actor.gameObject);
                yield return null;
            }

            if (playerPresentations != 60) failures.Add($"player-count={playerPresentations}/60");
            if (enemyPresentations != EnemyVariantCatalog.AllProfiles.Length)
                failures.Add($"enemy-count={enemyPresentations}/{EnemyVariantCatalog.AllProfiles.Length}");
            if (bossPresentations != 10) failures.Add($"boss-count={bossPresentations}/10");
            // Archer production frames now own only their exact source cell, rather than reading
            // a neighbour and deleting it after the fact. A removal count of zero is therefore
            // valid; the exhaustive per-frame outer-component check above is the release gate.
            if (exportedArcherAudit.Count < 21)
                failures.Add($"archer-live-export:{exportedArcherAudit.Count}/21");
            if (archerAimChecks != 12 || archerAimFailures != 0)
                failures.Add($"archer-oblique-aim:{archerAimChecks}/12:fail={archerAimFailures}");

            File.WriteAllText(Path.Combine(artifactRoot, "all-unit-pose-audit.csv"), csv.ToString(),
                new UTF8Encoding(false));
            var passedAll = failures.Count == 0;
            File.WriteAllText(Path.Combine(artifactRoot, "summary.txt"),
                $"passed={passedAll}\nplayers={playerPresentations}/60\n" +
                $"enemies={enemyPresentations}/{EnemyVariantCatalog.AllProfiles.Length}\n" +
                $"bosses={bossPresentations}/10\nposes={poseCount}\nuniqueSprites={uniqueSprites.Count}\n" +
                $"archerNeighbourRemoved={archerNeighbourFragmentsRemoved}\n" +
                $"archerLiveFrames={exportedArcherAudit.Count}\n" +
                $"archerAim={archerAimChecks}/12:fail={archerAimFailures}\n" +
                $"failures={string.Join(Environment.NewLine, failures)}\n", new UTF8Encoding(false));
            Debug.Log($"QA_ALL_UNIT_POSES_320 passed={passedAll} players={playerPresentations}/60 " +
                      $"enemies={enemyPresentations}/{EnemyVariantCatalog.AllProfiles.Length} " +
                      $"bosses={bossPresentations}/10 poses={poseCount} sprites={uniqueSprites.Count} " +
                      $"archerNeighbourRemoved={archerNeighbourFragmentsRemoved} " +
                      $"archerLiveFrames={exportedArcherAudit.Count} " +
                      $"archerAim={archerAimChecks}/12:fail={archerAimFailures} " +
                      $"fail={string.Join("|", failures.Take(40))}");
            Application.Quit(passedAll ? 0 : 132);
        }

        private static bool ArcherFrameHasNoOuterDetachedComponents320(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return false;
            try
            {
                var pixels = sprite.texture.GetPixels32();
                var width = sprite.texture.width;
                var height = sprite.texture.height;
                var ownedWidth = Mathf.Max(1, width - 48);
                var ownedHeight = Mathf.Max(1, height - 48);
                var audit = KeepPrimarySpriteComponent(pixels, width, height, sprite.pivot,
                    ownedWidth, ownedHeight, strictEdgeOwnership: true,
                    detachedComponentLimitX: .20f, detachedComponentLimitY: .36f);
                return audit.x == 0;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static bool OppositeFacingPairIsDistinct320(
            IReadOnlyDictionary<FacingOctant, string> signatures, FacingOctant first, FacingOctant second) =>
            signatures.TryGetValue(first, out var a) && signatures.TryGetValue(second, out var b) &&
            !string.Equals(a, b, StringComparison.Ordinal);

        private static Vector3 SpriteSilhouette320(Sprite sprite, IDictionary<Sprite, Vector3> cache)
        {
            if (sprite == null || sprite.pixelsPerUnit <= .01f) return Vector3.zero;
            if (cache.TryGetValue(sprite, out var cached)) return cached;
            if (PlayerUnit.TryGetRegisteredSilhouetteForQa(sprite, out var registered))
            {
                cache[sprite] = registered;
                return registered;
            }
            var rect = sprite.textureRect;
            var pixels = sprite.texture.GetPixels32();
            var textureWidth = sprite.texture.width;
            var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, textureWidth - 1);
            var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, textureWidth);
            var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, sprite.texture.height - 1);
            var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, sprite.texture.height);
            var count = 0;
            var sumX = 0f;
            var sumY = 0f;
            for (var y = bottom; y < top; y++)
            for (var x = left; x < right; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                count++;
                sumX += x + .5f - rect.xMin;
                sumY += y + .5f - rect.yMin;
            }
            if (count == 0) return Vector3.zero;
            var area = count / (sprite.pixelsPerUnit * sprite.pixelsPerUnit);
            var opaqueCenterX = sumX / count;
            var opaqueCenterY = sumY / count;
            var localCenterX = (opaqueCenterX - sprite.pivot.x) / sprite.pixelsPerUnit;
            var localCenterY = (opaqueCenterY - sprite.pivot.y) / sprite.pixelsPerUnit;
            var result = new Vector3(area, localCenterX, localCenterY);
            cache[sprite] = result;
            return result;
        }

        private static void AppendPose320(StringBuilder csv, string category, string id, int variant,
            bool hero, FacingOctant direction, int state, int frame, Sprite sprite,
            SpriteFrameIsolationRegistry.Audit audit, Vector4 margins, int channels,
            float height, float scale, float groundFailure, Vector3 silhouette, bool passed)
        {
            static string Q(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
            static string N(float value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
            csv.Append(Q(category)).Append(',').Append(Q(id)).Append(',').Append(variant).Append(',')
                .Append(hero ? 1 : 0).Append(',').Append(Q(direction.ToString())).Append(',')
                .Append(state).Append(',').Append(frame).Append(',')
                .Append(Q(sprite != null ? sprite.name : "<null>")).Append(',')
                .Append(Q(audit.Source)).Append(',').Append(N(margins.x)).Append(',')
                .Append(N(margins.y)).Append(',').Append(N(margins.z)).Append(',')
                .Append(N(margins.w)).Append(',').Append(audit.RemainingForeignComponents).Append(',')
                .Append(audit.BodySeamClosed ? 1 : 0).Append(',').Append(channels).Append(',')
                .Append(N(height)).Append(',').Append(N(scale)).Append(',')
                .Append(N(groundFailure)).Append(',').Append(N(silhouette.x)).Append(',')
                .Append(N(silhouette.y)).Append(',').Append(N(silhouette.z)).Append(',')
                .Append(passed ? 1 : 0).AppendLine();
        }

        private static void ExportFailureFrame320(Sprite sprite, string root, ISet<Sprite> exported,
            string fileName)
        {
            if (sprite == null || !exported.Add(sprite)) return;
            try
            {
                ExportSpriteForQa263(sprite, Path.Combine(root, SanitizeFileName320(fileName) + ".bmp"));
            }
            catch (Exception)
            {
                // The CSV still contains every failed pose when a GPU-only texture cannot be read.
            }
        }

        private static string SanitizeFileName320(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }
    }
}
