using UnityEngine;

namespace JellyGate
{
    public sealed class EnemyVariantProfile
    {
        public readonly string Id;
        public readonly EnemyClass FamilyClass;
        public readonly EnemyClass CombatClass;
        public readonly string KoreanName;
        public readonly string EnglishName;
        public readonly float HealthMultiplier;
        public readonly float AttackMultiplier;
        public readonly float MagicMultiplier;
        public readonly float SpeedMultiplier;
        public readonly float ScaleMultiplier;
        public readonly Color Accent;

        public string Name => GameLocalization.English
            ? EnglishName
            : EnemyVariantCatalog.KoreanNameFor(Id, KoreanName);

        public EnemyVariantProfile(string id, EnemyClass familyClass, EnemyClass combatClass,
            string koreanName, string englishName, float health, float attack, float magic,
            float speed, float scale, Color accent)
        {
            Id = id;
            FamilyClass = familyClass;
            CombatClass = combatClass;
            KoreanName = koreanName;
            EnglishName = englishName;
            HealthMultiplier = health;
            AttackMultiplier = attack;
            MagicMultiplier = magic;
            SpeedMultiplier = speed;
            ScaleMultiplier = scale;
            Accent = accent;
        }
    }

    public static class EnemyVariantCatalog
    {
        private static EnemyVariantProfile P(string id, EnemyClass family, EnemyClass combat,
            string ko, string en, float hp, float atk, float magic, float speed, float scale, Color accent) =>
            new(id, family, combat, ko, en, hp, atk, magic, speed, scale, accent);

        private static readonly EnemyVariantProfile[,] Profiles =
        {
            {
                P("jelly_vanguard", EnemyClass.Melee, EnemyClass.Melee, "젤리 선봉대", "JELLY VANGUARD", .78f, .72f, .5f, 1.02f, .92f, new Color(.88f,.35f,.76f)),
                P("jelly_sprinter", EnemyClass.Melee, EnemyClass.Runner, "질주 젤리", "JELLY SPRINTER", .82f, .78f, .55f, 1.08f, .9f, new Color(.38f,1f,.58f)),
                P("jelly_mage", EnemyClass.Melee, EnemyClass.Mage, "젤리 마도사", "JELLY MAGE", .88f, .75f, .88f, 1f, .95f, new Color(.55f,.58f,1f)),
                P("jelly_bomber", EnemyClass.Melee, EnemyClass.Siege, "젤리 폭탄병", "JELLY BOMBER", .94f, .92f, .8f, .94f, 1f, new Color(1f,.48f,.24f)),
                P("jelly_king", EnemyClass.Melee, EnemyClass.Brute, "킹 젤리", "JELLY KING", 1f, 1f, .86f, .96f, 1.08f, new Color(1f,.32f,.7f))
            },
            {
                P("skeleton_soldier", EnemyClass.Skeleton, EnemyClass.Melee, "해골 병사", "SKELETON SOLDIER", .9f, .84f, .55f, 1f, .94f, new Color(.82f,.86f,.76f)),
                P("skeleton_archer", EnemyClass.Skeleton, EnemyClass.Melee, "해골 방패병", "SKELETON SHIELD GUARD", .94f, .9f, .55f, .98f, .97f, new Color(.72f,.86f,1f)),
                P("skeleton_mage", EnemyClass.Skeleton, EnemyClass.Melee, "해골 철퇴병", "SKELETON MACE GUARD", 1f, .96f, .55f, .94f, 1.01f, new Color(.68f,.62f,.88f)),
                P("death_knight", EnemyClass.Skeleton, EnemyClass.Melee, "죽음의 기사", "DEATH KNIGHT", 1.08f, 1.12f, .58f, .93f, 1.07f, new Color(.86f,.18f,.24f)),
                P("lich", EnemyClass.Skeleton, EnemyClass.Shaman, "리치", "LICH", 1.08f, .94f, 1.16f, .94f, 1.08f, new Color(.55f,.2f,1f))
            },
            {
                P("goblin_scout", EnemyClass.Runner, EnemyClass.Runner, "고블린 정찰병", "GOBLIN SCOUT", .9f, .86f, .52f, 1.08f, .9f, new Color(.56f,1f,.3f)),
                P("goblin_slinger", EnemyClass.Runner, EnemyClass.Mage, "고블린 투석병", "GOBLIN SLINGER", .94f, .92f, .62f, 1.02f, .94f, new Color(.84f,.7f,.25f)),
                P("goblin_hexer", EnemyClass.Runner, EnemyClass.Shaman, "고블린 주술사", "GOBLIN HEXER", .98f, .84f, 1.04f, 1f, .98f, new Color(.58f,.28f,1f)),
                P("goblin_raider", EnemyClass.Runner, EnemyClass.Piercer, "고블린 약탈대장", "GOBLIN RAIDER", 1.04f, 1.1f, .65f, 1.04f, 1.02f, new Color(1f,.38f,.14f)),
                P("goblin_warchief", EnemyClass.Runner, EnemyClass.Brute, "고블린 대족장", "GOBLIN WARCHIEF", 1.1f, 1.12f, .72f, .98f, 1.1f, new Color(1f,.2f,.1f))
            },
            {
                P("stone_shard", EnemyClass.Brute, EnemyClass.Melee, "파편 분쇄 골렘", "SHARDBREAKER GOLEM", .94f, .88f, .48f, .96f, 1f, new Color(.62f,.38f,.82f)),
                P("stone_guard", EnemyClass.Brute, EnemyClass.Brute, "성채 골렘", "BASTION GOLEM", 1f, 1f, .5f, .92f, 1.01f, new Color(.72f,.6f,.38f)),
                P("rune_golem", EnemyClass.Brute, EnemyClass.Mage, "룬 비전 골렘", "RUNE ARCANIST GOLEM", 1.02f, .86f, 1.08f, .91f, 1.02f, new Color(.48f,.24f,1f)),
                P("cannon_golem", EnemyClass.Brute, EnemyClass.Siege, "용광로 포격 골렘", "FURNACE BOMBARD GOLEM", 1.08f, 1.06f, .86f, .88f, 1.03f, new Color(1f,.46f,.18f)),
                P("mountain_titan", EnemyClass.Brute, EnemyClass.Brute, "고대 산악 거신", "ANCIENT MOUNTAIN TITAN", 1.14f, 1.14f, .7f, .86f, 1.08f, new Color(.32f,.72f,1f))
            },
            {
                P("thorn_wolf", EnemyClass.Shaman, EnemyClass.Runner, "가시 늑대", "THORN WOLF", .92f, .9f, .58f, 1.08f, .92f, new Color(.35f,1f,.42f)),
                P("bark_guard", EnemyClass.Shaman, EnemyClass.Melee, "나무껍질 수호자", "BARK GUARD", 1f, .96f, .62f, .96f, 1f, new Color(.48f,.78f,.24f)),
                P("forest_sprite", EnemyClass.Shaman, EnemyClass.Mage, "숲의 정령", "FOREST SPRITE", 1.03f, .84f, 1.06f, 1.02f, .96f, new Color(.2f,1f,.68f)),
                P("grove_shaman", EnemyClass.Shaman, EnemyClass.Shaman, "수림 주술사", "GROVE SHAMAN", 1.07f, .9f, 1.12f, .96f, 1.02f, new Color(.54f,.24f,1f)),
                P("ancient_ent", EnemyClass.Shaman, EnemyClass.Brute, "고대 나무거신", "ANCIENT ENT", 1.12f, 1.08f, 1f, .88f, 1.12f, new Color(.35f,.85f,.18f))
            },
            {
                P("gear_scout", EnemyClass.Siege, EnemyClass.Runner, "태엽 정찰기", "GEAR SCOUT", .94f, .9f, .56f, 1.06f, .92f, new Color(.78f,.65f,.36f)),
                P("clock_lancer", EnemyClass.Siege, EnemyClass.Piercer, "태엽 창기병", "CLOCK LANCER", 1f, 1.06f, .58f, 1f, .98f, new Color(.9f,.55f,.2f)),
                P("arc_coil", EnemyClass.Siege, EnemyClass.Mage, "전류 코일병", "ARC COIL", 1.02f, .9f, 1.08f, .96f, 1f, new Color(.24f,.82f,1f)),
                P("siege_engine", EnemyClass.Siege, EnemyClass.Siege, "태엽 공성포", "CLOCKWORK CANNON", 1.09f, 1.12f, .86f, .9f, 1.08f, new Color(1f,.46f,.16f)),
                P("iron_colossus", EnemyClass.Siege, EnemyClass.Brute, "철갑 거신", "IRON COLOSSUS", 1.16f, 1.16f, .76f, .86f, 1.14f, new Color(1f,.3f,.12f))
            },
            {
                P("crimson_hound", EnemyClass.Piercer, EnemyClass.Runner, "리자드 정찰병", "LIZARD SCOUT", .94f, .94f, .52f, 1.08f, .92f, new Color(.45f,.88f,.3f)),
                P("crimson_guard", EnemyClass.Piercer, EnemyClass.Melee, "리자드 방패병", "LIZARD SHIELDGUARD", 1.02f, 1.02f, .6f, .98f, 1f, new Color(.62f,.74f,.26f)),
                P("crimson_assassin", EnemyClass.Piercer, EnemyClass.Piercer, "리자드 창투사", "LIZARD SPEARMASTER", 1.06f, 1.12f, .58f, 1.06f, .98f, new Color(.88f,.42f,.16f)),
                P("blood_witch", EnemyClass.Piercer, EnemyClass.Mage, "리자드 용술사", "LIZARD DRACOMANCER", 1.1f, .9f, 1.14f, .98f, 1.02f, new Color(.26f,.82f,.72f)),
                P("crimson_tyrant", EnemyClass.Piercer, EnemyClass.Piercer, "흰수염 리자드 장로", "WHITE-BEARDED LIZARD ELDER", 1.14f, 1.12f, 1.08f, .94f, 1.12f, new Color(.24f,.76f,.48f))
            },
            {
                P("spirit_mote", EnemyClass.Wisp, EnemyClass.Wisp, "영혼 불씨", "SPIRIT MOTE", .84f, .76f, .9f, 1.02f, .9f, new Color(.3f,.9f,1f)),
                P("restless_ghost", EnemyClass.Wisp, EnemyClass.Mage, "떠도는 망령", "RESTLESS GHOST", .87f, .82f, .94f, 1f, .96f, new Color(.52f,.66f,1f)),
                P("banshee", EnemyClass.Wisp, EnemyClass.Shaman, "밴시", "BANSHEE", .9f, .84f, 1f, .98f, 1f, new Color(.72f,.34f,1f)),
                P("soul_reaper", EnemyClass.Wisp, EnemyClass.Piercer, "영혼 수확자", "SOUL REAPER", .96f, 1.02f, .8f, .96f, 1.06f, new Color(.62f,.16f,1f)),
                P("wraith_king", EnemyClass.Wisp, EnemyClass.Shaman, "망령왕", "WRAITH KING", 1.16f, 1.08f, 1.18f, .94f, 1.13f, new Color(.56f,.12f,1f))
            },
            {
                P("sky_scout", EnemyClass.Flyer, EnemyClass.Runner, "하늘 정찰병", "SKY SCOUT", .92f, .88f, .58f, 1.08f, .9f, new Color(.42f,.88f,1f)),
                P("wing_archer", EnemyClass.Flyer, EnemyClass.Mage, "날개 궁수", "WING ARCHER", .98f, .96f, .72f, 1.04f, .96f, new Color(.76f,.9f,1f)),
                P("storm_caller", EnemyClass.Flyer, EnemyClass.Shaman, "폭풍 소환사", "STORM CALLER", 1.02f, .9f, 1.14f, 1f, 1f, new Color(.38f,.56f,1f)),
                P("sky_bomber", EnemyClass.Flyer, EnemyClass.Siege, "비공 포격병", "SKY BOMBER", 1.08f, 1.08f, .9f, .96f, 1.05f, new Color(1f,.52f,.16f)),
                P("tempest_dragon", EnemyClass.Flyer, EnemyClass.Brute, "폭풍 비룡", "TEMPEST DRAGON", 1.16f, 1.16f, 1f, .96f, 1.14f, new Color(.28f,.72f,1f))
            },
            {
                P("abyss_crawler", EnemyClass.Mage, EnemyClass.Runner, "심연 추적자", "ABYSS CRAWLER", .88f, .86f, .68f, 1.06f, .94f, new Color(.44f,.2f,.9f)),
                P("void_eye", EnemyClass.Mage, EnemyClass.Wisp, "공허의 눈", "VOID EYE", .9f, .8f, 1f, 1.02f, .98f, new Color(.22f,.72f,1f)),
                P("abyss_priest", EnemyClass.Mage, EnemyClass.Shaman, "심연 사제", "ABYSS PRIEST", .94f, .84f, 1.03f, .98f, 1.02f, new Color(.64f,.18f,1f)),
                P("void_artillery", EnemyClass.Mage, EnemyClass.Siege, "공허 포격체", "VOID ARTILLERY", 1f, .96f, .94f, .92f, 1.08f, new Color(1f,.18f,.65f)),
                P("abyss_sovereign", EnemyClass.Mage, EnemyClass.Shaman, "심연 군주", "ABYSS SOVEREIGN", 1.08f, 1f, 1.08f, .9f, 1.16f, new Color(.72f,.08f,1f))
            }
        };

        // These are complete specialist silhouettes and kits, not recolours of an existing pawn.
        private static readonly EnemyVariantProfile SilenceShroud = P(
            "silence_shroud", EnemyClass.Wisp, EnemyClass.Silencer,
            "침묵 수의령", "SILENCE SHROUD", .92f, .64f, 1.04f, .96f, 1.02f,
            new Color(.28f, .78f, .94f));

        private static readonly EnemyVariantProfile VeilBinder = P(
            "veil_binder", EnemyClass.Skeleton, EnemyClass.Cursebinder,
            "봉인 수의 사제", "VEIL BINDER", .96f, .68f, 1.02f, .92f, 1.02f,
            new Color(.30f, .80f, .92f));

        private static readonly EnemyVariantProfile ArmorRender = P(
            "armor_render", EnemyClass.Siege, EnemyClass.Sunderer,
            "갑주 파쇄기", "ARMOR RENDER", 1.02f, .96f, .44f, .91f, 1.04f,
            new Color(.94f, .60f, .18f));

        public static EnemyVariantProfile[] SpecialProfiles =>
            new[] { VeilBinder, ArmorRender, SilenceShroud };

        public static EnemyVariantProfile[] AllProfiles
        {
            get
            {
                var result = new EnemyVariantProfile[Profiles.Length + 3];
                var index = 0;
                for (var chapter = 0; chapter < Profiles.GetLength(0); chapter++)
                for (var stage = 0; stage < Profiles.GetLength(1); stage++)
                    result[index++] = Profiles[chapter, stage];
                result[index++] = VeilBinder;
                result[index++] = ArmorRender;
                result[index] = SilenceShroud;
                return result;
            }
        }

        public static EnemyVariantProfile ForRound(int round)
        {
            var chapter = Mathf.Clamp((round - 1) / 5, 0, 9);
            var stage = Mathf.Clamp((round - 1) % 5, 0, 4);
            return Profiles[chapter, stage];
        }

        private static readonly int[][] MixedWaveLineups =
        {
            new[] { 0, 0, 1, 0, 2, 0, 1, 3, 0, 1 },
            new[] { 0, 1, 0, 2, 1, 0, 3, 1, 0, 2 },
            new[] { 0, 1, 2, 0, 3, 1, 0, 2, 3, 1 },
            new[] { 0, 2, 1, 3, 0, 2, 3, 1, 2, 0 },
            new[] { 0, 1, 2, 3, 1, 0, 3, 2, 0, 1 }
        };

        public static EnemyVariantProfile ForWaveMember(int round, int memberIndex)
        {
            var chapter = Mathf.Clamp((round - 1) / 5, 0, 9);
            var stage = Mathf.Clamp((round - 1) % 5, 0, 4);
            var member = Mathf.Abs(memberIndex);
            if (chapter == 1 && stage >= 1 &&
                member % (stage == 1 ? 11 : stage == 2 ? 9 : stage == 3 ? 7 : 6) == 4)
                return VeilBinder;
            if (chapter == 5 && stage >= 2 && member % (stage == 2 ? 9 : stage == 3 ? 7 : 6) == 3)
                return ArmorRender;
            if (chapter == 7 && stage >= 2 && member % (stage == 2 ? 9 : stage == 3 ? 7 : 6) == 4)
                return SilenceShroud;
            var lineup = MixedWaveLineups[stage];
            var profileIndex = lineup[(member + chapter * 3) % lineup.Length];
            return Profiles[chapter, profileIndex];
        }

        public static string FamilyNameForChapter(int chapter)
        {
            chapter = Mathf.Clamp(chapter, 0, 9);
            if (GameLocalization.English)
                return new[] { "OOZE ECOSYSTEM", "UNDEAD HOST", "GOBLIN", "GOLEM", "FOREST",
                    "CLOCKWORK", "LIZARD", "SPIRIT", "SKY", "ABYSS" }[chapter];
            return new[] { "점액 생태계", "언데드 군세", "고블린", "골렘", "숲", "태엽",
                "리자드", "망령", "비행", "심연" }[chapter];
        }

        public static EnemyVariantProfile ForChapterStage(int chapter, int stage) =>
            Profiles[Mathf.Clamp(chapter, 0, 9), Mathf.Clamp(stage, 0, 4)];

        public static EnemyVariantProfile ForFamilyStage(EnemyClass family, int stage)
        {
            for (var chapter = 0; chapter < Profiles.GetLength(0); chapter++)
                if (Profiles[chapter, 0].FamilyClass == family)
                    return Profiles[chapter, Mathf.Clamp(stage, 0, 4)];
            return Profiles[0, Mathf.Clamp(stage, 0, 4)];
        }

        public static string KoreanNameFor(string id, string fallback) => id switch
        {
            "jelly_vanguard" => "젤리 선봉대",
            "jelly_sprinter" => "질주 젤리",
            "jelly_mage" => "젤리 마도사",
            "jelly_bomber" => "젤리 폭탄병",
            "jelly_king" => "킹 젤리",
            "silence_shroud" => "침묵 수의령",
            "skeleton_soldier" => "해골 병사",
            "skeleton_archer" => "해골 방패병",
            "skeleton_mage" => "해골 철퇴병",
            "death_knight" => "죽음의 기사",
            "lich" => "리치",
            "veil_binder" => "봉인 수의 사제",
            "armor_render" => "갑주 파쇄기",
            "goblin_scout" => "고블린 정찰병",
            "goblin_slinger" => "고블린 투석병",
            "goblin_hexer" => "고블린 주술사",
            "goblin_raider" => "고블린 약탈대장",
            "goblin_warchief" => "고블린 대족장",
            "stone_shard" => "파편 분쇄 골렘",
            "stone_guard" => "성채 골렘",
            "rune_golem" => "룬 비전 골렘",
            "cannon_golem" => "용광로 포격 골렘",
            "mountain_titan" => "고대 산악 거신",
            "thorn_wolf" => "가시 늑대",
            "bark_guard" => "나무껍질 수호자",
            "forest_sprite" => "숲의 정령",
            "grove_shaman" => "수림 주술사",
            "ancient_ent" => "고대 나무거신",
            "gear_scout" => "태엽 정찰기",
            "clock_lancer" => "태엽 창기병",
            "arc_coil" => "전류 코일병",
            "siege_engine" => "태엽 공성포",
            "iron_colossus" => "철갑 거신",
            "crimson_hound" => "리자드 정찰병",
            "crimson_guard" => "리자드 방패병",
            "crimson_assassin" => "리자드 창투사",
            "blood_witch" => "리자드 용술사",
            "crimson_tyrant" => "흰수염 리자드 장로",
            "spirit_mote" => "영혼 불씨",
            "restless_ghost" => "떠도는 망령",
            "banshee" => "밴시",
            "soul_reaper" => "영혼 수확자",
            "wraith_king" => "망령왕",
            "sky_scout" => "하늘 정찰병",
            "wing_archer" => "날개 궁수",
            "storm_caller" => "폭풍 소환사",
            "sky_bomber" => "비공 포격병",
            "tempest_dragon" => "폭풍 비룡",
            "abyss_crawler" => "심연 추적자",
            "void_eye" => "공허의 눈",
            "abyss_priest" => "심연 사제",
            "void_artillery" => "공허 포격체",
            "abyss_sovereign" => "심연 군주",
            _ => fallback
        };
    }
}
