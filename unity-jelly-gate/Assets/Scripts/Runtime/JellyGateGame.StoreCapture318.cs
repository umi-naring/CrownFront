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
        private const string StoreCaptureItemKeyPrefix318 = "Crownfront.Economy.Item.v1.";

        private IEnumerator QaStoreCapture318Routine()
        {
            while (FindFirstObjectByType<CrownfrontBootLoader>() != null) yield return null;

            var locale = ReadCommandLineValue318("-qaStoreLocale");
            GameLocalization.Current = string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase)
                ? GameLanguage.English
                : GameLanguage.Korean;

            showMainMenu = false;
            showResumePrompt = false;
            showSettings = false;
            showMissionPanel = false;
            showShopPanel = false;
            showSkinPanel = false;
            showGuidePanel = false;
            showFormationPanel = false;
            showAugmentSummary = false;
            showPregameLoadout = false;
            augmentOverlayHidden = false;
            selectedUnits.Clear();

            var outputDirectory = ReadCommandLineValue318("-qaStoreOutputDir");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                foreach (var captureScreen in new[] { "menu", "battle", "boss", "augment", "loadout" })
                {
                    ResetStoreCaptureState318();
                    ConfigureStoreScreen318(captureScreen);
                    yield return null;
                    yield return new WaitForSecondsRealtime(captureScreen == "battle" || captureScreen == "boss"
                        ? 1.15f
                        : .3f);
                    if (captureScreen == "battle" || captureScreen == "boss")
                    {
                        PrimeStoreCombat318(captureScreen == "boss");
                        yield return new WaitForSecondsRealtime(.09f);
                    }
                    var outputPath = Path.Combine(outputDirectory, captureScreen + ".bmp");
                    yield return CaptureStorePng318(outputPath);
                    Debug.Log($"QA_STORE_CAPTURE_318 saved=True locale={locale} screen={captureScreen} " +
                              $"item12={(captureScreen == "loadout")} output={outputPath}");
                }

                Application.Quit(0);
                yield break;
            }

            var screen = (ReadCommandLineValue318("-qaStoreScreen") ?? "menu").ToLowerInvariant();
            ConfigureStoreScreen318(screen);

            yield return null;
            Debug.Log($"QA_STORE_CAPTURE_318 ready=True locale={locale} screen={screen} " +
                      $"item12={(screen == "loadout")} menuArt={mainMenuMoonlitTexture?.name}");
            var output = ReadCommandLineValue318("-qaStoreOutput");
            if (!string.IsNullOrWhiteSpace(output))
            {
                yield return null;
                yield return new WaitForSecondsRealtime(.18f);
                yield return CaptureFullFrameRoutine(output);
                Debug.Log($"QA_STORE_CAPTURE_318 saved=True output={output}");
                Application.Quit(0);
                yield break;
            }

            while (true) yield return null;
        }

        private void ConfigureStoreScreen318(string screen)
        {
            switch (screen)
            {
                case "loadout": ConfigureStoreLoadout318(); break;
                case "augment": ConfigureStoreAugment318(); break;
                case "battle": ConfigureStoreBattle318(false); break;
                case "boss": ConfigureStoreBattle318(true); break;
                default: ConfigureStoreMenu318(); break;
            }
        }

        private void ResetStoreCaptureState318()
        {
            showMainMenu = false;
            showResumePrompt = false;
            showSettings = false;
            showMissionPanel = false;
            showShopPanel = false;
            showSkinPanel = false;
            showGuidePanel = false;
            showFormationPanel = false;
            showAugmentSummary = false;
            showPregameLoadout = false;
            augmentOverlayHidden = false;
            selectedUnits.Clear();
            currentOffers = Array.Empty<AugmentOffer>();
            Time.timeScale = 1f;
        }

        private static IEnumerator CaptureStorePng318(string outputPath)
        {
            yield return new WaitForEndOfFrame();
            var captureType = Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule");
            var captureMethod = captureType?.GetMethod("CaptureScreenshotAsTexture",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null, Type.EmptyTypes, null);
            var image = captureMethod?.Invoke(null, null) as Texture2D;
            if (image == null) throw new InvalidOperationException("Final UI frame capture is unavailable.");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            SaveStoreBmp318(outputPath, image.GetPixels32(), image.width, image.height);
            Destroy(image);
        }

        private static void SaveStoreBmp318(string outputPath, Color32[] pixels, int width, int height)
        {
            const int headerSize = 54;
            var pixelBytes = width * height * 4;
            using var stream = File.Create(outputPath);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(headerSize + pixelBytes);
            writer.Write(0);
            writer.Write(headerSize);
            writer.Write(40);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(0);
            writer.Write(pixelBytes);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var color = pixels[y * width + x];
                writer.Write(color.b);
                writer.Write(color.g);
                writer.Write(color.r);
                writer.Write((byte)255);
            }
        }

        private void ConfigureStoreMenu318()
        {
            showMainMenu = true;
            showPregameLoadout = false;
            Time.timeScale = 1f;
        }

        private void ConfigureStoreLoadout318()
        {
            showMainMenu = true;
            showPregameLoadout = true;
            var previous = new Dictionary<TacticalItemId, int>();
            foreach (var item in economy.Catalog.Where(item => item.PregameSelectable))
            {
                previous[item.Id] = economy.Count(item.Id);
                PlayerPrefs.SetInt(StoreCaptureItemKeyPrefix318 + item.Id, 12);
            }
            PlayerPrefs.Save();
            Application.quitting += () =>
            {
                foreach (var entry in previous)
                    PlayerPrefs.SetInt(StoreCaptureItemKeyPrefix318 + entry.Key, entry.Value);
                PlayerPrefs.Save();
            };
        }

        private void ConfigureStoreAugment318()
        {
            showMainMenu = false;
            Phase = GamePhase.Augment;
            Round = 12;
            lastTier = AugmentTier.Gold;
            var tier = AugmentTier.Gold;
            var power = TierPower(tier);
            currentOffers = GetAvailableAugmentTemplates(tier).Take(3)
                .Select(template => new AugmentOffer(
                    GameLocalization.AugmentName(template.EffectKey, template.Name),
                    GameLocalization.AugmentDescription(template.EffectKey, power,
                        DescribeAugment(template, power)), template.EffectKey, tier, power))
                .ToArray();
            Time.timeScale = 0f;
        }

        private void ConfigureStoreBattle318(bool boss)
        {
            showMainMenu = false;
            Phase = GamePhase.Battle;
            Round = boss ? 10 : 13;
            Money = 8;
            gateHealth = GateMaxHealth * (boss ? .76f : .94f);
            cameraMapCenter = new Vector2(0f, -4.55f);
            cameraZoom = cameraZoomTarget = 4.75f;
            enemyActionsFrozenUntil = Time.time + 8f;
            ApplyCameraPose();

            foreach (var unit in units.Where(unit => unit != null).ToArray()) Destroy(unit.gameObject);
            foreach (var enemy in enemies.Where(enemy => enemy != null).ToArray()) Destroy(enemy.gameObject);
            units.Clear();
            enemies.Clear();

            var allyTypes = new[]
            {
                UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
                UnitArchetype.AreaMage, UnitArchetype.SingleMage
            };
            var allyPositions = new[]
            {
                new Vector2(-.45f, -4.35f), new Vector2(.48f, -4.28f),
                new Vector2(-1.35f, -5.15f), new Vector2(1.28f, -5.08f), new Vector2(.05f, -5.42f)
            };
            for (var index = 0; index < allyTypes.Length; index++)
            {
                var type = allyTypes[index];
                var definition = definitions[type];
                var actor = new GameObject($"STORE 318 {type}").AddComponent<PlayerUnit>();
                actor.Initialize(this, type, definition,
                    NearestWalkable(allyPositions[index], definition.Radius));
                units.Add(actor);
            }

            var chapter = Mathf.Clamp((Round - 1) / 5, 0, 9);
            var enemyPositions = new[]
            {
                new Vector2(-.55f, -3.48f), new Vector2(-1.34f, -3.82f),
                new Vector2(-.2f, -3.48f), new Vector2(.42f, -3.86f),
                new Vector2(1.02f, -3.5f), new Vector2(1.48f, -3.92f),
                new Vector2(-1.15f, -4.25f), new Vector2(1.14f, -4.3f)
            };
            for (var index = 0; index < enemyPositions.Length; index++)
            {
                var stage = boss && index == 0 ? 4 : index % 4;
                var profile = EnemyVariantCatalog.ForChapterStage(chapter, stage);
                var actor = new GameObject($"STORE 318 {profile.Id} {index}").AddComponent<EnemyUnit>();
                actor.Initialize(this, index % Mathf.Max(1, paths.Count), boss && index == 0 ? 4200f : 780f,
                    boss && index == 0, chapter, profile.CombatClass, profile);
                actor.ForcePositionForQa(NearestWalkable(enemyPositions[index], boss && index == 0 ? .42f : .18f));
                actor.ApplyTimeFreeze(8f);
                enemies.Add(actor);
            }

            Time.timeScale = 1f;
        }

        private void PrimeStoreCombat318(bool boss)
        {
            var liveEnemies = enemies.Where(enemy => enemy != null && enemy.IsAlive).ToArray();
            for (var index = 0; index < units.Count && index < liveEnemies.Length; index++)
            {
                var actor = units[index];
                if (actor == null || !actor.IsAlive) continue;
                actor.TriggerAttackMotionForQa(liveEnemies[index].Position);
                PerformAttack(actor, liveEnemies[index], definitions[actor.Archetype]);
            }

            SpawnUltimateImpactFlash(new Vector2(0f, -3.45f),
                boss ? new Color(.75f, .26f, .9f) : new Color(.28f, .72f, 1f),
                boss ? UnitArchetype.AreaMage : UnitArchetype.Archer);
        }

        private static string ReadCommandLineValue318(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            return string.Empty;
        }
    }
}
