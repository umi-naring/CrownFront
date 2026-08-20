using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private static readonly Vector2[] TankMovementDirections267 =
            Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();

        private IEnumerator QaTankMovement267Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Preparation;

            var failures = new List<string>();
            var originalSkin = monetization.EquippedUnitSkin(UnitArchetype.Tank);
            var products = monetization.Products
                .Where(product => product.Category == ShopCategory.Unit &&
                                  product.TargetUnit == UnitArchetype.Tank)
                .OrderBy(product => product.Id).ToArray();
            var metricMissesBefore = PlayerUnit.OpaqueMetricCacheMisses;
            var totalPoses = 0;
            var totalLiveActors = 0;
            var maxPathMilliseconds = 0d;
            var startedAt = Time.realtimeSinceStartup;
            var startedFrame = Time.frameCount;

            try
            {
                for (var variant = 0; variant <= 2; variant++)
                {
                    if (variant == 0) monetization.EquipDefault(ShopCategory.Unit, UnitArchetype.Tank);
                    else
                    {
                        var product = products.FirstOrDefault(item => item.Id.EndsWith(
                            variant == 1 ? ".a" : ".b", StringComparison.Ordinal));
                        if (product == null)
                        {
                            failures.Add($"missing-skin-v{variant}");
                            continue;
                        }
                        monetization.GrantForQa(product.Id);
                        monetization.Equip(product);
                    }

                    for (var hero = 0; hero <= 1; hero++)
                    {
                        var actors = new List<PlayerUnit>();
                        var starts = new List<Vector2>();
                        var targets = new List<Vector2>();
                        for (var index = 0; index < TankMovementDirections267.Length; index++)
                        {
                            // Overlap is intentional: player units have no body collision, and a
                            // shared central-plaza origin keeps every direction equally far from
                            // curved road shoulders. This measures motion rather than map snapping.
                            var start = NearestWalkable(new Vector2(0f, -6.18f), .2f);
                            var actor = new GameObject($"QA 267 Tank v{variant} h{hero} d{index}")
                                .AddComponent<PlayerUnit>();
                            actor.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank], start);
                            if (hero == 1) actor.RestoreCheckpointState(5, 9999f, actor.MaxHealth,
                                false, 0f, 0f);

                            var uniqueFrames = new HashSet<int>();
                            foreach (var direction in TankMovementDirections267)
                            for (var frame = 0; frame < 64; frame++)
                            {
                                actor.PreviewMotionPoseForQa(direction, 0, (frame + .19f) / 64f);
                                totalPoses++;
                                uniqueFrames.Add(actor.CurrentSpriteIdForQa);
                                if (!actor.CurrentSpriteMetricsReadyForQa)
                                    failures.Add($"uncached:v{variant}:h{hero}:d{direction}:f{frame}");
                            }
                            if (!actor.HasCompleteDirectionalAnimation || actor.DirectionScaleSpread > .001f ||
                                uniqueFrames.Count < 10)
                                failures.Add($"rig:v{variant}:h{hero}:frames={uniqueFrames.Count}:" +
                                             $"spread={actor.DirectionScaleSpread:0.000}");

                            var directionForActor = TankMovementDirections267[index];
                            var target = NearestWalkableOnSameTerrain(
                                start + directionForActor * .58f, start, actor.Radius * .55f);
                            actors.Add(actor);
                            starts.Add(start);
                            targets.Add(target);
                        }

                        var pathClock = System.Diagnostics.Stopwatch.StartNew();
                        for (var index = 0; index < actors.Count; index++) actors[index].MoveTo(targets[index]);
                        pathClock.Stop();
                        maxPathMilliseconds = Math.Max(maxPathMilliseconds,
                            pathClock.Elapsed.TotalMilliseconds / Mathf.Max(1, actors.Count));
                        yield return new WaitForSecondsRealtime(1.25f);

                        for (var index = 0; index < actors.Count; index++)
                        {
                            var actor = actors[index];
                            totalLiveActors++;
                            var expectedTravel = Vector2.Distance(starts[index], targets[index]);
                            var actualTravel = Vector2.Distance(starts[index], actor.Position);
                            if (expectedTravel > .25f && actualTravel < expectedTravel * .72f)
                                failures.Add($"travel:v{variant}:h{hero}:d{index}:" +
                                             $"{actualTravel:0.00}/{expectedTravel:0.00}");
                            if (!actor.CurrentSpriteMetricsReadyForQa)
                                failures.Add($"live-cache:v{variant}:h{hero}:d{index}");
                            // A 0.68-unit march is shorter than one complete tank gait. More than
                            // two puffs means the old five-spawns-per-cycle allocation path returned.
                            if (actor.MovementDustSpawnsForQa > 2)
                                failures.Add($"dust:v{variant}:h{hero}:d{index}:" +
                                             actor.MovementDustSpawnsForQa);
                            Destroy(actor.gameObject);
                        }
                        yield return null;
                    }
                }
            }
            finally
            {
                if (string.IsNullOrEmpty(originalSkin))
                    monetization.EquipDefault(ShopCategory.Unit, UnitArchetype.Tank);
                else
                {
                    var originalProduct = monetization.FindProduct(originalSkin);
                    if (originalProduct != null)
                    {
                        monetization.GrantForQa(originalProduct.Id);
                        monetization.Equip(originalProduct);
                    }
                }
            }

            var elapsed = Mathf.Max(.001f, Time.realtimeSinceStartup - startedAt);
            var averageFps = (Time.frameCount - startedFrame) / elapsed;
            var metricMisses = PlayerUnit.OpaqueMetricCacheMisses - metricMissesBefore;
            if (metricMisses != 0) failures.Add($"metric-misses={metricMisses}");
            if (playerSpriteMetricsPrimed < 100) failures.Add($"prime={playerSpriteMetricsPrimed}");
            if (averageFps < 45f) failures.Add($"fps={averageFps:0.0}");
            if (maxPathMilliseconds > 12f) failures.Add($"path-ms={maxPathMilliseconds:0.00}");

            var passed = failures.Count == 0 && totalPoses == 24576 && totalLiveActors == 48;
            Debug.Log($"QA_TANK_MOVEMENT_267 passed={passed} poses={totalPoses} actors={totalLiveActors} " +
                      $"metricMisses={metricMisses} primed={playerSpriteMetricsPrimed} " +
                      $"fps={averageFps:0.0} pathMs={maxPathMilliseconds:0.00} " +
                      $"fail={string.Join(",", failures.Take(60))}");
            Application.Quit(passed ? 0 : 68);
        }
    }
}
