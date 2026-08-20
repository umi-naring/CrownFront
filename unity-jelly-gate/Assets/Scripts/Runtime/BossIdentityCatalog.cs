using UnityEngine;

namespace JellyGate
{
    public sealed class BossIdentityProfile
    {
        public readonly string SkillId;
        public readonly string PassiveId;
        public readonly string KoreanPassive;
        public readonly string EnglishPassive;
        public readonly string KoreanDescription;
        public readonly string EnglishDescription;
        public readonly Color Accent;

        public BossIdentityProfile(string skillId, string passiveId, string koPassive, string enPassive,
            string koDescription, string enDescription, Color accent)
        {
            SkillId = skillId;
            PassiveId = passiveId;
            KoreanPassive = koPassive;
            EnglishPassive = enPassive;
            KoreanDescription = koDescription;
            EnglishDescription = enDescription;
            Accent = accent;
        }

        public string PassiveName => GameLocalization.Text(KoreanPassive, EnglishPassive);
        public string PassiveDescription => GameLocalization.Text(KoreanDescription, EnglishDescription);
    }

    public static class BossIdentityCatalog
    {
        public static BossIdentityProfile For(EnemyClass family) => family switch
        {
            EnemyClass.Melee => new("slime_royal_split", "gelatin_crown", "젤라틴 왕관", "GELATIN CROWN",
                "한 번의 피해가 최대 체력의 8%를 넘지 않습니다.", "A single hit cannot exceed 8% max health.", new Color(.95f,.3f,.72f)),
            EnemyClass.Skeleton => new("lich_legion", "ossuary_plate", "납골당 갑주", "OSSUARY PLATE",
                "물리 피해를 28% 줄이지만 마법 피해를 15% 더 받습니다.", "Takes 28% less physical but 15% more magic damage.", new Color(.74f,.82f,.7f)),
            EnemyClass.Runner => new("warlord_charge", "warpath", "전쟁길", "WARPATH",
                "피격될수록 이동과 근접 공격이 최대 5회 가속됩니다.", "Hits accelerate movement and melee pressure up to 5 stacks.", new Color(1f,.38f,.12f)),
            EnemyClass.Brute => new("titan_quake", "bedrock_layers", "암반 적층", "BEDROCK LAYERS",
                "다섯 번째 피격마다 피해를 45% 줄이고 최대 체력 3.5%의 방벽을 얻습니다.", "Every fifth hit deals 45% less damage and grants a 3.5% max-health barrier.", new Color(.78f,.55f,.24f)),
            EnemyClass.Shaman => new("ancestral_hex", "elder_sap", "고목의 수액", "ELDER SAP",
                "5초마다 받는 피해 일부를 생명력으로 전환합니다.", "Converts part of an incoming hit into healing every 5 seconds.", new Color(.2f,1f,.58f)),
            EnemyClass.Siege => new("void_barrage", "clockwork_guard", "태엽식 방호", "CLOCKWORK GUARD",
                "네 번째 피격마다 피해의 60%를 흡수합니다.", "Absorbs 60% of every fourth hit.", new Color(.72f,.3f,1f)),
            EnemyClass.Piercer => new("bloodline_impale", "bloodscale_counter", "혈린 반격", "BLOODSCALE COUNTER",
                "근접 공격을 받으면 3초마다 공격자에게 순수 피해로 반격합니다.", "Counters melee attackers with pure damage every 3 seconds.", new Color(1f,.18f,.1f)),
            EnemyClass.Wisp => new("astral_tempest", "astral_phase", "성운 위상", "ASTRAL PHASE",
                "5초마다 물리 저항과 마법 저항 상태를 교대합니다.", "Alternates physical and magic resistance every 5 seconds.", new Color(.32f,.8f,1f)),
            EnemyClass.Flyer => new("skyfall_hunt", "storm_wing", "폭풍 날개", "STORM WING",
                "4초마다 원거리 공격 한 번을 회피합니다.", "Evades one ranged hit every 4 seconds.", new Color(.38f,.66f,1f)),
            EnemyClass.Mage => new("arcane_prism", "abyssal_lens", "심연 렌즈", "ABYSSAL LENS",
                "마법 피해를 받을 때마다 최대 15%까지 마력 방벽을 축적합니다.", "Magic hits build an arcane barrier up to 15% max health.", new Color(.62f,.28f,1f)),
            _ => new("slime_royal_split", "gelatin_crown", "젤라틴 왕관", "GELATIN CROWN",
                "한 번의 피해가 최대 체력의 8%를 넘지 않습니다.", "A single hit cannot exceed 8% max health.", new Color(.95f,.3f,.72f))
        };
    }
}
