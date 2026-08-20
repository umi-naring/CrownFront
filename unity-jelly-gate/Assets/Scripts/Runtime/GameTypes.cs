using UnityEngine;

namespace JellyGate
{
    public enum UnitArchetype { None, Tank, Melee, Archer, AreaMage, SingleMage, Bombardier, Lancer, Druid, Musketeer, Oracle }
    public enum DefenderRole { Tank, Melee, Ranged, Mage, Support }
    public enum CombatVfxTier { Basic, Skill, Ultimate }
    public enum EnemyClass
    {
        Melee, Mage, Skeleton, Runner, Brute, Shaman, Siege, Piercer, Wisp, Flyer,
        Silencer, Cursebinder, Sunderer, Boss
    }
    public enum GameLanguage { Korean, English }
    public enum VoiceCue { Select, Move, Attack, Skill, Hero, Defeat, Spawn }
    public enum GamePhase { Preparation, Battle, Augment, Defeat, Victory }
    public enum AugmentTier { Bronze, Silver, Gold, Platinum, Diamond }
    public enum DamageType { Physical, Magic, Pure }
    public enum FacingOctant { South, SouthWest, West, NorthWest, North, NorthEast, East, SouthEast }

    public static class EightWayFacing
    {
        private static readonly Vector2[] Directions =
        {
            Vector2.down,
            new Vector2(-.7071068f, -.7071068f),
            Vector2.left,
            new Vector2(-.7071068f, .7071068f),
            Vector2.up,
            new Vector2(.7071068f, .7071068f),
            Vector2.right,
            new Vector2(.7071068f, -.7071068f)
        };

        public static FacingOctant FromVector(Vector2 direction)
        {
            if (direction.sqrMagnitude < .0001f) return FacingOctant.South;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var clockwiseFromSouth = Mathf.Repeat(270f - angle + 22.5f, 360f);
            return (FacingOctant)(Mathf.FloorToInt(clockwiseFromSouth / 45f) & 7);
        }

        public static Vector2 VectorFor(FacingOctant octant) => Directions[(int)octant];

        public static bool IsRight(FacingOctant octant) =>
            octant is FacingOctant.NorthEast or FacingOctant.East or FacingOctant.SouthEast;

        public static bool IsBack(FacingOctant octant) =>
            octant is FacingOctant.NorthWest or FacingOctant.North or FacingOctant.NorthEast;

        public static bool IsDiagonal(FacingOctant octant) =>
            octant is FacingOctant.SouthWest or FacingOctant.NorthWest or
                FacingOctant.NorthEast or FacingOctant.SouthEast;
    }

    public static class CombatMath
    {
        public static float EffectiveDefense(float defense, float penetration) =>
            Mathf.Max(0f, defense - Mathf.Max(0f, penetration));

        public static float MitigatedDamage(float amount, DamageType damageType, float armor,
            float magicResistance, float physicalPenetration = 0f, float magicPenetration = 0f)
        {
            if (amount <= 0f) return 0f;
            if (damageType == DamageType.Pure) return amount;
            var defense = damageType == DamageType.Physical
                ? EffectiveDefense(armor, physicalPenetration)
                : EffectiveDefense(magicResistance, magicPenetration);
            return amount * 100f / (100f + defense);
        }
    }

    public readonly struct UnitDefinition
    {
        public readonly string Name;
        public readonly string Mark;
        public readonly int Cost;
        public readonly float MaxHealth;
        public readonly float AttackPower;
        public readonly float MagicPower;
        public readonly float Range;
        public readonly float AttackDelay;
        public readonly float SkillCooldown;
        public readonly float Radius;
        public readonly float MoveSpeed;
        public readonly float SplashRadius;
        public readonly float Armor;
        public readonly float MagicResistance;
        public readonly float PhysicalPenetration;
        public readonly float MagicPenetration;
        public readonly Color Color;

        public UnitDefinition(string name, string mark, int cost, float maxHealth, float attackPower,
            float magicPower, float range, float attackDelay, float radius, float moveSpeed, Color color,
            float splashRadius = 0f, float armor = 0f, float magicResistance = 0f, float skillCooldown = 7f,
            float physicalPenetration = 0f, float magicPenetration = 0f)
        {
            Name = name;
            Mark = mark;
            Cost = cost;
            MaxHealth = maxHealth;
            AttackPower = attackPower;
            MagicPower = magicPower;
            Range = range;
            AttackDelay = attackDelay;
            SkillCooldown = skillCooldown;
            Radius = radius;
            MoveSpeed = moveSpeed;
            Color = color;
            SplashRadius = splashRadius;
            Armor = armor;
            MagicResistance = magicResistance;
            PhysicalPenetration = physicalPenetration;
            MagicPenetration = magicPenetration;
        }
    }

    public readonly struct AugmentOffer
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string EffectKey;
        public readonly AugmentTier Tier;
        public readonly float Power;

        public AugmentOffer(string name, string description, string effectKey, AugmentTier tier, float power)
        {
            Name = name;
            Description = description;
            EffectKey = effectKey;
            Tier = tier;
            Power = power;
        }
    }

    public readonly struct AugmentTemplate
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string EffectKey;

        public AugmentTemplate(string name, string description, string effectKey)
        {
            Name = name;
            Description = description;
            EffectKey = effectKey;
        }
    }

    public sealed class DirectionalAnimationSet
    {
        public readonly Sprite[] Down;
        public readonly Sprite[] DownDiagonal;
        public readonly Sprite[] Side;
        public readonly Sprite[] UpDiagonal;
        public readonly Sprite[] Up;
        public readonly float DownScale;
        public readonly float SideScale;
        public readonly float UpScale;
        public readonly float UpVerticalOffset;
        // Legacy/default directional sheets were painted facing screen-right, while the
        // canonical cosmetic rigs were painted facing screen-left.  Keeping this as source
        // metadata prevents one family from walking backwards when the other is corrected.
        public readonly bool SideFacesRight;

        public DirectionalAnimationSet(Sprite[] down, Sprite[] side, Sprite[] up,
            float downScale = 1f, float sideScale = 1f, float upScale = 1f, float upVerticalOffset = 0f,
            Sprite[] downDiagonal = null, Sprite[] upDiagonal = null, bool sideFacesRight = true)
        {
            Down = down ?? System.Array.Empty<Sprite>();
            Side = side ?? System.Array.Empty<Sprite>();
            Up = up ?? System.Array.Empty<Sprite>();
            DownDiagonal = downDiagonal ?? OffsetStateCycles(Side.Length > 0 ? Side : Down, 1);
            UpDiagonal = upDiagonal ?? OffsetStateCycles(Up.Length > 0 ? Up : Side, 2);
            DownScale = downScale;
            SideScale = sideScale;
            UpScale = upScale;
            UpVerticalOffset = upVerticalOffset;
            SideFacesRight = sideFacesRight;
        }

        public Sprite[] FramesFor(Vector2 facing)
        {
            return FramesFor(EightWayFacing.FromVector(facing));
        }

        public Sprite[] FramesFor(FacingOctant octant)
        {
            return octant switch
            {
                FacingOctant.South => FirstAvailable(Down, DownDiagonal, Side, Up),
                FacingOctant.SouthWest or FacingOctant.SouthEast =>
                    FirstAvailable(DownDiagonal, Side, Down, Up),
                FacingOctant.West or FacingOctant.East => FirstAvailable(Side, DownDiagonal, Down, Up),
                FacingOctant.NorthWest or FacingOctant.NorthEast =>
                    FirstAvailable(UpDiagonal, Up, Side, Down),
                _ => FirstAvailable(Up, UpDiagonal, Side, Down)
            };
        }

        public float ScaleFor(Vector2 facing)
        {
            return EightWayFacing.FromVector(facing) switch
            {
                FacingOctant.South => DownScale,
                FacingOctant.SouthWest or FacingOctant.SouthEast => (DownScale + SideScale) * .5f,
                FacingOctant.West or FacingOctant.East => SideScale,
                FacingOctant.NorthWest or FacingOctant.NorthEast => (UpScale + SideScale) * .5f,
                _ => UpScale
            };
        }

        public float VerticalOffsetFor(Vector2 facing)
        {
            return EightWayFacing.FromVector(facing) switch
            {
                FacingOctant.North => UpVerticalOffset,
                FacingOctant.NorthWest or FacingOctant.NorthEast => UpVerticalOffset * .62f,
                _ => 0f
            };
        }

        public float DirectionScaleSpread =>
            Mathf.Max(DownScale, Mathf.Max(SideScale, UpScale)) -
            Mathf.Min(DownScale, Mathf.Min(SideScale, UpScale));

        public bool SupportsEightDirections
        {
            get
            {
                foreach (FacingOctant octant in System.Enum.GetValues(typeof(FacingOctant)))
                    if (FramesFor(octant).Length == 0) return false;
                return true;
            }
        }

        private static Sprite[] FirstAvailable(params Sprite[][] choices)
        {
            foreach (var choice in choices)
                if (choice != null && choice.Length > 0) return choice;
            return System.Array.Empty<Sprite>();
        }

        private static Sprite[] OffsetStateCycles(Sprite[] source, int offset)
        {
            if (source == null || source.Length == 0) return System.Array.Empty<Sprite>();
            var result = new Sprite[source.Length];
            var stateLength = source.Length >= 64 ? 16 : source.Length >= 56 ? 16 :
                source.Length >= 48 ? 12 : source.Length >= 40 ? 12 :
                source.Length >= 17 ? 8 : Mathf.Min(4, source.Length);
            for (var stateStart = 0; stateStart < source.Length; stateStart += stateLength)
            {
                var length = Mathf.Min(stateLength, source.Length - stateStart);
                for (var i = 0; i < length; i++)
                    result[stateStart + i] = source[stateStart + (i + offset) % length];
            }
            return result;
        }
    }
}
