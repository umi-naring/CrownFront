using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaBossGrounding278Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            yield return PrewarmBossPresentations();

            var failures = new List<string>();
            var sourceFrames = 0;
            var runtimePoses = 0;
            var sidePairs = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                Round = (chapter + 1) * 5;
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var set = EnsureBossDirectionalAnimation(profile.Id);
                if (set == null)
                {
                    failures.Add($"set-missing:{profile.Id}");
                    continue;
                }

                var rows = new[] { set.Down, set.DownDiagonal, set.Side, set.UpDiagonal, set.Up };
                var downHeight = rows[0].Where(sprite => sprite != null)
                    .Select(EnemyUnit.SpriteOpaqueHeightForQa).DefaultIfEmpty(0f).OrderBy(value => value).ToArray();
                var downReference = downHeight.Length == 0 ? 0f : downHeight[downHeight.Length / 2];
                for (var row = 0; row < rows.Length; row++)
                {
                    var frames = rows[row] ?? Array.Empty<Sprite>();
                    if (frames.Length < 72) failures.Add($"timeline:{profile.Id}:r{row}:{frames.Length}");
                    foreach (var sprite in frames.Distinct())
                    {
                        sourceFrames++;
                        var textureName = sprite != null && sprite.texture != null ? sprite.texture.name : string.Empty;
                        if (sprite == null || row is > 0 and < 4 &&
                            !textureName.Contains($"-isolated-r{row}-",
                                StringComparison.OrdinalIgnoreCase))
                            AddBoss278Failure(failures, $"wrong-row:{profile.Id}:r{row}:{textureName}");
                        if (!EnemyUnit.SpriteBodySeamClosedForQa(sprite))
                            AddBoss278Failure(failures, $"body-seam:{profile.Id}:r{row}:{textureName}");
                        var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                        if (margins.x < 28f || margins.y < 28f || margins.z < 28f || margins.w < 28f)
                            AddBoss278Failure(failures, $"margin:{profile.Id}:r{row}:{margins}");
                    }
                    // Walk frames establish the actor's standing anatomy. Attack/skill poses may
                    // legitimately crouch, coil or fold wings and are checked by the stable-scale
                    // runtime matrix instead. This standing-height gate is what catches a side row
                    // containing only an Orc torso while permitting a dragon's authored dive.
                    if (row is > 0 and < 4 && downReference > .001f)
                    foreach (var sprite in frames.Take(24).Distinct())
                    {
                        var frameHeight = EnemyUnit.SpriteOpaqueHeightForQa(sprite);
                        if (frameHeight >= downReference * .52f) continue;
                        AddBoss278Failure(failures,
                            $"incomplete-walk-side:{profile.Id}:r{row}:{frameHeight:0.000}/{downReference:0.000}:" +
                            $"{sprite?.texture?.name}");
                    }
                }

                // East and west must both resolve to the authored side row. Mirroring the same
                // painted side view is intentional, but the renderer flip must be opposite.
                var boss = new GameObject($"QA278 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                bool? westFlip = null;
                bool? eastFlip = null;
                foreach (FacingOctant octant in Enum.GetValues(typeof(FacingOctant)))
                {
                    var direction = EightWayFacing.VectorFor(octant);
                    var expectedRow = octant switch
                    {
                        FacingOctant.South => 0,
                        FacingOctant.SouthWest or FacingOctant.SouthEast => 1,
                        FacingOctant.West or FacingOctant.East => 2,
                        FacingOctant.NorthWest or FacingOctant.NorthEast => 3,
                        _ => 4
                    };
                    for (var state = 1; state <= 3; state++)
                    for (var frame = 0; frame < 24; frame++)
                    {
                        boss.PreviewPresentationStateForQa(direction, state, (frame + .19f) / 24f);
                        runtimePoses++;
                        var textureName = boss.CurrentFrameTextureNameForQa;
                        if (expectedRow is > 0 and < 4 &&
                            !textureName.Contains($"-isolated-r{expectedRow}-",
                                StringComparison.OrdinalIgnoreCase))
                            AddBoss278Failure(failures,
                                $"runtime-row:{profile.Id}:{octant}:s{state}:f{frame}:{textureName}");
                        if (boss.CurrentSpriteHasInternalHorizontalBodySeamForQa)
                            AddBoss278Failure(failures,
                                $"runtime-seam:{profile.Id}:{octant}:s{state}:f{frame}");
                        var gap = Mathf.Abs(boss.CurrentGroundContactLocalYForQa - boss.ShadowLocalYForQa);
                        var allowed = boss.IsFlying ? boss.Radius * .09f : boss.Radius * .015f;
                        if (float.IsNaN(gap) || float.IsInfinity(gap) || gap > allowed + .002f)
                            AddBoss278Failure(failures,
                                $"ground:{profile.Id}:{octant}:s{state}:f{frame}:{gap:0.000}/{allowed:0.000}");
                    }
                    if (octant == FacingOctant.West) westFlip = boss.CurrentBodyFlipXForQa;
                    if (octant == FacingOctant.East) eastFlip = boss.CurrentBodyFlipXForQa;
                }
                sidePairs++;
                if (!westFlip.HasValue || !eastFlip.HasValue || westFlip.Value == eastFlip.Value)
                    AddBoss278Failure(failures, $"side-flip:{profile.Id}:{westFlip}/{eastFlip}");
                Destroy(boss.gameObject);
            }

            const int expectedPoses = 10 * 8 * 3 * 24;
            var passed = failures.Count == 0 && runtimePoses == expectedPoses && sidePairs == 10;
            Debug.Log($"QA_BOSS_GROUNDING_278 passed={passed} sourceFrames={sourceFrames} " +
                      $"runtimePoses={runtimePoses}/{expectedPoses} sidePairs={sidePairs}/10 " +
                      $"fail={string.Join(",", failures.Take(160))}");
            Application.Quit(passed ? 0 : 138);
        }

        private static void AddBoss278Failure(ICollection<string> failures, string failure)
        {
            if (failures.Count < 160) failures.Add(failure);
        }
    }
}
