using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private void ConfigureEconomyShopPreview()
        {
            showMainMenu = true;
            showShopPanel = true;
            showPregameLoadout = false;
            shopCategory = ShopCategory.Supplies;
        }

        private void ConfigurePregameLoadoutPreview()
        {
            showMainMenu = true;
            showShopPanel = false;
            showPregameLoadout = true;
            foreach (var item in economy.Catalog.Where(item => item.PregameSelectable))
                economy.GrantPurchasedItem(item.Id, 2);
        }

        private void ConfigureActiveTacticalItemsPreview()
        {
            showMainMenu = false;
            showPregameLoadout = false;
            activeRunItems.Clear();
            activeRunItems.Add(TacticalItemId.FateCompass);
            activeRunItems.Add(TacticalItemId.MasteryManual);
            activeRunItems.Add(TacticalItemId.AllBoost);
            inspectedRunItem = (int)TacticalItemId.FateCompass;
        }

        private void EnforceEconomyVisualPreviewState()
        {
            if (HasCommandLineArgument("-qaPregameLoadoutView"))
            {
                showMainMenu = true;
                showShopPanel = false;
                showPregameLoadout = true;
            }
            else if (HasCommandLineArgument("-qaEconomyShopView"))
            {
                showMainMenu = true;
                showShopPanel = true;
                showPregameLoadout = false;
                shopCategory = ShopCategory.Supplies;
            }
            else if (HasCommandLineArgument("-qaActiveTacticalItemsView"))
            {
                showMainMenu = false;
                showPregameLoadout = false;
                activeRunItems.Clear();
                activeRunItems.Add(TacticalItemId.FateCompass);
                activeRunItems.Add(TacticalItemId.MasteryManual);
                activeRunItems.Add(TacticalItemId.AllBoost);
                inspectedRunItem = (int)TacticalItemId.FateCompass;
            }
        }

        private IEnumerator QaEconomy300Routine()
        {
            yield return null;
            var failures = new List<string>();
            var items = economy?.Catalog ?? Array.Empty<TacticalItemDefinition>();
            if (items.Count != 11) failures.Add($"item-count:{items.Count}");
            if (items.Count(item => item.PregameSelectable) != 8 || economy.PregameSelectionLimit != 3)
                failures.Add("pregame-selection-layout-data");
            if (tacticalItemAtlasTexture == null || removeAdsTexture == null)
                failures.Add("item-icon-assets");
            if (items.Any(item => item.KoreanName.Contains("정찰") || item.EnglishName.Contains("SCOUT")))
                failures.Add("scouting-report-present");
            var expectedGems = new[] { 100, 305, 515, 1040, 2100 };
            var baseGems = new[] { 100, 300, 500, 1000, 2000 };
            var gemProducts = monetization.Products.Where(product => product.Category == ShopCategory.Currency)
                .OrderBy(product => product.GrantedGems).ToArray();
            if (!gemProducts.Select(product => product.GrantedGems).SequenceEqual(expectedGems))
                failures.Add("gem-ladder");
            var previousBonusRate = 0f;
            for (var index = 0; index < Math.Min(gemProducts.Length, baseGems.Length); index++)
            {
                var bonusRate = (gemProducts[index].GrantedGems - baseGems[index]) / (float)baseGems[index];
                if (bonusRate > .0501f || bonusRate + .0001f < previousBonusRate)
                    failures.Add($"gem-bonus-curve:{index}:{bonusRate:0.###}");
                previousBonusRate = bonusRate;
            }
            if (monetization.Products.Count(product => product.DirectPurchase) != 7)
                failures.Add("direct-product-count");
            var revive = items.FirstOrDefault(item => item.Id == TacticalItemId.ReviveTicket);
            if (revive == null || revive.GoldPrice != 200 || revive.GemPrice != 9)
                failures.Add("revive-standard-price");
            var all = items.FirstOrDefault(item => item.Id == TacticalItemId.AllBoost);
            if (all == null || all.GoldPrice >= 90 * 5 || all.GemPrice >= 7 * 5)
                failures.Add("all-boost-bundle-price");

            activeRunItems.Clear();
            activeRunItems.Add(TacticalItemId.AllBoost);
            activeRunItems.Add(TacticalItemId.TankBoost);
            if (!Mathf.Approximately(GetTacticalHealthMultiplier(UnitArchetype.Tank), 1.05f) ||
                !Mathf.Approximately(GetTacticalHealthMultiplier(UnitArchetype.Archer), 1.02f))
                failures.Add("percentage-boosts");
            activeRunItems.Add(TacticalItemId.MasteryManual);
            if (!Mathf.Approximately(GetTacticalExperienceMultiplier(), 1.06f))
                failures.Add("mastery-six-percent");
            activeRunItems.Clear();

            var previous = new string[RevivalSnapshotCount];
            var hadPrevious = new bool[RevivalSnapshotCount];
            for (var i = 0; i < RevivalSnapshotCount; i++)
            {
                var key = RevivalSnapshotKeyPrefix + i;
                hadPrevious[i] = PlayerPrefs.HasKey(key);
                previous[i] = PlayerPrefs.GetString(key, string.Empty);
            }
            try
            {
                ClearRevivalSnapshots();
                Money = 99;
                var seedDefinition = definitions[UnitArchetype.Tank];
                var seedPosition = NearestWalkable(new Vector2(0f, -5.72f), seedDefinition.Radius * .55f);
                TryPlaceUnit(UnitArchetype.Tank, seedPosition);
                if (units.Count == 0) failures.Add("snapshot-seed-unit");
                else
                {
                    foreach (var round in new[] { 3, 4, 5 })
                    {
                        Round = round;
                        CaptureRevivalSnapshot();
                    }
                    var savedRounds = Enumerable.Range(0, RevivalSnapshotCount)
                        .Select(index => ReadRevivalSnapshot(index)?.round ?? -1).ToArray();
                    if (!savedRounds.SequenceEqual(new[] { 5, 4, 3 }))
                    {
                        var rawLengths = Enumerable.Range(0, RevivalSnapshotCount)
                            .Select(index => PlayerPrefs.GetString(RevivalSnapshotKeyPrefix + index, string.Empty).Length)
                            .ToArray();
                        failures.Add($"snapshot-ring-order:{string.Join("/", savedRounds)}" +
                                     $":raw={string.Join("/", rawLengths)}:phase={Phase}:units={units.Count}:alive={units.Count(unit => unit != null && unit.IsAlive)}");
                    }
                    if (string.IsNullOrWhiteSpace(RevivalRosterSummary(ReadRevivalSnapshot(0))))
                        failures.Add("snapshot-roster-summary");
                }
            }
            finally
            {
                for (var i = 0; i < RevivalSnapshotCount; i++)
                {
                    var key = RevivalSnapshotKeyPrefix + i;
                    if (hadPrevious[i]) PlayerPrefs.SetString(key, previous[i]);
                    else PlayerPrefs.DeleteKey(key);
                }
                PlayerPrefs.Save();
            }

            var passed = failures.Count == 0;
            Debug.Log($"QA_ECONOMY_300 passed={passed} failures={string.Join(",", failures)} " +
                      $"items={items.Count} gemProducts={gemProducts.Length} direct=7 snapshots=3 " +
                      "rewardedRevive=false standardRevive=200g/9gem emergency=250g/or-play");
            Application.Quit(passed ? 0 : 100);
        }
    }
}
