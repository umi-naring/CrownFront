using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public enum ShopCategory
    {
        Castle,
        Unit,
        MainMenu,
        Supplies,
        Currency,
        Utility
    }

    [Serializable]
    public sealed class CrownfrontShopProduct
    {
        public string Id;
        public ShopCategory Category;
        public string KoreanName;
        public string EnglishName;
        public string KoreanDescription;
        public string EnglishDescription;
        public string FallbackPrice;
        public string EnglishFallbackPrice;
        public Color Accent;
        public UnitArchetype TargetUnit;
        public int GoldPrice;
        public int GemPrice;
        public int GrantedGems;
        public bool DirectPurchase;
        public bool ShowInShop = true;
        public bool Consumable;
        public TacticalItemId TacticalItem;
        public bool HasTacticalItem;

        public string Name => GameLocalization.English ? EnglishName : KoreanName;
        public string Description => GameLocalization.English ? EnglishDescription : KoreanDescription;
        public string FallbackPriceForLocale =>
            GameLocalization.English && !string.IsNullOrWhiteSpace(EnglishFallbackPrice)
                ? EnglishFallbackPrice
                : FallbackPrice;
    }

    /// <summary>
    /// Owns Google Play one-time products, cosmetic entitlements and post-run interstitial ads.
    /// AdMob mediation selects Google demand first and can fill with Unity Ads bidding. If that
    /// complete request fails, the Android bridge performs one direct Unity Ads fallback using
    /// the dashboard Game ID and interstitial placement configured in the services asset.
    /// The Android bridge is injected into the Gradle project by GooglePlayAndroidPostprocessor.
    /// Editor/desktop builds retain the complete shop UI but never grant paid products.
    /// </summary>
    public sealed class CrownfrontMonetization : MonoBehaviour
    {
        public const string RemoveAdsId = "crownfront.remove_ads_2000";
        public const string EmergencyReviveId = "crownfront.revive.emergency";
        public const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";

        private const string OwnedPrefix = "Crownfront.Shop.Owned.";
        private const string EquippedCastleKey = "Crownfront.Shop.EquippedCastle";
        private const string EquippedUnitKeyPrefix = "Crownfront.Shop.EquippedUnit.";
        private const string EquippedMenuKey = "Crownfront.Shop.EquippedMenu";

        private readonly List<CrownfrontShopProduct> products = new();
        private readonly HashSet<string> owned = new();
        private readonly Dictionary<string, string> localizedPrices = new();
        private readonly Dictionary<UnitArchetype, string> equippedUnitSkins = new();
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject androidBridge;
#endif
        private Action<string> statusSink;
        private bool initialized;
        private bool runAdShown;

        private static bool IsRuntimeQa => Array.Exists(Environment.GetCommandLineArgs(), arg =>
            arg.StartsWith("-qa", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(arg, "-qaScreenshot", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(arg, "-qaDelay", StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<CrownfrontShopProduct> Products => products;
        public event Action CosmeticsChanged;
        public event Action InterstitialClosed;
        public event Action<int> GemsPurchased;
        public event Action EmergencyRevivePurchased;
        public bool BillingReady { get; private set; }
        public bool AdsReady { get; private set; }
        public bool ConsentStatusKnown { get; private set; }
        public bool PrivacyOptionsRequired { get; private set; }
        public bool PurchaseInProgress { get; private set; }
        public string PurchaseStatusMessage { get; private set; } = string.Empty;
        public string LastRequestedProductId { get; private set; } = string.Empty;
        public string LastNativeEventType { get; private set; } = string.Empty;
        public string LastAdNetwork { get; private set; } = string.Empty;
        public int CataloguedProductCount => localizedPrices.Count;
        public bool AdsRemoved => IsOwned(RemoveAdsId);
        public bool AllProductsOwnedForTesting =>
            products.Exists(product => !product.Consumable && !product.HasTacticalItem) &&
            products.Where(product => !product.Consumable && !product.HasTacticalItem)
                .All(product => IsOwned(product.Id));
        public string EquippedCastle { get; private set; } = string.Empty;
        public string EquippedMenu { get; private set; } = string.Empty;

        [Serializable]
        private sealed class NativeEvent
        {
            public string type;
            public string productId;
            public string price;
            public string message;
        }

        [Serializable]
        private sealed class GoogleServicesConfig
        {
            public bool useTestAds = true;
            public string interstitialAdUnitId = string.Empty;
            public string unityAdsAndroidGameId = string.Empty;
            public string unityAdsInterstitialPlacementId = string.Empty;
        }

        public void Initialize(Action<string> messageSink)
        {
            if (initialized) return;
            initialized = true;
            statusSink = messageSink;
            name = "CrownfrontMonetization";
            BuildCatalog();
            LoadEntitlements();

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridgeClass = new AndroidJavaClass(
                    "com.crownfront.monetization.CrownfrontMonetizationBridge");
                var directProducts = products.FindAll(product => product.DirectPurchase);
                var productIds = new string[directProducts.Count];
                for (var i = 0; i < directProducts.Count; i++) productIds[i] = directProducts[i].Id;
                var config = ResolveServicesConfig();
                var testAds = config == null || config.useTestAds || !IsPlayStoreInstall();
                var interstitialId = testAds
                    ? TestInterstitialId
                    : config.interstitialAdUnitId?.Trim() ?? string.Empty;
                var unityGameId = config?.unityAdsAndroidGameId?.Trim() ?? string.Empty;
                var unityPlacementId = config?.unityAdsInterstitialPlacementId?.Trim() ?? string.Empty;
                androidBridge = bridgeClass.CallStatic<AndroidJavaObject>("create", activity, name,
                    productIds, interstitialId, unityGameId, unityPlacementId, testAds);
            }
            catch (Exception exception)
            {
                BillingReady = false;
                SetPurchaseStatus(GameLocalization.Text(
                    "Google Play 결제 모듈을 시작하지 못했습니다. 앱을 다시 실행해 주세요.",
                    "GOOGLE PLAY BILLING COULD NOT START. RESTART THE APP."), false);
                Debug.LogWarning($"Google Play monetization bridge unavailable: {exception.Message}");
            }
#else
            BillingReady = false;
#endif
        }

        private static GoogleServicesConfig ResolveServicesConfig()
        {
            var configAsset = Resources.Load<TextAsset>("crownfront-google-services");
            if (configAsset == null) return null;
            try
            {
                return JsonUtility.FromJson<GoogleServicesConfig>(configAsset.text);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPlayStoreInstall()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return string.Equals(Application.installerName, "com.android.vending",
                StringComparison.OrdinalIgnoreCase);
#else
            return false;
#endif
        }

        public void ShowPrivacyOptions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null)
            {
                androidBridge.Call("showPrivacyOptions");
                return;
            }
#endif
            statusSink?.Invoke(GameLocalization.Text(
                "개인정보 설정은 Android 기기에서 사용할 수 있습니다.",
                "PRIVACY OPTIONS ARE AVAILABLE ON ANDROID DEVICES."));
        }

        private void BuildCatalog()
        {
            products.Clear();
            Add("crownfront.castle.azure", ShopCategory.Castle, "청람 왕성", "AZURE CITADEL",
                "왕성을 청금석 지붕, 푸른 깃발과 성벽 광채로 단장합니다.", "Blue roofs, banners and a citadel wall glow.",
                "보석 45", "45 GEMS", new Color(.25f, .66f, 1f), gemPrice: 45);
            Add("crownfront.castle.ember", ShopCategory.Castle, "홍염 왕성", "EMBER CITADEL",
                "왕성에 붉은 금속 장식, 불꽃 봉화와 성벽 광채를 적용합니다.", "Crimson metalwork, ember beacons and a wall glow.",
                "보석 45", "45 GEMS", new Color(1f, .35f, .16f), gemPrice: 45);
            AddUnitSkins(UnitArchetype.Tank, "tank", "왕관 방패병", "CROWN SHIELD GUARD",
                ("청람 성기사", "AZURE PALADIN", new Color(.38f, .78f, 1f)),
                ("흑금 수호자", "OBSIDIAN WARDEN", new Color(.95f, .62f, .18f)));
            AddUnitSkins(UnitArchetype.Melee, "melee", "대지 망치병", "EARTHSHAKER GUARD",
                ("용광로 투사", "FORGE BRAWLER", new Color(1f, .34f, .16f)),
                ("빙정 파쇄자", "FROST BREAKER", new Color(.46f, .9f, 1f)));
            AddUnitSkins(UnitArchetype.Archer, "archer", "바람길 궁수", "GALE PATHFINDER",
                ("황혼 추적자", "DUSK RANGER", new Color(.74f, .44f, 1f)),
                ("금엽 명사수", "GOLDLEAF MARKSMAN", new Color(1f, .82f, .24f)));
            AddUnitSkins(UnitArchetype.AreaMage, "area_mage", "별가루 마법사", "STARDUST MAGE",
                ("적월 성운술사", "RED MOON NEBULIST", new Color(1f, .28f, .56f)),
                ("백야 천문관", "WHITE NIGHT ASTROLOGER", new Color(.7f, .9f, 1f)));
            AddUnitSkins(UnitArchetype.SingleMage, "single_mage", "유리구슬 마법사", "GLASS ORB MAGE",
                ("심해 수정술사", "ABYSS CRYSTALIST", new Color(.16f, .68f, 1f)),
                ("태양 프리즘술사", "SOLAR PRISMATIC", new Color(1f, .72f, .2f)));
            AddUnitSkins(UnitArchetype.Bombardier, "bombardier", "시계태엽 포병", "CLOCKWORK BOMBARDIER",
                ("증기 제독", "STEAM ADMIRAL", new Color(.76f, .5f, .28f)),
                ("청동 폭뢰관", "BRONZE THUNDERER", new Color(.35f, .92f, 1f)));
            AddUnitSkins(UnitArchetype.Lancer, "lancer", "용맥 창기병", "DRAGONVEIN LANCER",
                ("루비 용기병", "RUBY DRAGOON", new Color(1f, .25f, .28f)),
                ("백은 성창", "SILVER HOLY LANCE", new Color(.83f, .9f, 1f)));
            AddUnitSkins(UnitArchetype.Druid, "druid", "숲의 정령술사", "GROVE SPIRITCALLER",
                ("가을숲 현자", "AUTUMN SAGE", new Color(1f, .52f, .17f)),
                ("달꽃 정령사", "MOONBLOOM CALLER", new Color(.67f, .48f, 1f)));
            AddUnitSkins(UnitArchetype.Musketeer, "musketeer", "왕실 머스킷병", "ROYAL MUSKETEER",
                ("진홍 총사", "CRIMSON GUNNER", new Color(1f, .3f, .24f)),
                ("극광 저격수", "AURORA SNIPER", new Color(.3f, 1f, .78f)));
            AddUnitSkins(UnitArchetype.Oracle, "oracle", "달빛 예언자", "MOONLIGHT ORACLE",
                ("일식 예언자", "ECLIPSE ORACLE", new Color(.72f, .26f, 1f)),
                ("새벽 성녀", "DAWN SAINT", new Color(1f, .8f, .38f)));
            Add("crownfront.menu.sunrise", ShopCategory.MainMenu, "새벽 출정", "DAWN MUSTER",
                "불타는 여명, 왕실 군기와 공성전의 역광으로 메인 화면을 연출합니다.", "A hand-painted royal muster framed by burning dawn and siege-lit banners.",
                "보석 25", "25 GEMS", new Color(1f, .68f, .24f), gemPrice: 25);
            Add("crownfront.menu.moonlit", ShopCategory.MainMenu, "월광 전선", "MOONLIT FRONT",
                "거대한 월식, 폭풍운과 절제된 수정광으로 메인 화면을 연출합니다.", "A hand-painted lunar front with a vast eclipse, storm clouds, and restrained crystal light.",
                "보석 25", "25 GEMS", new Color(.48f, .54f, 1f), gemPrice: 25);
            var economy = GetComponent<CrownfrontEconomy>();
            if (economy != null)
            {
                foreach (var item in economy.Catalog)
                    Add($"crownfront.item.{item.Id.ToString().ToLowerInvariant()}", ShopCategory.Supplies,
                        item.KoreanName, item.EnglishName, item.KoreanDescription, item.EnglishDescription,
                        $"골드 {item.GoldPrice:N0} / 보석 {item.GemPrice:N0}",
                        $"{item.GoldPrice:N0} GOLD / {item.GemPrice:N0} GEMS", item.Accent,
                        goldPrice: item.GoldPrice, gemPrice: item.GemPrice,
                        tacticalItem: item.Id, hasTacticalItem: true);
            }
            Add(RemoveAdsId, ShopCategory.Utility, "광고 제거", "REMOVE ADS",
                "전투 결과 뒤에 표시되는 전면 광고를 영구히 제거합니다.",
                "Permanently removes interstitial ads shown after battle results.",
                "₩4,900", "$4.00", new Color(1f, .82f, .3f), directPurchase: true);
            AddGemPack("crownfront.gems.100", 100, "₩1,100", "$0.99", new Color(.4f, .86f, 1f));
            AddGemPack("crownfront.gems.310", 305, "₩4,200", "$2.99", new Color(.46f, .82f, 1f));
            AddGemPack("crownfront.gems.525", 515, "₩7,000", "$4.99", new Color(.58f, .72f, 1f));
            AddGemPack("crownfront.gems.1075", 1040, "₩14,000", "$9.99", new Color(.72f, .6f, 1f));
            AddGemPack("crownfront.gems.2200", 2100, "₩28,000", "$19.99", new Color(.94f, .58f, 1f));
            Add(EmergencyReviveId, ShopCategory.Utility, "긴급 전선 복귀", "EMERGENCY FRONT RETURN",
                "패배 화면에서 선택한 안전 편성으로 즉시 복귀합니다.",
                "Immediately return to the selected safe formation after defeat.",
                "₩150", "$0.15", new Color(.42f, .95f, .82f), directPurchase: true,
                showInShop: false, consumable: true);
        }

        private void AddGemPack(string id, int grantedGems, string koreanPrice, string englishPrice, Color accent)
        {
            var bonusText = grantedGems switch
            {
                305 => "300 + 5",
                515 => "500 + 15",
                1040 => "1,000 + 40",
                2100 => "2,000 + 100",
                _ => "100"
            };
            Add(id, ShopCategory.Currency, $"보석 {bonusText}", $"{bonusText} GEMS",
                "Google Play 결제 완료 즉시 보석 지갑에 지급됩니다.",
                "Added to your gem wallet immediately after Google Play confirms payment.",
                koreanPrice, englishPrice, accent, grantedGems: grantedGems,
                directPurchase: true, consumable: true);
        }

        private void AddUnitSkins(UnitArchetype target, string id, string koUnit, string enUnit,
            (string ko, string en, Color color) first, (string ko, string en, Color color) second)
        {
            Add($"crownfront.unit.{id}.a", ShopCategory.Unit, first.ko, first.en,
                $"{koUnit}와 영웅 진화 외형에 전용 색상·광채·문양을 적용합니다.",
                $"Applies an exclusive palette, glow and crest to {enUnit} and its hero form.",
                "보석 35", "35 GEMS", first.color, target, gemPrice: 35);
            Add($"crownfront.unit.{id}.b", ShopCategory.Unit, second.ko, second.en,
                $"{koUnit}와 영웅 진화 외형에 전용 색상·광채·문양을 적용합니다.",
                $"Applies an exclusive palette, glow and crest to {enUnit} and its hero form.",
                "보석 35", "35 GEMS", second.color, target, gemPrice: 35);
        }

        private void Add(string id, ShopCategory category, string koName, string enName,
            string koDescription, string enDescription, string koreanPrice, string englishPrice, Color accent,
            UnitArchetype targetUnit = UnitArchetype.None, int goldPrice = 0, int gemPrice = 0,
            int grantedGems = 0, bool directPurchase = false, bool showInShop = true,
            bool consumable = false, TacticalItemId tacticalItem = TacticalItemId.ReviveTicket,
            bool hasTacticalItem = false)
        {
            products.Add(new CrownfrontShopProduct
            {
                Id = id,
                Category = category,
                KoreanName = koName,
                EnglishName = enName,
                KoreanDescription = koDescription,
                EnglishDescription = enDescription,
                FallbackPrice = koreanPrice,
                EnglishFallbackPrice = englishPrice,
                Accent = accent,
                TargetUnit = targetUnit,
                GoldPrice = goldPrice,
                GemPrice = gemPrice,
                GrantedGems = grantedGems,
                DirectPurchase = directPurchase,
                ShowInShop = showInShop,
                Consumable = consumable,
                TacticalItem = tacticalItem,
                HasTacticalItem = hasTacticalItem
            });
        }

        private void LoadEntitlements()
        {
            owned.Clear();
            foreach (var product in products)
                if (PlayerPrefs.GetInt(OwnedPrefix + product.Id, 0) == 1) owned.Add(product.Id);
            EquippedCastle = PlayerPrefs.GetString(EquippedCastleKey, string.Empty);
            equippedUnitSkins.Clear();
            foreach (UnitArchetype unit in Enum.GetValues(typeof(UnitArchetype)))
            {
                if (unit == UnitArchetype.None) continue;
                var equipped = PlayerPrefs.GetString(EquippedUnitKeyPrefix + unit, string.Empty);
                if (!string.IsNullOrEmpty(equipped)) equippedUnitSkins[unit] = equipped;
            }
            EquippedMenu = PlayerPrefs.GetString(EquippedMenuKey, string.Empty);
        }

        public bool IsOwned(string productId) => !string.IsNullOrEmpty(productId) && owned.Contains(productId);

        public CrownfrontShopProduct GrantRandomVictoryCosmetic()
        {
            var priorities = new[] { ShopCategory.Unit, ShopCategory.Castle, ShopCategory.MainMenu };
            foreach (var category in priorities)
            {
                var candidates = products.FindAll(product => product.Category == category && !IsOwned(product.Id));
                if (candidates.Count == 0) continue;
                var reward = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                Grant(reward.Id);
                CosmeticsChanged?.Invoke();
                return reward;
            }
            return null;
        }

        public string PriceFor(CrownfrontShopProduct product) =>
            product != null && localizedPrices.TryGetValue(product.Id, out var price) &&
            !string.IsNullOrWhiteSpace(price) ? price : product?.FallbackPriceForLocale ?? string.Empty;

        public bool IsEquipped(CrownfrontShopProduct product)
        {
            if (product == null) return false;
            return product.Category switch
            {
                ShopCategory.Castle => EquippedCastle == product.Id,
                ShopCategory.Unit => EquippedUnitSkin(product.TargetUnit) == product.Id,
                ShopCategory.MainMenu => EquippedMenu == product.Id,
                _ => false
            };
        }

        public void Purchase(CrownfrontShopProduct product)
        {
            if (product == null)
            {
                SetPurchaseStatus(GameLocalization.Text("상품 정보를 찾지 못했습니다.",
                    "PRODUCT INFORMATION IS MISSING."), false);
                return;
            }
            LastRequestedProductId = product.Id;
            if (!product.DirectPurchase)
            {
                SetPurchaseStatus(GameLocalization.Text(
                    "이 상품은 게임 내 골드 또는 보석으로 구매합니다.",
                    "BUY THIS ITEM WITH IN-GAME GOLD OR GEMS."), false);
                return;
            }
            if (!product.Consumable && IsOwned(product.Id))
            {
                SetPurchaseStatus(GameLocalization.Text(
                    "이미 보유한 스킨입니다. 메인 메뉴의 스킨 보관함에서 장착할 수 있습니다.",
                    "OWNED. EQUIP IT FROM THE SKIN VAULT ON THE MAIN MENU."), false);
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null)
            {
                PurchaseInProgress = true;
                if (!BillingReady)
                {
                    SetPurchaseStatus(GameLocalization.Text(
                        "Google Play 결제 서비스 연결을 확인한 뒤 자동으로 다시 시도합니다.",
                        "CHECKING GOOGLE PLAY BILLING, THEN RETRYING AUTOMATICALLY."), true);
                    androidBridge.Call("retryBillingAndPurchase", product.Id);
                }
                else
                {
                    SetPurchaseStatus(GameLocalization.Text("Google Play 결제창을 준비 중입니다.",
                        "PREPARING GOOGLE PLAY CHECKOUT."), true);
                    androidBridge.Call("purchase", product.Id);
                }
                return;
            }
#endif
            SetPurchaseStatus(GameLocalization.Text(
                "결제는 Google Play 테스트 트랙 또는 라이선스 테스터 빌드에서 사용할 수 있습니다.",
                "PURCHASES REQUIRE A GOOGLE PLAY TEST TRACK OR LICENSE-TESTER BUILD."), false);
        }

        private void SetPurchaseStatus(string message, bool pending)
        {
            PurchaseInProgress = pending;
            PurchaseStatusMessage = message ?? string.Empty;
        }

        public void Equip(CrownfrontShopProduct product)
        {
            if (product == null || !IsOwned(product.Id) || product.Category == ShopCategory.Utility) return;
            switch (product.Category)
            {
                case ShopCategory.Castle:
                    EquippedCastle = product.Id;
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedCastleKey, EquippedCastle);
                    break;
                case ShopCategory.Unit:
                    equippedUnitSkins[product.TargetUnit] = product.Id;
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedUnitKeyPrefix + product.TargetUnit, product.Id);
                    break;
                case ShopCategory.MainMenu:
                    EquippedMenu = product.Id;
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedMenuKey, EquippedMenu);
                    break;
            }
            if (!IsRuntimeQa) PlayerPrefs.Save();
            CosmeticsChanged?.Invoke();
            statusSink?.Invoke(GameLocalization.Text("스킨 장착 상태가 변경되었습니다.",
                "Cosmetic loadout updated."));
        }

        public void EquipDefault(ShopCategory category, UnitArchetype unit = UnitArchetype.None)
        {
            switch (category)
            {
                case ShopCategory.Castle:
                    EquippedCastle = string.Empty;
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedCastleKey, string.Empty);
                    break;
                case ShopCategory.Unit when unit != UnitArchetype.None:
                    equippedUnitSkins.Remove(unit);
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedUnitKeyPrefix + unit, string.Empty);
                    break;
                case ShopCategory.MainMenu:
                    EquippedMenu = string.Empty;
                    if (!IsRuntimeQa) PlayerPrefs.SetString(EquippedMenuKey, string.Empty);
                    break;
                default:
                    return;
            }
            if (!IsRuntimeQa) PlayerPrefs.Save();
            CosmeticsChanged?.Invoke();
            statusSink?.Invoke(GameLocalization.Text("기본 스킨을 장착했습니다.",
                "DEFAULT SKIN EQUIPPED."));
        }

        public CrownfrontShopProduct FindProduct(string productId) =>
            products.Find(product => product.Id == productId);

        public string EquippedUnitSkin(UnitArchetype unit) =>
            equippedUnitSkins.TryGetValue(unit, out var id) ? id : string.Empty;

        public int UnitSkinVariant(UnitArchetype unit)
        {
            var id = EquippedUnitSkin(unit);
            if (id.EndsWith(".a", StringComparison.Ordinal)) return 1;
            if (id.EndsWith(".b", StringComparison.Ordinal)) return 2;
            return 0;
        }

        public Color UnitTint(UnitArchetype unit)
        {
            var id = EquippedUnitSkin(unit);
            var product = products.Find(item => item.Id == id);
            if (product == null) return Color.white;
            // These are authored material palettes, not a single accent multiplied over every
            // unit.  Each paid skin changes the readable costume mass while the crest/accent
            // remains available separately for its weapon and spell effects.
            return id switch
            {
                "crownfront.unit.tank.a" => new Color(.48f, .78f, 1f),
                "crownfront.unit.tank.b" => new Color(.36f, .31f, .38f),
                "crownfront.unit.melee.a" => new Color(1f, .4f, .24f),
                "crownfront.unit.melee.b" => new Color(.56f, .9f, 1f),
                "crownfront.unit.archer.a" => new Color(.46f, .28f, .76f),
                "crownfront.unit.archer.b" => new Color(.38f, .68f, .36f),
                "crownfront.unit.area_mage.a" => new Color(.82f, .24f, .5f),
                "crownfront.unit.area_mage.b" => new Color(.76f, .9f, 1f),
                "crownfront.unit.single_mage.a" => new Color(.16f, .32f, .65f),
                "crownfront.unit.single_mage.b" => new Color(1f, .78f, .32f),
                "crownfront.unit.bombardier.a" => new Color(.65f, .43f, .26f),
                "crownfront.unit.bombardier.b" => new Color(.32f, .76f, .86f),
                "crownfront.unit.lancer.a" => new Color(.88f, .22f, .3f),
                "crownfront.unit.lancer.b" => new Color(.83f, .9f, .98f),
                "crownfront.unit.druid.a" => new Color(.9f, .46f, .18f),
                "crownfront.unit.druid.b" => new Color(.5f, .32f, .76f),
                "crownfront.unit.musketeer.a" => new Color(.74f, .22f, .2f),
                "crownfront.unit.musketeer.b" => new Color(.22f, .72f, .64f),
                "crownfront.unit.oracle.a" => new Color(.46f, .2f, .72f),
                "crownfront.unit.oracle.b" => new Color(1f, .78f, .36f),
                _ => UnitSkinVariant(unit) == 1
                    ? Color.Lerp(Color.white, product.Accent, .6f)
                    : Color.Lerp(new Color(.58f, .6f, .68f), product.Accent, .68f)
            };
        }

        public Color UnitSkinAccent(UnitArchetype unit)
        {
            var product = products.Find(item => item.Id == EquippedUnitSkin(unit));
            return product?.Accent ?? Color.clear;
        }

        public Color UnitSkinSecondary(UnitArchetype unit)
        {
            var id = EquippedUnitSkin(unit);
            var product = products.Find(item => item.Id == id);
            if (product == null) return Color.clear;
            if (id == "crownfront.unit.archer.a") return new Color(.11f, .08f, .18f, 1f);
            if (id == "crownfront.unit.archer.b") return new Color(.94f, .88f, .7f, 1f);
            return UnitSkinVariant(unit) == 1
                ? Color.Lerp(product.Accent, Color.white, .72f)
                : Color.Lerp(product.Accent, new Color(.05f, .04f, .08f), .64f);
        }

        public Color CastleAccent()
        {
            return EquippedCastle switch
            {
                "crownfront.castle.azure" => new Color(.24f, .72f, 1f),
                "crownfront.castle.ember" => new Color(1f, .32f, .13f),
                _ => new Color(.3f, .7f, 1f)
            };
        }

        public Color MenuTheme()
        {
            // Menu cosmetics now use complete authored scenes rather than a color wash.
            return Color.clear;
        }

        public void BeginRun() => runAdShown = false;

        public bool NotifyRunEnded()
        {
            if (runAdShown || AdsRemoved) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null)
            {
                runAdShown = true;
                androidBridge.Call("showInterstitial");
                return true;
            }
#endif
            return false;
        }

        public void OnMonetizationEvent(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            NativeEvent nativeEvent;
            try
            {
                nativeEvent = JsonUtility.FromJson<NativeEvent>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Invalid monetization event: {exception.Message}");
                return;
            }
            if (nativeEvent == null) return;
            LastNativeEventType = nativeEvent.type ?? string.Empty;
            switch (nativeEvent.type)
            {
                case "billing_ready":
                    BillingReady = true;
                    if (!PurchaseInProgress)
                        PurchaseStatusMessage = GameLocalization.Text("Google Play 결제 서비스 준비 완료",
                            "GOOGLE PLAY BILLING READY");
                    break;
                case "billing_unavailable":
                    BillingReady = false;
                    SetPurchaseStatus(string.IsNullOrWhiteSpace(nativeEvent.message)
                        ? GameLocalization.Text("Google Play 결제 서비스에 연결하지 못했습니다.",
                            "GOOGLE PLAY BILLING IS UNAVAILABLE.")
                        : LocalizeNativeBillingMessage(nativeEvent.message), false);
                    break;
                case "price":
                    if (!string.IsNullOrEmpty(nativeEvent.productId))
                        localizedPrices[nativeEvent.productId] = nativeEvent.price;
                    break;
                case "purchase_waiting":
                    SetPurchaseStatus(GameLocalization.Text(
                        "상품 정보를 확인 중입니다. 잠시 후 결제창이 자동으로 열립니다.",
                        "CHECKING PRODUCT DETAILS. CHECKOUT WILL OPEN AUTOMATICALLY."), true);
                    break;
                case "checkout_launched":
                    SetPurchaseStatus(GameLocalization.Text("Google Play 결제창을 열었습니다.",
                        "GOOGLE PLAY CHECKOUT OPENED."), true);
                    break;
                case "product_unavailable":
                    SetPurchaseStatus(GameLocalization.Text(
                        "이 상품을 Google Play에서 찾지 못했습니다. Play Console 상품 활성화와 테스트 트랙 설치 여부를 확인하세요.",
                        "PRODUCT NOT FOUND. CHECK PLAY CONSOLE ACTIVATION AND TEST-TRACK INSTALLATION."), false);
                    break;
                case "purchase_cancelled":
                    SetPurchaseStatus(GameLocalization.Text("구매를 취소했습니다.",
                        "PURCHASE CANCELLED."), false);
                    break;
                case "owned":
                    GrantNativePurchase(nativeEvent.productId, true);
                    SetPurchaseStatus(GameLocalization.Text("구매 소유권을 확인했습니다.",
                        "PURCHASE OWNERSHIP CONFIRMED."), false);
                    break;
                case "purchased":
                    GrantNativePurchase(nativeEvent.productId, false);
                    SetPurchaseStatus(GameLocalization.Text(
                        "구매가 확인되어 상품을 지급했습니다.",
                        "PURCHASE CONFIRMED AND DELIVERED."), false);
                    break;
                case "ad_loaded":
                    AdsReady = true;
                    LastAdNetwork = nativeEvent.message ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(LastAdNetwork))
                        Debug.Log($"Interstitial mediation adapter ready: {LastAdNetwork}");
                    break;
                case "unity_ad_loaded":
                    AdsReady = true;
                    LastAdNetwork = "UNITY_DIRECT";
                    Debug.Log("Direct Unity Ads fallback ready.");
                    break;
                case "ads_initialized":
                    ConsentStatusKnown = true;
                    break;
                case "consent_updating":
                    ConsentStatusKnown = false;
                    break;
                case "privacy_options_required":
                    ConsentStatusKnown = true;
                    PrivacyOptionsRequired = true;
                    break;
                case "privacy_options_not_required":
                    ConsentStatusKnown = true;
                    PrivacyOptionsRequired = false;
                    break;
                case "consent_error":
                    ConsentStatusKnown = true;
                    AdsReady = false;
                    Debug.LogWarning($"Ad consent error: {nativeEvent.message}");
                    break;
                case "ad_waiting_consent":
                    AdsReady = false;
                    break;
                case "ad_error":
                    AdsReady = false;
                    Debug.LogWarning($"Interstitial ad unavailable: {nativeEvent.message}");
                    InterstitialClosed?.Invoke();
                    break;
                case "ad_dismissed":
                    AdsReady = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                    androidBridge?.Call("loadInterstitial");
#endif
                    InterstitialClosed?.Invoke();
                    break;
                case "error":
                    SetPurchaseStatus(string.IsNullOrWhiteSpace(nativeEvent.message)
                        ? GameLocalization.Text("Google Play 요청을 완료하지 못했습니다.",
                            "Google Play request could not be completed.")
                        : LocalizeNativeBillingMessage(nativeEvent.message), false);
                    break;
            }
        }

        private static string LocalizeNativeBillingMessage(string nativeMessage)
        {
            if (string.IsNullOrWhiteSpace(nativeMessage))
                return GameLocalization.Text("Google Play 결제 요청을 완료하지 못했습니다.",
                    "GOOGLE PLAY BILLING REQUEST FAILED.");
            if (nativeMessage.StartsWith("BILLING_NOT_READY|", StringComparison.Ordinal))
                return GameLocalization.Text("Google Play 결제 서비스가 준비되지 않았습니다. Play 스토어 로그인과 네트워크를 확인하세요.",
                    "BILLING IS NOT READY. CHECK PLAY STORE SIGN-IN AND NETWORK.");
            if (nativeMessage.StartsWith("PRODUCT_NOT_FOUND|", StringComparison.Ordinal))
                return GameLocalization.Text("상품이 활성화되지 않았거나 현재 설치본에서 판매할 수 없습니다.",
                    "THE PRODUCT IS NOT ACTIVE OR IS UNAVAILABLE FOR THIS INSTALLATION.");
            return nativeMessage;
        }

        private void Grant(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId)) return;
            owned.Add(productId);
            if (!IsRuntimeQa)
            {
                PlayerPrefs.SetInt(OwnedPrefix + productId, 1);
                PlayerPrefs.Save();
            }
        }

        public void GrantInGameProduct(string productId)
        {
            var product = FindProduct(productId);
            if (product == null || product.DirectPurchase || product.Consumable) return;
            Grant(productId);
            CosmeticsChanged?.Invoke();
        }

        private void GrantNativePurchase(string productId, bool restored)
        {
            var product = FindProduct(productId);
            if (product == null) return;
            if (product.GrantedGems > 0)
            {
                // Consumables are never granted during ownership restoration; the Android bridge
                // consumes them and reports only a completed fresh purchase.
                if (!restored) GemsPurchased?.Invoke(product.GrantedGems);
                return;
            }
            if (product.Id == EmergencyReviveId)
            {
                if (!restored) EmergencyRevivePurchased?.Invoke();
                return;
            }
            Grant(productId);
            CosmeticsChanged?.Invoke();
        }

        internal void GrantForQa(string productId)
        {
            Grant(productId);
        }

        public void GrantAllProductsForTesting()
        {
            foreach (var product in products)
                if (!product.Consumable && !product.HasTacticalItem) Grant(product.Id);
            CosmeticsChanged?.Invoke();
            statusSink?.Invoke(GameLocalization.Text(
                "\uD14C\uC2A4\uD2B8 \uC0C1\uD488\uC744 \uBAA8\uB450 \uC7A0\uAE08 \uD574\uC81C\uD588\uC2B5\uB2C8\uB2E4.",
                "ALL TEST PRODUCTS UNLOCKED."));
        }

        public void ResetAllProductsForTesting()
        {
            foreach (var product in products)
                PlayerPrefs.DeleteKey(OwnedPrefix + product.Id);
            owned.Clear();
            EquippedCastle = string.Empty;
            EquippedMenu = string.Empty;
            equippedUnitSkins.Clear();
            PlayerPrefs.DeleteKey(EquippedCastleKey);
            PlayerPrefs.DeleteKey(EquippedMenuKey);
            foreach (UnitArchetype unit in Enum.GetValues(typeof(UnitArchetype)))
                PlayerPrefs.DeleteKey(EquippedUnitKeyPrefix + unit);
            PlayerPrefs.Save();
            CosmeticsChanged?.Invoke();
            statusSink?.Invoke(GameLocalization.Text(
                "테스트 상품을 모두 다시 잠그고 기본 외형으로 복원했습니다.",
                "ALL TEST PRODUCTS LOCKED AND DEFAULT COSMETICS RESTORED."));
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge == null) return;
            try
            {
                androidBridge.Call("dispose");
                androidBridge.Dispose();
            }
            catch
            {
                // The Android activity can already be gone during process shutdown.
            }
            androidBridge = null;
#endif
        }
    }
}
