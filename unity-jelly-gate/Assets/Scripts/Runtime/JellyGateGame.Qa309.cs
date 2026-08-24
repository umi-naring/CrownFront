using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaUiReview309Routine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;
            GameLocalization.Current = GameLanguage.Korean;

            var safe = SafeGuiRect;
            var briefing = MainMenuBriefingRect(safe);
            var play = MainMenuPlayRect(safe);
            var dock = MainMenuDockRect(safe);
            var hierarchyPassed = briefing.width >= 320f && briefing.height >= 88f &&
                                  briefing.yMax + 12f <= play.y && play.yMax + 10f <= dock.y &&
                                  briefing.x >= safe.x && briefing.xMax <= safe.xMax;

            ResetUiReviewState();
            showMainMenu = true;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-main-menu.ppm");

            ResetUiReviewState();
            showMainMenu = true;
            showShopPanel = true;
            shopCategory = ShopCategory.Supplies;
            shopScroll = Vector2.zero;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-shop.ppm");

            ResetUiReviewState();
            showMainMenu = true;
            showSkinPanel = true;
            skinCategory = ShopCategory.Castle;
            skinUnit = UnitArchetype.Tank;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-skin-vault.ppm");

            ResetUiReviewState();
            showMainMenu = true;
            showPregameLoadout = true;
            foreach (var item in economy.Catalog.Where(item => item.PregameSelectable))
                economy.GrantPurchasedItem(item.Id, 2);
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-pregame.ppm");

            ResetUiReviewState();
            showMainMenu = false;
            showFormationPanel = true;
            Phase = GamePhase.Preparation;
            Round = 8;
            Money = 15;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-battle-hud.ppm");

            ResetUiReviewState();
            showMainMenu = false;
            Phase = GamePhase.Augment;
            currentOffers = GenerateOffers();
            augmentOverlayHidden = false;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-augment.ppm");

            ResetUiReviewState();
            showMainMenu = true;
            var castleProduct = monetization.Products.First(product => product.Category == ShopCategory.Castle);
            OpenCurrencyShortageDialog(ShopCurrency.Gems, castleProduct);
            var shortagePassed = CurrencyShortagePromptVisible && !MainMenuBaseInputEnabled;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-shortage.ppm");
            CloseCurrencyShortageDialog();

            ResetUiReviewState();
            showMainMenu = true;
            showMissionPanel = true;
            challengeScroll = Vector2.zero;
            yield return new WaitForSecondsRealtime(.1f);
            yield return CaptureFullFrameRoutine("Crownfront-ui-review-missions.ppm");

            var passed = hierarchyPassed && shortagePassed && currentOffers.Length == 0;
            Debug.Log($"QA_UI_REVIEW_309 passed={passed} hierarchy={hierarchyPassed}:" +
                      $"{briefing}/{play}/{dock} shortage={shortagePassed} captures=8");
            Application.Quit(passed ? 0 : 129);
        }

        private void ResetUiReviewState()
        {
            showMainMenu = false;
            showShopPanel = false;
            showSkinPanel = false;
            showMissionPanel = false;
            showGuidePanel = false;
            showSettings = false;
            showPregameLoadout = false;
            showResumePrompt = false;
            showExitConfirm = false;
            showSystemMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            currentOffers = System.Array.Empty<AugmentOffer>();
            augmentOverlayHidden = false;
            CloseCurrencyShortageDialog();
        }
    }
}
