using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private bool qaBossGallery263Active;
        private int qaBossGallery263Chapter;
        private int qaBossGallery263State;

        private static Sprite[][] BossRows263(DirectionalAnimationSet set) => new[]
        {
            set.Down, set.DownDiagonal, set.Side, set.UpDiagonal, set.Up
        };

        private static void WriteRawSpriteBmp263(string path, Color32[] pixels, int width, int height)
        {
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length < width * height) return;
            var rowBytes = (width * 3 + 3) & ~3;
            var pixelBytes = rowBytes * height;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(54 + pixelBytes);
            writer.Write(0);
            writer.Write(54);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixelBytes);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            var padding = rowBytes - width * 3;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = pixels[y * width + x];
                    // Premultiply against the gallery's dark slate so transparent gutters stay
                    // visible while preserving every cut edge and neighbouring fragment.
                    var alpha = color.a / 255f;
                    writer.Write((byte)Mathf.RoundToInt(color.b * alpha + 14f * (1f - alpha)));
                    writer.Write((byte)Mathf.RoundToInt(color.g * alpha + 20f * (1f - alpha)));
                    writer.Write((byte)Mathf.RoundToInt(color.r * alpha + 31f * (1f - alpha)));
                }
                for (var pad = 0; pad < padding; pad++) writer.Write((byte)0);
            }
        }

        private void ExportFinalBossFramesForQa(string bossId, string state, int row, Sprite[] frames)
        {
            if (!HasCommandLineArgument("-qaExportBossFrames") || frames == null) return;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "qa-exported-boss-frames-v263", "runtime"));
            Directory.CreateDirectory(root);
            for (var column = 0; column < frames.Length; column++)
            {
                var sprite = frames[column];
                if (sprite == null || sprite.texture == null) continue;
                try
                {
                    var rect = sprite.textureRect;
                    var x = Mathf.RoundToInt(rect.x);
                    var y = Mathf.RoundToInt(rect.y);
                    var width = Mathf.RoundToInt(rect.width);
                    var height = Mathf.RoundToInt(rect.height);
                    var pixels = sprite.texture.GetPixels32();
                    var textureWidth = sprite.texture.width;
                    var crop = new Color32[width * height];
                    for (var py = 0; py < height; py++)
                    for (var px = 0; px < width; px++)
                        crop[py * width + px] = pixels[(y + py) * textureWidth + x + px];
                    WriteRawSpriteBmp263(Path.Combine(root,
                        $"runtime-{bossId}-{state}-r{row}-c{column}.bmp"), crop, width, height);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"QA 263 final sprite export failed: {bossId}/{state}/r{row}/c{column}: " +
                                   exception.Message);
                }
            }
        }

        private static bool ExportSpriteForQa263(Sprite sprite, string path)
        {
            if (sprite == null || sprite.texture == null) return false;
            try
            {
                var rect = sprite.textureRect;
                var x = Mathf.RoundToInt(rect.x);
                var y = Mathf.RoundToInt(rect.y);
                var width = Mathf.RoundToInt(rect.width);
                var height = Mathf.RoundToInt(rect.height);
                var source = sprite.texture.GetPixels32();
                var crop = new Color32[width * height];
                for (var py = 0; py < height; py++)
                for (var px = 0; px < width; px++)
                    crop[py * width + px] = source[(y + py) * sprite.texture.width + x + px];
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                WriteRawSpriteBmp263(path, crop, width, height);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"QA 263 sprite export failed: {sprite.name}: {exception.Message}");
                return false;
            }
        }

        private void DrawQaBossGallery263()
        {
            DrawPanel(new Rect(0f, 0f, GuiWidth, GuiHeight), new Color(.006f, .01f, .022f, 1f));
            var profile = EnemyVariantCatalog.ForChapterStage(qaBossGallery263Chapter, 4);
            var stateName = qaBossGallery263State == 0 ? "WALK" :
                qaBossGallery263State == 1 ? "ATTACK" : "SKILL";
            GUI.Label(new Rect(0f, 4f, GuiWidth, 31f),
                $"R{(qaBossGallery263Chapter + 1) * 5} {profile.EnglishName} · {stateName} · ALL SOURCE POSES",
                modalTitleStyle);
            var set = GetBossDirectionalAnimation(profile);
            if (set == null) return;
            var rows = BossRows263(set);
            var rowNames = new[] { "DOWN", "DOWN-DIAGONAL", "SIDE", "UP-DIAGONAL", "UP" };
            const float leftLabel = 86f;
            const float top = 42f;
            const float gap = 5f;
            var rowHeight = (GuiHeight - top - 12f - gap * 4f) / 5f;
            for (var row = 0; row < rows.Length; row++)
            {
                var stateSprites = rows[row].Skip(qaBossGallery263State * 24).Take(24)
                    .Where(sprite => sprite != null).Distinct().ToArray();
                var y = top + row * (rowHeight + gap);
                GUI.Label(new Rect(3f, y, leftLabel - 6f, rowHeight), rowNames[row],
                    new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 8 });
                var width = (GuiWidth - leftLabel - gap * Mathf.Max(0, stateSprites.Length - 1) - 6f) /
                            Mathf.Max(1, stateSprites.Length);
                for (var column = 0; column < stateSprites.Length; column++)
                {
                    var cell = new Rect(leftLabel + column * (width + gap), y, width, rowHeight);
                    DrawOrnatePanel(cell, new Color(.025f, .04f, .073f, 1f), profile.Accent, 1f);
                    DrawSpriteContained(new Rect(cell.x + 2f, cell.y + 2f, cell.width - 4f, cell.height - 15f),
                        stateSprites[column]);
                    GUI.Label(new Rect(cell.x, cell.yMax - 14f, cell.width, 13f), $"{column + 1}",
                        new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 7 });
                }
            }
        }

        private IEnumerator QaBossGallery263Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            yield return PrewarmBossPresentations();
            qaBossGallery263Active = true;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "boss-frame-gallery-v263"));
            Directory.CreateDirectory(root);
            for (var chapter = 0; chapter < 10; chapter++)
            for (var state = 0; state < 3; state++)
            {
                qaBossGallery263Chapter = chapter;
                qaBossGallery263State = state;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var path = Path.Combine(root, $"{chapter + 1:00}-{profile.Id}-state-{state}.bmp");
                CaptureCurrentFrameForQa(path);
            }
            qaBossGallery263Active = false;
            Debug.Log($"QA_BOSS_GALLERY_263 passed=True pages=30 path={root}");
            Application.Quit(0);
        }

        private IEnumerator QaRelease263Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            Time.timeScale = 1f;
            var failures = new List<string>();
            yield return PrewarmBossPresentations();

            var directions = Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>()
                .Select(EightWayFacing.VectorFor).ToArray();
            var sampledPoses = 0;
            var fallbackFrames = 0;
            var isolatedFrames = 0;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, 4);
                var set = GetBossDirectionalAnimation(profile);
                if (set == null)
                {
                    failures.Add($"boss-null-{profile.Id}");
                    continue;
                }
                var unique = BossRows263(set).SelectMany(row => row).Where(sprite => sprite != null)
                    .Distinct().ToArray();
                foreach (var sprite in unique)
                {
                    var textureName = sprite.texture != null ? sprite.texture.name : string.Empty;
                    var isolated = textureName.Contains("-isolated-r", StringComparison.OrdinalIgnoreCase);
                    if (isolated) isolatedFrames++;
                    else fallbackFrames++;
                    var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                    var marginSafe = margins.x >= 18f && margins.y >= 18f &&
                                     margins.z >= 18f && margins.w >= 18f;
                    var audited = !isolated || bossFrameIsolationAudits.TryGetValue(sprite.name, out var valid) && valid;
                    var aspect = sprite.bounds.size.x / Mathf.Max(.001f, sprite.bounds.size.y);
                    if (!marginSafe || !audited || aspect is < .18f or > 3.15f)
                        failures.Add($"boss-frame-{profile.Id}:{sprite.name}:m{margins}:a{aspect:0.00}:q{audited}");
                }

                var actor = new GameObject($"QA 263 Boss {profile.Id}").AddComponent<EnemyUnit>();
                actor.Initialize(this, chapter, 900000f, true, chapter % Mathf.Max(1, LaneCount),
                    profile.CombatClass, profile);
                var stateHeightRatios = new List<float>();
                var actorSafe = actor.HasCompleteDirectionalAnimationForQa;
                foreach (var direction in directions)
                for (var state = 0; state < 3; state++)
                {
                    var heights = new List<float>();
                    for (var frame = 0; frame < 24; frame++)
                    {
                        actor.PreviewMotionPoseForQa(direction, state, (frame + .15f) / 24f);
                        sampledPoses++;
                        heights.Add(actor.VisualWorldHeight);
                        actorSafe &= actor.ActiveBossArtworkChannelsForQa == 1 &&
                                     actor.CurrentSpriteHasSafeCellMarginForQa &&
                                     actor.CurrentSpriteForeignComponentsForQa <= 0 &&
                                     actor.CurrentSpriteRenderAspectForQa is > .18f and < 3.15f;
                    }
                    stateHeightRatios.Add(heights.Max() / Mathf.Max(.001f, heights.Min()));
                }
                var worstHeightRatio = stateHeightRatios.Max();
                actorSafe &= worstHeightRatio <= 1.46f;
                if (!actorSafe) failures.Add($"boss-live-{profile.Id}:height={worstHeightRatio:0.000}");
                Destroy(actor.gameObject);
            }

            // The jelly caster must use the same authored mage identity from both sides. The
            // rear frame is a dedicated hat/staff painting and is normalized to the live front
            // height; the generic horned jelly back is never accepted for this profile.
            var mageProfile = EnemyVariantCatalog.ForChapterStage(0, 2);
            var mage = new GameObject("QA 263 Jelly Mage Identity").AddComponent<EnemyUnit>();
            mage.Initialize(this, 0, 500f, false, 0, mageProfile.CombatClass, mageProfile);
            var mageHeights = new List<float>();
            var mageTextures = new Dictionary<FacingOctant, string>();
            foreach (var octant in Enum.GetValues(typeof(FacingOctant)).Cast<FacingOctant>())
            {
                var direction = EightWayFacing.VectorFor(octant);
                for (var frame = 0; frame < 24; frame++)
                {
                    mage.PreviewMotionPoseForQa(direction, 0, (frame + .15f) / 24f);
                    mageHeights.Add(mage.VisualWorldHeight);
                    mageTextures[octant] = mage.CurrentFrameTextureNameForQa;
                    if (!mage.CurrentSpriteHasSafeCellMarginForQa || mage.ActivePrimaryBodyChannelsForQa != 1)
                        failures.Add($"mage-frame-{octant}-{frame}");
                    if (frame == 12 && HasCommandLineArgument("-qaExportBossFrames"))
                    {
                        var mageRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                            "qa-exported-jelly-frames-v263", "runtime"));
                        ExportSpriteForQa263(mage.CurrentSpriteForQa,
                            Path.Combine(mageRoot, $"jelly-mage-{octant}.bmp"));
                    }
                }
            }
            var mageRatio = mageHeights.Max() / Mathf.Max(.001f, mageHeights.Min());
            var rearIdentity = new[] { FacingOctant.NorthWest, FacingOctant.North, FacingOctant.NorthEast }
                .All(octant => mageTextures.TryGetValue(octant, out var name) &&
                               name.Contains("enemy-jelly-mage-back-v1", StringComparison.OrdinalIgnoreCase));
            if (!rearIdentity || mageRatio > 1.10f)
                failures.Add($"mage-identity={rearIdentity}:height={mageRatio:0.000}:" +
                             string.Join("/", mageTextures.Select(pair => $"{pair.Key}:{pair.Value}")));
            Destroy(mage.gameObject);

            var portraitSafe = true;
            for (var chapter = 0; chapter < 10; chapter++)
            {
                var sprite = GuideBossPortraitSprite(chapter);
                if (sprite == null)
                {
                    portraitSafe = false;
                    failures.Add($"portrait-null-{chapter + 1}");
                    continue;
                }
                var margins = EnemyUnit.SpriteOpaqueMarginsForQa(sprite);
                var aspect = sprite.bounds.size.x / Mathf.Max(.001f, sprite.bounds.size.y);
                var isolation = SpriteFrameIsolationRegistry.For(sprite);
                var safe = margins.x >= 20f && margins.y >= 20f && margins.z >= 20f && margins.w >= 20f &&
                           aspect is > .25f and < 1.75f &&
                           SpriteFrameIsolationRegistry.HasAudit(sprite) &&
                           isolation.RemainingForeignComponents == 0;
                portraitSafe &= safe;
                if (!safe) failures.Add($"portrait-{chapter + 1}:m{margins}:a{aspect:0.00}:" +
                                        $"foreign={isolation.RemainingForeignComponents}");
                if (HasCommandLineArgument("-qaExportBossFrames"))
                {
                    var portraitRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                        "qa-exported-guide-portraits-v263"));
                    ExportSpriteForQa263(sprite,
                        Path.Combine(portraitRoot, $"boss-{chapter + 1:00}.bmp"));
                }
            }

            var passed = failures.Count == 0;
            Debug.Log($"QA_RELEASE_263 passed={passed} bosses=10 poses={sampledPoses} " +
                      $"frames={isolatedFrames}+fallback{fallbackFrames} mage={rearIdentity}:{mageRatio:0.000} " +
                      $"portraits={portraitSafe} rejected/repair=" +
                      $"{bossFrameRejectedCompositeCount}/{bossFrameFallbackRepairCount} " +
                      $"fail={string.Join(",", failures.Take(40))}");
            Application.Quit(passed ? 0 : 123);
        }
    }
}
