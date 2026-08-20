using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private static readonly FacingOctant[] QaOctants269 =
            Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>().ToArray();

        private IEnumerator QaEnemyPresentation269Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            yield return PrewarmBossPresentations();

            var failures = new List<string>();
            var regularPoses = 0;
            var bossPoses = 0;
            var transitionChecks = 0;
            var profiles = EnemyVariantCatalog.AllProfiles;
            for (var profileIndex = 0; profileIndex < profiles.Length; profileIndex++)
            {
                var profile = profiles[profileIndex];
                Round = Mathf.Clamp(profileIndex + 1, 1, 50);
                var actor = new GameObject($"QA269 Enemy {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, profileIndex % 10, 100000f, false, profileIndex % 6,
                    profile.CombatClass, profile);
                AuditActor269(actor, profile.Id, false, failures, ref regularPoses, ref transitionChecks);
                Destroy(actor.gameObject);
            }

            for (var chapter = 0; chapter < 10; chapter++)
            {
                Round = (chapter + 1) * 5;
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA269 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                AuditActor269(boss, profile.Id, true, failures, ref bossPoses, ref transitionChecks);
                Destroy(boss.gameObject);
            }

            var expectedRegular = profiles.Length * 8 * 6 * 24;
            var expectedBoss = 10 * 8 * 6 * 24;
            var expectedTransitions = (profiles.Length + 10) * 8 * 5;
            var passed = failures.Count == 0 && regularPoses == expectedRegular &&
                         bossPoses == expectedBoss && transitionChecks == expectedTransitions;
            Debug.Log($"QA_ENEMY_PRESENTATION_269 passed={passed} profiles={profiles.Length} " +
                      $"regularPoses={regularPoses}/{expectedRegular} " +
                      $"bossPoses={bossPoses}/{expectedBoss} " +
                      $"transitions={transitionChecks}/{expectedTransitions} " +
                      $"fail={string.Join(",", failures.Take(120))}");
            Application.Quit(passed ? 0 : 129);
        }

        private static void AuditActor269(EnemyUnit actor, string id, bool boss,
            ICollection<string> failures, ref int poseCount, ref int transitionChecks)
        {
            foreach (var octant in QaOctants269)
            {
                var direction = EightWayFacing.VectorFor(octant);
                var stateHeights = new float[6][];
                for (var state = 0; state < 6; state++)
                {
                    stateHeights[state] = new float[24];
                    for (var frame = 0; frame < 24; frame++)
                    {
                        actor.PreviewPresentationStateForQa(direction, state, (frame + .17f) / 24f);
                        poseCount++;
                        var sprite = actor.CurrentSpriteForQa;
                        var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                        stateHeights[state][frame] = actor.VisualWorldHeight;
                        var safe = sprite != null && actor.ActivePrimaryBodyChannelsForQa == 1 &&
                                   actor.CurrentSpriteHasIsolationAuditForQa &&
                                   actor.CurrentSpriteForeignComponentsForQa == 0 &&
                                   actor.CurrentSpriteHasSafeCellMarginForQa &&
                                   margins.x >= (boss ? 28f : 10f) &&
                                   margins.y >= (boss ? 28f : 10f) &&
                                   margins.z >= (boss ? 28f : 10f) &&
                                   margins.w >= (boss ? 28f : 10f) &&
                                   actor.VisualOctant == octant &&
                                   actor.CurrentBodyFlipXForQa == actor.ExpectedBodyFlipForQa(direction);
                        if (safe) continue;
                        if (failures.Count < 120)
                            failures.Add($"frame:{id}:{octant}:s{state}:f{frame}:" +
                                         $"m={margins}:foreign={actor.CurrentSpriteForeignComponentsForQa}:" +
                                         $"parts={actor.CurrentSpriteSignificantComponentsForQa}:" +
                                         $"face={actor.VisualOctant}:flip={actor.CurrentBodyFlipXForQa}/" +
                                         $"{actor.ExpectedBodyFlipForQa(direction)}");
                    }
                    var min = stateHeights[state].Where(value => value > .001f).DefaultIfEmpty(0f).Min();
                    var max = stateHeights[state].DefaultIfEmpty(0f).Max();
                    if (min <= .001f || max / min > (boss ? 1.13f : 1.16f))
                        if (failures.Count < 120)
                            failures.Add($"scale:{id}:{octant}:s{state}:{min:0.000}-{max:0.000}");
                }

                // Exercise the exact transition boundaries where a stale sprite or direction is
                // most likely to survive: idle->walk, walk->attack, attack->skill, skill->hit,
                // hit->stunned. Every transition must retain one audited body and target facing.
                var transitionStates = new[] { 0, 1, 2, 3, 4, 5 };
                for (var transition = 0; transition < transitionStates.Length - 1; transition++)
                {
                    actor.PreviewPresentationStateForQa(direction, transitionStates[transition], .985f);
                    actor.PreviewPresentationStateForQa(direction, transitionStates[transition + 1], .015f);
                    transitionChecks++;
                    var locked = actor.PreviewTargetLockForQa(direction);
                    if (locked != octant || actor.ActivePrimaryBodyChannelsForQa != 1 ||
                        !actor.CurrentSpriteHasIsolationAuditForQa ||
                        actor.CurrentSpriteForeignComponentsForQa != 0)
                        if (failures.Count < 120)
                            failures.Add($"transition:{id}:{octant}:{transition}:lock={locked}");
                }
            }
        }
    }
}
