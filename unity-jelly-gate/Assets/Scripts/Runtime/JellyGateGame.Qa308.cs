using System.Collections;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaRoyalUi308Routine()
        {
            yield return null;
            while (GetComponent<CrownfrontBootLoader>() != null) yield return null;
            GameLocalization.Current = GameLanguage.Korean;

            var castleNames = monetization.Products
                .Where(product => product.Category == ShopCategory.Castle)
                .Select(product => product.KoreanName).ToArray();
            var namesPassed = castleNames.Contains("하늘빛 성") && castleNames.Contains("노을빛 성") &&
                              !castleNames.Contains("청람 왕성") && !castleNames.Contains("홍염 왕성");
            var uvPassed = Mathf.Approximately(CastlePreviewUv.x, 0f) &&
                           Mathf.Approximately(CastlePreviewUv.y, .54f) &&
                           Mathf.Approximately(CastlePreviewUv.width, 1f) &&
                           Mathf.Approximately(CastlePreviewUv.height, .46f);
            var menuArtPassed = mainMenuTexture != null && mainMenuTexture.name == "main-menu-core-v6" &&
                                 mainMenuTexture.width == 941 && mainMenuTexture.height == 1672;
            var playRect = MainMenuPlayRect(SafeGuiRect);
            var dockRect = MainMenuDockRect(SafeGuiRect);
            var hubLayoutPassed = playRect.width >= 240f && playRect.yMax + 10f <= dockRect.y &&
                                  dockRect.width <= SafeGuiRect.width && dockRect.height >= 68f;

            showMainMenu = true;
            showShopPanel = showSkinPanel = showMissionPanel = showGuidePanel = showSettings = false;
            yield return new WaitForSecondsRealtime(.08f);
            yield return CaptureFullFrameRoutine("Crownfront-code17-main-menu-restyle.ppm");

            showSkinPanel = true;
            skinCategory = ShopCategory.Castle;
            yield return new WaitForSecondsRealtime(.08f);
            yield return CaptureFullFrameRoutine("Crownfront-code17-castle-preview-unified.ppm");
            showSkinPanel = false;

            var castleProduct = monetization.Products.First(product => product.Category == ShopCategory.Castle);
            OpenCurrencyShortageDialog(ShopCurrency.Gems, castleProduct);
            var shortageBlocksInput = CurrencyShortagePromptVisible && !MainMenuBaseInputEnabled;
            yield return new WaitForSecondsRealtime(.08f);
            yield return CaptureFullFrameRoutine("Crownfront-code17-gem-shortage-modal.ppm");
            OpenGemStoreFromShortage();
            var gemRoutePassed = !CurrencyShortagePromptVisible && showShopPanel &&
                                 shopCategory == ShopCategory.Currency && pendingPurchaseProduct == null;
            showShopPanel = false;

            OpenCurrencyShortageDialog(ShopCurrency.Gold, castleProduct);
            var goldDialogPassed = CurrencyShortagePromptVisible && !MainMenuBaseInputEnabled;
            CloseCurrencyShortageDialog();

            var passed = namesPassed && uvPassed && menuArtPassed && hubLayoutPassed && shortageBlocksInput &&
                         gemRoutePassed && goldDialogPassed && RosterDragPreviewCircularForQa;
            Debug.Log($"QA_ROYAL_UI_308 passed={passed} names={namesPassed}:{string.Join("/", castleNames)} " +
                       $"castleUv={uvPassed}:{CastlePreviewUv} menuArt={menuArtPassed}:{mainMenuTexture?.name} " +
                       $"hubLayout={hubLayoutPassed}:{playRect}/{dockRect} " +
                      $"shortageBlock={shortageBlocksInput} gemRoute={gemRoutePassed} " +
                      $"goldDialog={goldDialogPassed} dragRing={RosterDragPreviewCircularForQa}");
            Application.Quit(passed ? 0 : 128);
        }
    }
}
