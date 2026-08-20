using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaCheckpoint266Routine()
        {
            yield return null;
            var hadPrevious = PlayerPrefs.HasKey(RunCheckpointKey);
            var previous = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
            var passed = false;
            try
            {
                showMainMenu = false;
                RestartGame(false);
                Round = 18;
                Money = 27;
                gateHealth = 413f;
                Phase = GamePhase.Preparation;
                lastTier = AugmentTier.Gold;
                augmentOffersWithoutHighTier = 2;
                lastStandCharges = 1;
                augmentPower["RangedCrit"] = .63f;
                augmentCount["QA Gold"] = 2;
                acquiredAugments["RangedCrit"] = new AugmentOffer(
                    "QA Gold", "checkpoint", "RangedCrit", AugmentTier.Gold, .63f);
                unlockedUnits.Add(UnitArchetype.Lancer);

                var tank = new GameObject("QA 266 Saved Tank").AddComponent<PlayerUnit>();
                tank.Initialize(this, UnitArchetype.Tank, ApplyUnitAugments(UnitArchetype.Tank,
                    definitions[UnitArchetype.Tank]), new Vector2(-1.42f, -5.66f));
                tank.RestoreCheckpointState(4, 390f, 117f, true, 2.4f, 0f);
                units.Add(tank);
                var archer = new GameObject("QA 266 Saved Archer").AddComponent<PlayerUnit>();
                archer.Initialize(this, UnitArchetype.Archer, ApplyUnitAugments(UnitArchetype.Archer,
                    definitions[UnitArchetype.Archer]), new Vector2(1.38f, -4.92f));
                archer.RestoreCheckpointState(3, 245f, 41f, false, 1.2f, 0f);
                units.Add(archer);

                SaveRunCheckpoint(true);
                var serialized = PlayerPrefs.GetString(RunCheckpointKey, string.Empty);
                var writeValid = !string.IsNullOrEmpty(serialized) && HasRunCheckpoint();

                // A battle autosave must retain the preparation snapshot byte-for-byte.
                Phase = GamePhase.Battle;
                Money = 3;
                gateHealth = 201f;
                SaveRunCheckpoint();
                var battleStable = serialized == PlayerPrefs.GetString(RunCheckpointKey, string.Empty);

                var saved = ReadRunCheckpoint();
                var restored = RestoreRunCheckpoint(saved);
                var restoredTank = units.FirstOrDefault(unit => unit.Archetype == UnitArchetype.Tank);
                var restoredArcher = units.FirstOrDefault(unit => unit.Archetype == UnitArchetype.Archer);
                var stateValid = restored && Phase == GamePhase.Preparation && Round == 18 && Money == 27 &&
                                 Mathf.Abs(gateHealth - 413f) < .01f && units.Count == 2 &&
                                 restoredTank != null && restoredTank.Level == 4 && restoredTank.IsHoldingPosition &&
                                 Mathf.Abs(restoredTank.Health - 117f) < .02f &&
                                 restoredArcher != null && restoredArcher.Level == 3 &&
                                 Mathf.Abs(restoredArcher.Health - 41f) < .02f &&
                                 unlockedUnits.Contains(UnitArchetype.Lancer) &&
                                 Mathf.Abs(StackPower("RangedCrit") - .63f) < .001f;

                showMainMenu = true;
                showResumePrompt = false;
                RequestFrontStart();
                var choiceVisible = showResumePrompt && showMainMenu;
                passed = writeValid && battleStable && stateValid && choiceVisible;
                Debug.Log($"QA_CHECKPOINT_266 write={writeValid} battleStable={battleStable} " +
                          $"restore={stateValid} choice={choiceVisible} units={units.Count} round={Round}");
            }
            finally
            {
                if (hadPrevious) PlayerPrefs.SetString(RunCheckpointKey, previous);
                else PlayerPrefs.DeleteKey(RunCheckpointKey);
                PlayerPrefs.Save();
            }
            Application.Quit(passed ? 0 : 66);
        }

        private IEnumerator QaGuideBossFrame266Routine()
        {
            yield return null;
            var card = new Rect(4f, 0f, 640f, 220f);
            var frame = GuideBossPortraitFrameRect(card);
            var inner = GuideBossPortraitRect(card);
            var layoutSafe = frame.Contains(inner.min) && frame.Contains(inner.max) &&
                             inner.xMin - frame.xMin >= 9f && frame.xMax - inner.xMax >= 9f &&
                             inner.yMin - frame.yMin >= 8f && frame.yMax - inner.yMax >= 9f;
            var portraits = Enumerable.Range(0, 10).Select(GuideBossPortraitSprite).ToArray();
            var allBodiesPresent = portraits.All(sprite => sprite != null && sprite.texture != null &&
                                                           SpriteGuiOpaquePixelRect(sprite).width >= 20f &&
                                                           SpriteGuiOpaquePixelRect(sprite).height >= 28f);

            showMainMenu = true;
            showResumePrompt = false;
            showGuidePanel = true;
            guideTab = 2;
            guideScroll = Vector2.zero;
            yield return null;
            if (HasCommandLineArgument("-qaScreenshot"))
            {
                // QaScreenshotRunner owns the final full-screen IMGUI capture. Keep this probe
                // alive long enough for its end-of-frame capture instead of reading the camera
                // target, which intentionally excludes IMGUI.
                yield return new WaitForSecondsRealtime(3f);
            }

            var passed = layoutSafe && allBodiesPresent;
            Debug.Log($"QA_GUIDE_BOSS_FRAME_266 layout={layoutSafe} bodies={allBodiesPresent} " +
                      $"frame={frame} inner={inner} portraits={portraits.Count(sprite => sprite != null)}/10");
            Application.Quit(passed ? 0 : 67);
        }
    }
}
