using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRelease319Routine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;
            GameLocalization.Current = GameLanguage.Korean;

            var crestPassed = TopHudCrestFontIndependentForQa;
            var noSaveCopyPassed = MainMenuBriefingHeading(null) == "오늘의 전선" &&
                                   MainMenuRepresentativeProfileForRound(1) != null;
            var shortagePassed = CurrencyShortageMessage(true, null) == "보석을 충전하시겠습니까?" &&
                                 CurrencyShortageMessage(true, "테스트 스킨")
                                     .EndsWith("\n보석을 충전하시겠습니까?");
            var roster = new[]
            {
                UnitArchetype.Tank, UnitArchetype.Melee, UnitArchetype.Archer,
                UnitArchetype.AreaMage, UnitArchetype.SingleMage, UnitArchetype.Bombardier,
                UnitArchetype.Lancer, UnitArchetype.Druid, UnitArchetype.Musketeer,
                UnitArchetype.Oracle
            };
            var cardsPassed = roster.All(archetype =>
                canonicalDefaultCardSprites.TryGetValue($"{archetype}:0:0", out var card) &&
                card != null && card.texture != null &&
                card.texture.name.Contains("-canonical-card-normal-") &&
                SpriteGuiOpaquePixelRect(card).height > 0f) &&
                rosterCardArtifactsRemaining == 0;
            var mageCardsPassed = new[] { UnitArchetype.AreaMage, UnitArchetype.SingleMage }
                .All(archetype => canonicalDefaultCardSprites.TryGetValue($"{archetype}:0:0", out var card) &&
                                  card.texture.name.Contains("-clean"));
            foreach (var archetype in new[] { UnitArchetype.AreaMage, UnitArchetype.SingleMage })
            {
                if (!canonicalDefaultCardSprites.TryGetValue($"{archetype}:0:0", out var card) ||
                    card == null || card.texture == null) continue;
                WriteRawSpriteBmp263(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..",
                        $"Crownfront-code19-{archetype}-card.bmp")),
                    card.texture.GetPixels32(), card.texture.width, card.texture.height);
            }

            showMainMenu = false;
            showFormationPanel = true;
            Phase = GamePhase.Preparation;
            Round = 2;
            Money = 11;
            yield return new WaitForSecondsRealtime(.12f);
            yield return CaptureFullFrameRoutine("Crownfront-code19-hud-roster-fix.ppm");

            showMainMenu = true;
            showFormationPanel = false;
            yield return new WaitForSecondsRealtime(.12f);
            yield return CaptureFullFrameRoutine("Crownfront-code19-no-save-briefing.ppm");

            var passed = crestPassed && noSaveCopyPassed && shortagePassed && cardsPassed && mageCardsPassed;
            Debug.Log($"QA_RELEASE_319 passed={passed} crest={crestPassed} noSave={noSaveCopyPassed} " +
                      $"shortage={shortagePassed} cards={cardsPassed}:removed={rosterCardArtifactsRemoved}:" +
                      $"remaining={rosterCardArtifactsRemaining} mages={mageCardsPassed}");
            Application.Quit(passed ? 0 : 131);
        }
    }
}
