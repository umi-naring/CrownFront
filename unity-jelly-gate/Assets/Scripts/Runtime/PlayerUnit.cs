using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    public sealed class PlayerUnit : MonoBehaviour
    {
        private static readonly Dictionary<Sprite, float> OpaqueHeightCache = new();
        private static readonly Dictionary<Sprite, float> OpaqueBodyHeightCache = new();
        private static readonly Dictionary<Sprite, Vector2> OpaqueFootAnchorCache = new();
        private static readonly Dictionary<Sprite, Vector2> OpaqueLowestAnchorCache = new();
        private static readonly Dictionary<Sprite, Vector4> OpaqueMarginCache = new();
        private static readonly Dictionary<Sprite, Vector3> OpaqueSilhouetteCache = new();
        public static int OpaqueMetricCacheMisses { get; private set; }
        private JellyGateGame game;
        private UnitDefinition definition;
        private SpriteRenderer body;
        private SpriteRenderer shadow;
        private SpriteRenderer leftFoot;
        private SpriteRenderer rightFoot;
        private SpriteRenderer actionAccent;
        private CrownfrontModelUnitVisual visualRig;
        private GameObject selectionRing;
        private GameObject holdIndicator;
        private SpriteRenderer heroAura;
        private SpriteRenderer heroCrestOuter;
        private SpriteRenderer heroCrestCore;
        private GameObject skinSignatureRoot;
        private SpriteRenderer skinAuthoredBody;
        private SpriteRenderer skinBodyOutline;
        private SpriteRenderer skinCape;
        private SpriteRenderer skinShoulderLeft;
        private SpriteRenderer skinShoulderRight;
        private SpriteRenderer skinCrest;
        private SpriteRenderer skinBackGlow;
        private SpriteRenderer skinHelmLeft;
        private SpriteRenderer skinHelmRight;
        private SpriteRenderer skinTabard;
        private SpriteRenderer skinWeaponSigil;
        private SpriteRenderer skinAccessoryA;
        private SpriteRenderer skinAccessoryB;
        private SpriteRenderer skinAccessoryC;
        private SpriteRenderer skinAccessoryD;
        private Sprite[] animationFrames = System.Array.Empty<Sprite>();
        private DirectionalAnimationSet directionalAnimation;
        private readonly List<Vector2> movePath = new();
        private int movePathIndex;
        private Transform healthFill;
        private Transform experienceFill;
        private TextMesh levelText;
        private Vector3 bodyBaseScale;
        private float visualReferenceSilhouetteHeight;
        private float visualReferenceBodyHeight;
        private float visualGroundLineY;
        private Vector3 shadowBaseScale;
        private Vector3 leftFootBaseScale;
        private Vector3 rightFootBaseScale;
        private Color bodyBaseColor;
        private Vector2 moveTarget;
        private Vector2 moveDestination;
        private EnemyUnit commandedTarget;
        private bool forcedAttackOrder;
        private bool holdPosition;
        private float nextPursuitRepathAt;
        private Vector2 lastPursuitTargetPosition;
        private Vector2 velocity;
        private Vector2 facing = Vector2.up;
        private Vector2 visualFacing = Vector2.up;
        private FacingOctant visualOctant = FacingOctant.North;
        private float combatFacingLockUntil;
        private Vector2 contactDirection;
        private Vector2 knockbackStart;
        private Vector2 knockbackTarget;
        private float health;
        private float maxHealth;
        private float experience;
        private float lastAttackAt;
        private float animationPhase;
        private Vector2 lastAnimationSamplePosition;
        private float attackMotion;
        private float skillMotion;
        private float ultimateMotion;
        private float ultimateRecoveryUntil;
        private int ultimateRecoverySerial;
        private float hurtMotion;
        private float contactMotion;
        private float knockbackTime;
        private float currentMoveSpeed;
        private float nextDustAt;
        private int lastFootstepPhase = int.MinValue;
        private float levelUpMotion;
        private float defensiveStanceUntil;
        private float defensiveDamageReduction;
        private float defensiveArmorBonus;
        private float defensiveResistanceBonus;
        private float defensiveDebuffImmunityUntil;
        private float armorShredUntil;
        private float armorShredAmount;
        private float resistanceCurseUntil;
        private float resistanceCurseAmount;
        private float magicSealUntil;
        private float skillCooldownRemaining;
        private float ultimateCooldownRemaining;
        private float stuckTime;
        private Vector2 lastProgressPosition;
        private int repathAttempts;
        private bool usingStaticSkinBody;
        private int cachedSkinVariant;
        private Sprite cachedMotionMetricSprite;
        private float cachedMotionBodyHeight;
        private Vector2 cachedMotionFootAnchor;
        private int motionSpriteWrites;
        private int movementDustSpawns;
        private bool invulnerableForQa;

        public UnitArchetype Archetype { get; private set; }
        public float Radius => definition.Radius;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Experience => experience;
        public int PlacementRound { get; private set; }
        public int PlacementBatchId { get; private set; }
        public int PlacementRefundCost { get; private set; }
        public int Level { get; private set; } = 1;
        public bool IsHero { get; private set; }
        public bool HeroPresentationReady => IsHero && heroCrestOuter != null && heroCrestOuter.enabled &&
                                             heroCrestCore != null && heroCrestCore.enabled;
        public Color CosmeticPresentationColor => bodyBaseColor;
        // The equipped cosmetic only changes through RefreshCosmeticPresentation. Reading
        // PlayerPrefs-backed monetization state from the 60 Hz animation loop caused avoidable
        // main-thread work on low-end Android devices, most visibly on the large tank rig.
        public int SkinVariant => cachedSkinVariant;
        public int SkinSignaturePartCount => skinSignatureRoot == null || !skinSignatureRoot.activeSelf
            ? 0
            : skinSignatureRoot.GetComponentsInChildren<SpriteRenderer>(true).Length;
        public bool HasAuthoredSkinPresentation =>
            game != null &&
            (game.HasAuthoredSkinAnimation(Archetype, SkinVariant, IsHero) ||
             skinAuthoredBody != null && skinAuthoredBody.enabled && skinAuthoredBody.sprite != null);
        public float DirectionScaleSpread => directionalAnimation?.DirectionScaleSpread ?? 0f;
        public bool HasCompleteDirectionalAnimation => directionalAnimation != null &&
                                                       directionalAnimation.Down.Length >= 7 &&
                                                       directionalAnimation.DownDiagonal.Length >= 7 &&
                                                       directionalAnimation.Side.Length >= 7 &&
                                                       directionalAnimation.UpDiagonal.Length >= 7 &&
                                                       directionalAnimation.Up.Length >= 7 &&
                                                       directionalAnimation.SupportsEightDirections;
        public string CurrentFrameTextureName =>
            body != null && body.sprite != null && body.sprite.texture != null
                ? body.sprite.texture.name
                : string.Empty;
        public int CurrentSpriteIdForQa => body != null && body.sprite != null
            ? body.sprite.GetInstanceID()
            : 0;
        public bool CurrentSpriteMetricsReadyForQa => body != null &&
                                                      SpriteMetricsReadyForQa(body.sprite);
        public int ActivePrimaryBodyChannelsForQa =>
            (body != null && body.enabled && body.gameObject.activeInHierarchy ? 1 : 0) +
            (skinAuthoredBody != null && skinAuthoredBody.enabled &&
             skinAuthoredBody.gameObject.activeInHierarchy ? 1 : 0) +
            (visualRig != null && visualRig.gameObject.activeInHierarchy ? 1 : 0);
        public int AnimationFrameCount => animationFrames?.Length ?? 0;
        public bool VisualSpriteFlipped => body != null && body.flipX;
        public float AnimationPhaseForQa => animationPhase;
        public int MotionSpriteWritesForQa => motionSpriteWrites;
        public int MovementDustSpawnsForQa => movementDustSpawns;
        public bool UltimateRecoveryActiveForQa => Time.time < ultimateRecoveryUntil;
        public int UltimateRecoverySerialForQa => ultimateRecoverySerial;
        public float GroundPlaneLocalYForQa => visualGroundLineY;
        public float GaitCyclesPerSecondForQa => definition.MoveSpeed / GaitStrideDistance;
        private float GaitStrideDistance => Mathf.Max(definition.Radius * 3.65f,
            definition.MoveSpeed * .74f);
        public bool ActionAccentUsesSolidCircleForQa => actionAccent != null && actionAccent.sprite == game.CircleSprite;
        public bool HasArticulatedLimbRigForQa => false;
        public float ArticulatedPoseSignatureForQa => 0f;
        public bool UsesSeamSafeWholeBodyAnimationForQa => body != null;
        public float VisualWorldHeightForQa => body == null || body.sprite == null
            ? 0f
            : OpaqueWorldHeight(body.sprite) * Mathf.Abs(body.transform.localScale.y);
        public Vector3 VisualScaleForQa => body != null ? body.transform.localScale : Vector3.one;
        public float VisualBodyWorldHeightForQa => body == null || body.sprite == null
            ? 0f
            : OpaqueBodyWorldHeight(body.sprite) * Mathf.Abs(body.transform.localScale.y);
        public Sprite PortraitSprite => skinAuthoredBody != null && usingStaticSkinBody && skinAuthoredBody.sprite != null
            ? skinAuthoredBody.sprite
            : body != null ? body.sprite : null;
        public Vector2 VisualFacing => visualFacing;
        public FacingOctant VisualOctant => visualOctant;
        public Vector2 AttackOrigin => AttackOriginFor(Position + EightWayFacing.VectorFor(visualOctant));
        public bool IsAlive { get; private set; } = true;
        public bool IsRangedCombatant => Archetype is UnitArchetype.Archer or UnitArchetype.AreaMage or
            UnitArchetype.SingleMage or UnitArchetype.Bombardier or UnitArchetype.Druid or
            UnitArchetype.Musketeer or UnitArchetype.Oracle;
        public bool IsMoving { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 HitPoint => Position + Vector2.up * Radius * (IsHero ? .88f : .74f);
        public string DisplayName => IsHero ? HeroTitle : definition.Name;

        public void MarkPlacementForUndo(int round, int batchId, int refundCost)
        {
            PlacementRound = Mathf.Max(0, round);
            PlacementBatchId = Mathf.Max(0, batchId);
            PlacementRefundCost = Mathf.Max(0, refundCost);
        }
        public float HealthRatio => Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
        private bool DefensiveStanceActive => Time.time < defensiveStanceUntil;
        public float Armor => Mathf.Max(0f, definition.Armor + (Level - 1) * 4f + (IsHero ? 12f : 0f) +
                               (DefensiveStanceActive ? defensiveArmorBonus : 0f) +
                               (game != null ? game.GetHighGroundDefenseBonus(this) +
                                               game.GetRoleDefenseBonus(this) +
                                               game.GetTacticalDefenseBonus(this, definition.Armor) : 0f) -
                              (Time.time < armorShredUntil ? armorShredAmount : 0f));
        public float MagicResistance => Mathf.Max(0f, definition.MagicResistance + (Level - 1) * 3.5f + (IsHero ? 10f : 0f) +
                                        (DefensiveStanceActive ? defensiveResistanceBonus : 0f) +
                                        (game != null ? game.GetHighGroundDefenseBonus(this) +
                                                        game.GetRoleResistanceBonus(this) +
                                                        game.GetTacticalDefenseBonus(this, definition.MagicResistance) : 0f) -
                                        (Time.time < resistanceCurseUntil ? resistanceCurseAmount : 0f));
        public float PhysicalPenetration => Mathf.Max(0f, definition.PhysicalPenetration +
            (Level - 1) * (Archetype is UnitArchetype.Tank or UnitArchetype.Melee or
                UnitArchetype.Archer or UnitArchetype.Musketeer or UnitArchetype.Bombardier or
                UnitArchetype.Lancer ? 1.15f : .35f) +
            (IsHero ? Archetype is UnitArchetype.Tank or UnitArchetype.Melee or
                UnitArchetype.Archer or UnitArchetype.Musketeer or UnitArchetype.Bombardier or
                UnitArchetype.Lancer ? 4f : 1f : 0f) +
            (game != null ? game.GetRolePhysicalPenetrationBonus(this) : 0f));
        public float MagicPenetration => Mathf.Max(0f, definition.MagicPenetration +
            (Level - 1) * (Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle ? 1.15f : .35f) +
            (IsHero ? Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle ? 4f : 1f : 0f));
        public float ActiveArmorShredForQa => Time.time < armorShredUntil ? armorShredAmount : 0f;
        public float ActiveResistanceCurseForQa => Time.time < resistanceCurseUntil ? resistanceCurseAmount : 0f;
        public bool IsMagicSealed => Time.time < magicSealUntil &&
                                     Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage;
        public bool HasDefensiveStanceForQa => DefensiveStanceActive;
        public float DefensiveDamageReductionForQa => DefensiveStanceActive ? defensiveDamageReduction : 0f;
        public bool HasDefensiveDebuffImmunityForQa => Time.time < defensiveDebuffImmunityUntil;
        public float DamageMultiplier => Level switch
        {
            1 => .55f,
            2 => .69f,
            3 => .87f,
            4 => 1.1f,
            _ => IsHero ? 1.62f : 1.42f
        };
        public float AttackDelayMultiplier => Level switch
        {
            1 => 1.18f,
            2 => 1.09f,
            3 => .99f,
            4 => .9f,
            _ => IsHero ? .75f : .82f
        };
        public float AttackPower => definition.AttackPower * DamageMultiplier *
                                    (game != null ? game.GetHighGroundDamageMultiplier(this) *
                                                    game.GetRoleDamageMultiplier(this) *
                                                    game.GetTacticalDamageMultiplier(this) : 1f);
        public float MagicPower => definition.MagicPower * DamageMultiplier *
                                   (game != null ? game.GetHighGroundDamageMultiplier(this) *
                                                   game.GetRoleDamageMultiplier(this) *
                                                   game.GetTacticalDamageMultiplier(this) : 1f);
        public float AttackRange => definition.Range +
                                    (game != null ? game.GetHighGroundRangeBonus(this) +
                                                    game.GetRoleRangeBonus(this) : 0f);
        public float DetectionRange => AttackRange + Mathf.Max(1.35f, AttackRange * .55f) +
                                       (game != null ? game.GetRoleDetectionRangeBonus(this) : 0f);
        public bool IsHoldingPosition => holdPosition;
        public bool HasForcedAttackOrderOn(EnemyUnit target) =>
            IsAlive && forcedAttackOrder && commandedTarget != null && commandedTarget == target;
        public float AttackDamagePreview => Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or UnitArchetype.Druid or UnitArchetype.Oracle
            ? MagicPower * (Archetype == UnitArchetype.AreaMage ? .26f : Archetype == UnitArchetype.SingleMage ? .55f : .34f)
            : AttackPower;
        public bool IsOnHighGround => game != null && game.IsHighGround(Position);
        public string SkillName => LocalizedSkillName(Archetype);
        /*
        {
            UnitArchetype.Tank => GameLocalization.Text("왕실 방패", "Royal Bulwark"),
            UnitArchetype.Melee => GameLocalization.Text("태엽 회전격", "Wind-up Whirl"),
            UnitArchetype.Archer => GameLocalization.Text("관통 연사", "Piercing Volley"),
            UnitArchetype.Musketeer => GameLocalization.Text("정밀 일제사", "Aimed Volley"),
            UnitArchetype.AreaMage => GameLocalization.Text("별무리 폭발", "Star Cluster"),
            UnitArchetype.SingleMage => GameLocalization.Text("수정 창", "Crystal Lance"),
            UnitArchetype.Bombardier => GameLocalization.Text("연금 폭격", "Alchemical Barrage"),
            UnitArchetype.Lancer => GameLocalization.Text("초승달 돌진", "Crescent Charge"),
            UnitArchetype.Druid => GameLocalization.Text("숲의 결계", "Grove Ward"),
            UnitArchetype.Oracle => GameLocalization.Text("달빛 파동", "Moonlight Pulse"),
            _ => GameLocalization.Text("전투 기술", "Combat Skill")
        };
        */
        private static string LocalizedSkillName(UnitArchetype archetype) => archetype switch
        {
            UnitArchetype.Tank => GameLocalization.Text("왕실 방패", "Royal Bulwark"),
            UnitArchetype.Melee => GameLocalization.Text("태엽 회전격", "Wind-up Whirl"),
            UnitArchetype.Archer => GameLocalization.Text("관통 연사", "Piercing Volley"),
            UnitArchetype.Musketeer => GameLocalization.Text("정밀 일제사격", "Aimed Volley"),
            UnitArchetype.AreaMage => GameLocalization.Text("별무리 폭발", "Star Cluster"),
            UnitArchetype.SingleMage => GameLocalization.Text("수정 창", "Crystal Lance"),
            UnitArchetype.Bombardier => GameLocalization.Text("연금 포격", "Alchemical Barrage"),
            UnitArchetype.Lancer => GameLocalization.Text("초승달 돌진", "Crescent Charge"),
            UnitArchetype.Druid => GameLocalization.Text("꽃잎 결계", "Petal Ward"),
            UnitArchetype.Oracle => GameLocalization.Text("달빛 파동", "Moonlight Pulse"),
            _ => GameLocalization.Text("전투 기술", "Combat Skill")
        };

        public float SkillCooldownRemaining => Mathf.Max(0f, skillCooldownRemaining);
        public float SkillCooldownDuration => Mathf.Max(.1f, definition.SkillCooldown);
        public float UltimateCooldownRemaining => Mathf.Max(0f, ultimateCooldownRemaining);
        public float UltimateCooldownDuration => Archetype switch
        {
            UnitArchetype.Tank => 18f,
            UnitArchetype.Melee => 16f,
            UnitArchetype.Archer => 17f,
            UnitArchetype.Musketeer => 18f,
            UnitArchetype.AreaMage => 20f,
            UnitArchetype.SingleMage => 18f,
            UnitArchetype.Bombardier => 19f,
            UnitArchetype.Lancer => 16f,
            UnitArchetype.Druid => 20f,
            UnitArchetype.Oracle => 19f,
            _ => 18f
        };
        public string UltimateName => LocalizedUltimateName(Archetype);
        /*
        {
            UnitArchetype.Tank => GameLocalization.Text("왕성의 천벽", "Citadel Sky-Wall"),
            UnitArchetype.Melee => GameLocalization.Text("황금 태엽 난무", "Golden Wind-up Storm"),
            UnitArchetype.Archer => GameLocalization.Text("왕실 화살비", "Royal Arrow Rain"),
            UnitArchetype.Musketeer => GameLocalization.Text("왕실 삼중 사격", "Royal Triple Shot"),
            UnitArchetype.AreaMage => GameLocalization.Text("초신성 낙하", "Supernova Fall"),
            UnitArchetype.SingleMage => GameLocalization.Text("절대 수정창", "Absolute Crystal Lance"),
            UnitArchetype.Bombardier => GameLocalization.Text("왕실 전탄 포격", "Royal Full Salvo"),
            UnitArchetype.Lancer => GameLocalization.Text("비취 돌격대", "Jade Charge Line"),
            UnitArchetype.Druid => GameLocalization.Text("만개한 성역", "Bloom Sanctuary"),
            UnitArchetype.Oracle => GameLocalization.Text("보름달 심판", "Full-Moon Judgment"),
            _ => GameLocalization.Text("영웅 궁극기", "Hero Ultimate")
        };
        private string HeroTitle => Archetype switch
        {
            UnitArchetype.Lancer => GameLocalization.Text("비취 돌격대장", "Jade Vanguard"),
            UnitArchetype.Druid => GameLocalization.Text("만개한 대정령", "Grand Bloom Spirit"),
            UnitArchetype.Musketeer => GameLocalization.Text("황동 총사령관", "Brass Commander"),
            UnitArchetype.Oracle => GameLocalization.Text("월광 대예언자", "Moonlight High Oracle"),
            _ => definition.Name
        };
        */
        private static string LocalizedUltimateName(UnitArchetype archetype) => archetype switch
        {
            UnitArchetype.Tank => GameLocalization.Text("왕성의 천벽", "Citadel Sky-Wall"),
            UnitArchetype.Melee => GameLocalization.Text("황금 태엽 폭풍", "Golden Wind-up Storm"),
            UnitArchetype.Archer => GameLocalization.Text("왕실 화살비", "Royal Arrow Rain"),
            UnitArchetype.Musketeer => GameLocalization.Text("왕실 삼중 사격", "Royal Triple Shot"),
            UnitArchetype.AreaMage => GameLocalization.Text("초신성 낙하", "Supernova Fall"),
            UnitArchetype.SingleMage => GameLocalization.Text("절대 수정창", "Absolute Crystal Lance"),
            UnitArchetype.Bombardier => GameLocalization.Text("시계태엽 공성 포격", "Clockwork Siege Barrage"),
            UnitArchetype.Lancer => GameLocalization.Text("용맥 돌진", "Dragonvein Charge"),
            UnitArchetype.Druid => GameLocalization.Text("자연의 성역", "Verdant Sanctuary"),
            UnitArchetype.Oracle => GameLocalization.Text("달빛 심판", "Moonlit Verdict"),
            _ => GameLocalization.Text("영웅 궁극기", "Hero Ultimate")
        };

        private string HeroTitle => Archetype switch
        {
            UnitArchetype.Lancer => GameLocalization.Text("용맥 선봉대장", "Dragonvein Vanguard"),
            UnitArchetype.Druid => GameLocalization.Text("만개의 대정령", "Grand Bloom Spirit"),
            UnitArchetype.Musketeer => GameLocalization.Text("왕실 총사령관", "Royal Commander"),
            UnitArchetype.Oracle => GameLocalization.Text("달빛 대예언자", "Moonlight High Oracle"),
            UnitArchetype.Tank => GameLocalization.Text("왕관의 천벽", "Crown Sky-Wall"),
            UnitArchetype.Melee => GameLocalization.Text("태엽 폭풍대장", "Clockwork Storm Captain"),
            UnitArchetype.Archer => GameLocalization.Text("왕실 명사수", "Royal Sharpshooter"),
            UnitArchetype.AreaMage => GameLocalization.Text("초신성 대마도사", "Supernova Archmage"),
            UnitArchetype.SingleMage => GameLocalization.Text("수정창 현자", "Crystal Lance Sage"),
            UnitArchetype.Bombardier => GameLocalization.Text("왕실 공성장인", "Royal Siege Master"),
            _ => definition.Name
        };

        public bool CanUseUltimate => IsHero && IsAlive && game != null &&
                                      game.Phase == GamePhase.Battle && !IsMoving &&
                                      knockbackTime <= 0f && ultimateCooldownRemaining <= 0f && !IsMagicSealed &&
                                      game.HasValidUltimateContext(this);

        public void PreviewProductionAction(int action)
        {
            attackMotion = 1f;
            if (action % 3 == 1) skillMotion = 1f;
            if (action % 3 == 2) ultimateMotion = 1f;
        }

        public void RefreshCosmeticPresentation()
        {
            if (game == null || body == null) return;
            var variant = game.GetUnitSkinVariant(Archetype);
            cachedSkinVariant = variant;
            var skinAnimation = variant > 0
                ? game.GetAuthoredSkinAnimation(Archetype, variant, IsHero)
                : null;
            var presentationAnimation = skinAnimation ?? game.GetDirectionalAnimation(Archetype, IsHero);
            var authored = game.GetAuthoredUnitSkinSprite(Archetype, variant, IsHero);
            // Always replace the complete animation source. Previously only paid skins entered
            // this branch, so equipping DEFAULT after a paid skin left the paid directional rig
            // alive in battle while the UI already claimed that the default skin was equipped.
            if (presentationAnimation != null)
            {
                directionalAnimation = presentationAnimation;
                animationFrames = directionalAnimation.FramesFor(visualOctant);
                if (animationFrames.Length > 0) SetMotionSprite(animationFrames[0]);
            }
            bodyBaseScale = Vector3.one * (definition.Radius * (IsHero ? 4.32f : 2.95f));
            body.transform.localScale = bodyBaseScale;
            // Every authored directional timeline already contains its final skin and skin-tone
            // colours. Hero tinting is reserved for the geometric fallback only; applying it to
            // a loaded base/skin atlas recoloured the face, hands and clothing at level five.
            bodyBaseColor = presentationAnimation != null || authored != null
                ? Color.white
                : IsHero ? game.GetHeroSpriteTint(Archetype) : game.GetUnitSpriteTint(Archetype);
            body.color = bodyBaseColor;
            CaptureVisualReferenceHeight();
            if (actionAccent != null)
            {
                var accent = game.GetUnitSkinAccent(Archetype);
                actionAccent.color = new Color(accent.r, accent.g, accent.b, .9f);
            }
            if (heroAura != null && IsHero)
            {
                var aura = game.GetHeroAuraColor(Archetype);
                heroAura.color = new Color(aura.r, aura.g, aura.b, .31f);
                if (heroCrestOuter != null) heroCrestOuter.color = new Color(aura.r, aura.g, aura.b, .88f);
            }
            RefreshSkinSignature();
        }

        public void Initialize(JellyGateGame owner, UnitArchetype archetype, UnitDefinition unitDefinition, Vector2 position)
        {
            game = owner;
            Archetype = archetype;
            cachedSkinVariant = game != null ? game.GetUnitSkinVariant(archetype) : 0;
            definition = unitDefinition;
            maxHealth = definition.MaxHealth * (game != null
                ? game.GetTacticalHealthMultiplier(archetype)
                : 1f);
            health = maxHealth;
            moveTarget = position;
            moveDestination = position;
            lastProgressPosition = position;
            animationPhase = Random.value * Mathf.PI * 2f;
            skillCooldownRemaining = definition.SkillCooldown;
            ultimateCooldownRemaining = 0f;
            transform.position = game.ActorWorldPosition(position);
            lastAnimationSamplePosition = Position;
            name = definition.Name;

            heroAura = game.CreateSpriteChild(transform, "Hero Aura Disabled", game.GlowSprite,
                Color.clear, .01f, 1);
            heroAura.enabled = false;
            heroCrestOuter = game.CreateSpriteChild(transform, "Hero Crest Spark", game.SparkSprite,
                new Color(1f, .78f, .14f, .9f), 1f, 8);
            heroCrestOuter.transform.localPosition = new Vector3(0f, definition.Radius + .72f, -.28f);
            heroCrestOuter.transform.localScale = Vector3.one * (definition.Radius * .62f);
            heroCrestOuter.enabled = false;
            heroCrestCore = game.CreateSpriteChild(transform, "Hero Crest Core", game.SquareSprite,
                new Color(1f, .97f, .68f, 1f), 1f, 9);
            heroCrestCore.transform.localPosition = heroCrestOuter.transform.localPosition +
                                                    new Vector3(0f, 0f, -.02f);
            heroCrestCore.transform.localScale = Vector3.one * (definition.Radius * .25f);
            heroCrestCore.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            heroCrestCore.enabled = false;
            shadow = game.CreateSpriteChild(transform, "Ground Shadow", game.CircleSprite,
                new Color(.025f, .035f, .055f, .42f), 1f, 1);
            // The contact shadow and the selection ring represent the same floor plane.  The
            // former value (-.42R) sat inside the painted torso and made every actor hover well
            // above the ring (-1.30R), particularly obvious on the small default skins.
            shadow.transform.localPosition = new Vector3(0f, -definition.Radius * 1.30f, .06f);
            shadow.transform.localScale = new Vector3(definition.Radius * 2.45f, definition.Radius * .7f, 1f);
            shadowBaseScale = shadow.transform.localScale;
            var footColor = Color.Lerp(definition.Color, new Color(.08f, .07f, .1f), .58f);
            leftFoot = game.CreateSpriteChild(transform, "Left Foot", game.CircleSprite, footColor, 1f, 2);
            rightFoot = game.CreateSpriteChild(transform, "Right Foot", game.CircleSprite, footColor, 1f, 2);
            leftFoot.transform.localScale = new Vector3(definition.Radius * .66f, definition.Radius * .25f, 1f);
            rightFoot.transform.localScale = leftFoot.transform.localScale;
            leftFootBaseScale = leftFoot.transform.localScale;
            rightFootBaseScale = rightFoot.transform.localScale;
            selectionRing = CreateSelectionRing(definition.Radius);
            selectionRing.SetActive(false);

            directionalAnimation = game.GetDirectionalAnimation(archetype, false);
            animationFrames = directionalAnimation.FramesFor(facing);
            var characterSprite = animationFrames.Length > 0 ? animationFrames[0] : game.GetUnitSprite(archetype);
            // Keep the chibi squad readable without making it visually larger than a paved lane.
            var visualScale = characterSprite != null ? definition.Radius * 2.95f : definition.Radius * 2f;
            body = game.CreateSpriteChild(transform, "Character", characterSprite ?? game.CircleSprite,
                characterSprite != null && game.HasAuthoredSkinAnimation(archetype,
                    game.GetUnitSkinVariant(archetype), false)
                    ? Color.white
                    : characterSprite != null ? game.GetUnitSpriteTint(archetype) : definition.Color,
                visualScale, 3);
            bodyBaseScale = body.transform.localScale;
            bodyBaseColor = body.color;
            CaptureVisualReferenceHeight();
            if (animationFrames.Length > 0)
            {
                leftFoot.enabled = false;
                rightFoot.enabled = false;
            }
            if (game.Use2p5DPresentation && CrownfrontModelUnitVisual.HasProductionModel(archetype))
            {
                body.enabled = false;
                leftFoot.enabled = false;
                rightFoot.enabled = false;
                visualRig = CrownfrontModelUnitVisual.CreateDefender(transform, archetype, definition.Color,
                    definition.Radius, false);
            }
            BuildSkinSignature();
            RefreshSkinSignature();

            actionAccent = game.CreateSpriteChild(transform, "Weapon Contact Accent",
                game.SparkSprite,
                new Color(game.GetUnitSkinAccent(archetype).r, game.GetUnitSkinAccent(archetype).g,
                    game.GetUnitSkinAccent(archetype).b, .9f), 1f, 5);
            actionAccent.enabled = false;

            if (characterSprite == null)
            {
                var mark = new GameObject("Mark");
                mark.transform.SetParent(transform, false);
                mark.transform.localPosition = new Vector3(0f, -.02f, -0.1f);
                var text = mark.AddComponent<TextMesh>();
                text.text = definition.Mark;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 48;
                text.characterSize = .055f;
                text.color = new Color(.14f, .11f, .22f);
                text.GetComponent<MeshRenderer>().sortingOrder = 5;
            }

            var badge = new GameObject("Level Badge");
            badge.transform.SetParent(transform, false);
            badge.transform.localPosition = new Vector3(0f,
                visualRig != null ? definition.Radius + .82f : definition.Radius + .34f, -0.3f);
            levelText = badge.AddComponent<TextMesh>();
            levelText.text = "Lv.1";
            levelText.anchor = TextAnchor.MiddleCenter;
            levelText.alignment = TextAlignment.Center;
            levelText.fontSize = 40;
            levelText.characterSize = .035f;
            levelText.fontStyle = FontStyle.Bold;
            levelText.color = new Color(1f, .95f, .7f);
            levelText.GetComponent<MeshRenderer>().sortingOrder = 9;

            holdIndicator = new GameObject("Hold Position Indicator");
            holdIndicator.transform.SetParent(transform, false);
            holdIndicator.transform.localPosition = new Vector3(-definition.Radius * .72f,
                definition.Radius + .34f, -0.36f);
            var holdBarColor = new Color(1f, .78f, .3f, .98f);
            var holdBarLeft = game.CreateSpriteChild(holdIndicator.transform, "Hold Bar Left",
                game.SquareSprite, holdBarColor, 1f, 11);
            var holdBarRight = game.CreateSpriteChild(holdIndicator.transform, "Hold Bar Right",
                game.SquareSprite, holdBarColor, 1f, 11);
            holdBarLeft.transform.localPosition = new Vector3(-.035f, 0f, -.08f);
            holdBarRight.transform.localPosition = new Vector3(.035f, 0f, -.08f);
            holdBarLeft.transform.localScale = new Vector3(.024f, .11f, 1f);
            holdBarRight.transform.localScale = holdBarLeft.transform.localScale;
            holdIndicator.SetActive(false);

            var healthBack = game.CreateSpriteChild(transform, "Health Back", game.SquareSprite,
                new Color(.08f, .1f, .18f, .95f), 1f, 6).transform;
            healthBack.localPosition = new Vector3(0f, -definition.Radius * 1.65f - .08f, -0.1f);
            healthBack.localScale = new Vector3(definition.Radius * 2.1f, .065f, 1f);
            healthFill = game.CreateSpriteChild(transform, "Health Fill", game.SquareSprite,
                new Color(.35f, .95f, .62f), 1f, 7).transform;

            var experienceBack = game.CreateSpriteChild(transform, "Experience Back", game.SquareSprite,
                new Color(.08f, .1f, .18f, .9f), 1f, 6).transform;
            experienceBack.localPosition = new Vector3(0f, -definition.Radius * 1.65f - .17f, -0.1f);
            experienceBack.localScale = new Vector3(definition.Radius * 2.1f, .045f, 1f);
            experienceFill = game.CreateSpriteChild(transform, "Experience Fill", game.SquareSprite,
                new Color(1f, .72f, .16f), 1f, 7).transform;
            UpdateBars();
        }

        public void SetSelected(bool selected)
        {
            selectionRing.SetActive(selected);
            if (selected) game.PlayUnitVoice(transform, Archetype, VoiceCue.Select);
            if (levelText != null) levelText.color = selected || IsHero
                ? new Color(1f, .87f, .25f)
                : new Color(1f, .95f, .7f);
        }

        private GameObject CreateSelectionRing(float radius)
        {
            var root = new GameObject("Ground Selection Ring");
            root.transform.SetParent(transform, false);
            var rear = game.CreateSpriteChild(root.transform, "Rear Arc", game.SelectionRearSprite,
                new Color(.18f, .9f, 1f, .98f), 1f, 2);
            var front = game.CreateSpriteChild(root.transform, "Front Arc", game.SelectionFrontSprite,
                new Color(.42f, .96f, 1f, 1f), 1f, 4);
            var scale = new Vector3(radius * 3.1f, radius * .84f, 1f);
            var position = new Vector3(0f, -radius * 1.3f, .02f);
            rear.transform.localScale = scale;
            front.transform.localScale = scale;
            rear.transform.localPosition = position;
            front.transform.localPosition = position;
            return root;
        }

        public void MoveTo(Vector2 target)
        {
            CancelAttackOrder();
            holdPosition = false;
            if (holdIndicator != null) holdIndicator.SetActive(false);
            moveDestination = game.NearestWalkableOnSameTerrain(target, Position, Radius * .55f);
            repathAttempts = 0;
            RebuildMovePath(false);
        }

        // A ground order always wins over an active focus-fire order.
        public void CancelAttackOrder()
        {
            commandedTarget = null;
            forcedAttackOrder = false;
        }

        public void Stop()
        {
            HaltMovement(true);
        }

        private void HaltMovement(bool hold)
        {
            if (hold)
            {
                holdPosition = true;
                commandedTarget = null;
                forcedAttackOrder = false;
            }
            moveTarget = Position;
            moveDestination = Position;
            movePath.Clear();
            movePathIndex = 0;
            velocity = Vector2.zero;
            currentMoveSpeed = 0f;
            IsMoving = false;
            stuckTime = 0f;
            if (holdIndicator != null) holdIndicator.SetActive(holdPosition);
        }

        public bool TryCommandAttack(EnemyUnit target)
        {
            if (target == null || !target.IsAlive || !game.CanUnitTargetEnemy(this, target) || game.Phase != GamePhase.Battle) return false;
            HaltMovement(false);
            commandedTarget = target;
            forcedAttackOrder = true;
            holdPosition = false;
            if (holdIndicator != null) holdIndicator.SetActive(false);
            nextPursuitRepathAt = 0f;
            lastPursuitTargetPosition = target.Position;
            facing = (target.Position - Position).normalized;
            game.PlayUnitVoice(transform, Archetype, VoiceCue.Attack);
            return true;
        }

        private void RebuildMovePath(bool recovering)
        {
            if (recovering && ++repathAttempts >= 3)
            {
                HaltMovement(false);
                return;
            }
            movePath.Clear();
            movePath.AddRange(game.FindWalkPath(Position, moveDestination, Radius * .55f));
            movePathIndex = 0;
            if (movePath.Count == 0)
            {
                HaltMovement(false);
                return;
            }
            moveTarget = movePath[0];
            lastProgressPosition = Position;
            stuckTime = 0f;
            IsMoving = true;
        }

        public void PrepareForNextRound(float recoveryFraction = 0f)
        {
            if (!IsAlive) return;
            var preserveHoldOrder = holdPosition;
            HaltMovement(false);
            commandedTarget = null;
            forcedAttackOrder = false;
            holdPosition = preserveHoldOrder;
            if (holdIndicator != null) holdIndicator.SetActive(holdPosition);
            // Surviving a round no longer grants a free heal. Recovery is an explicit augment
            // reward and is passed in by the round controller, so positioning damage remains
            // meaningful between waves.
            if (recoveryFraction > 0f)
                health = Mathf.Min(maxHealth, health + maxHealth * Mathf.Clamp01(recoveryFraction));
            UpdateBars();
        }

        public void TakeDamage(float amount, DamageType damageType = DamageType.Physical, bool meleeImpact = false,
            float physicalPenetration = 0f, float magicPenetration = 0f)
        {
            if (!IsAlive) return;
            if (invulnerableForQa) return;
            var heroReduction = IsHero ? .88f : 1f;
            var stanceReduction = DefensiveStanceActive ? 1f - defensiveDamageReduction : 1f;
            var roleReduction = game != null ? game.GetRoleDamageTakenMultiplier(this) : 1f;
            var formationReduction = game != null ? game.GetFormationDamageTakenMultiplier(this) : 1f;
            var mitigatedDamage = CombatMath.MitigatedDamage(amount, damageType, Armor, MagicResistance,
                physicalPenetration, magicPenetration);
            var appliedDamage = Mathf.Max(1f, mitigatedDamage * heroReduction * stanceReduction *
                                               roleReduction * formationReduction);
            health = Mathf.Max(0f, health - appliedDamage);
            hurtMotion = 1f;
            game.SpawnDamageNumber(Position, appliedDamage, damageType, false);
            var hitColor = damageType switch
            {
                DamageType.Magic => new Color(.62f, .3f, 1f),
                DamageType.Pure => new Color(1f, .27f, .15f),
                _ => new Color(1f, .58f, .16f)
            };
            var incoming = contactDirection.sqrMagnitude > .001f ? contactDirection : Vector2.down;
            var intensity = Mathf.Clamp(appliedDamage / Mathf.Max(10f, maxHealth * .12f), .72f, 1.55f);
            if (meleeImpact) game.SpawnMeleeImpactFeedback(Position, incoming, hitColor, intensity, true);
            else game.SpawnHitFeedback(Position, incoming, hitColor, intensity, true);
            UpdateBars();
            if (health > 0f)
            {
                if (Archetype == UnitArchetype.Tank && game.Phase == GamePhase.Battle)
                    AddExperience(Mathf.Clamp(appliedDamage * .055f, .08f, .55f));
                return;
            }
            IsAlive = false;
            game.PlayUnitVoice(transform, Archetype, VoiceCue.Defeat);
            game.RemoveUnit(this);
            Destroy(gameObject);
        }

        public void SetInvulnerableForQa(bool value) => invulnerableForQa = value;

        public void RestoreHealth(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            health = Mathf.Min(maxHealth, health + amount);
            UpdateBars();
        }

        public void ApplyArmorShred(float amount, float duration)
        {
            if (!IsAlive || amount <= 0f || duration <= 0f) return;
            if (Time.time < defensiveDebuffImmunityUntil) return;
            armorShredAmount = Mathf.Max(armorShredAmount, amount);
            armorShredUntil = Mathf.Max(armorShredUntil, Time.time + duration);
            game?.SpawnCombatImpact(HitPoint, UnitArchetype.Melee,
                new Color(.82f, .76f, .18f), Radius * 1.38f, Vector2.down, CombatVfxTier.Skill);
        }

        public void ApplyResistanceCurse(float amount, float duration)
        {
            if (!IsAlive || amount <= 0f || duration <= 0f) return;
            if (Time.time < defensiveDebuffImmunityUntil) return;
            resistanceCurseAmount = Mathf.Max(resistanceCurseAmount, amount);
            resistanceCurseUntil = Mathf.Max(resistanceCurseUntil, Time.time + duration);
            game?.SpawnCombatImpact(HitPoint, UnitArchetype.SingleMage,
                new Color(.22f, .83f, .94f), Radius * 1.46f, Vector2.down, CombatVfxTier.Skill);
        }

        public bool ApplyMagicSeal(float duration)
        {
            if (!IsAlive || duration <= 0f ||
                Archetype is not (UnitArchetype.AreaMage or UnitArchetype.SingleMage)) return false;
            if (Time.time < defensiveDebuffImmunityUntil) return false;
            magicSealUntil = Mathf.Max(magicSealUntil, Time.time + duration);
            game?.SpawnCombatImpact(HitPoint, UnitArchetype.SingleMage,
                new Color(.30f, .88f, 1f), Radius * 1.62f, Vector2.down, CombatVfxTier.Skill);
            return true;
        }

        public void ReduceSkillCooldown(float seconds)
        {
            if (!IsAlive || seconds <= 0f) return;
            skillCooldownRemaining = Mathf.Max(0f, skillCooldownRemaining - seconds);
        }

        public void ApplyDefensiveStance(float duration, float damageReduction, float armorBonus,
            float resistanceBonus)
        {
            if (!IsAlive) return;
            if (!DefensiveStanceActive)
            {
                defensiveDamageReduction = 0f;
                defensiveArmorBonus = 0f;
                defensiveResistanceBonus = 0f;
            }
            defensiveStanceUntil = Mathf.Max(defensiveStanceUntil, Time.time + Mathf.Max(.1f, duration));
            defensiveDamageReduction = Mathf.Max(defensiveDamageReduction,
                Mathf.Clamp(damageReduction, 0f, .72f));
            defensiveArmorBonus = Mathf.Max(defensiveArmorBonus, Mathf.Max(0f, armorBonus));
            defensiveResistanceBonus = Mathf.Max(defensiveResistanceBonus, Mathf.Max(0f, resistanceBonus));
        }

        public void ApplyUltimateBulwark(float duration, float damageReduction, float armorBonus,
            float resistanceBonus)
        {
            if (!IsAlive) return;
            ApplyDefensiveStance(duration, damageReduction, armorBonus, resistanceBonus);
            defensiveDebuffImmunityUntil = Mathf.Max(defensiveDebuffImmunityUntil,
                Time.time + Mathf.Max(.1f, duration));
            armorShredUntil = 0f;
            armorShredAmount = 0f;
            resistanceCurseUntil = 0f;
            resistanceCurseAmount = 0f;
            magicSealUntil = 0f;
        }

        public void AddExperience(float amount)
        {
            if (!IsAlive || Level >= 5 || amount <= 0f) return;
            experience += amount * (game != null ? game.GetTacticalExperienceMultiplier() : 1f);
            var leveled = false;
            while (Level < 5 && experience >= RequiredExperience(Level + 1))
            {
                Level++;
                maxHealth *= Level switch { 2 => 1.08f, 3 => 1.12f, 4 => 1.16f, _ => 1.28f };
                health = Mathf.Min(maxHealth, health + maxHealth * .3f);
                leveled = true;
            }

            if (Level >= 5 && !IsHero) EvolveToHero();
            else if (leveled) game.NotifyUnitLevelUp(this);
            UpdateBars();
        }

        public float ExperienceProgress()
        {
            if (Level >= 5) return 1f;
            var current = RequiredExperience(Level);
            var next = RequiredExperience(Level + 1);
            return Mathf.Clamp01((experience - current) / Mathf.Max(1f, next - current));
        }

        public float ExperienceToNextLevel() =>
            Level >= 5 ? 0f : Mathf.Max(0f, RequiredExperience(Level + 1) - experience);

        public float ExperienceWithinCurrentLevel() =>
            Level >= 5 ? 0f : Mathf.Max(0f, experience - RequiredExperience(Level));

        public float ExperienceRequiredForCurrentLevel() =>
            Level >= 5 ? 0f : Mathf.Max(1f, RequiredExperience(Level + 1) - RequiredExperience(Level));

        private float RequiredExperience(int level) =>
            ExperienceThreshold(level) * (Archetype == UnitArchetype.Tank ? .98f : 1f);

        private static float ExperienceThreshold(int level) => level switch
        {
            <= 1 => 0f,
            2 => 72f,
            3 => 182f,
            4 => 365f,
            _ => 635f
        };

        private void EvolveToHero(bool announce = true)
        {
            IsHero = true;
            if (visualRig != null) visualRig.SetHero(true);
            var heroAnimation = game.GetDirectionalAnimation(Archetype, true);
            var heroFrames = heroAnimation.FramesFor(facing);
            if (heroFrames.Length > 0)
            {
                directionalAnimation = heroAnimation;
                animationFrames = heroFrames;
                SetMotionSprite(animationFrames[0]);
                body.transform.localScale = Vector3.one * (definition.Radius * 4.32f);
                bodyBaseScale = body.transform.localScale;
            }
            else
            {
                var heroSprite = game.GetHeroSprite(Archetype);
                if (heroSprite != null)
                {
                    SetMotionSprite(heroSprite);
                    body.transform.localScale = Vector3.one * (definition.Radius * 4.32f);
                    bodyBaseScale = body.transform.localScale;
                }
            }
            bodyBaseColor = directionalAnimation != null ? Color.white : game.GetHeroSpriteTint(Archetype);
            body.color = bodyBaseColor;
            heroAura.enabled = false;
            heroCrestOuter.enabled = true;
            heroCrestCore.enabled = true;
            levelText.text = "HERO";
            levelText.color = new Color(1f, .78f, .12f);
            levelText.transform.localPosition = new Vector3(0f, definition.Radius + .76f, -.3f);
            shadowBaseScale = new Vector3(definition.Radius * 2.78f, definition.Radius * .76f, 1f);
            shadow.transform.localScale = shadowBaseScale;
            ultimateCooldownRemaining = 0f;
            RefreshCosmeticPresentation();
            if (announce)
            {
                game.PlayUnitVoice(transform, Archetype, VoiceCue.Hero);
                game.NotifyUnitLevelUp(this);
            }
        }

        public void RestoreCheckpointState(int savedLevel, float savedExperience, float savedHealth,
            bool savedHolding, float savedSkillCooldown, float savedUltimateCooldown)
        {
            // Rebuild progression from the same level multipliers used during live play.  This
            // avoids replaying level-up particles, voices and analytics while a save is loaded.
            Level = Mathf.Clamp(savedLevel, 1, 5);
            experience = Mathf.Max(0f, savedExperience);
            maxHealth = definition.MaxHealth * (game != null
                ? game.GetTacticalHealthMultiplier(Archetype)
                : 1f);
            for (var level = 2; level <= Level; level++)
                maxHealth *= level switch { 2 => 1.08f, 3 => 1.12f, 4 => 1.16f, _ => 1.28f };

            if (Level >= 5 && !IsHero) EvolveToHero(false);
            else if (levelText != null)
            {
                levelText.text = $"Lv.{Level}";
                levelText.color = Color.white;
            }

            health = Mathf.Clamp(savedHealth, 1f, maxHealth);
            skillCooldownRemaining = Mathf.Max(0f, savedSkillCooldown);
            ultimateCooldownRemaining = IsHero ? Mathf.Max(0f, savedUltimateCooldown) : 0f;
            HaltMovement(savedHolding);
            RefreshCosmeticPresentation();
            UpdateBars();
        }

        public bool TryUseUltimate()
        {
            if (!CanUseUltimate || !game.PerformUltimate(this)) return false;
            ultimateCooldownRemaining = UltimateCooldownDuration;
            attackMotion = 1f;
            ultimateMotion = 1f;
            return true;
        }

        public void CompleteUltimateRecoveryAndRetarget()
        {
            if (!IsAlive) return;
            // Lancer charge recovery is a real combat-state boundary, not only a visual pose.
            // Stop residual pursuit, discard the pre-charge target and let the next Update pick
            // the nearest valid threat from a clean state.
            HaltMovement(false);
            commandedTarget = null;
            forcedAttackOrder = false;
            nextPursuitRepathAt = 0f;
            lastPursuitTargetPosition = Position;
            lastAttackAt = Time.time;
            ultimateRecoveryUntil = Mathf.Max(ultimateRecoveryUntil, Time.time + .22f);
            ultimateRecoverySerial++;
        }

        public void BeginUltimateActionLock(float duration)
        {
            if (!IsAlive) return;
            // Ultimate poses own the actor root until their authored recovery frame. Without
            // this boundary auto-pursuit can restart underneath the cast and read as skating.
            HaltMovement(false);
            ultimateRecoveryUntil = Mathf.Max(ultimateRecoveryUntil, Time.time + duration);
        }

        private void Update()
        {
            if (!IsAlive || game == null) return;
            // Drive the gait from distance travelled, not wall-clock time.  After the timeline
            // grew from 7 to 16 walk frames, the old 11.2 frames/second cadence let the root move
            // several body widths during one step and read as skating.  One full gait now spans
            // roughly 3.65 radii (or 0.74 seconds of travel), with blocked actors correctly
            // ceasing their foot cycle.
            var travelled = Vector2.Distance(Position, lastAnimationSamplePosition);
            if (IsMoving)
            {
                var maximumExpected = Mathf.Max(.01f, definition.MoveSpeed * Time.deltaTime * 1.75f);
                travelled = Mathf.Min(travelled, maximumExpected);
                animationPhase += travelled / Mathf.Max(.01f, GaitStrideDistance) * 16f;
            }
            else animationPhase += Time.deltaTime * 2.4f;
            lastAnimationSamplePosition = Position;

            EnemyUnit target = null;
            if (game.Phase == GamePhase.Battle)
            {
                skillCooldownRemaining = Mathf.Max(0f, skillCooldownRemaining - Time.deltaTime);
                if (IsHero) ultimateCooldownRemaining = Mathf.Max(0f, ultimateCooldownRemaining - Time.deltaTime);
                // A manual ground move always has priority over combat pursuit.  This preserves
                // kiting: the player can click away during an attack without the unit instantly
                // rewriting that move into another chase order.
                if (knockbackTime <= 0f && !IsMoving && Time.time >= ultimateRecoveryUntil)
                {
                    target = ResolveCombatTarget();
                    if (target != null)
                    {
                        var targetDistance = game.DistanceToEnemySurface(Position, target);
                        if (targetDistance <= AttackRange)
                        {
                            if (IsMoving) HaltMovement(false);
                            // Lock the painted aim before UpdateCharacterMotion samples a frame.
                            // Previously the projectile was spawned toward the new target at the
                            // end of this Update while the body still displayed the preceding
                            // direction for one frame.  It was most visible on the hero archer:
                            // she looked up/right while an arrow left her bow toward up/left.
                            FaceCombatTargetImmediately(target.Position - Position);
                        }
                        else PursueTarget(target);
                    }
                }
            }

            if (knockbackTime > 0f) UpdateKnockback();
            else if (IsMoving) UpdateMovement();
            UpdateCharacterMotion();

            var attackDelay = definition.AttackDelay * AttackDelayMultiplier *
                              game.GetHighGroundAttackDelayMultiplier(this) *
                              game.GetRoleAttackDelayMultiplier(this);
            if (game.Phase != GamePhase.Battle || IsMoving || knockbackTime > 0f || Time.time < lastAttackAt + attackDelay) return;
            if (target == null || !target.IsAlive || game.DistanceToEnemySurface(Position, target) > AttackRange) return;
            lastAttackAt = Time.time;
            FaceCombatTargetImmediately(target.Position - Position);
            attackMotion = 1f;
            if (skillCooldownRemaining <= 0f && !IsMagicSealed)
            {
                game.PlayUnitVoice(transform, Archetype, VoiceCue.Skill);
                game.PerformSkill(this, target, definition);
                skillMotion = 1f;
                skillCooldownRemaining = definition.SkillCooldown * AttackDelayMultiplier *
                                         game.GetRoleSkillCooldownMultiplier(this);
            }
            else
            {
                game.PlayUnitVoice(transform, Archetype, VoiceCue.Attack);
                game.PerformAttack(this, target, definition);
            }
        }

        private void FaceCombatTargetImmediately(Vector2 targetDirection)
        {
            if (targetDirection.sqrMagnitude <= .0001f) return;
            facing = targetDirection.normalized;

            // Ranged attacks cannot visually lag behind their projectile.  The archer's rear and
            // front sheets have one authored left/right draw bias, so a mostly vertical shot with
            // a real horizontal component must use the corresponding diagonal pose instead of a
            // neutral North/South pose that can appear to aim across the arrow path.
            var visualAim = Archetype == UnitArchetype.Archer
                ? EightWayFacing.VectorFor(CombatAimOctant(facing))
                : facing;
            visualFacing = visualAim;
            visualOctant = EightWayFacing.FromVector(visualAim);
            // Keep the authored archer pose locked through the projectile spawn and the first
            // readable attack frames.  Without this short lock, the generic turn smoothing below
            // immediately blended a SouthEast/NorthWest pose back into pure South/North, so the
            // body could face right while the arrow travelled left (or vice versa).
            if (Archetype == UnitArchetype.Archer)
                combatFacingLockUntil = Mathf.Max(combatFacingLockUntil, Time.time + .34f);
        }

        private FacingOctant CombatAimOctant(Vector2 targetDirection)
        {
            var normalized = targetDirection.sqrMagnitude > .0001f
                ? targetDirection.normalized
                : Vector2.down;
            var octant = EightWayFacing.FromVector(normalized);
            if (Archetype != UnitArchetype.Archer || Mathf.Abs(normalized.x) < .035f) return octant;
            return octant switch
            {
                FacingOctant.North => normalized.x > 0f
                    ? FacingOctant.NorthEast
                    : FacingOctant.NorthWest,
                FacingOctant.South => normalized.x > 0f
                    ? FacingOctant.SouthEast
                    : FacingOctant.SouthWest,
                _ => octant
            };
        }

        private EnemyUnit ResolveCombatTarget()
        {
            // Hold position means no pursuit, not no combat.  A stopped defender still guards
            // its weapon range, but never acquires a target outside that range.
            if (holdPosition) return game.FindEnemyInRange(Position, AttackRange, this);
            if (commandedTarget != null)
            {
                if (!commandedTarget.IsAlive ||
                    (!forcedAttackOrder && game.DistanceToEnemySurface(Position, commandedTarget) > DetectionRange * 1.12f))
                {
                    commandedTarget = null;
                    forcedAttackOrder = false;
                }
            }
            if (commandedTarget != null) return commandedTarget;
            commandedTarget = game.FindEnemyInRange(Position, DetectionRange, this);
            forcedAttackOrder = false;
            return commandedTarget;
        }

        private void PursueTarget(EnemyUnit target)
        {
            if (target == null || !target.IsAlive || holdPosition) return;
            var fromTarget = Position - target.Position;
            if (fromTarget.sqrMagnitude < .001f) fromTarget = Vector2.down;
            var desiredRange = Mathf.Max(.16f, AttackRange * .88f + target.TargetableRadius * .72f);
            var desired = target.Position + fromTarget.normalized * desiredRange;
            desired = game.NearestWalkableOnSameTerrain(desired, Position, Radius * .55f);
            var targetShifted = Vector2.Distance(target.Position, lastPursuitTargetPosition) > .16f;
            var destinationShifted = Vector2.Distance(moveDestination, desired) > .14f;
            if (Time.time < nextPursuitRepathAt || IsMoving && !targetShifted && !destinationShifted) return;
            moveDestination = desired;
            repathAttempts = 0;
            RebuildMovePath(false);
            nextPursuitRepathAt = Time.time + .24f;
            lastPursuitTargetPosition = target.Position;
        }

        private void UpdateCharacterMotion()
        {
            // Give the attack a readable anticipation/contact/recovery window.  The old
            // half-second cycle skipped directly between three painted poses on phones.
            var motionDelta = Mathf.Min(Time.deltaTime, .05f);
            attackMotion = Mathf.Max(0f, attackMotion - motionDelta * 1.52f);
            skillMotion = Mathf.Max(0f, skillMotion - motionDelta * 1.35f);
            ultimateMotion = Mathf.Max(0f, ultimateMotion - motionDelta * .82f);
            hurtMotion = Mathf.Max(0f, hurtMotion - motionDelta * 6.5f);
            contactMotion = Mathf.Max(0f, contactMotion - motionDelta * 4.5f);
            levelUpMotion = Mathf.Max(0f, levelUpMotion - motionDelta * .85f);
            var travelBounce = IsMoving ? Mathf.Abs(Mathf.Sin(animationPhase)) : 0f;
            var breathe = Mathf.Sin(animationPhase * .72f);
            var attackT = 1f - attackMotion;
            var attackActive = attackMotion > 0f;
            var skillT = 1f - skillMotion;
            var skillActive = skillMotion > 0f;
            var ultimateT = 1f - ultimateMotion;
            var ultimateActive = ultimateMotion > 0f;
            var squash = IsMoving ? Mathf.Sin(animationPhase) * .055f : breathe * .018f;
            var attackForward = 0f;
            var attackLift = 0f;
            var attackTilt = 0f;
            var attackSquash = 0f;
            if (facing.sqrMagnitude > .001f)
            {
                var requestedFacing = facing.normalized;
                var archerCombatFacingLocked = Archetype == UnitArchetype.Archer && !IsMoving &&
                                               Time.time <= combatFacingLockUntil;
                // Directional sheets may blend within one octant, but must never keep the old
                // horizontal hemisphere after movement reverses. The Petal Spiritcaller exposed
                // this most clearly: she travelled left while the smoothed pose still faced
                // right. Snap an opposite turn, then resume the softer eight-direction blend.
                var oppositeTurn = IsMoving && visualFacing.sqrMagnitude > .001f &&
                                   (Vector2.Dot(visualFacing, requestedFacing) < .12f ||
                                    Mathf.Abs(requestedFacing.x) > .35f &&
                                    Mathf.Sign(visualFacing.x) != Mathf.Sign(requestedFacing.x));
                if (archerCombatFacingLocked)
                    visualFacing = EightWayFacing.VectorFor(CombatAimOctant(requestedFacing));
                else if (oppositeTurn) visualFacing = requestedFacing;
                else
                {
                    var turnBlend = 1f - Mathf.Exp(-motionDelta * (IsMoving ? 22f : 24f));
                    visualFacing = Vector2.Lerp(visualFacing, requestedFacing, turnBlend).normalized;
                }
            }
            var poseFacing = visualFacing.sqrMagnitude > .001f ? visualFacing : facing;
            visualOctant = EightWayFacing.FromVector(poseFacing);
            var octantForward = EightWayFacing.VectorFor(visualOctant);

            if (directionalAnimation != null)
            {
                animationFrames = directionalAnimation.FramesFor(visualOctant);
            }

            if (animationFrames.Length >= 7)
            {
                var cinematicTimeline = animationFrames.Length >= 64;
                var completeTimeline = animationFrames.Length >= 48;
                var extendedTimeline = animationFrames.Length >= 17;
                var walkFrameCount = cinematicTimeline ? 16 : completeTimeline ? 12 : extendedTimeline ? 8 : 4;
                var attackFrameCount = cinematicTimeline ? 16 : completeTimeline ? 12 : extendedTimeline ? 9 : 3;
                var combatMotionActive = ultimateActive || skillActive || attackActive;
                var combatMotionT = ultimateActive ? ultimateT : skillActive ? skillT : attackT;
                var attackPhase = Mathf.Clamp(Mathf.FloorToInt(combatMotionT * attackFrameCount),
                    0, attackFrameCount - 1);
                var sideFacing = visualOctant is FacingOctant.West or FacingOctant.East;
                var diagonalFacing = EightWayFacing.IsDiagonal(visualOctant);
                var upFacing = EightWayFacing.IsBack(visualOctant);
                var sparseHeroMageImpact = IsHero && sideFacing &&
                                           (Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage);
                var attackColumn = cinematicTimeline
                    ? ultimateActive ? 48 + attackPhase : skillActive ? 32 + attackPhase : 16 + attackPhase
                    : completeTimeline
                    ? ultimateActive ? 36 + attackPhase : skillActive ? 24 + attackPhase : 12 + attackPhase
                    : extendedTimeline ? walkFrameCount + attackPhase : 4 + attackPhase;
                if (!extendedTimeline && attackPhase == 1 && (upFacing || sparseHeroMageImpact))
                    attackColumn = 4;
                var frameIndex = combatMotionActive
                    ? attackColumn
                    : IsMoving ? Mathf.FloorToInt(animationPhase) % walkFrameCount : 0;
                SetMotionSprite(animationFrames[frameIndex]);
                UpdateActionAccent(combatMotionActive, combatMotionT, skillActive, ultimateActive);
                var hurtCurve = Mathf.Sin(hurtMotion * Mathf.PI);
                var frameContactCurve = Mathf.Sin(contactMotion * Mathf.PI);
                // Hit feedback must not move the complete painted actor sideways. On a sprite
                // timeline that translation reads as a bad pivot and compounds with a frame's
                // own anticipation pose. Keep a very small vertical settle only; impact flash,
                // shadow and VFX carry the hit response.
                var frameContactOffset = new Vector2(0f,
                    contactDirection.y * frameContactCurve * .022f);
                var frameLevelCurve = levelUpMotion > 0f ? Mathf.Sin((1f - levelUpMotion) * Mathf.PI) : 0f;
                var strikeCurve = combatMotionActive ? Mathf.Sin(combatMotionT * Mathf.PI) : 0f;
                var invariantSkinAction = combatMotionActive && SkinVariant > 0 && game != null &&
                                          game.HasAuthoredSkinAnimation(Archetype, SkinVariant, IsHero);
                var skinActionPower = Archetype is UnitArchetype.Melee or UnitArchetype.Lancer ? 1f :
                    Archetype is UnitArchetype.Tank or UnitArchetype.Bombardier ? .78f : .62f;
                var directionScale = directionalAnimation.ScaleFor(octantForward);
                var directionOffset = directionalAnimation.VerticalOffsetFor(octantForward) * definition.Radius;
                body.transform.localScale = new Vector3(
                    bodyBaseScale.x * directionScale *
                    (1f + frameLevelCurve * .12f + hurtCurve * .035f +
                     strikeCurve * (invariantSkinAction ? .042f * skinActionPower : .025f)),
                    bodyBaseScale.y * directionScale *
                    (1f + frameLevelCurve * .12f + hurtCurve * .02f -
                     strikeCurve * (invariantSkinAction ? .032f * skinActionPower : .018f)),
                    1f);
                NormalizeCurrentSpriteHeight();
                // The painted sheets already contain the complete gait. The previous runtime
                // hip/stride translation moved the *entire* SpriteRenderer around its shadow,
                // so a correctly authored sequence still appeared to skate left and right.
                // Locomotion now advances the world transform only; the renderer changes frames
                // around one registered body centre.
                var stepLift = 0f;
                var footPlantSettle = Vector2.zero;
                var actionScale = ultimateActive ? 1.65f : skillActive ? 1.22f : 1f;
                var stagedAction = combatMotionActive
                    ? Archetype == UnitArchetype.Lancer
                        // A spear thrust braces in place, then commits forward. The generic melee
                        // backswing moved the complete lancer sprite rearward before an already
                        // extended authored pose appeared, producing the visible moonwalk.
                        ? combatMotionT < .18f
                            ? -Mathf.SmoothStep(0f, .045f, combatMotionT / .18f)
                            : combatMotionT < .48f
                                ? Mathf.Lerp(-.045f, 1f,
                                    Mathf.SmoothStep(0f, 1f, (combatMotionT - .18f) / .3f))
                                : combatMotionT < .64f
                                    ? Mathf.Lerp(1f, .72f,
                                        Mathf.SmoothStep(0f, 1f, (combatMotionT - .48f) / .16f))
                                    : Mathf.Lerp(.72f, 0f,
                                        Mathf.SmoothStep(0f, 1f, (combatMotionT - .64f) / .36f))
                        : combatMotionT < .26f
                            ? -Mathf.SmoothStep(0f, .32f, combatMotionT / .26f)
                            : combatMotionT < .5f
                                ? Mathf.Lerp(-.32f, 1f,
                                    Mathf.SmoothStep(0f, 1f, (combatMotionT - .26f) / .24f))
                                : Mathf.Lerp(1f, 0f,
                                    Mathf.SmoothStep(0f, 1f, (combatMotionT - .5f) / .5f))
                    : 0f;
                var roleTravel = Archetype switch
                {
                    UnitArchetype.Melee => .36f,
                    // The two authored contact paintings brace the torso roughly one body-width
                    // behind their neutral cell center. Counter that paint-space retreat with a
                    // renderer-only lunge; transform position, pursuit and attack range stay fixed.
                    // The authored lancer frames already extend the spear and torso. A full body
                    // radius of renderer translation doubled that motion and read as skating.
                    UnitArchetype.Lancer => ultimateActive ? .64f : skillActive ? .34f : .46f,
                    UnitArchetype.Tank => .23f,
                    UnitArchetype.Bombardier => .18f,
                    // Ranged and caster timelines carry their own draw/cast motion. Translating
                    // their whole body towards a target made the bow and staff origins jump.
                    UnitArchetype.Archer or UnitArchetype.Musketeer => 0f,
                    _ => 0f
                };
                var actionTravel = stagedAction * definition.Radius * roleTravel * actionScale;
                var casterAction = Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                    UnitArchetype.Druid or UnitArchetype.Oracle;
                if (casterAction && (skillActive || ultimateActive))
                    roleTravel *= ultimateActive ? .12f : .34f;
                actionTravel = stagedAction * definition.Radius * roleTravel * actionScale;
                var actionLift = combatMotionActive && casterAction
                    ? Mathf.Sin(combatMotionT * Mathf.PI) * definition.Radius *
                      (ultimateActive ? .16f : skillActive ? .1f : .055f)
                    : 0f;
                body.transform.localPosition = new Vector3(
                    frameContactOffset.x + footPlantSettle.x,
                    frameContactOffset.y + footPlantSettle.y +
                    frameLevelCurve * .18f + stepLift + actionLift + directionOffset, -.15f);
                body.transform.localPosition += new Vector3(
                    octantForward.x * actionTravel, octantForward.y * actionTravel, 0f);
                var facingTiltSign = Mathf.Abs(octantForward.x) > .2f ? -octantForward.x :
                    octantForward.y >= 0f ? -.42f : .42f;
                var meleeSwingDegrees = Archetype switch
                {
                    UnitArchetype.Melee => 29f,
                    // A lance is driven along its shaft. Rotating the whole painted body like a
                    // sword swing made the feet and spear direction detach from the target line.
                    UnitArchetype.Lancer => 6.5f,
                    UnitArchetype.Tank => 19f,
                    _ => invariantSkinAction ? 15f * skinActionPower : 10f
                };
                var actionTilt = casterAction && (skillActive || ultimateActive)
                    ? Mathf.Sin(combatMotionT * Mathf.PI * 2f) *
                      (ultimateActive ? 14f : 8f)
                    : facingTiltSign * stagedAction *
                      meleeSwingDegrees * actionScale;
                // Do not rotate the complete renderer during locomotion. Each authored frame is
                // already an eight-direction pose; procedural roll exposed the rectangular crop
                // edge and made the registered centre orbit around the feet.
                body.transform.localEulerAngles = new Vector3(0f, 0f,
                    combatMotionActive ? actionTilt : 0f);
                body.flipX = ShouldFlipForDirection(visualOctant);
                AnchorCurrentSpriteToGround(frameLevelCurve * .18f + actionLift +
                                            (IsMoving ? 0f : frameContactOffset.y) +
                                            octantForward.y * actionTravel);
                body.color = Color.Lerp(bodyBaseColor, new Color(1f, .62f, .58f), hurtCurve * .72f);
                if (shadow != null)
                {
                    shadow.transform.localScale = shadowBaseScale * (1f - frameLevelCurve * .08f);
                    var shadowColor = shadow.color;
                    shadowColor.a = .42f * (1f - frameLevelCurve * .25f);
                    shadow.color = shadowColor;
                }
                UpdateHeroPresentation();
                UpdateSkinSignatureMotion();
                if (visualRig != null) visualRig.Animate(octantForward, IsMoving,
                    combatMotionActive ? combatMotionT : 0f,
                    skillMotion, ultimateMotion, hurtMotion, levelUpMotion, IsHero, false);
                return;
            }

            if (attackActive)
            {
                switch (Archetype)
                {
                    case UnitArchetype.Tank:
                    case UnitArchetype.Melee:
                    case UnitArchetype.Lancer:
                    {
                        var power = Archetype is UnitArchetype.Melee or UnitArchetype.Lancer ? 1f : .68f;
                        if (attackT < .28f)
                        {
                            var windup = Smooth01(attackT / .28f);
                            attackForward = Mathf.Lerp(0f, -.075f * power, windup);
                            attackTilt = Mathf.Lerp(0f, facing.x * 9f * power, windup);
                            attackSquash = -.035f * windup;
                        }
                        else if (attackT < .5f)
                        {
                            var strike = Smooth01((attackT - .28f) / .22f);
                            attackForward = Mathf.Lerp(-.075f * power, .22f * power, strike);
                            attackTilt = Mathf.Lerp(facing.x * 9f * power, -facing.x * 13f * power, strike);
                            attackSquash = Mathf.Sin(strike * Mathf.PI) * .12f;
                        }
                        else
                        {
                            var recover = Smooth01((attackT - .5f) / .5f);
                            attackForward = Mathf.Lerp(.22f * power, 0f, recover);
                            attackTilt = Mathf.Lerp(-facing.x * 13f * power, 0f, recover);
                            attackSquash = Mathf.Lerp(.05f, 0f, recover);
                        }
                        break;
                    }
                    case UnitArchetype.Archer:
                    case UnitArchetype.Musketeer:
                    {
                        var draw = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
                        attackForward = -.095f * draw;
                        attackLift = .025f * draw;
                        attackTilt = facing.x * 7f * draw;
                        attackSquash = -.045f * draw;
                        break;
                    }
                    case UnitArchetype.AreaMage:
                    case UnitArchetype.Druid:
                    {
                        var cast = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
                        attackLift = .12f * cast;
                        attackForward = -.035f * cast;
                        attackTilt = Mathf.Sin(attackT * Mathf.PI * 2f) * 5f;
                        attackSquash = cast * .085f;
                        break;
                    }
                    case UnitArchetype.SingleMage:
                    case UnitArchetype.Oracle:
                    {
                        var focus = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
                        attackLift = .085f * focus;
                        attackForward = -.06f * focus;
                        attackTilt = -facing.x * 6f * focus;
                        attackSquash = focus * .055f;
                        break;
                    }
                    case UnitArchetype.Bombardier:
                    {
                        var recoil = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
                        attackForward = -.12f * recoil;
                        attackLift = .03f * recoil;
                        attackTilt = facing.x * 8f * recoil;
                        attackSquash = -.045f * recoil;
                        break;
                    }
                }
            }

            var hurtSquash = Mathf.Sin(hurtMotion * Mathf.PI) * .13f;
            var contactCurve = Mathf.Sin(contactMotion * Mathf.PI);
            var contactOffset = contactDirection * (contactCurve * .09f);
            var levelCurve = levelUpMotion > 0f ? Mathf.Sin((1f - levelUpMotion) * Mathf.PI) : 0f;

            UpdateFootMotion();
            UpdateActionAccent(attackActive, attackT);

            body.transform.localScale = new Vector3(
                bodyBaseScale.x * (1f + squash * .45f + attackSquash + hurtSquash + levelCurve * .13f),
                bodyBaseScale.y * (1f - squash * .3f - attackSquash * .62f - hurtSquash * .55f + levelCurve * .13f),
                1f);
            body.transform.localPosition = new Vector3(
                facing.x * attackForward + contactOffset.x,
                travelBounce * .022f + breathe * .012f + facing.y * attackForward + attackLift + contactOffset.y + levelCurve * .2f,
                -0.15f);
            var movementTilt = IsMoving ? -velocity.x * 7f : breathe * 1.2f;
            body.transform.localEulerAngles = new Vector3(0f, 0f, movementTilt + attackTilt);
            body.flipX = ShouldFlipForDirection(visualOctant);
            AnchorCurrentSpriteToGround(levelCurve * .2f + attackLift +
                                        (IsMoving ? 0f : contactOffset.y));
            body.color = Color.Lerp(bodyBaseColor, new Color(1f, .62f, .58f),
                Mathf.Sin(hurtMotion * Mathf.PI) * .72f);

            if (shadow != null)
            {
                var height = travelBounce * .12f + Mathf.Max(0f, attackLift) * 1.2f;
                shadow.transform.localScale = new Vector3(
                    shadowBaseScale.x * (1f - height * .22f + Mathf.Abs(attackForward) * .12f),
                    shadowBaseScale.y * (1f - height * .38f),
                    1f);
                var shadowColor = shadow.color;
                shadowColor.a = .42f * (1f - height * .55f);
                shadow.color = shadowColor;
            }

            UpdateHeroPresentation();
            UpdateSkinSignatureMotion();
            if (visualRig != null) visualRig.Animate(EightWayFacing.VectorFor(visualOctant), IsMoving,
                attackActive ? attackT : 0f,
                skillMotion, ultimateMotion, hurtMotion, levelUpMotion, IsHero, false);
        }

        private void BuildSkinSignature()
        {
            skinSignatureRoot = new GameObject("Equipped Skin Silhouette");
            skinSignatureRoot.transform.SetParent(transform, false);
            skinAuthoredBody = game.CreateSpriteChild(skinSignatureRoot.transform, "Authored Skin Character",
                game.CircleSprite, Color.clear, 1f, 4);
            skinAuthoredBody.enabled = false;
            skinBackGlow = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Back Glow",
                game.GlowSprite, Color.clear, 1f, 2);
            skinBodyOutline = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Armor Silhouette",
                body != null ? body.sprite : game.CircleSprite, Color.clear, 1f, 2);
            skinCape = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Mantle",
                game.SquareSprite, Color.clear, 1f, 2);
            skinShoulderLeft = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Left Shoulder",
                game.SparkSprite, Color.clear, 1f, 5);
            skinShoulderRight = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Right Shoulder",
                game.SparkSprite, Color.clear, 1f, 5);
            skinCrest = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Crown Crest",
                game.SparkSprite, Color.clear, 1f, 6);
            skinHelmLeft = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Left Helm Wing",
                game.SparkSprite, Color.clear, 1f, 6);
            skinHelmRight = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Right Helm Wing",
                game.SparkSprite, Color.clear, 1f, 6);
            skinTabard = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Tabard",
                game.SparkSprite, Color.clear, 1f, 5);
            skinWeaponSigil = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Weapon Sigil",
                game.CommandRingSprite, Color.clear, 1f, 7);
            skinAccessoryA = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Signature A",
                game.SquareSprite, Color.clear, 1f, 4);
            skinAccessoryB = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Signature B",
                game.SparkSprite, Color.clear, 1f, 7);
            skinAccessoryC = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Signature C",
                game.SquareSprite, Color.clear, 1f, 6);
            skinAccessoryD = game.CreateSpriteChild(skinSignatureRoot.transform, "Skin Signature D",
                game.SparkSprite, Color.clear, 1f, 7);
            skinSignatureRoot.SetActive(false);
        }

        private void RefreshSkinSignature()
        {
            if (skinSignatureRoot == null || game == null) return;
            var variant = game.GetUnitSkinVariant(Archetype);
            var hasAnimatedSkin = game.HasAuthoredSkinAnimation(Archetype, variant, IsHero);
            skinSignatureRoot.SetActive(variant > 0 && !hasAnimatedSkin);
            if (hasAnimatedSkin)
            {
                usingStaticSkinBody = false;
                if (skinAuthoredBody != null) skinAuthoredBody.enabled = false;
                if (body != null) body.enabled = visualRig == null;
                return;
            }
            if (variant <= 0)
            {
                usingStaticSkinBody = false;
                if (skinAuthoredBody != null) skinAuthoredBody.enabled = false;
                if (body != null) body.enabled = visualRig == null;
                return;
            }

            var authoredSprite = game.GetAuthoredUnitSkinSprite(Archetype, variant, IsHero);
            if (authoredSprite != null && skinAuthoredBody != null)
            {
                usingStaticSkinBody = true;
                SetProceduralSkinPartsEnabled(false);
                skinAuthoredBody.enabled = true;
                skinAuthoredBody.sprite = authoredSprite;
                skinAuthoredBody.color = Color.white;
                skinAuthoredBody.flipX = body != null && body.flipX;
                var targetHeight = definition.Radius * (IsHero ? 4.48f : 3.34f);
                var authoredHeight = Mathf.Max(.01f, authoredSprite.bounds.size.y);
                var authoredScale = targetHeight / authoredHeight;
                skinAuthoredBody.transform.localPosition = new Vector3(0f,
                    definition.Radius * (IsHero ? .1f : .06f), -.04f);
                skinAuthoredBody.transform.localScale = Vector3.one * authoredScale;
                skinAuthoredBody.transform.localEulerAngles = Vector3.zero;
                if (body != null) body.enabled = false;
                return;
            }

            if (skinAuthoredBody != null) skinAuthoredBody.enabled = false;
            usingStaticSkinBody = false;
            if (body != null) body.enabled = visualRig == null;
            SetProceduralSkinPartsEnabled(true);

            var accent = game.GetUnitSkinAccent(Archetype);
            var secondary = game.GetUnitSkinSecondary(Archetype);
            var radius = definition.Radius * (IsHero ? 1.2f : 1f);
            skinBackGlow.color = new Color(accent.r, accent.g, accent.b, variant == 1 ? .24f : .36f);
            skinBackGlow.transform.localPosition = new Vector3(0f, .05f, .035f);
            skinBackGlow.transform.localScale = Vector3.one * radius * (variant == 1 ? 2.35f : 2.7f);
            skinBodyOutline.color = new Color(secondary.r, secondary.g, secondary.b,
                variant == 1 ? .52f : .68f);

            skinCape.color = new Color(secondary.r, secondary.g, secondary.b, variant == 1 ? .62f : .88f);
            skinCape.transform.localPosition = new Vector3(0f, -radius * .12f, .03f);
            skinCape.transform.localScale = new Vector3(radius * (variant == 1 ? 1.15f : 1.5f),
                radius * (variant == 1 ? 1.58f : 1.76f), 1f);
            skinCape.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? 45f : 0f);

            var shoulderColor = variant == 1
                ? Color.Lerp(accent, Color.white, .45f)
                : Color.Lerp(accent, new Color(.05f, .035f, .08f), .28f);
            skinShoulderLeft.color = shoulderColor;
            skinShoulderRight.color = shoulderColor;
            skinShoulderLeft.transform.localPosition = new Vector3(-radius * .72f, radius * .15f, -.03f);
            skinShoulderRight.transform.localPosition = new Vector3(radius * .72f, radius * .15f, -.03f);
            var shoulderScale = new Vector3(radius * (variant == 1 ? .46f : .62f),
                radius * (variant == 1 ? .7f : .88f), 1f);
            skinShoulderLeft.transform.localScale = shoulderScale;
            skinShoulderRight.transform.localScale = shoulderScale;
            skinShoulderLeft.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? 22f : 48f);
            skinShoulderRight.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? -22f : -48f);

            skinCrest.color = variant == 1 ? Color.Lerp(accent, Color.white, .68f) : accent;
            skinCrest.transform.localPosition = new Vector3(0f, radius * 1.03f, -.05f);
            skinCrest.transform.localScale = Vector3.one * radius * (variant == 1 ? .58f : .8f);
            skinCrest.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? 0f : 45f);

            var caster = Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle;
            var ranged = Archetype is UnitArchetype.Archer or UnitArchetype.Musketeer or
                UnitArchetype.Bombardier;
            var heavy = Archetype is UnitArchetype.Tank or UnitArchetype.Melee or UnitArchetype.Lancer;
            var silhouetteWidth = heavy ? 1.18f : ranged ? .96f : 1.04f;

            skinHelmLeft.color = variant == 1
                ? Color.Lerp(accent, Color.white, .78f)
                : Color.Lerp(secondary, Color.black, .18f);
            skinHelmRight.color = skinHelmLeft.color;
            skinHelmLeft.transform.localPosition = new Vector3(-radius * .48f, radius * .83f, -.07f);
            skinHelmRight.transform.localPosition = new Vector3(radius * .48f, radius * .83f, -.07f);
            skinHelmLeft.transform.localScale = new Vector3(radius * (variant == 1 ? .42f : .56f),
                radius * (variant == 1 ? .82f : 1.05f), 1f);
            skinHelmRight.transform.localScale = skinHelmLeft.transform.localScale;
            skinHelmLeft.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? 62f : 28f);
            skinHelmRight.transform.localEulerAngles = new Vector3(0f, 0f, variant == 1 ? -62f : -28f);

            skinTabard.color = new Color(secondary.r, secondary.g, secondary.b, variant == 1 ? .82f : .94f);
            skinTabard.transform.localPosition = new Vector3(0f, -radius * .58f, -.08f);
            skinTabard.transform.localScale = new Vector3(radius * silhouetteWidth,
                radius * (caster ? 1.5f : variant == 1 ? 1.16f : 1.38f), 1f);
            skinTabard.transform.localEulerAngles = new Vector3(0f, 0f, 180f);

            var sigilX = caster ? radius * .78f : ranged ? -radius * .78f : radius * .68f;
            skinWeaponSigil.color = variant == 1
                ? Color.Lerp(accent, Color.white, .72f)
                : Color.Lerp(accent, new Color(.08f, .02f, .12f), .28f);
            skinWeaponSigil.transform.localPosition = new Vector3(sigilX,
                radius * (caster ? .38f : .08f), -.12f);
            skinWeaponSigil.transform.localScale = Vector3.one * radius *
                                                     (heavy ? .44f : caster ? .52f : .38f);
            skinWeaponSigil.transform.localEulerAngles = new Vector3(0f, 0f,
                variant == 1 ? Time.time * 0f : 45f);

            void SetPart(SpriteRenderer part, Sprite sprite, Color color, Vector2 position,
                Vector2 scale, float angle, int order)
            {
                part.sprite = sprite;
                part.color = color;
                part.transform.localPosition = new Vector3(position.x * radius, position.y * radius, -.1f);
                part.transform.localScale = new Vector3(scale.x * radius, scale.y * radius, 1f);
                part.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                part.sortingOrder = order;
            }

            var bright = Color.Lerp(accent, Color.white, variant == 1 ? .74f : .38f);
            var darkAccent = Color.Lerp(secondary, new Color(.035f, .025f, .07f), variant == 1 ? .2f : .52f);
            switch (Archetype)
            {
                case UnitArchetype.Tank:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(-.86f, -.03f),
                        variant == 1 ? new Vector2(.72f, 1.62f) : new Vector2(.94f, 1.82f), 0f, 4);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(-.86f, .12f),
                        Vector2.one * (variant == 1 ? .66f : .86f), 45f, 8);
                    SetPart(skinAccessoryC, game.SparkSprite, bright, new Vector2(-.42f, 1.12f),
                        new Vector2(.34f, variant == 1 ? .82f : 1.12f), variant == 1 ? 28f : 4f, 7);
                    SetPart(skinAccessoryD, game.SparkSprite, bright, new Vector2(.42f, 1.12f),
                        new Vector2(.34f, variant == 1 ? .82f : 1.12f), variant == 1 ? -28f : -4f, 7);
                    break;
                case UnitArchetype.Melee:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(.86f, .38f),
                        variant == 1 ? new Vector2(.92f, .55f) : new Vector2(1.15f, .72f), 16f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(1.23f, .43f),
                        Vector2.one * (variant == 1 ? .48f : .72f), 90f, 8);
                    SetPart(skinAccessoryC, game.SquareSprite, secondary, new Vector2(-.76f, .12f),
                        new Vector2(.24f, 1.08f), -15f, 4);
                    SetPart(skinAccessoryD, game.SparkSprite, bright, new Vector2(-.76f, .82f),
                        Vector2.one * .42f, variant == 1 ? 0f : 45f, 8);
                    break;
                case UnitArchetype.Archer:
                    SetPart(skinAccessoryA, game.SparkSprite, bright, new Vector2(-.52f, 1.02f),
                        new Vector2(.38f, 1.18f), variant == 1 ? 38f : 72f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(.52f, 1.02f),
                        new Vector2(.38f, 1.18f), variant == 1 ? -38f : -72f, 7);
                    SetPart(skinAccessoryC, game.SquareSprite, darkAccent, new Vector2(-.91f, .16f),
                        new Vector2(.12f, 1.66f), 22f, 5);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(-1.04f, .22f),
                        new Vector2(.43f, 1.48f), -5f, 6);
                    break;
                case UnitArchetype.AreaMage:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(0f, .78f),
                        new Vector2(1.35f, .18f), 0f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(0f, 1.42f),
                        new Vector2(.62f, variant == 1 ? 1.18f : 1.55f), 0f, 8);
                    SetPart(skinAccessoryC, game.SparkSprite, accent, new Vector2(-.78f, .22f),
                        new Vector2(.42f, 1.08f), variant == 1 ? 118f : 76f, 6);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(.78f, .22f),
                        new Vector2(.42f, 1.08f), variant == 1 ? -118f : -76f, 6);
                    break;
                case UnitArchetype.SingleMage:
                    SetPart(skinAccessoryA, game.SparkSprite, bright, new Vector2(-.76f, .35f),
                        new Vector2(.5f, variant == 1 ? 1.28f : 1.62f), 128f, 5);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(.76f, .35f),
                        new Vector2(.5f, variant == 1 ? 1.28f : 1.62f), -128f, 5);
                    SetPart(skinAccessoryC, game.SquareSprite, darkAccent, new Vector2(.92f, .02f),
                        new Vector2(.13f, 1.75f), -8f, 7);
                    SetPart(skinAccessoryD, game.SparkSprite, bright, new Vector2(.84f, .92f),
                        Vector2.one * (variant == 1 ? .5f : .76f), 45f, 8);
                    break;
                case UnitArchetype.Bombardier:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(-.78f, .28f),
                        new Vector2(.38f, 1.24f), -14f, 4);
                    SetPart(skinAccessoryB, game.SquareSprite, darkAccent, new Vector2(.78f, .28f),
                        new Vector2(.38f, 1.24f), 14f, 4);
                    SetPart(skinAccessoryC, game.SparkSprite, bright, new Vector2(-.88f, .91f),
                        Vector2.one * .42f, 0f, 8);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(.88f, .91f),
                        Vector2.one * (variant == 1 ? .42f : .66f), 45f, 8);
                    break;
                case UnitArchetype.Lancer:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(.96f, .08f),
                        new Vector2(.13f, 2.32f), -19f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(.68f, 1.13f),
                        new Vector2(.52f, .92f), -19f, 8);
                    SetPart(skinAccessoryC, game.SparkSprite, accent, new Vector2(-.57f, 1.02f),
                        new Vector2(.38f, 1.08f), variant == 1 ? 42f : 76f, 7);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(.24f, 1.19f),
                        new Vector2(.32f, 1.12f), variant == 1 ? -24f : -62f, 7);
                    break;
                case UnitArchetype.Druid:
                    SetPart(skinAccessoryA, game.SparkSprite, darkAccent, new Vector2(-.52f, 1.17f),
                        new Vector2(.36f, 1.36f), 35f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, darkAccent, new Vector2(.52f, 1.17f),
                        new Vector2(.36f, 1.36f), -35f, 7);
                    SetPart(skinAccessoryC, game.SparkSprite, bright, new Vector2(-.82f, .04f),
                        new Vector2(.54f, 1.25f), 135f, 5);
                    SetPart(skinAccessoryD, game.SparkSprite, bright, new Vector2(.82f, .04f),
                        new Vector2(.54f, 1.25f), -135f, 5);
                    break;
                case UnitArchetype.Musketeer:
                    SetPart(skinAccessoryA, game.SquareSprite, darkAccent, new Vector2(0f, .88f),
                        new Vector2(1.48f, .18f), variant == 1 ? -6f : 6f, 7);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(.38f, 1.35f),
                        new Vector2(.36f, variant == 1 ? 1.12f : 1.48f), -18f, 8);
                    SetPart(skinAccessoryC, game.SquareSprite, secondary, new Vector2(-.98f, .15f),
                        new Vector2(.12f, 1.92f), 68f, 7);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(-1.33f, .38f),
                        new Vector2(.46f, .32f), 68f, 8);
                    break;
                case UnitArchetype.Oracle:
                    SetPart(skinAccessoryA, game.SparkSprite, bright, new Vector2(-.43f, 1.31f),
                        new Vector2(.38f, 1.08f), 58f, 8);
                    SetPart(skinAccessoryB, game.SparkSprite, bright, new Vector2(.43f, 1.31f),
                        new Vector2(.38f, 1.08f), -58f, 8);
                    SetPart(skinAccessoryC, game.SquareSprite, darkAccent, new Vector2(0f, -.22f),
                        new Vector2(1.22f, 1.78f), 0f, 4);
                    SetPart(skinAccessoryD, game.SparkSprite, accent, new Vector2(.92f, .5f),
                        Vector2.one * (variant == 1 ? .52f : .78f), 45f, 8);
                    break;
            }
        }

        private void SetProceduralSkinPartsEnabled(bool enabled)
        {
            if (skinBackGlow != null) skinBackGlow.enabled = enabled;
            if (skinBodyOutline != null) skinBodyOutline.enabled = enabled;
            if (skinCape != null) skinCape.enabled = enabled;
            if (skinShoulderLeft != null) skinShoulderLeft.enabled = enabled;
            if (skinShoulderRight != null) skinShoulderRight.enabled = enabled;
            if (skinCrest != null) skinCrest.enabled = enabled;
            if (skinHelmLeft != null) skinHelmLeft.enabled = enabled;
            if (skinHelmRight != null) skinHelmRight.enabled = enabled;
            if (skinTabard != null) skinTabard.enabled = enabled;
            if (skinWeaponSigil != null) skinWeaponSigil.enabled = enabled;
            if (skinAccessoryA != null) skinAccessoryA.enabled = enabled;
            if (skinAccessoryB != null) skinAccessoryB.enabled = enabled;
            if (skinAccessoryC != null) skinAccessoryC.enabled = enabled;
            if (skinAccessoryD != null) skinAccessoryD.enabled = enabled;
        }

        private void UpdateSkinSignatureMotion()
        {
            if (skinSignatureRoot == null || !skinSignatureRoot.activeSelf || body == null) return;
            var bodyPosition = body.transform.localPosition;
            skinSignatureRoot.transform.localPosition = new Vector3(bodyPosition.x, bodyPosition.y, 0f);
            skinSignatureRoot.transform.localEulerAngles = new Vector3(0f, 0f,
                body.transform.localEulerAngles.z);
            var pulse = IsHero ? 1f + Mathf.Sin(Time.time * 3.5f + animationPhase) * .035f : 1f;
            var scaleX = bodyBaseScale.x > .001f ? body.transform.localScale.x / bodyBaseScale.x : 1f;
            var scaleY = bodyBaseScale.y > .001f ? body.transform.localScale.y / bodyBaseScale.y : 1f;
            skinSignatureRoot.transform.localScale = new Vector3(scaleX * pulse, scaleY * pulse, 1f);
            if (skinAuthoredBody != null && usingStaticSkinBody)
            {
                skinAuthoredBody.flipX = body.flipX;
                return;
            }
            skinBodyOutline.sprite = body.sprite;
            skinBodyOutline.flipX = body.flipX;
            skinBodyOutline.transform.localPosition = new Vector3(0f, 0f, .02f);
            skinBodyOutline.transform.localScale = new Vector3(
                body.transform.localScale.x * 1.085f,
                body.transform.localScale.y * 1.085f, 1f);
        }

        private void UpdateHeroPresentation()
        {
            if (!IsHero) return;
            var auraColor = game.GetHeroAuraColor(Archetype);

            if (heroCrestOuter == null || !heroCrestOuter.enabled) return;
            var crestPulse = 1f + Mathf.Sin(Time.time * 4.1f + animationPhase) * .12f;
            heroCrestOuter.transform.localScale = Vector3.one * (definition.Radius * .7f * crestPulse);
            var crestColor = auraColor;
            crestColor.a = .86f;
            heroCrestOuter.color = crestColor;
            heroCrestCore.transform.localScale = Vector3.one * (definition.Radius * .29f * crestPulse);
            heroCrestCore.transform.Rotate(0f, 0f, Time.deltaTime * 38f);
        }

        private void UpdateFootMotion()
        {
            if (leftFoot == null || rightFoot == null) return;
            var forward = EightWayFacing.VectorFor(visualOctant);
            var side = new Vector2(-forward.y, forward.x);
            var gait = IsMoving ? Mathf.Sin(animationPhase) : 0f;
            var stride = definition.Radius * .34f;
            var spread = definition.Radius * .28f;
            var rear = Vector2.down * definition.Radius * .19f;
            var leftPosition = side * spread + forward * (gait * stride) + rear;
            var rightPosition = -side * spread - forward * (gait * stride) + rear;
            leftFoot.transform.localPosition = new Vector3(leftPosition.x, leftPosition.y, -.08f);
            rightFoot.transform.localPosition = new Vector3(rightPosition.x, rightPosition.y, -.08f);
            var angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f;
            leftFoot.transform.localEulerAngles = new Vector3(0f, 0f, angle + gait * 6f);
            rightFoot.transform.localEulerAngles = new Vector3(0f, 0f, angle - gait * 6f);
            var leftPlant = IsMoving ? .78f + Mathf.Max(0f, -gait) * .34f : 1f;
            var rightPlant = IsMoving ? .78f + Mathf.Max(0f, gait) * .34f : 1f;
            leftFoot.transform.localScale = new Vector3(leftFootBaseScale.x * leftPlant,
                leftFootBaseScale.y * (2f - leftPlant), 1f);
            rightFoot.transform.localScale = new Vector3(rightFootBaseScale.x * rightPlant,
                rightFootBaseScale.y * (2f - rightPlant), 1f);
        }

        private void UpdateActionAccent(bool attackActive, float attackT,
            bool skillActive = false, bool ultimateActive = false)
        {
            if (actionAccent == null) return;
            actionAccent.enabled = attackActive;
            if (!attackActive) return;
            var forward = EightWayFacing.VectorFor(visualOctant);
            var side = new Vector2(-forward.y, forward.x);
            var angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            var curve = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
            var actionPower = ultimateActive ? 1.72f : skillActive ? 1.34f : 1f;
            var contactFlash = Mathf.Clamp01(1f - Mathf.Abs(attackT - .48f) / .16f);
            var color = definition.Color;
            color = Color.Lerp(color, Color.white, contactFlash * .46f);
            var casterAccent = Archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                UnitArchetype.Druid or UnitArchetype.Oracle;
            color.a = casterAccent ? .22f + curve * .5f : .28f + curve * .62f;
            actionAccent.color = color;
            var handHeight = definition.Radius * (IsHero ? .82f : .68f);
            var handSide = Archetype is UnitArchetype.Archer or UnitArchetype.Musketeer
                ? -definition.Radius * .12f
                : definition.Radius * .07f;
            var handOrigin = Vector2.up * handHeight + side * handSide;

            switch (Archetype)
            {
                case UnitArchetype.Tank:
                    actionAccent.transform.localPosition = handOrigin + forward * (definition.Radius * .68f);
                    actionAccent.transform.localScale = Vector3.one *
                        (definition.Radius * Mathf.Lerp(.28f, .62f, contactFlash) * actionPower);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f,
                        angle + 45f + attackT * 95f);
                    break;
                case UnitArchetype.Melee:
                    actionAccent.transform.localPosition = handOrigin + forward * (definition.Radius * 1.05f);
                    actionAccent.transform.localScale = new Vector3(definition.Radius * .78f * actionPower,
                        definition.Radius * (.28f + contactFlash * .12f), 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f,
                        angle + Mathf.Lerp(-72f, 48f, Smooth01(attackT)));
                    break;
                case UnitArchetype.Lancer:
                    // The lance owns one straight attack axis. A narrow compression-to-contact
                    // flash reinforces the thrust without drawing the curved sword trail that
                    // previously contradicted the weapon direction.
                    var thrust = attackT < .22f
                        ? Mathf.SmoothStep(0f, .12f, attackT / .22f)
                        : attackT < .52f
                            ? Mathf.Lerp(.12f, 1f,
                                Mathf.SmoothStep(0f, 1f, (attackT - .22f) / .3f))
                            : 1f - Mathf.SmoothStep(0f, 1f, (attackT - .52f) / .48f);
                    actionAccent.transform.localPosition = handOrigin +
                        forward * (definition.Radius * (.62f + thrust * .78f));
                    actionAccent.transform.localScale = new Vector3(
                        definition.Radius * Mathf.Lerp(.28f, 1.18f, thrust) * actionPower,
                        definition.Radius * Mathf.Lerp(.075f, .16f, contactFlash), 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                    break;
                case UnitArchetype.Archer:
                    actionAccent.transform.localPosition = handOrigin +
                                                           forward * (definition.Radius * (.55f + curve * .45f));
                    actionAccent.transform.localScale = new Vector3(
                        definition.Radius * Mathf.Lerp(.78f, 2.45f, curve) * actionPower,
                        definition.Radius * (.11f + contactFlash * .08f), 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                    break;
                case UnitArchetype.Musketeer:
                    // A musket owns a compact muzzle flash, not the archer's long travelling
                    // streak. Keeping the accent on the quantized weapon axis prevents a flash
                    // from appearing on the actor's left when the pose turns or mirrors.
                    actionAccent.transform.localPosition = handOrigin +
                        forward * (definition.Radius * 1.22f);
                    actionAccent.transform.localScale = new Vector3(
                        definition.Radius * Mathf.Lerp(.18f, .72f, contactFlash) * actionPower,
                        definition.Radius * Mathf.Lerp(.11f, .25f, contactFlash), 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f, angle);
                    color.a = .08f + contactFlash * .9f;
                    actionAccent.color = Color.Lerp(color, Color.white, contactFlash * .52f);
                    break;
                case UnitArchetype.Bombardier:
                    actionAccent.transform.localPosition = handOrigin +
                                                           forward * (definition.Radius * (.72f + curve * .3f));
                    actionAccent.transform.localScale = new Vector3(
                        definition.Radius * Mathf.Lerp(.5f, 1.72f, curve) * actionPower,
                        definition.Radius * Mathf.Lerp(.32f, .76f, curve) * actionPower, 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f, angle + attackT * 90f);
                    break;
                case UnitArchetype.AreaMage:
                case UnitArchetype.SingleMage:
                case UnitArchetype.Druid:
                case UnitArchetype.Oracle:
                    actionAccent.transform.localPosition = handOrigin +
                                                           forward * (definition.Radius * (1f + curve * .35f));
                    var orbScale = definition.Radius * Mathf.Lerp(.34f, .9f, curve) * actionPower;
                    actionAccent.transform.localScale = new Vector3(orbScale * 1.25f, orbScale * .48f, 1f);
                    actionAccent.transform.localEulerAngles = new Vector3(0f, 0f, angle + attackT * 220f);
                    break;
            }
        }

        public Vector2 AttackOriginFor(Vector2 targetPosition)
        {
            var requested = targetPosition - Position;
            var octant = requested.sqrMagnitude > .0001f
                ? CombatAimOctant(requested)
                : visualOctant;
            var direction = EightWayFacing.VectorFor(octant);
            var side = new Vector2(-direction.y, direction.x);
            var forwardDistance = Archetype switch
            {
                UnitArchetype.Tank => Radius * .82f,
                UnitArchetype.Melee => Radius * 1.16f,
                UnitArchetype.Lancer => Radius * 1.42f,
                UnitArchetype.Archer => Radius * 1.38f,
                UnitArchetype.Musketeer => Radius * 1.18f,
                UnitArchetype.Bombardier => Radius * 1.08f,
                UnitArchetype.AreaMage => Radius * 1.08f,
                UnitArchetype.SingleMage => Radius * 1.04f,
                UnitArchetype.Druid => Radius * 1.08f,
                UnitArchetype.Oracle => Radius * 1.04f,
                _ => Radius * .72f
            };
            var handSide = Archetype switch
            {
                UnitArchetype.Archer => -Radius * .42f,
                UnitArchetype.Musketeer => Radius * .08f,
                UnitArchetype.Bombardier => Radius * .16f,
                UnitArchetype.Melee or UnitArchetype.Lancer => Radius * .18f,
                _ => Radius * .08f
            };
            var directionHeightCorrection = octant switch
            {
                FacingOctant.North => Radius * .1f,
                FacingOctant.NorthEast or FacingOctant.NorthWest => Radius * .06f,
                FacingOctant.South => -Radius * .07f,
                FacingOctant.SouthEast or FacingOctant.SouthWest => -Radius * .035f,
                _ => 0f
            };
            var weaponHeight = Archetype switch
            {
                UnitArchetype.Archer => Radius * (IsHero ? .42f : .38f),
                UnitArchetype.Musketeer => Radius * (IsHero ? .5f : .46f),
                UnitArchetype.Bombardier => Radius * .48f,
                _ => Radius * (IsHero ? .72f : .62f)
            };
            var handHeight = weaponHeight + directionHeightCorrection;
            return Position + Vector2.up * handHeight + direction * forwardDistance + side * handSide;
        }

        public float AttackOriginForwardProjectionForQa(Vector2 direction)
        {
            var normalized = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
            var quantized = EightWayFacing.VectorFor(EightWayFacing.FromVector(normalized));
            return Vector2.Dot(AttackOriginFor(Position + normalized * 3f) - Position, quantized);
        }

        public void TriggerAttackMotionForQa(Vector2 targetPosition)
        {
            var targetDirection = targetPosition - Position;
            FaceCombatTargetImmediately(targetDirection);
            IsMoving = false;
            attackMotion = 1f;
        }

        public FacingOctant PreviewCombatAimForQa(Vector2 direction, float normalizedPhase = .45f)
        {
            FaceCombatTargetImmediately(direction);
            IsMoving = false;
            attackMotion = Mathf.Max(.02f, 1f - Mathf.Clamp01(normalizedPhase));
            skillMotion = 0f;
            ultimateMotion = 0f;
            UpdateCharacterMotion();
            return visualOctant;
        }

        public float PreviewDirectionHeightForQa(Vector2 direction, float normalizedPhase = .153125f)
        {
            if (body == null) return 0f;
            facing = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.down;
            visualFacing = facing;
            visualOctant = EightWayFacing.FromVector(facing);
            IsMoving = true;
            animationPhase = Mathf.Clamp01(normalizedPhase) * 16f;
            UpdateCharacterMotion();
            IsMoving = false;
            return OpaqueWorldHeight(body.sprite) * Mathf.Abs(body.transform.lossyScale.y);
        }

        public bool PreviewFacingMirrorForQa(Vector2 direction, float normalizedPhase = .25f)
        {
            PreviewMotionPoseForQa(direction, 0, normalizedPhase);
            return body != null && body.flipX;
        }

        public Vector4 PreviewMotionPoseForQa(Vector2 direction, int state, float normalizedPhase)
        {
            if (body == null) return Vector4.zero;
            facing = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.down;
            visualFacing = facing;
            visualOctant = EightWayFacing.FromVector(facing);
            hurtMotion = 0f;
            contactMotion = 0f;
            levelUpMotion = 0f;
            attackMotion = 0f;
            skillMotion = 0f;
            ultimateMotion = 0f;
            normalizedPhase = Mathf.Clamp01(normalizedPhase);
            IsMoving = state == 0;
            animationPhase = normalizedPhase * 16f;
            if (state == 1) attackMotion = Mathf.Max(.02f, 1f - normalizedPhase);
            if (state == 2) skillMotion = Mathf.Max(.02f, 1f - normalizedPhase);
            if (state == 3) ultimateMotion = Mathf.Max(.02f, 1f - normalizedPhase);
            UpdateCharacterMotion();
            var position = body.transform.localPosition / Mathf.Max(.01f, definition.Radius);
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) / 45f;
            var scaleRatio = body.transform.localScale.x / Mathf.Max(.001f, bodyBaseScale.x);
            IsMoving = false;
            return new Vector4(position.x, position.y, angle, scaleRatio);
        }

        public void UseDirectionalAnimationForQa(DirectionalAnimationSet presentation)
        {
            if (presentation == null || body == null) return;
            directionalAnimation = presentation;
            animationFrames = directionalAnimation.FramesFor(visualOctant);
            if (animationFrames.Length > 0) SetMotionSprite(animationFrames[0]);
            CaptureVisualReferenceHeight();
        }

        public float PreviewGroundContactForQa(Vector2 direction, float normalizedPhase)
        {
            PreviewMotionPoseForQa(direction, 0, normalizedPhase);
            var foot = OpaqueFootAnchor(body.sprite);
            if (body.flipX) foot.x = -foot.x;
            var scale = body.transform.localScale;
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) * Mathf.Deg2Rad;
            var rotatedFootY = foot.x * Mathf.Abs(scale.x) * Mathf.Sin(angle) +
                               foot.y * Mathf.Abs(scale.y) * Mathf.Cos(angle);
            return body.transform.localPosition.y + rotatedFootY;
        }

        public float PreviewSilhouetteBottomForQa(Vector2 direction, float normalizedPhase)
        {
            PreviewMotionPoseForQa(direction, 0, normalizedPhase);
            var lowest = OpaqueLowestAnchor(body.sprite);
            if (body.flipX) lowest.x = -lowest.x;
            var scale = body.transform.localScale;
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) * Mathf.Deg2Rad;
            var rotatedY = lowest.x * Mathf.Abs(scale.x) * Mathf.Sin(angle) +
                           lowest.y * Mathf.Abs(scale.y) * Mathf.Cos(angle);
            return body.transform.localPosition.y + rotatedY;
        }

        private void CaptureVisualReferenceHeight()
        {
            if (body == null || body.sprite == null) return;
            EnsureMotionSpriteMetrics();
            var silhouetteHeight = OpaqueWorldHeight(body.sprite);
            if (silhouetteHeight <= .001f) return;
            visualReferenceSilhouetteHeight = silhouetteHeight * Mathf.Abs(bodyBaseScale.y);
            var bodyHeight = OpaqueBodyWorldHeight(body.sprite);
            visualReferenceBodyHeight = (bodyHeight > .001f ? bodyHeight : silhouetteHeight) *
                                        Mathf.Abs(bodyBaseScale.y);
            // Ground is an authored world-space plane shared by the contact shadow and rear/front
            // selection arcs.  Basing it on frame zero merely preserved the source image's empty
            // lower padding and was the reason default characters hovered.
            visualGroundLineY = shadow != null
                ? shadow.transform.localPosition.y
                : -definition.Radius * 1.30f;
        }

        private void AnchorCurrentSpriteToGround(float intentionalLift = 0f)
        {
            if (body == null || body.sprite == null) return;
            EnsureMotionSpriteMetrics();
            var foot = cachedMotionFootAnchor;
            if (body.flipX) foot.x = -foot.x;
            var scale = body.transform.localScale;
            var scaled = new Vector2(foot.x * Mathf.Abs(scale.x), foot.y * Mathf.Abs(scale.y));
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) * Mathf.Deg2Rad;
            var rotatedFootY = scaled.x * Mathf.Sin(angle) + scaled.y * Mathf.Cos(angle);
            var position = body.transform.localPosition;
            position.y = visualGroundLineY - rotatedFootY + intentionalLift;
            body.transform.localPosition = position;
        }

        private void NormalizeCurrentSpriteHeight()
        {
            if (body == null || body.sprite == null || visualReferenceBodyHeight <= .001f) return;
            // A weapon tip, spell mote, or one leaked edge pixel must not decide the actor's
            // apparent size. Use the dense central body silhouette for scale and reserve the full
            // silhouette only for clipping/grounding. This keeps every default-skin walk frame at
            // one readable body size without inflating a side-facing sword or staff.
            EnsureMotionSpriteMetrics();
            var currentHeight = cachedMotionBodyHeight * Mathf.Abs(body.transform.localScale.y);
            if (currentHeight <= .001f) return;
            var correction = Mathf.Clamp(visualReferenceBodyHeight / currentHeight, .72f, 1.55f);
            var scale = body.transform.localScale;
            body.transform.localScale = new Vector3(scale.x * correction, scale.y * correction, scale.z);
        }

        private bool ShouldFlipForDirection(FacingOctant octant)
        {
            var hasHorizontalDirection = octant is FacingOctant.SouthWest or FacingOctant.West or
                FacingOctant.NorthWest or FacingOctant.NorthEast or FacingOctant.East or
                FacingOctant.SouthEast;
            if (!hasHorizontalDirection) return false;
            var movingRight = EightWayFacing.IsRight(octant);
            // Base/hero/recruit sheets face right; canonical cosmetic sheets face left.
            return directionalAnimation != null && directionalAnimation.SideFacesRight
                ? !movingRight
                : movingRight;
        }

        private static float OpaqueWorldHeight(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return 0f;
            if (OpaqueHeightCache.TryGetValue(sprite, out var cached)) return cached;
            OpaqueMetricCacheMisses++;
            try
            {
                CacheSpriteMetrics(sprite, sprite.texture.GetPixels32(), sprite.texture.width);
                return OpaqueHeightCache.GetValueOrDefault(sprite, 0f);
            }
            catch (System.Exception)
            {
                cached = sprite.bounds.size.y;
                OpaqueHeightCache[sprite] = cached;
                return cached;
            }
        }

        private static float OpaqueBodyWorldHeight(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return 0f;
            if (OpaqueBodyHeightCache.TryGetValue(sprite, out var cached)) return cached;
            OpaqueMetricCacheMisses++;
            try
            {
                CacheSpriteMetrics(sprite, sprite.texture.GetPixels32(), sprite.texture.width);
                return OpaqueBodyHeightCache.GetValueOrDefault(sprite,
                    OpaqueHeightCache.GetValueOrDefault(sprite, 0f));
            }
            catch (System.Exception)
            {
                cached = OpaqueHeightCache.GetValueOrDefault(sprite, sprite.bounds.size.y);
                OpaqueBodyHeightCache[sprite] = cached;
                return cached;
            }
        }

        private static Vector2 OpaqueFootAnchor(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return Vector2.zero;
            if (OpaqueFootAnchorCache.TryGetValue(sprite, out var cached)) return cached;
            OpaqueMetricCacheMisses++;
            try
            {
                CacheSpriteMetrics(sprite, sprite.texture.GetPixels32(), sprite.texture.width);
                return OpaqueFootAnchorCache.GetValueOrDefault(sprite, new Vector2(0f, sprite.bounds.min.y));
            }
            catch (System.Exception)
            {
                return new Vector2(0f, sprite.bounds.min.y);
            }
        }

        // Runtime animation textures are deliberately made non-readable after upload.  Register
        // their true painted metrics while the source pixel array still exists, so neither live
        // animation nor QA ever falls back to transparent canvas bounds.
        public static void RegisterSpriteMetrics(Sprite sprite, Color32[] pixels, int textureWidth)
        {
            if (sprite == null || pixels == null || pixels.Length == 0 || textureWidth <= 0) return;
            var textureHeight = pixels.Length / textureWidth;
            var minX = textureWidth;
            var minY = textureHeight;
            var maxX = -1;
            var maxY = -1;
            var opaqueCount = 0;
            var opaqueSumX = 0f;
            var opaqueSumY = 0f;
            for (var y = 0; y < textureHeight; y++)
            for (var x = 0; x < textureWidth; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                opaqueCount++;
                opaqueSumX += x + .5f;
                opaqueSumY += y + .5f;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            OpaqueMarginCache[sprite] = maxX < minX || maxY < minY
                ? Vector4.zero
                : new Vector4(minX, minY, textureWidth - 1 - maxX, textureHeight - 1 - maxY);
            OpaqueSilhouetteCache[sprite] = opaqueCount <= 0
                ? Vector3.zero
                : new Vector3(opaqueCount / Mathf.Max(.0001f, sprite.pixelsPerUnit * sprite.pixelsPerUnit),
                    (opaqueSumX / opaqueCount - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (opaqueSumY / opaqueCount - sprite.pivot.y) / sprite.pixelsPerUnit);
            try { CacheSpriteMetrics(sprite, pixels, textureWidth); }
            catch (System.Exception) { }
        }

        public static bool TryGetRegisteredSilhouetteForQa(Sprite sprite, out Vector3 silhouette)
        {
            if (sprite != null && OpaqueSilhouetteCache.TryGetValue(sprite, out silhouette)) return true;
            silhouette = Vector3.zero;
            return false;
        }

        public static int PrimeOpaqueMetrics(IEnumerable<Sprite> sprites)
        {
            var unique = new HashSet<Sprite>();
            if (sprites != null)
                foreach (var sprite in sprites)
                    if (sprite != null) unique.Add(sprite);
            foreach (var sprite in unique)
            {
                OpaqueWorldHeight(sprite);
                OpaqueBodyWorldHeight(sprite);
                OpaqueFootAnchor(sprite);
                OpaqueLowestAnchor(sprite);
            }
            return unique.Count;
        }

        public static bool SpriteMetricsReadyForQa(Sprite sprite) => sprite != null &&
            OpaqueHeightCache.ContainsKey(sprite) && OpaqueBodyHeightCache.ContainsKey(sprite) &&
            OpaqueFootAnchorCache.ContainsKey(sprite) && OpaqueLowestAnchorCache.ContainsKey(sprite);

        private void SetMotionSprite(Sprite sprite)
        {
            if (body == null || sprite == null) return;
            if (body.sprite != sprite)
            {
                body.sprite = sprite;
                motionSpriteWrites++;
            }
            if (cachedMotionMetricSprite != sprite) CacheActiveMotionSpriteMetrics(sprite);
        }

        private void EnsureMotionSpriteMetrics()
        {
            if (body == null || body.sprite == null) return;
            if (cachedMotionMetricSprite != body.sprite) CacheActiveMotionSpriteMetrics(body.sprite);
        }

        private void CacheActiveMotionSpriteMetrics(Sprite sprite)
        {
            cachedMotionMetricSprite = sprite;
            cachedMotionBodyHeight = OpaqueBodyWorldHeight(sprite);
            cachedMotionFootAnchor = OpaqueFootAnchor(sprite);
        }

        private static void CacheSpriteMetrics(Sprite sprite, Color32[] pixels, int textureWidth)
        {
            if (sprite == null || pixels == null || sprite.pixelsPerUnit <= .01f) return;
            var textureHeight = pixels.Length / textureWidth;
            var rect = sprite.textureRect;
            var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, textureWidth - 1);
            var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, textureWidth);
            var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, textureHeight - 1);
            var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, textureHeight);
            var minY = top;
            var maxY = bottom - 1;
            var lowestSumX = 0f;
            var lowestCount = 0;
            for (var y = bottom; y < top; y++)
            for (var x = left; x < right; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                if (y < minY)
                {
                    minY = y;
                    lowestSumX = x + .5f;
                    lowestCount = 1;
                }
                else if (y == minY)
                {
                    lowestSumX += x + .5f;
                    lowestCount++;
                }
                maxY = Mathf.Max(maxY, y);
            }
            if (maxY < minY)
            {
                OpaqueHeightCache[sprite] = 0f;
                OpaqueBodyHeightCache[sprite] = 0f;
                OpaqueFootAnchorCache[sprite] = Vector2.zero;
                OpaqueLowestAnchorCache[sprite] = Vector2.zero;
                OpaqueMarginCache[sprite] = Vector4.zero;
                return;
            }
            var minX = right;
            var maxX = left - 1;
            for (var y = bottom; y < top; y++)
            for (var x = left; x < right; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
            }
            OpaqueMarginCache[sprite] = new Vector4(
                minX - left, minY - bottom, right - 1 - maxX, top - 1 - maxY);
            OpaqueHeightCache[sprite] = (maxY - minY + 1f) / sprite.pixelsPerUnit;
            var centralLeft = Mathf.Clamp(Mathf.FloorToInt(rect.xMin + rect.width * .16f), left, right - 1);
            var centralRight = Mathf.Clamp(Mathf.CeilToInt(rect.xMax - rect.width * .16f),
                centralLeft + 1, right);
            var denseThreshold = Mathf.Max(2, Mathf.RoundToInt((centralRight - centralLeft) * .018f));
            var denseMinY = top;
            var denseMaxY = bottom - 1;
            for (var y = bottom; y < top; y++)
            {
                var rowCount = 0;
                for (var x = centralLeft; x < centralRight; x++)
                    if (pixels[y * textureWidth + x].a > 12) rowCount++;
                if (rowCount < denseThreshold) continue;
                denseMinY = Mathf.Min(denseMinY, y);
                denseMaxY = Mathf.Max(denseMaxY, y);
            }
            OpaqueBodyHeightCache[sprite] = denseMaxY >= denseMinY
                ? (denseMaxY - denseMinY + 1f) / sprite.pixelsPerUnit
                : OpaqueHeightCache[sprite];
            var lowestX = lowestCount > 0 ? lowestSumX / lowestCount : rect.xMin + sprite.pivot.x;
            OpaqueLowestAnchorCache[sprite] = new Vector2(
                (lowestX - rect.xMin - sprite.pivot.x) / sprite.pixelsPerUnit,
                (minY + .5f - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit);

            var center = rect.xMin + sprite.pivot.x;
            var halfBodyWidth = Mathf.Max(4f, rect.width * .30f);
            centralLeft = Mathf.Max(left, Mathf.FloorToInt(center - halfBodyWidth));
            centralRight = Mathf.Min(right, Mathf.CeilToInt(center + halfBodyWidth));
            var columnBottoms = new List<Vector2Int>();
            for (var x = centralLeft; x < centralRight; x++)
            {
                for (var y = bottom; y < top; y++)
                {
                    if (pixels[y * textureWidth + x].a <= 12) continue;
                    columnBottoms.Add(new Vector2Int(x, y));
                    break;
                }
            }
            if (columnBottoms.Count == 0)
            {
                OpaqueFootAnchorCache[sprite] = OpaqueLowestAnchorCache[sprite];
                return;
            }
            columnBottoms.Sort((a, b) => a.y.CompareTo(b.y));
            var percentileIndex = Mathf.Clamp(Mathf.FloorToInt((columnBottoms.Count - 1) * .18f),
                0, columnBottoms.Count - 1);
            var footY = columnBottoms[percentileIndex].y;
            var band = Mathf.Max(2, Mathf.RoundToInt(rect.height * .018f));
            var footSumX = 0f;
            var footCount = 0;
            foreach (var column in columnBottoms)
            {
                if (column.y > footY + band) continue;
                footSumX += column.x + .5f;
                footCount++;
            }
            var footX = footCount > 0 ? footSumX / footCount : center;
            OpaqueFootAnchorCache[sprite] = new Vector2(
                (footX - rect.xMin - sprite.pivot.x) / sprite.pixelsPerUnit,
                (footY + .5f - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit);
        }

        public static Vector4 SpriteOpaqueMarginsForQa(Sprite sprite) =>
            sprite != null && OpaqueMarginCache.TryGetValue(sprite, out var margins)
                ? margins
                : Vector4.zero;

        private static Vector2 OpaqueLowestAnchor(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return Vector2.zero;
            if (OpaqueLowestAnchorCache.TryGetValue(sprite, out var cached)) return cached;
            OpaqueMetricCacheMisses++;
            try
            {
                CacheSpriteMetrics(sprite, sprite.texture.GetPixels32(), sprite.texture.width);
                return OpaqueLowestAnchorCache.GetValueOrDefault(sprite,
                    new Vector2(0f, sprite.bounds.min.y));
            }
            catch (System.Exception)
            {
                return new Vector2(0f, sprite.bounds.min.y);
            }
        }

        public void PlayLevelUpMotion()
        {
            levelUpMotion = 1f;
            HaltMovement(false);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void UpdateMovement()
        {
            var current = Position;
            var difference = moveTarget - current;
            if (difference.magnitude < .055f)
            {
                transform.position = game.ActorWorldPosition(moveTarget);
                movePathIndex++;
                if (movePathIndex >= movePath.Count)
                {
                    HaltMovement(false);
                    return;
                }
                moveTarget = movePath[movePathIndex];
                lastProgressPosition = Position;
                stuckTime = 0f;
                repathAttempts = 0;
                return;
            }

            var desired = difference.normalized;
            facing = desired;
            velocity = Vector2.Lerp(velocity, desired, Mathf.Clamp01(Time.deltaTime * 10f)).normalized;
            var roleMoveSpeed = game.GetRoleMoveSpeedMultiplier(this);
            var targetSpeed = definition.MoveSpeed * roleMoveSpeed * Mathf.Clamp01(difference.magnitude / .42f);
            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed,
                definition.MoveSpeed * roleMoveSpeed * 5.5f * Time.deltaTime);
            var step = Mathf.Min(difference.magnitude, currentMoveSpeed * Time.deltaTime);
            var next = current + velocity * step;
            if (game.CanTraverseUnit(current, next, Radius * .5f))
            {
                transform.position = game.ActorWorldPosition(next);
                TrackPathProgress();
                // One gait cycle contains two real foot plants. The old PI divisor emitted
                // five short-lived GameObjects per cycle, creating GC spikes whenever several
                // shield soldiers marched together on Android.
                var footstepPhase = Mathf.FloorToInt(animationPhase / 8f);
                if (footstepPhase != lastFootstepPhase && Time.time >= nextDustAt &&
                    currentMoveSpeed > definition.MoveSpeed * .38f)
                {
                    lastFootstepPhase = footstepPhase;
                    nextDustAt = Time.time + .1f;
                    movementDustSpawns++;
                    game.SpawnMovementDust(Position - velocity * Radius * .42f,
                        definition.Color, Radius, EightWayFacing.VectorFor(visualOctant));
                }
                return;
            }

            var slideX = new Vector2(current.x + velocity.x * step, current.y);
            var slideY = new Vector2(current.x, current.y + velocity.y * step);
            var canX = game.CanTraverseUnit(current, slideX, Radius * .5f);
            var canY = game.CanTraverseUnit(current, slideY, Radius * .5f);
            if (!canX && !canY)
            {
                stuckTime += Time.deltaTime;
                currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, 0f, definition.MoveSpeed * 8f * Time.deltaTime);
                if (stuckTime >= .18f) RebuildMovePath(true);
                return;
            }

            var chosen = canX && canY
                ? (Vector2.Distance(slideX, moveTarget) < Vector2.Distance(slideY, moveTarget) ? slideX : slideY)
                : (canX ? slideX : slideY);
            transform.position = game.ActorWorldPosition(chosen);
            velocity = chosen == slideX ? new Vector2(Mathf.Sign(velocity.x), 0f) : new Vector2(0f, Mathf.Sign(velocity.y));
            TrackPathProgress();
        }

        private void TrackPathProgress()
        {
            if (Vector2.Distance(Position, lastProgressPosition) >= .085f)
            {
                lastProgressPosition = Position;
                stuckTime = 0f;
                repathAttempts = 0;
                return;
            }
            stuckTime += Time.deltaTime;
            if (stuckTime >= .48f) RebuildMovePath(true);
        }

        public void ReactToContact(Vector2 origin, float force)
        {
            if (!IsAlive) return;
            contactDirection = (Position - origin).normalized;
            if (contactDirection.sqrMagnitude < .01f) contactDirection = Vector2.down;
            contactMotion = 1f;
            if (force <= .1f) return;
            knockbackStart = Position;
            knockbackTarget = game.NearestWalkableOnSameTerrain(
                Position + contactDirection * force, Position, Radius * .5f);
            knockbackTime = .24f;
            IsMoving = false;
            currentMoveSpeed = 0f;
        }

        private void UpdateKnockback()
        {
            knockbackTime = Mathf.Max(0f, knockbackTime - Time.deltaTime);
            var t = 1f - knockbackTime / .24f;
            var ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            var next = Vector2.Lerp(knockbackStart, knockbackTarget, ease);
            transform.position = game.ActorWorldPosition(next);
            if (knockbackTime <= 0f) moveTarget = Position;
        }

        private void UpdateBars()
        {
            if (healthFill == null || experienceFill == null) return;
            var healthRatio = Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
            healthFill.localScale = new Vector3(definition.Radius * 2.1f * healthRatio, .05f, 1f);
            healthFill.localPosition = new Vector3(-definition.Radius * 1.05f * (1f - healthRatio),
                -definition.Radius * 1.65f - .08f, -0.2f);

            var experienceRatio = ExperienceProgress();
            experienceFill.localScale = new Vector3(definition.Radius * 2.1f * experienceRatio, .032f, 1f);
            experienceFill.localPosition = new Vector3(-definition.Radius * 1.05f * (1f - experienceRatio),
                -definition.Radius * 1.65f - .17f, -0.2f);
            if (!IsHero) levelText.text = $"Lv.{Level}";
        }
    }
}
