using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaEnemyPursuit277Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            GameLocalization.Current = GameLanguage.Korean;
            Time.timeScale = 8f;
            Phase = GamePhase.Battle;
            Round = 1;

            var failures = new List<string>();
            var profiles = EnemyVariantCatalog.AllProfiles;
            var reachableChecks = 0;
            var reachablePassed = 0;
            var unreachableChecks = 0;
            var unreachablePassed = 0;
            var movementFacingSamples = 0;
            var movementFacingPassed = 0;
            var oppositeTurnCount = 0;
            var oscillationCount = 0;
            var corridorChecks = 0;
            var corridorPassed = 0;

            for (var profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
            {
                var profile = profiles[profileIndex];
                var laneIndex = profileIndex % Mathf.Max(1, paths.Count);
                var enemy = new GameObject($"QA277 Pursuit {profile.Id}").AddComponent<EnemyUnit>();
                enemy.Initialize(this, profileIndex, 900000f, false, laneIndex,
                    profile.CombatClass, profile);
                enemies.Add(enemy);

                if (!FindReachablePursuitScenario277(enemy, laneIndex, out var start, out var targetPoint,
                        out var curved))
                {
                    failures.Add($"scenario:{profile.Id}");
                    RemoveQaEnemy277(enemy);
                    continue;
                }

                enemy.ForcePositionForQa(start);
                enemy.ForcePathReanchorForQa();
                var defender = CreateQaPursuitDefender277($"Reachable {profile.Id}", targetPoint);
                var startDistance = Vector2.Distance(enemy.Position, defender.Position);
                var startPosition = enemy.Position;
                var previousFacing = Vector2.zero;
                var twoFramesBackFacing = Vector2.zero;
                var localOppositeTurns = 0;
                var localOscillations = 0;
                var localFacingSamples = 0;
                var localFacingPassed = 0;
                // Let ForcePosition and the freshly acquired target settle through one complete
                // Update/LateUpdate pair. The old pose before the scenario is not a pursuit frame.
                yield return null;
                var deadline = Time.realtimeSinceStartup + .22f;
                while (Time.realtimeSinceStartup < deadline && enemy != null && enemy.IsAlive)
                {
                    yield return null;
                    if (enemy.VelocityForQa.sqrMagnitude <= .0025f) continue;
                    enemy.RefreshVisualMotionForQa();
                    localFacingSamples++;
                    var velocity = enemy.VelocityForQa.normalized;
                    var facing = enemy.VisualFacingDirectionForQa.normalized;
                    if (Vector2.Dot(velocity, facing) >= .2f) localFacingPassed++;
                    if (previousFacing.sqrMagnitude > .2f && Vector2.Dot(previousFacing, facing) < -.55f)
                        localOppositeTurns++;
                    if (twoFramesBackFacing.sqrMagnitude > .2f && previousFacing.sqrMagnitude > .2f &&
                        Vector2.Dot(previousFacing, facing) < -.55f &&
                        Vector2.Dot(twoFramesBackFacing, facing) > .65f)
                        localOscillations++;
                    twoFramesBackFacing = previousFacing;
                    previousFacing = facing;
                }
                movementFacingSamples += localFacingSamples;
                movementFacingPassed += localFacingPassed;
                oppositeTurnCount += localOppositeTurns;
                oscillationCount += localOscillations;
                reachableChecks++;
                if (curved) corridorChecks++;
                var endDistance = Vector2.Distance(enemy.Position, defender.Position);
                var progressed = Vector2.Distance(startPosition, enemy.Position) > .06f ||
                                 endDistance + .05f < startDistance || enemy.IsEngagingDefender;
                var facingStable = localOscillations == 0 &&
                                   (localFacingSamples == 0 || localFacingPassed >= localFacingSamples * .90f);
                if (progressed && facingStable)
                {
                    reachablePassed++;
                    if (curved && enemy.CorridorPursuitStepCountForQa > 0) corridorPassed++;
                }
                else failures.Add($"reachable:{profile.Id}:{startDistance:0.00}->{endDistance:0.00}:" +
                                  $"move={Vector2.Distance(startPosition, enemy.Position):0.00}:" +
                                  $"face={localFacingPassed}/{localFacingSamples}:turn={localOppositeTurns}:" +
                                  $"osc={localOscillations}");

                RemoveQaDefender277(defender);
                RemoveQaEnemy277(enemy);

                if (profile.FamilyClass == EnemyClass.Flyer || profile.CombatClass == EnemyClass.Flyer)
                    continue;

                enemy = new GameObject($"QA277 Reject {profile.Id}").AddComponent<EnemyUnit>();
                enemy.Initialize(this, profileIndex, 900000f, false, laneIndex,
                    profile.CombatClass, profile);
                enemies.Add(enemy);
                if (!FindUnreachablePursuitScenario277(enemy, laneIndex, out start, out targetPoint))
                {
                    failures.Add($"unreachable-scenario:{profile.Id}");
                    RemoveQaEnemy277(enemy);
                    continue;
                }
                enemy.ForcePositionForQa(start);
                enemy.ForcePathReanchorForQa();
                defender = CreateQaPursuitDefender277($"Unreachable {profile.Id}", targetPoint);
                var rejectedAtStart = !enemy.CanAcquireDetectedTarget(defender);
                enemy.ForceDetectedTargetForQa(defender);
                startPosition = enemy.Position;
                previousFacing = Vector2.zero;
                twoFramesBackFacing = Vector2.zero;
                localOppositeTurns = 0;
                localOscillations = 0;
                localFacingSamples = 0;
                localFacingPassed = 0;
                yield return null;
                deadline = Time.realtimeSinceStartup + .18f;
                while (Time.realtimeSinceStartup < deadline && enemy != null && enemy.IsAlive)
                {
                    yield return null;
                    if (enemy.VelocityForQa.sqrMagnitude <= .0025f) continue;
                    enemy.RefreshVisualMotionForQa();
                    localFacingSamples++;
                    var velocity = enemy.VelocityForQa.normalized;
                    var facing = enemy.VisualFacingDirectionForQa.normalized;
                    if (Vector2.Dot(velocity, facing) >= .2f) localFacingPassed++;
                    if (previousFacing.sqrMagnitude > .2f && Vector2.Dot(previousFacing, facing) < -.55f)
                        localOppositeTurns++;
                    if (twoFramesBackFacing.sqrMagnitude > .2f && previousFacing.sqrMagnitude > .2f &&
                        Vector2.Dot(previousFacing, facing) < -.55f &&
                        Vector2.Dot(twoFramesBackFacing, facing) > .65f)
                        localOscillations++;
                    twoFramesBackFacing = previousFacing;
                    previousFacing = facing;
                }
                unreachableChecks++;
                var rejected = rejectedAtStart && enemy.UnreachableTargetRejectCountForQa > 0 &&
                               enemy.DetectedTargetForQa != defender;
                var resumedLane = Vector2.Distance(startPosition, enemy.Position) > .035f;
                var noSpin = localOscillations == 0 &&
                             (localFacingSamples == 0 || localFacingPassed >= localFacingSamples * .90f);
                if (rejected && resumedLane && noSpin) unreachablePassed++;
                else failures.Add($"reject:{profile.Id}:pre={rejectedAtStart}:" +
                                  $"count={enemy.UnreachableTargetRejectCountForQa}:move=" +
                                  $"{Vector2.Distance(startPosition, enemy.Position):0.00}:" +
                                  $"face={localFacingPassed}/{localFacingSamples}:turn={localOppositeTurns}:" +
                                  $"osc={localOscillations}");
                movementFacingSamples += localFacingSamples;
                movementFacingPassed += localFacingPassed;
                oppositeTurnCount += localOppositeTurns;
                oscillationCount += localOscillations;
                RemoveQaDefender277(defender);
                RemoveQaEnemy277(enemy);
            }

            // Actual boss entrance behaviour has a different route and formation offset. Run
            // every chapter boss on that live path, with no defender, to catch reverse-facing
            // frames or portal/route oscillation that a regular profile test cannot exercise.
            var bossChecks = 0;
            var bossPassed = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA277 Boss Route {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter, 9000000f, true, 0, profile.CombatClass, profile);
                boss.ConfigureBossEntrance((chapter % 3 - 1) * .18f, 0f);
                enemies.Add(boss);
                var start = boss.Position;
                var previousFacing = boss.VisualFacingDirectionForQa;
                var backwards = 0;
                var samples = 0;
                var deadline = Time.realtimeSinceStartup + .18f;
                while (Time.realtimeSinceStartup < deadline && boss != null && boss.IsAlive)
                {
                    yield return null;
                    if (boss.VelocityForQa.sqrMagnitude <= .0025f) continue;
                    boss.RefreshVisualMotionForQa();
                    samples++;
                    var facing = boss.VisualFacingDirectionForQa.normalized;
                    if (Vector2.Dot(facing, boss.VelocityForQa.normalized) < .2f) backwards++;
                    if (previousFacing.sqrMagnitude > .2f && Vector2.Dot(previousFacing, facing) < -.55f)
                        backwards++;
                    previousFacing = facing;
                }
                bossChecks++;
                if (Vector2.Distance(start, boss.Position) > .04f && samples > 0 && backwards == 0)
                    bossPassed++;
                else failures.Add($"boss-route:{profile.Id}:move={Vector2.Distance(start, boss.Position):0.00}:" +
                                  $"samples={samples}:back={backwards}");
                RemoveQaEnemy277(boss);
            }

            Time.timeScale = 1f;
            Phase = GamePhase.Preparation;
            var facingRatio = movementFacingSamples == 0 ? 0f :
                movementFacingPassed / (float)movementFacingSamples;
            var passed = failures.Count == 0 && reachablePassed == reachableChecks &&
                         unreachablePassed == unreachableChecks && bossPassed == bossChecks &&
                         corridorPassed == corridorChecks && facingRatio >= .90f && oscillationCount == 0;
            Debug.Log($"QA_ENEMY_PURSUIT_277 passed={passed} profiles={profiles.Length} " +
                      $"reachable={reachablePassed}/{reachableChecks} " +
                      $"unreachable={unreachablePassed}/{unreachableChecks} " +
                      $"corridor={corridorPassed}/{corridorChecks} boss={bossPassed}/{bossChecks} " +
                      $"facing={movementFacingPassed}/{movementFacingSamples}:{facingRatio:0.000} " +
                      $"opposite={oppositeTurnCount} oscillation={oscillationCount} " +
                      $"fail={string.Join(",", failures.Take(80))}");
            Application.Quit(passed ? 0 : 137);
        }

        private bool FindReachablePursuitScenario277(EnemyUnit enemy, int preferredLane,
            out Vector2 start, out Vector2 target, out bool curved)
        {
            start = target = Vector2.zero;
            curved = false;
            if (enemy == null || paths.Count == 0) return false;
            var clearance = enemy.Radius * .42f;
            // The enemy remains assigned to preferredLane. Sampling a bend from another lane
            // creates a false "reachable" case that no live acquisition could ever accept.
            // Exercise only the unit's authored route, exactly as gameplay does.
            var lane = paths[Mathf.Clamp(preferredLane, 0, paths.Count - 1)];
            for (var lanePass = 0; lanePass < 1; lanePass++)
            {
                for (var fromIndex = 2; fromIndex < Mathf.Min(lane.Count - 4, 62); fromIndex += 2)
                for (var toIndex = fromIndex + 2; toIndex < Mathf.Min(lane.Count, fromIndex + 72); toIndex++)
                {
                    var from = lane[fromIndex];
                    var to = lane[toIndex];
                    var distance = Vector2.Distance(from, to);
                    if (distance <= enemy.AttackRange + .16f || distance >= enemy.DetectionRange * .92f)
                        continue;
                    var direction = (to - from).normalized;
                    var attackPoint = from + direction * Mathf.Max(.04f,
                        distance - Mathf.Max(.08f, enemy.AttackRange - .035f));
                    var direct = enemy.IsFlying || CanTraverseGroundEnemy(from, attackPoint, clearance);
                    start = from;
                    target = to;
                    curved = !direct;
                    if (curved) return true;
                }
            }

            // Some long-range classes can see around every local bend. They still receive a
            // valid straight pursuit scenario so every profile is exercised in the live loop.
            var fallbackLane = paths[Mathf.Clamp(preferredLane, 0, paths.Count - 1)];
            for (var fromIndex = 2; fromIndex < fallbackLane.Count - 3; fromIndex++)
            for (var toIndex = fromIndex + 2; toIndex < Mathf.Min(fallbackLane.Count, fromIndex + 64); toIndex++)
            {
                var distance = Vector2.Distance(fallbackLane[fromIndex], fallbackLane[toIndex]);
                if (distance <= enemy.AttackRange + .16f || distance >= enemy.DetectionRange * .9f) continue;
                start = fallbackLane[fromIndex];
                target = fallbackLane[toIndex];
                return true;
            }
            return false;
        }

        private bool FindUnreachablePursuitScenario277(EnemyUnit enemy, int preferredLane,
            out Vector2 start, out Vector2 target)
        {
            start = target = Vector2.zero;
            if (enemy == null || paths.Count == 0) return false;
            var lane = paths[Mathf.Clamp(preferredLane, 0, paths.Count - 1)];
            // Put the lure near the outer detection rim.  A point merely outside the road can
            // still be legally attacked from its shoulder and is therefore not an unreachable
            // case.  The outer rim guarantees the legal release point itself would cross the
            // terrain mask, which is the exact edge condition that used to trigger spinning.
            var offset = Mathf.Lerp(enemy.AttackRange, enemy.DetectionRange, .9f);
            for (var index = 4; index < lane.Count - 4; index += 2)
            {
                var tangent = (lane[index + 1] - lane[index - 1]).normalized;
                if (tangent.sqrMagnitude <= .001f) continue;
                var normal = new Vector2(-tangent.y, tangent.x);
                foreach (var sign in new[] { -1f, 1f })
                {
                    var candidate = lane[index] + normal * (offset * sign);
                    if (Mathf.Abs(candidate.x) > PlayableHalfWidth - .25f ||
                        Mathf.Abs(candidate.y) > PlayableHalfHeight - .25f ||
                        IsWithinGroundEnemyRoadCorridor(candidate, enemy.Radius * .42f)) continue;
                    var directDirection = (candidate - lane[index]).normalized;
                    var directRelease = lane[index] + directDirection * Mathf.Max(.04f,
                        Vector2.Distance(lane[index], candidate) -
                        Mathf.Max(.08f, enemy.AttackRange - .035f));
                    if (CanTraverseGroundEnemy(lane[index], directRelease, enemy.Radius * .42f))
                        continue;
                    var reachableReleasePoint = false;
                    var reach = Mathf.Max(.08f, enemy.AttackRange - .035f);
                    for (var pathIndex = 0; pathIndex < lane.Count; pathIndex++)
                    {
                        if (Vector2.Distance(lane[pathIndex], candidate) > reach) continue;
                        reachableReleasePoint = true;
                        break;
                    }
                    if (reachableReleasePoint) continue;
                    start = lane[index];
                    target = candidate;
                    return true;
                }
            }
            return false;
        }

        private PlayerUnit CreateQaPursuitDefender277(string label, Vector2 position)
        {
            var defender = new GameObject($"QA277 {label}").AddComponent<PlayerUnit>();
            defender.Initialize(this, UnitArchetype.Tank, definitions[UnitArchetype.Tank], position);
            defender.SetInvulnerableForQa(true);
            units.Add(defender);
            return defender;
        }

        private void RemoveQaDefender277(PlayerUnit defender)
        {
            if (defender == null) return;
            units.Remove(defender);
            Destroy(defender.gameObject);
        }

        private void RemoveQaEnemy277(EnemyUnit enemy)
        {
            if (enemy == null) return;
            enemies.Remove(enemy);
            Destroy(enemy.gameObject);
        }
    }
}
