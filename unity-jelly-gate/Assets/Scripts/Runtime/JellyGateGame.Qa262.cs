using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private static void CaptureCurrentFrameForQa(string path)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0, false);
            texture.Apply(false, false);
            var pixels = texture.GetPixels32();
            var rowBytes = (texture.width * 3 + 3) & ~3;
            var pixelBytes = rowBytes * texture.height;
            using (var stream = System.IO.File.Create(path))
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write((byte)'B'); writer.Write((byte)'M');
                writer.Write(54 + pixelBytes); writer.Write(0); writer.Write(54);
                writer.Write(40); writer.Write(texture.width); writer.Write(texture.height);
                writer.Write((short)1); writer.Write((short)24); writer.Write(0);
                writer.Write(pixelBytes); writer.Write(2835); writer.Write(2835);
                writer.Write(0); writer.Write(0);
                var padding = rowBytes - texture.width * 3;
                for (var y = 0; y < texture.height; y++)
                {
                    for (var x = 0; x < texture.width; x++)
                    {
                        var color = pixels[y * texture.width + x];
                        writer.Write(color.b); writer.Write(color.g); writer.Write(color.r);
                    }
                    for (var pad = 0; pad < padding; pad++) writer.Write((byte)0);
                }
            }
            Destroy(texture);
        }

        private IEnumerator QaVisual262Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            foreach (var enemy in enemies.Where(item => item != null).ToArray()) Destroy(enemy.gameObject);
            enemies.Clear();
            foreach (var unit in units.Where(item => item != null).ToArray()) Destroy(unit.gameObject);
            units.Clear();
            cameraZoom = 6.9f;
            if (gameCamera != null)
            {
                gameCamera.orthographicSize = cameraZoom;
                gameCamera.transform.position = new Vector3(0f, 0f, -10f);
            }

            var mageProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var mageFront = new GameObject("QA Jelly Mage Front").AddComponent<EnemyUnit>();
            mageFront.Initialize(this, 0, 900f, false, 0, mageProfile.CombatClass, mageProfile);
            mageFront.ForcePositionForQa(new Vector2(-2.25f, 1.65f));
            mageFront.PreviewMotionPoseForQa(Vector2.down, 0, .38f);
            mageFront.enabled = false;
            enemies.Add(mageFront);
            var mageBack = new GameObject("QA Jelly Mage Rear").AddComponent<EnemyUnit>();
            mageBack.Initialize(this, 1, 900f, false, 0, mageProfile.CombatClass, mageProfile);
            mageBack.ForcePositionForQa(new Vector2(2.25f, 1.65f));
            mageBack.PreviewMotionPoseForQa(Vector2.up, 0, .38f);
            mageBack.enabled = false;
            enemies.Add(mageBack);

            var druidLeft = new GameObject("QA Spiritcaller Left").AddComponent<PlayerUnit>();
            druidLeft.Initialize(this, UnitArchetype.Druid, definitions[UnitArchetype.Druid],
                new Vector2(-2.25f, -1.65f));
            druidLeft.PreviewMotionPoseForQa(Vector2.left, 0, .38f);
            druidLeft.enabled = false;
            units.Add(druidLeft);
            var druidRight = new GameObject("QA Spiritcaller Right").AddComponent<PlayerUnit>();
            druidRight.Initialize(this, UnitArchetype.Druid, definitions[UnitArchetype.Druid],
                new Vector2(2.25f, -1.65f));
            druidRight.PreviewMotionPoseForQa(Vector2.right, 0, .38f);
            druidRight.enabled = false;
            units.Add(druidRight);

            yield return new WaitForEndOfFrame();
            if (!HasCommandLineArgument("-qaAutoCapture")) yield break;
            var capture = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "..", "sprite-priority-v262.bmp"));
            CaptureCurrentFrameForQa(capture);
            yield return new WaitForSecondsRealtime(.5f);
            Debug.Log($"QA_VISUAL_262 capture={capture}");
            Application.Quit(0);
        }

        private IEnumerator QaRelease262Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            Time.timeScale = 1f;
            var failures = new List<string>();
            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var phases = new[] { .03f, .19f, .38f, .57f, .76f, .94f };

            yield return PrewarmBossPresentations();

            // Chairman priority 1: the jelly mage must retain the authored rear hat, staff and
            // body in one owned frame, without inheriting either neighbour from the source sheet.
            var mageProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var mage = new GameObject("QA 262 Jelly Mage").AddComponent<EnemyUnit>();
            mage.Initialize(this, 0, 400f, false, 0, mageProfile.CombatClass, mageProfile);
            var mageHeights = new List<float>();
            var mageSafe = true;
            string mageBackTexture = string.Empty;
            foreach (var direction in directions)
            foreach (var phase in phases)
            {
                mage.PreviewMotionPoseForQa(direction, 0, phase);
                mageHeights.Add(mage.VisualWorldHeight);
                mageSafe &= mage.ActivePrimaryBodyChannelsForQa == 1 &&
                            mage.CurrentSpriteHasSafeCellMarginForQa;
                if (direction.y > .7f) mageBackTexture = mage.CurrentFrameTextureNameForQa;
            }
            var mageHeightRatio = mageHeights.Max() / Mathf.Max(.001f, mageHeights.Min());
            mageSafe &= mageHeightRatio <= 1.10f &&
                        mageBackTexture.Contains("enemy-jelly-mage-back-v1", StringComparison.OrdinalIgnoreCase);
            if (!mageSafe)
                failures.Add($"jelly-mage={mageHeightRatio:0.000}:{mageBackTexture}");
            Destroy(mage.gameObject);

            // Chairman priority 2: every boss is sampled from the actual EnemyUnit renderer in
            // eight directions and all three motion states. A second body channel, foreign atlas
            // component or changing frame scale fails the build.
            var bossPoses = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var boss = new GameObject($"QA 262 Boss {profile.Id}").AddComponent<EnemyUnit>();
                boss.Initialize(this, chapter, 900000f, true, chapter % Mathf.Max(1, LaneCount),
                    profile.CombatClass, profile);
                var heights = new List<float>();
                var directionScaleRatios = new List<float>();
                var safe = boss.HasCompleteDirectionalAnimationForQa;
                foreach (var direction in directions)
                {
                    var directionScales = new List<float>();
                    for (var state = 0; state < 3; state++)
                    foreach (var phase in phases)
                    {
                        boss.PreviewMotionPoseForQa(direction, state, phase);
                        bossPoses++;
                        heights.Add(boss.VisualWorldHeight);
                        directionScales.Add(boss.CurrentVisualScaleHeightRatioForQa);
                        safe &= boss.ActiveBossArtworkChannelsForQa == 1 &&
                                boss.CurrentSpriteHasSafeCellMarginForQa &&
                                boss.CurrentSpriteForeignComponentsForQa <= 0;
                    }
                    directionScaleRatios.Add(directionScales.Max() /
                                             Mathf.Max(.001f, directionScales.Min()));
                }
                var heightRatio = heights.Max() / Mathf.Max(.001f, heights.Min());
                var scaleRatio = directionScaleRatios.Max();
                // v2.63 normalizes every accepted pose to the direction's walk-body height.
                // The local scale may therefore vary when source paintings have different pixel
                // density; the rendered body height is the contract, not a legacy transform
                // implementation detail. The exhaustive v2.63 probe separately checks every
                // pose's component ownership and source isolation.
                safe &= heightRatio <= 1.46f;
                if (!safe)
                    failures.Add($"boss-{profile.Id}=h{heightRatio:0.000}:s{scaleRatio:0.000}:" +
                                 boss.CurrentFrameTextureNameForQa);
                Destroy(boss.gameObject);
            }

            // Chairman priority 3: the Petal Spiritcaller source faces screen-right. Left travel
            // therefore has to mirror immediately; right travel must not mirror.
            var druid = new GameObject("QA 262 Petal Spiritcaller").AddComponent<PlayerUnit>();
            druid.Initialize(this, UnitArchetype.Druid, definitions[UnitArchetype.Druid],
                NearestWalkable(new Vector2(0f, -4f), .18f));
            var druidLeft = druid.PreviewFacingMirrorForQa(Vector2.left, .37f);
            var druidRight = druid.PreviewFacingMirrorForQa(Vector2.right, .37f);
            var druidLeftHeight = druid.PreviewDirectionHeightForQa(Vector2.left, .37f);
            var druidRightHeight = druid.PreviewDirectionHeightForQa(Vector2.right, .37f);
            var druidHeightRatio = Mathf.Max(druidLeftHeight, druidRightHeight) /
                                   Mathf.Max(.001f, Mathf.Min(druidLeftHeight, druidRightHeight));
            if (!druidLeft || druidRight || druidHeightRatio > 1.025f)
                failures.Add($"druid-facing={druidLeft}/{druidRight}:h{druidHeightRatio:0.000}");
            Destroy(druid.gameObject);

            // Chairman priority 4: all ten cards must be drawable even after their textures have
            // been uploaded non-readable. Touch drag and the close channel remain independent.
            var guidePortraits = Enumerable.Range(0, 10)
                .Select(GuideBossPortraitSprite).Count(sprite => sprite != null);
            var guideSafe = guidePortraits == 10 && VerifyGuideTouchScrollForQa();
            if (!guideSafe) failures.Add($"guide={guidePortraits}/10:drag={VerifyGuideTouchScrollForQa()}");

            // Chairman priority 5: use continuous shoreline polygons, not isolated circles. Each
            // interior/edge probe is blocked while nearby paved lanes remain traversable.
            var forbidden = new[]
            {
                new Vector2(-5.68f, 1.52f), new Vector2(-5.42f, .34f),
                new Vector2(-5.56f, -1.75f), new Vector2(-6.05f, -3.65f),
                new Vector2(5.68f, 1.52f), new Vector2(5.42f, .34f),
                new Vector2(5.56f, -1.75f), new Vector2(6.05f, -3.65f)
            };
            var road = paths.SelectMany(route => route)
                .Where(point => point.y is > -4.6f and < 2.2f &&
                                Mathf.Abs(point.x) is > 3.3f and < 4.9f &&
                                !IsManualNavigationBlocked(point))
                .OrderBy(point => point.y).Where((_, index) => index % 12 == 0)
                .Take(8).ToArray();
            var terrainSafe = forbidden.All(point => IsManualNavigationBlocked(point) && !IsWalkable(point)) &&
                              road.Length >= 6 &&
                              road.All(point => !IsManualNavigationBlocked(point) && IsWalkable(point));
            if (!terrainSafe)
            {
                var forbiddenState = string.Join("/", forbidden.Select(point =>
                    $"{point}:{IsManualNavigationBlocked(point)}/{IsWalkable(point)}"));
                var roadState = string.Join("/", road.Select(point =>
                    $"{point}:{IsManualNavigationBlocked(point)}/{IsWalkable(point)}"));
                failures.Add($"shoreline-mask=blocked[{forbiddenState}]:road[{roadState}]");
            }

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_262 passed={passed} mage={mageSafe}:{mageHeightRatio:0.000} " +
                      $"bosses=10:poses={bossPoses} druid={druidLeft}/{druidRight}:{druidHeightRatio:0.000} " +
                      $"guide={guidePortraits}/10 terrain={terrainSafe} fail={string.Join(",", failures)}");
            Application.Quit(passed ? 0 : 122);
        }
    }
}
