using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private void DrawQaBossGallery259()
        {
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(.008f, .014f, .03f, 1f));
            GUI.Label(new Rect(0f, 5f, GuiWidth, 30f), "BOSS FRAME ISOLATION · v2.59",
                modalTitleStyle);
            const float margin = 7f;
            var top = 42f;
            var cellWidth = (GuiWidth - margin * 6f) / 5f;
            var cellHeight = (GuiHeight - top - margin * 3f) / 2f;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var column = chapter % 5;
                var row = chapter / 5;
                var rect = new Rect(margin + column * (cellWidth + margin),
                    top + row * (cellHeight + margin), cellWidth, cellHeight);
                DrawOrnatePanel(rect, new Color(.025f, .045f, .082f, 1f),
                    profile.Accent, 2f);
                var set = GetBossDirectionalAnimation(profile);
                var sprite = set != null && set.Down.Length > 0
                    ? set.Down[Mathf.Min(14, set.Down.Length - 1)] : null;
                if (sprite != null)
                    DrawSpriteInGui(sprite, new Rect(rect.x + 2f, rect.y + 18f,
                        rect.width - 4f, rect.height - 22f), Color.white);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 1f, rect.width - 4f, 18f),
                    $"R{chapter * 5 + 5} {profile.EnglishName}",
                    new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 8 });
            }
        }

        private IEnumerator QaBossGallery259Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            yield return PrewarmBossPresentations();
            cameraZoom = 7.2f;
            if (gameCamera != null)
            {
                gameCamera.orthographicSize = cameraZoom;
                gameCamera.transform.position = new Vector3(0f, 0f, -10f);
            }
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var actor = new GameObject($"QA 259 Gallery {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                var position = new Vector2(-4.8f + chapter % 5 * 2.4f,
                    chapter < 5 ? 2.15f : -2.15f);
                actor.ForcePositionForQa(position);
                actor.PreviewMotionPoseForQa(Vector2.down, chapter % 3, .61f);
                enemies.Add(actor);
            }
            if (HasCommandLineArgument("-qaAutoCapture"))
            {
                while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
                yield return new WaitForSecondsRealtime(.35f);
                yield return new WaitForEndOfFrame();
                var capture = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    Application.dataPath, "..", "boss-gallery-v262.bmp"));
                CaptureCurrentFrameForQa(capture);
                yield return new WaitForSecondsRealtime(1.2f);
                Debug.Log($"QA_BOSS_GALLERY_262 capture={capture}");
                Application.Quit(0);
            }
        }

        private IEnumerator QaRelease259Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            Time.timeScale = 1f;
            var failures = new List<string>();

            yield return PrewarmBossPresentations();
            var bossSprites = bossDirectionalAnimations.Values
                .SelectMany(AllDirectionalSprites259).Where(sprite => sprite != null)
                .Distinct().ToArray();
            var unsafeBossSprites = bossSprites.Where(sprite =>
            {
                var margin = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                return margin.x < 10f || margin.y < 10f || margin.z < 10f || margin.w < 10f;
            }).ToArray();
            var bossFrames = bossDirectionalAnimations.Count == 10 &&
                             BossFrameIsolationCheckCountForQa == 895 &&
                             bossSprites.Length >= 850 && unsafeBossSprites.Length == 0 &&
                             BossFrameIsolationFailuresForQa.Length == 0 &&
                             BossFrameFallbackRepairCountForQa == BossFrameRejectedCompositeCountForQa;
            if (!bossFrames) failures.Add($"boss-frames={bossDirectionalAnimations.Count}:" +
                $"{BossFrameIsolationCheckCountForQa}:{bossSprites.Length}:unsafe={unsafeBossSprites.Length}:" +
                $"fail={BossFrameIsolationFailuresForQa.Length}:rejected/repair=" +
                $"{BossFrameRejectedCompositeCountForQa}/{BossFrameFallbackRepairCountForQa}");

            var bossRuntime = true;
            foreach (var chapter in Enumerable.Range(0, 10))
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var animation = GetBossDirectionalAnimation(profile);
                if (animation == null || !animation.SupportsEightDirections)
                {
                    bossRuntime = false;
                    continue;
                }
                foreach (var row in new[]
                         {
                             animation.Down, animation.DownDiagonal, animation.Side,
                             animation.UpDiagonal, animation.Up
                         })
                for (var state = 0; state < 3; state++)
                {
                    var distinct = row.Skip(state * 24).Take(24)
                        .Where(sprite => sprite != null).Select(sprite => sprite.GetInstanceID())
                        .Distinct().Count();
                    bossRuntime &= distinct >= 4;
                }
                var actor = new GameObject($"QA 259 {profile.Id} Boss").AddComponent<EnemyUnit>();
                actor.Initialize(this, chapter, 900000f, true, 0, profile.CombatClass, profile);
                actor.ForcePositionForQa(new Vector2(0f, -5.8f));
                foreach (var direction in new[]
                         {
                             Vector2.down, new Vector2(1f, -1f).normalized, Vector2.right,
                             new Vector2(1f, 1f).normalized, Vector2.up
                         })
                for (var state = 0; state < 3; state++)
                {
                    actor.PreviewMotionPoseForQa(direction, state, .61f);
                    bossRuntime &= actor.ActiveBossArtworkChannelsForQa == 1 &&
                                   actor.CurrentSpriteHasSafeCellMarginForQa;
                }
                Destroy(actor.gameObject);
            }
            if (!bossRuntime) failures.Add("boss-runtime-channel-or-margin");

            var corridorComplete = paths.All(route => route.All(point =>
                IsWithinGroundEnemyRoadCorridor(point, .2f))) &&
                bossEntrancePath.All(point => IsWithinGroundEnemyRoadCorridor(point, .25f));
            var forbiddenTerrain = HighGroundZones.Select(zone => zone.Center)
                .Concat(CliffFootprintBlockers.Select(zone => zone.Center))
                .Concat(ExteriorFortressBlockers.Select(zone => zone.Center))
                .Concat(new[]
                {
                    new Vector2(-5.04f, -1.22f), new Vector2(5.04f, -1.22f),
                    new Vector2(-5.18f, -.18f), new Vector2(5.18f, -.18f),
                    new Vector2(-4.72f, .72f), new Vector2(4.72f, .72f)
                }).ToArray();
            var strictTerrain = forbiddenTerrain.All(point =>
                !IsWithinGroundEnemyRoadCorridor(point, .16f));
            var pursuitRejected = forbiddenTerrain.All(point =>
                !CanTraverseGroundEnemy(paths[0][12], point, .16f));
            var summonProjection = forbiddenTerrain.All(point =>
                IsWithinGroundEnemyRoadCorridor(NearestGroundEnemyRoadPosition(point, .16f), .16f));
            if (!corridorComplete || !strictTerrain || !pursuitRejected || !summonProjection)
                failures.Add($"road-corridor={corridorComplete}/{strictTerrain}/{pursuitRejected}/{summonProjection}");

            var guideTouch = VerifyGuideTouchScrollForQa();
            if (!guideTouch) failures.Add("guide-touch-scroll");

            var maximumLaterStep = 0f;
            var minimumLaterStep = float.MaxValue;
            var previous = 0f;
            for (var round = 1; round <= MaxRounds; round++)
            {
                Round = round;
                var pressure = WaveEnemyCount(round) * BaseEnemyHealth(round) *
                               EnemyRoundDamageMultiplierFor(round);
                if (round >= 6)
                {
                    var ratio = pressure / previous;
                    maximumLaterStep = Mathf.Max(maximumLaterStep, ratio);
                    minimumLaterStep = Mathf.Min(minimumLaterStep, ratio);
                }
                previous = pressure;
            }
            var balance = !float.IsNaN(minimumLaterStep) && !float.IsNaN(maximumLaterStep) &&
                          minimumLaterStep >= .99f && maximumLaterStep <= 1.18f;
            if (!balance) failures.Add($"balance={minimumLaterStep:0.000}-{maximumLaterStep:0.000}");

            var vfx = CombatVfxTierScale(CombatVfxTier.Skill) >= 1.7f &&
                      CombatVfxTierScale(CombatVfxTier.Ultimate) >= 2.7f &&
                      CombatVfxPeakAlpha(UnitArchetype.AreaMage, CombatVfxTier.Skill) >= .98f &&
                      CombatVfxDuration(UnitArchetype.SingleMage, CombatVfxTier.Ultimate) >= 1.2f;
            if (!vfx) failures.Add("skill-ultimate-vfx-readability");

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_259 passed={passed} bossFrames={bossFrames}:" +
                      $"{bossSprites.Length}:rejected/repair=" +
                      $"{BossFrameRejectedCompositeCountForQa}/{BossFrameFallbackRepairCountForQa} " +
                      $"bossRuntime={bossRuntime} corridor={corridorComplete}/{strictTerrain}/{pursuitRejected}/{summonProjection} " +
                      $"guideTouch={guideTouch} balance={balance}:{minimumLaterStep:0.000}-" +
                      $"{maximumLaterStep:0.000} vfx={vfx} fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 119);
        }

        private static IEnumerable<Sprite> AllDirectionalSprites259(DirectionalAnimationSet set)
        {
            if (set == null) yield break;
            foreach (var sprite in set.Down) yield return sprite;
            foreach (var sprite in set.DownDiagonal) yield return sprite;
            foreach (var sprite in set.Side) yield return sprite;
            foreach (var sprite in set.UpDiagonal) yield return sprite;
            foreach (var sprite in set.Up) yield return sprite;
        }
    }
}
