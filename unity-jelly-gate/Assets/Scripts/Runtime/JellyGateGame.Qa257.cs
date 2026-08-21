using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private IEnumerator QaDefaultSkinCommerce257Routine()
        {
            yield return null;
            showMainMenu = false;
            showFormationPanel = false;
            Phase = GamePhase.Preparation;
            GameLocalization.Current = GameLanguage.Korean;
            var failures = new List<string>();
            var roster = Enum.GetValues(typeof(UnitArchetype)).Cast<UnitArchetype>()
                .Where(value => value != UnitArchetype.None).ToArray();
            var directions = new[]
            {
                Vector2.down, new Vector2(-1f, -1f).normalized, Vector2.left,
                new Vector2(-1f, 1f).normalized, Vector2.up,
                new Vector2(1f, 1f).normalized, Vector2.right,
                new Vector2(1f, -1f).normalized
            };
            var phases = new[] { .03f, .14f, .27f, .39f, .52f, .64f, .77f, .91f };

            monetization.ResetAllProductsForTesting();
            var maxBodySpread = 0f;
            var maxGroundDrift = 0f;
            var minimumCardFill = 1f;
            var sampledPoses = 0;
            var restoredCount = 0;
            foreach (var archetype in roster)
            {
                monetization.EquipDefault(ShopCategory.Unit, archetype);
                var baseSet = GetDirectionalAnimation(archetype, false);
                var baseSpriteIds = AllDirectionalSprites257(baseSet).Where(sprite => sprite != null)
                    .Select(sprite => sprite.GetInstanceID()).ToHashSet();
                var actor = new GameObject($"QA 257 Default {archetype}").AddComponent<PlayerUnit>();
                actor.Initialize(this, archetype, definitions[archetype], new Vector2(0f, -5.4f));
                var heights = new List<float>();
                var groundLines = new List<float>();
                var defaultFramesOnly = true;
                foreach (var direction in directions)
                foreach (var phase in phases)
                {
                    actor.PreviewDirectionHeightForQa(direction, phase);
                    heights.Add(actor.VisualBodyWorldHeightForQa);
                    groundLines.Add(actor.PreviewGroundContactForQa(direction, phase));
                    defaultFramesOnly &= baseSpriteIds.Contains(actor.CurrentSpriteIdForQa);
                    sampledPoses++;
                }
                var positiveHeights = heights.Where(value => value > .001f).ToArray();
                var bodySpread = positiveHeights.Length == heights.Count
                    ? positiveHeights.Max() / Mathf.Max(.001f, positiveHeights.Min()) - 1f
                    : 99f;
                var groundDrift = groundLines.Count > 0 ? groundLines.Max() - groundLines.Min() : 99f;
                maxBodySpread = Mathf.Max(maxBodySpread, bodySpread);
                maxGroundDrift = Mathf.Max(maxGroundDrift, groundDrift);
                if (bodySpread > .065f || groundDrift > .055f || !defaultFramesOnly || actor.SkinVariant != 0)
                    failures.Add($"default-{archetype}:body={bodySpread:P1}:ground={groundDrift:0.000}:frames={defaultFramesOnly}");

                var card = GetUnitCardSprite(archetype);
                var cardFill = SpriteGuiOpaqueFillForQa(card);
                minimumCardFill = Mathf.Min(minimumCardFill, cardFill);
                if (card == null || cardFill < .62f)
                    failures.Add($"card-{archetype}:fill={cardFill:P0}");

                var paid = monetization.Products.FirstOrDefault(product =>
                    product.Category == ShopCategory.Unit && product.TargetUnit == archetype &&
                    product.Id.EndsWith(".a", StringComparison.Ordinal));
                if (paid == null)
                {
                    failures.Add($"skin-product-{archetype}:missing");
                }
                else
                {
                    monetization.GrantForQa(paid.Id);
                    monetization.Equip(paid);
                    actor.RefreshCosmeticPresentation();
                    actor.PreviewDirectionHeightForQa(Vector2.right, .37f);
                    var paidSet = GetAuthoredSkinAnimation(archetype, 1, false);
                    var paidIds = AllDirectionalSprites257(paidSet).Where(sprite => sprite != null)
                        .Select(sprite => sprite.GetInstanceID()).ToHashSet();
                    var paidApplied = actor.SkinVariant == 1 && paidIds.Contains(actor.CurrentSpriteIdForQa);
                    monetization.EquipDefault(ShopCategory.Unit, archetype);
                    actor.RefreshCosmeticPresentation();
                    actor.PreviewDirectionHeightForQa(Vector2.right, .37f);
                    var defaultRestored = actor.SkinVariant == 0 &&
                                          baseSpriteIds.Contains(actor.CurrentSpriteIdForQa);
                    if (!paidApplied || !defaultRestored)
                        failures.Add($"restore-{archetype}:paid={paidApplied}:default={defaultRestored}");
                    else restoredCount++;
                }
                Destroy(actor.gameObject);
            }

            var productIds = monetization.Products.Select(product => product.Id).ToArray();
            var catalogValid = productIds.Length == 25 && productIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
                               productIds.Distinct(StringComparer.Ordinal).Count() == productIds.Length;
            if (!catalogValid) failures.Add($"catalog:{productIds.Length}/{productIds.Distinct().Count()}");
            var previousLanguage = GameLocalization.Current;
            GameLocalization.Current = GameLanguage.Korean;
            var removeAds = monetization.FindProduct(CrownfrontMonetization.RemoveAdsId);
            var koreanPrices =
                monetization.Products.Where(product => product.Category == ShopCategory.Castle)
                    .All(product => monetization.PriceFor(product) == "₩4,900") &&
                monetization.Products.Where(product => product.Category == ShopCategory.Unit)
                    .All(product => monetization.PriceFor(product) == "₩3,900") &&
                monetization.Products.Where(product => product.Category == ShopCategory.MainMenu)
                    .All(product => monetization.PriceFor(product) == "₩2,900") &&
                monetization.PriceFor(removeAds) == "₩4,900";
            GameLocalization.Current = GameLanguage.English;
            var dollarPrices =
                monetization.Products.Where(product => product.Category == ShopCategory.Castle)
                    .All(product => monetization.PriceFor(product) == "$4.00") &&
                monetization.Products.Where(product => product.Category == ShopCategory.Unit)
                    .All(product => monetization.PriceFor(product) == "$3.00") &&
                monetization.Products.Where(product => product.Category == ShopCategory.MainMenu)
                    .All(product => monetization.PriceFor(product) == "$2.00") &&
                monetization.PriceFor(removeAds) == "$4.00";
            GameLocalization.Current = previousLanguage;
            if (!koreanPrices || !dollarPrices)
                failures.Add($"prices:ko={koreanPrices}:usd={dollarPrices}");
            var purchaseProbe = monetization.Products.FirstOrDefault(product => product.Category == ShopCategory.Castle);
            var purchasePreviewSourcesReady = castleAzureTexture != null && castleEmberTexture != null &&
                                              mainMenuSunriseTexture != null && mainMenuMoonlitTexture != null &&
                                              monetization.Products.Where(product => product.Category == ShopCategory.Unit)
                                                  .All(product =>
                                                  {
                                                      var variant = product.Id.EndsWith(".b", StringComparison.Ordinal) ? 2 : 1;
                                                      var normal = GetAuthoredSkinAnimation(product.TargetUnit, variant, false);
                                                      var hero = GetAuthoredSkinAnimation(product.TargetUnit, variant, true);
                                                      return normal != null && normal.Down.Length > 0 &&
                                                             hero != null && hero.Down.Length > 0;
                                                  });
            pendingPurchaseProduct = purchaseProbe;
            var purchaseModalBlocksUnderlyingInput = !MainMenuBaseInputEnabled;
            pendingPurchaseProduct = null;
            var toastBeforePurchase = toastText;
            monetization.Purchase(purchaseProbe);
            var editorClickVisible = purchaseProbe != null &&
                                     monetization.LastRequestedProductId == purchaseProbe.Id &&
                                     !string.IsNullOrWhiteSpace(monetization.PurchaseStatusMessage) &&
                                     toastText == toastBeforePurchase;
            monetization.OnMonetizationEvent("{\"type\":\"purchase_waiting\",\"productId\":\"crownfront.castle.azure\"}");
            var waitingState = monetization.PurchaseInProgress &&
                               monetization.PurchaseStatusMessage.Contains("자동으로");
            monetization.OnMonetizationEvent("{\"type\":\"product_unavailable\",\"productId\":\"crownfront.castle.azure\",\"message\":\"PRODUCT_NOT_FOUND|qa\"}");
            var unavailableState = !monetization.PurchaseInProgress &&
                                   monetization.PurchaseStatusMessage.Contains("Play Console");
            var statusBeforeLegacyLoginEvent = monetization.PurchaseStatusMessage;
            monetization.OnMonetizationEvent("{\"type\":\"legacy_login_event\",\"message\":\"LEGACY\"}");
            var legacyLoginIgnored = monetization.PurchaseStatusMessage == statusBeforeLegacyLoginEvent;
            if (!editorClickVisible || !waitingState || !unavailableState || !legacyLoginIgnored ||
                !purchasePreviewSourcesReady || !purchaseModalBlocksUnderlyingInput)
                failures.Add($"commerce:click={editorClickVisible}:wait={waitingState}:" +
                             $"unavailable={unavailableState}:loginRemoved={legacyLoginIgnored}:" +
                             $"preview={purchasePreviewSourcesReady}:modal={purchaseModalBlocksUnderlyingInput}");

            monetization.ResetAllProductsForTesting();
            var passed = failures.Count == 0;
            Debug.Log($"QA_DEFAULT_SKIN_COMMERCE_257 passed={passed} roster={roster.Length} " +
                      $"poses={sampledPoses} restored={restoredCount} bodySpread={maxBodySpread:P2} " +
                      $"groundDrift={maxGroundDrift:0.000} cardFill={minimumCardFill:P0} " +
                      $"catalog={catalogValid} prices={koreanPrices}/{dollarPrices} commerce={editorClickVisible}/{waitingState}/" +
                      $"{unavailableState}/{legacyLoginIgnored} preview={purchasePreviewSourcesReady} " +
                      $"modal={purchaseModalBlocksUnderlyingInput} fail={string.Join(",", failures.Take(20))}");
            Application.Quit(passed ? 0 : 117);
        }

        private static IEnumerable<Sprite> AllDirectionalSprites257(DirectionalAnimationSet set)
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
