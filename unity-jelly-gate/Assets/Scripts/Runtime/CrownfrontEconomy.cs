using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public enum TacticalItemId
    {
        ReviveTicket,
        TacticalReroll,
        FieldAid,
        FateCompass,
        MasteryManual,
        TankBoost,
        MeleeBoost,
        RangedBoost,
        MageBoost,
        SupportBoost,
        AllBoost
    }

    public enum ShopCurrency
    {
        Gold,
        Gems
    }

    [Serializable]
    public sealed class TacticalItemDefinition
    {
        public TacticalItemId Id;
        public string KoreanName;
        public string EnglishName;
        public string KoreanDescription;
        public string EnglishDescription;
        public int GoldPrice;
        public int GemPrice;
        public bool PregameSelectable;
        public Color Accent;

        public string Name => GameLocalization.English ? EnglishName : KoreanName;
        public string Description => GameLocalization.English ? EnglishDescription : KoreanDescription;
    }

    /// <summary>
    /// Persistent soft-currency wallet and tactical inventory. Google Play grants only gems and
    /// permanent ad removal; every tactical purchase is resolved here so there is one auditable
    /// balance path and no ad-based revival shortcut.
    /// </summary>
    public sealed class CrownfrontEconomy : MonoBehaviour
    {
        private const string GoldKey = "Crownfront.Economy.Gold.v1";
        private const string GemsKey = "Crownfront.Economy.Gems.v1";
        private const string ItemKeyPrefix = "Crownfront.Economy.Item.v1.";
        private const int NewAccountGold = 400;
        private const int MaxPregameSelection = 3;

        private readonly List<TacticalItemDefinition> catalog = new();
        private readonly HashSet<TacticalItemId> selectedPregameItems = new();

        public IReadOnlyList<TacticalItemDefinition> Catalog => catalog;
        public IReadOnlyCollection<TacticalItemId> SelectedPregameItems => selectedPregameItems;
        public int Gold { get; private set; }
        public int Gems { get; private set; }
        public int PregameSelectionLimit => MaxPregameSelection;
        public event Action Changed;

        public void Initialize()
        {
            BuildCatalog();
            Gold = Mathf.Max(0, PlayerPrefs.GetInt(GoldKey, NewAccountGold));
            Gems = Mathf.Max(0, PlayerPrefs.GetInt(GemsKey, 0));
        }

        private void BuildCatalog()
        {
            catalog.Clear();
            Add(TacticalItemId.ReviveTicket, "전선 복귀권", "FRONT RETURN TOKEN",
                "패배 시 이번 전선에서 한 번, 1~3라운드 전 편성으로 복귀합니다.",
                "Once per run, return to a formation from 1–3 rounds earlier after defeat.",
                200, 9, false, new Color(.42f, .9f, .82f));
            Add(TacticalItemId.TacticalReroll, "증강 재정비권", "AUGMENT REROLL",
                "증강 선택지를 같은 등급 안에서 한 번 다시 구성합니다.",
                "Reroll the current choices once within the same augment tier.",
                80, 7, false, new Color(.68f, .52f, 1f));
            Add(TacticalItemId.FieldAid, "야전 구호품", "FIELD AID",
                "전투 화면에서 사용하면 생존 유닛의 체력을 12% 회복합니다. 전선당 최대 2회.",
                "Use in battle to heal surviving units by 12%. Maximum twice per run.",
                100, 8, false, new Color(.38f, 1f, .68f));
            Add(TacticalItemId.FateCompass, "운명의 나침반", "COMPASS OF FATE",
                "이번 전선의 상위 등급 증강 가중치를 3%p 높입니다.",
                "Increase higher-tier augment weighting by 3 percentage points this run.",
                220, 10, true, new Color(.9f, .58f, 1f));
            Add(TacticalItemId.MasteryManual, "숙련 교본", "MASTERY MANUAL",
                "이번 전선에서 모든 아군의 경험치 획득량이 6% 증가합니다.",
                "All defenders gain 6% more experience this run.",
                240, 10, true, new Color(.4f, .76f, 1f));
            AddClassBoost(TacticalItemId.TankBoost, "수호 전술서", "WARDEN DRILL", "탱커", "Tank", new Color(.38f, .68f, 1f));
            AddClassBoost(TacticalItemId.MeleeBoost, "돌격 전술서", "VANGUARD DRILL", "근접", "Melee", new Color(1f, .42f, .23f));
            AddClassBoost(TacticalItemId.RangedBoost, "사격 전술서", "MARKSMAN DRILL", "원거리", "Ranged", new Color(.5f, .9f, .42f));
            AddClassBoost(TacticalItemId.MageBoost, "비전 전술서", "ARCANE DRILL", "마법사", "Mage", new Color(.73f, .42f, 1f));
            AddClassBoost(TacticalItemId.SupportBoost, "지원 전술서", "SUPPORT DRILL", "서포터", "Support", new Color(.42f, 1f, .84f));
            Add(TacticalItemId.AllBoost, "왕실 합동 교범", "ROYAL COMBINED DRILL",
                "이번 전선에서 모든 병과의 핵심 능력치가 2% 증가합니다.",
                "Increase every class's core stats by 2% this run.",
                350, 12, true, new Color(1f, .78f, .3f));
        }

        private void AddClassBoost(TacticalItemId id, string koName, string enName,
            string koRole, string enRole, Color accent) =>
            Add(id, koName, enName,
                $"이번 전선에서 {koRole} 병과의 핵심 능력치가 3% 증가합니다.",
                $"Increase {enRole} core stats by 3% this run.", 90, 7, true, accent);

        private void Add(TacticalItemId id, string koName, string enName, string koDescription,
            string enDescription, int gold, int gems, bool pregame, Color accent) =>
            catalog.Add(new TacticalItemDefinition
            {
                Id = id,
                KoreanName = koName,
                EnglishName = enName,
                KoreanDescription = koDescription,
                EnglishDescription = enDescription,
                GoldPrice = gold,
                GemPrice = gems,
                PregameSelectable = pregame,
                Accent = accent
            });

        public TacticalItemDefinition Definition(TacticalItemId id) =>
            catalog.FirstOrDefault(item => item.Id == id);

        public int Count(TacticalItemId id) => Mathf.Max(0,
            PlayerPrefs.GetInt(ItemKeyPrefix + id, 0));

        public void GrantGold(int amount)
        {
            if (amount <= 0) return;
            Gold = Mathf.Min(9_999_999, Gold + amount);
            PersistWallet();
        }

        public void GrantGems(int amount)
        {
            if (amount <= 0) return;
            Gems = Mathf.Min(999_999, Gems + amount);
            PersistWallet();
        }

        public bool TryBuyItem(TacticalItemId id, ShopCurrency currency)
        {
            var item = Definition(id);
            if (item == null) return false;
            var price = currency == ShopCurrency.Gold ? item.GoldPrice : item.GemPrice;
            if (!TrySpend(currency, price)) return false;
            SetCount(id, Count(id) + 1);
            return true;
        }

        public void GrantPurchasedItem(TacticalItemId id, int amount = 1)
        {
            if (Definition(id) == null || amount <= 0) return;
            SetCount(id, Count(id) + amount);
        }

        public bool TrySpend(ShopCurrency currency, int amount)
        {
            amount = Mathf.Max(0, amount);
            if (currency == ShopCurrency.Gold)
            {
                if (Gold < amount) return false;
                Gold -= amount;
            }
            else
            {
                if (Gems < amount) return false;
                Gems -= amount;
            }
            PersistWallet();
            return true;
        }

        public bool TryConsume(TacticalItemId id, int amount = 1)
        {
            amount = Mathf.Max(1, amount);
            var count = Count(id);
            if (count < amount) return false;
            SetCount(id, count - amount);
            selectedPregameItems.Remove(id);
            return true;
        }

        public bool TogglePregameSelection(TacticalItemId id)
        {
            var item = Definition(id);
            if (item == null || !item.PregameSelectable || Count(id) <= 0) return false;
            if (selectedPregameItems.Remove(id))
            {
                Changed?.Invoke();
                return true;
            }
            if (selectedPregameItems.Count >= MaxPregameSelection) return false;
            selectedPregameItems.Add(id);
            Changed?.Invoke();
            return true;
        }

        public HashSet<TacticalItemId> ConsumeSelectedPregameItems()
        {
            var result = new HashSet<TacticalItemId>();
            foreach (var id in selectedPregameItems.ToArray())
                if (TryConsume(id)) result.Add(id);
            selectedPregameItems.Clear();
            Changed?.Invoke();
            return result;
        }

        private void SetCount(TacticalItemId id, int count)
        {
            PlayerPrefs.SetInt(ItemKeyPrefix + id, Mathf.Max(0, count));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private void PersistWallet()
        {
            PlayerPrefs.SetInt(GoldKey, Gold);
            PlayerPrefs.SetInt(GemsKey, Gems);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
