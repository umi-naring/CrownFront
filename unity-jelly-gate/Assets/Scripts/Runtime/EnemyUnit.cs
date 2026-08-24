using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    public sealed class EnemyUnit : MonoBehaviour
    {
        private static readonly float[] RecoverySteeringAngles = { 0f, 18f, -18f, 34f, -34f, 52f, -52f };
        private static readonly float[] PursuitLateralOffsets = { 0f, -.18f, .18f };
        private static readonly float[] SpawnLateralOffsets = { -.18f, .18f, 0f, -.09f, .09f };
        private static readonly Dictionary<Sprite, Vector2> OpaqueFootAnchorCache = new();
        private static readonly Dictionary<Sprite, float> OpaqueHeightCache = new();
        private static readonly Dictionary<Sprite, float> OpaqueWidthCache = new();
        private static readonly Dictionary<Sprite, float> OpaqueAreaCache = new();
        private static readonly Dictionary<Sprite, Vector4> OpaqueMarginCache = new();
        public static int OpaqueMetricCacheMisses { get; private set; }

        public static int PrimeOpaqueMetrics(IEnumerable<Sprite> sprites)
        {
            var unique = new HashSet<Sprite>();
            if (sprites != null)
                foreach (var sprite in sprites)
                    if (sprite != null) unique.Add(sprite);
            foreach (var sprite in unique)
            {
                OpaqueWorldHeight(sprite);
                OpaqueFootAnchor(sprite);
            }
            return unique.Count;
        }

        public static void RegisterOpaqueMetrics(Sprite sprite, Color32[] pixels, int textureWidth)
        {
            if (sprite == null || pixels == null || textureWidth <= 0 || sprite.pixelsPerUnit <= .01f) return;
            var textureHeight = pixels.Length / textureWidth;
            var rect = sprite.rect;
            var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, textureWidth - 1);
            var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, textureWidth);
            var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, textureHeight - 1);
            var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, textureHeight);
            var minY = top;
            var maxY = bottom - 1;
            var minX = right;
            var maxX = left - 1;
            var opaquePixels = 0;
            for (var y = bottom; y < top; y++)
            for (var x = left; x < right; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                opaquePixels++;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            if (maxY < minY || maxX < minX)
            {
                OpaqueHeightCache[sprite] = 0f;
                OpaqueWidthCache[sprite] = 0f;
                OpaqueAreaCache[sprite] = 0f;
                OpaqueFootAnchorCache[sprite] = Vector2.zero;
                OpaqueMarginCache[sprite] = Vector4.zero;
                return;
            }

            OpaqueMarginCache[sprite] = new Vector4(
                minX - left, minY - bottom, right - 1 - maxX, top - 1 - maxY);
            OpaqueHeightCache[sprite] = (maxY - minY + 1f) / sprite.pixelsPerUnit;
            OpaqueWidthCache[sprite] = (maxX - minX + 1f) / sprite.pixelsPerUnit;
            OpaqueAreaCache[sprite] = opaquePixels /
                                      (sprite.pixelsPerUnit * sprite.pixelsPerUnit);
            var center = (minX + maxX) * .5f;
            var halfBodyWidth = Mathf.Max(2f, (maxX - minX + 1f) * .28f);
            var centralLeft = Mathf.Max(left, Mathf.FloorToInt(center - halfBodyWidth));
            var centralRight = Mathf.Min(right, Mathf.CeilToInt(center + halfBodyWidth));
            var footY = top;
            for (var y = bottom; y < top && footY == top; y++)
            for (var x = centralLeft; x < centralRight; x++)
                if (pixels[y * textureWidth + x].a > 12) { footY = y; break; }
            if (footY == top) footY = minY;
            var xSum = 0f;
            var count = 0;
            for (var y = footY; y <= Mathf.Min(top - 1, footY + 2); y++)
            for (var x = centralLeft; x < centralRight; x++)
            {
                if (pixels[y * textureWidth + x].a <= 12) continue;
                xSum += x + .5f;
                count++;
            }
            var anchorX = count > 0 ? xSum / count : center;
            OpaqueFootAnchorCache[sprite] = new Vector2(
                (anchorX - rect.xMin - sprite.pivot.x) / sprite.pixelsPerUnit,
                (footY + .5f - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit);
        }

        public static Vector4 SpriteOpaqueMarginsForQa(Sprite sprite) =>
            sprite != null && OpaqueMarginCache.TryGetValue(sprite, out var margins)
                ? margins
                : Vector4.zero;
        public static float SpriteOpaqueHeightForQa(Sprite sprite) =>
            sprite != null && OpaqueHeightCache.TryGetValue(sprite, out var height) ? height : 0f;
        public static float SpriteOpaqueWidthForQa(Sprite sprite) =>
            sprite != null && OpaqueWidthCache.TryGetValue(sprite, out var width) ? width : 0f;
        public static float SpriteOpaqueAreaForQa(Sprite sprite) =>
            sprite != null && OpaqueAreaCache.TryGetValue(sprite, out var area) ? area : 0f;
        public static bool SpriteBodySeamClosedForQa(Sprite sprite) =>
            sprite == null || !SpriteFrameIsolationRegistry.HasAudit(sprite) ||
            SpriteFrameIsolationRegistry.For(sprite).BodySeamClosed;
        public static bool SpriteHasInternalHorizontalBodySeamForQa(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return true;
            // Jelly King's crown, mantle and liquid body intentionally form separately floating
            // silhouette bands. Treating that negative space as an armour seam is a false positive;
            // its cell margins, foreign-component count, direction row and grounding remain fully
            // audited by the surrounding presentation tests.
            if (sprite.texture.name.Contains("boss-jelly-king", System.StringComparison.OrdinalIgnoreCase))
                return false;
            try
            {
                var texture = sprite.texture;
                var pixels = texture.GetPixels32();
                var rect = sprite.textureRect;
                var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, texture.width - 1);
                var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, texture.width);
                var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, texture.height - 1);
                var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, texture.height);
                var rowCounts = new int[top - bottom];
                var minY = rowCounts.Length;
                var maxY = -1;
                var total = 0;
                for (var y = bottom; y < top; y++)
                for (var x = left; x < right; x++)
                {
                    if (pixels[y * texture.width + x].a <= 12) continue;
                    var localY = y - bottom;
                    rowCounts[localY]++;
                    minY = Mathf.Min(minY, localY);
                    maxY = Mathf.Max(maxY, localY);
                    total++;
                }
                if (maxY <= minY || total <= 0) return true;

                var peakRow = 0;
                for (var y = 0; y < rowCounts.Length; y++)
                    peakRow = Mathf.Max(peakRow, rowCounts[y]);
                var below = 0;
                for (var y = minY; y <= maxY; y++)
                {
                    var current = rowCounts[y];
                    var above = total - below - current;
                    var middle = (y - minY) / Mathf.Max(1f, maxY - minY);
                    var lowerNeighbour = 0;
                    var upperNeighbour = 0;
                    for (var offset = 1; offset <= 4; offset++)
                    {
                        if (y - offset >= minY) lowerNeighbour = Mathf.Max(lowerNeighbour, rowCounts[y - offset]);
                        if (y + offset <= maxY) upperNeighbour = Mathf.Max(upperNeighbour, rowCounts[y + offset]);
                    }
                    // A transparent production row with substantial anatomy on both sides is
                    // never a valid whole-body pose. Detached crowns, flying familiars and spell
                    // ornaments are excluded by the central band, mass and neighbouring-width
                    // gates so legitimate negative space does not become a false seam failure.
                    if (middle is > .28f and < .68f && current == 0 &&
                        below >= total * .24f && above >= total * .24f &&
                        lowerNeighbour >= peakRow * .14f && upperNeighbour >= peakRow * .14f)
                        return true;
                    below += current;
                }
                return false;
            }
            catch (System.Exception)
            {
                return true;
            }
        }
        private JellyGateGame game;
        private SpriteRenderer body;
        private Sprite bossFrontSprite;
        private Sprite bossBackSprite;
        private Sprite normalBackSprite;
        private SpriteRenderer shadow;
        private SpriteRenderer bossAura;
        private SpriteRenderer barrierAura;
        private SpriteRenderer motionAccentA;
        private SpriteRenderer motionAccentB;
        private readonly List<SpriteRenderer> roleSilhouette = new();
        private Transform roleSilhouetteRoot;
        private TextMesh bossRankLabel;
        private SpriteRenderer[] bossCrownProngs = System.Array.Empty<SpriteRenderer>();
        private KayKit2p5DUnitVisual visualRig;
        private SpriteLimbRig2D limbRig;
        private DirectionalAnimationSet directionalAnimation;
        private Sprite[] animationFrames = System.Array.Empty<Sprite>();
        private Transform healthFill;
        private Vector3 bodyBaseScale;
        private readonly float[] bossDirectionalScaleCorrections =
            { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        private readonly float[] bossDirectionalReferenceOpaqueHeights =
            { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        private Vector3 shadowBaseScale;
        private float visualReferenceOpaqueHeight = 1f;
        private float currentSpriteHeightCorrection = 1f;
        private float visualGroundLineY;
        private Color bodyBaseColor;
        private Vector2 velocity = Vector2.up;
        private Vector2 facingDirection = Vector2.up;
        private Vector2 visualFacingDirection = Vector2.up;
        private Vector2 lastVisualWorldPosition;
        private FacingOctant visualOctant = FacingOctant.North;
        private Vector2 bossMorphScale = Vector2.one;
        private float health;
        private float maxHealth;
        private float speed;
        private float contactDamage;
        private float attackPower;
        private float magicPower;
        private float armor;
        private float magicResistance;
        private float physicalPenetration;
        private float magicPenetration;
        private float barrier;
        private float lastAttackAt;
        private float nextBossSkillAt;
        private float nextSkillAt;
        private float nextDustAt;
        private int lastFootstepPhase = int.MinValue;
        private float stunnedUntil;
        private float timeFrozenUntil;
        private float lastMovementAt;
        private float nextPathReanchorAt;
        private float nextTerrainAuditAt;
        private float nextVisualUpdateAt;
        private float lastVisualUpdateAt;
        private float nextRecoveryAt;
        private float nextTargetScanAt;
        private float nextBlockerScanAt;
        private float ignoredTargetUntil;
        private float nextDetectionAllowedAt;
        private float bossSilhouetteSeparationUntil;
        private float nextPursuitApproachScanAt;
        private float moveSpeedFactor = .65f;
        private float wriggle;
        private float hitMotion;
        private float attackMotion;
        private float skillMotion;
        private float skillMotionSpeed = 1f;
        private int visibleAnimationFrame;
        private int pathIndex;
        private int lane;
        private float lateralOffset;
        private int bossFormationLagSamples;
        private bool usesBossEntrance;
        private bool castingBossSkill;
        private bool usesStaticTimeline;
        private bool enraged;
        private bool attackingGate;
        private bool engagingDefender;
        private bool avoidsBossSilhouette;
        private PlayerUnit detectedTarget;
        private PlayerUnit cachedBlocker;
        private PlayerUnit temporarilyIgnoredTarget;
        private PlayerUnit pursuitApproachTarget;
        private Vector2 pursuitApproachTargetPosition;
        private Vector2 cachedPursuitApproachPoint;
        private int cachedPursuitApproachIndex = -1;
        private Vector2 lastMovementPosition;
        private int stallRecoveryCount;
        private int unreachableTargetRejectCount;
        private int corridorPursuitStepCount;
        private int bossSkillCastCount;
        private string lastBossSkillId = string.Empty;
        private string bossSkillLabel = string.Empty;
        private readonly Dictionary<PlayerUnit, float> damageContributors = new();
        private EnemyVariantProfile variantProfile;
        private float bossPassiveReadyAt;
        private float lastBossPassiveHitAt = -10f;
        private int bossPassiveHitCount;
        private int bossMomentumStacks;
        private float armorBreakUntil;
        private float armorBreakAmount;
        private string lastSpecialQaState = string.Empty;
        private bool presentationInitialized;
        private string presentationPoolKey = string.Empty;

        public bool IsAlive { get; private set; } = true;
        public bool IsBoss { get; private set; }
        public EnemyClass Class { get; private set; }
        public EnemyClass VisualClass { get; private set; }
        public string VariantId => variantProfile?.Id ?? string.Empty;
        public string PoolKey => presentationPoolKey;

        public static string PoolKeyFor(EnemyVariantProfile profile, bool boss, EnemyClass enemyClass) =>
            $"{(boss ? 'B' : 'R')}:{profile?.Id ?? enemyClass.ToString()}";
        public int AuthoredVariantDesignCode => VariantId switch
        {
            "stone_shard" => 31,
            "stone_guard" => 32,
            "rune_golem" => 33,
            "cannon_golem" => 34,
            "mountain_titan" => 35,
            _ => 0
        };
        public string VariantName => variantProfile?.Name ?? EnemyDisplayName();
        public bool IsRanged => Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege or EnemyClass.Wisp or
            EnemyClass.Silencer or EnemyClass.Cursebinder;
        public bool IsFlying => VisualClass == EnemyClass.Flyer || Class == EnemyClass.Flyer;
        public bool RequiresMagicDamage => Class == EnemyClass.Wisp;
        public bool IgnoresArmor => Class == EnemyClass.Piercer;
        public float Radius { get; private set; }
        public float GateDamage { get; private set; }
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float HealthRatio => Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
        public float Armor => Mathf.Max(0f, armor - (Time.time < armorBreakUntil ? armorBreakAmount : 0f));
        public float MagicResistance => magicResistance;
        public float PhysicalPenetration => physicalPenetration;
        public float MagicPenetration => magicPenetration;
        public bool AppliesMagicSeal => Class == EnemyClass.Silencer;
        public bool AppliesArmorShred => Class == EnemyClass.Sunderer;
        public bool AppliesResistanceCurse => Class == EnemyClass.Cursebinder;
        public string DebuffSkillLabel => Class switch
        {
            EnemyClass.Silencer => GameLocalization.Text("침묵의 종가름", "SILENCING TOLL"),
            EnemyClass.Cursebinder => GameLocalization.Text("봉인 저주", "VEIL CURSE"),
            EnemyClass.Sunderer => GameLocalization.Text("갑주 절단", "ARMOR REND"),
            _ => string.Empty
        };
        public string LastSpecialQaState => lastSpecialQaState;
        public float AttackPower => attackPower;
        public float MagicPower => magicPower;
        public float Barrier => barrier;
        public float AttackRange => IsFlying ? 1.62f : Class switch
        {
            EnemyClass.Siege => 3.1f,
            EnemyClass.Shaman => 2.75f,
            EnemyClass.Mage => 2.45f,
            EnemyClass.Wisp => 2.65f,
            EnemyClass.Silencer => 2.42f,
            EnemyClass.Cursebinder => 2.48f,
            EnemyClass.Sunderer => Mathf.Max(.66f, Radius + .36f),
            _ => Mathf.Max(.58f, Radius + .32f)
        };
        public float DetectionRange => AttackRange + (IsRanged ? 1.55f : IsFlying ? 1.2f : 1.05f);
        public bool CanTargetHighGround => IsRanged || IsFlying;
        public string BossSkillLabel => bossSkillLabel;
        public int BossSkillCastCount => bossSkillCastCount;
        public string LastBossSkillId => lastBossSkillId;
        public string BossSkillProfileId => BossSkillIdForClass(VisualClass);
        public string BossPassiveProfileId => IsBoss ? BossIdentityCatalog.For(VisualClass).PassiveId : string.Empty;
        public string BossPassiveLabel => IsBoss ? BossIdentityCatalog.For(VisualClass).PassiveName : string.Empty;
        public string BossPassiveDescription => IsBoss ? BossIdentityCatalog.For(VisualClass).PassiveDescription : string.Empty;
        public bool IsAtGate => attackingGate;
        public bool IsEngagingDefender => engagingDefender;
        public float IdleDuration => Time.time - Mathf.Max(lastMovementAt, lastAttackAt);
        public int StallRecoveryCount => stallRecoveryCount;
        public int UnreachableTargetRejectCountForQa => unreachableTargetRejectCount;
        public int CorridorPursuitStepCountForQa => corridorPursuitStepCount;
        public PlayerUnit DetectedTargetForQa => detectedTarget;
        public int PathIndex => pathIndex;
        public int LaneIndex => lane;
        public float LaneOffset => lateralOffset;
        public int BossFormationLagSamplesForQa => bossFormationLagSamples;
        public Vector2 Position => transform.position;
        public Vector2 VelocityForQa => velocity;
        public Vector2 VisualFacingDirectionForQa => visualFacingDirection;
        public Vector2 HitPoint => Position + Vector2.up * Radius * (IsBoss ? 1.38f : .7f);
        public FacingOctant VisualOctant => visualOctant;
        public Vector2 AttackOrigin => AttackOriginFor(Position + EightWayFacing.VectorFor(visualOctant));
        public int AnimationFrameCount => animationFrames.Length;
        public int VisibleAnimationFrame => visibleAnimationFrame;
        public int CurrentSpriteIdForQa => body != null && body.sprite != null
            ? body.sprite.GetInstanceID()
            : 0;
        public Sprite CurrentSpriteForQa => body != null ? body.sprite : null;
        public string CurrentFrameTextureNameForQa =>
            body != null && body.sprite != null && body.sprite.texture != null
                ? body.sprite.texture.name
                : string.Empty;
        public float ShadowLocalYForQa => shadow != null ? shadow.transform.localPosition.y : float.NaN;
        public float CurrentGroundContactLocalYForQa
        {
            get
            {
                if (body == null || body.sprite == null) return float.NaN;
                var foot = OpaqueFootAnchor(body.sprite);
                if (body.flipX) foot.x = -foot.x;
                var scale = body.transform.localScale;
                var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) * Mathf.Deg2Rad;
                var rotatedFootY = foot.x * Mathf.Abs(scale.x) * Mathf.Sin(angle) +
                                   foot.y * Mathf.Abs(scale.y) * Mathf.Cos(angle);
                return body.transform.localPosition.y + rotatedFootY;
            }
        }
        public bool CurrentSpriteHasInternalHorizontalBodySeamForQa =>
            !SpriteBodySeamClosedForQa(CurrentSpriteForQa);
        public int ActivePrimaryBodyChannelsForQa =>
            (body != null && body.enabled && body.gameObject.activeInHierarchy ? 1 : 0) +
            (visualRig != null && visualRig.gameObject.activeInHierarchy ? 1 : 0);
        public int ActiveBossArtworkChannelsForQa
        {
            get
            {
                if (!IsBoss) return 0;
                var count = 0;
                foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (renderer == body || renderer.sprite != null && renderer.sprite.texture != null &&
                        renderer.sprite.texture.name.StartsWith("boss-", System.StringComparison.OrdinalIgnoreCase))
                        count++;
                }
                return count;
            }
        }
        public int DistinctAnimationSpritesForQa(int state)
        {
            if (animationFrames == null || animationFrames.Length == 0) return 0;
            var first = Mathf.Clamp(state, 0, 2) * 24;
            var last = Mathf.Min(animationFrames.Length, first + 24);
            var sprites = new HashSet<int>();
            for (var index = first; index < last; index++)
                if (animationFrames[index] != null)
                    sprites.Add(animationFrames[index].GetInstanceID());
            return sprites.Count;
        }
        public bool HasAuthoredBossDirectionalAnimationForQa => IsBoss && directionalAnimation != null;
        public bool HasAuthoredVariantDirectionalAnimationForQa => !IsBoss && directionalAnimation != null;
        public bool HasCompleteDirectionalAnimationForQa => directionalAnimation != null &&
            directionalAnimation.SupportsEightDirections &&
            directionalAnimation.Down.Length >= 72 && directionalAnimation.DownDiagonal.Length >= 72 &&
            directionalAnimation.Side.Length >= 72 && directionalAnimation.UpDiagonal.Length >= 72 &&
            directionalAnimation.Up.Length >= 72;
        public bool CurrentSpriteUsesStrictAtlasCellForQa
        {
            get
            {
                if (body == null || body.sprite == null || body.sprite.texture == null) return false;
                var texture = body.sprite.texture;
                if (!texture.name.Contains("directions-v")) return false;
                if (texture.name.Contains("-isolated-r")) return true;
                // Most authored states use six poses; a few preserve five genuinely distinct
                // poses instead of fabricating a sixth. Both layouts are strict five-direction
                // grids and neither may sample outside its own cell.
                var expectedWidth = Mathf.Ceil(texture.width / 5f) + 1f;
                var expectedHeight = Mathf.Ceil(texture.height / 5f) + 1f;
                return body.sprite.rect.width <= expectedWidth && body.sprite.rect.height <= expectedHeight;
            }
        }
        public bool CurrentSpriteHasSafeCellMarginForQa
        {
            get
            {
                if (body == null || body.sprite == null || body.sprite.texture == null) return false;
                var sprite = body.sprite;
                var texture = sprite.texture;
                if (OpaqueMarginCache.TryGetValue(sprite, out var cachedMargins))
                {
                    var required = texture.name.Contains("-isolated-r") ? 10f : 2f;
                    return cachedMargins.x >= required && cachedMargins.y >= required &&
                           cachedMargins.z >= required && cachedMargins.w >= required;
                }
                if (texture.name.Contains("-isolated-r"))
                {
                    var margins = SpriteOpaqueMarginsForQa(sprite);
                    return margins.x >= 10f && margins.y >= 10f &&
                           margins.z >= 10f && margins.w >= 10f;
                }
                Color32[] pixels;
                try { pixels = texture.GetPixels32(); }
                catch (System.Exception)
                {
                    // Full static roster portraits can be uploaded non-readable after their card
                    // canvas has been generated. They are not packed animation cells and cannot
                    // contain a neighbouring frame, so lack of CPU pixels is not an atlas failure.
                    return !texture.name.Contains("-isolated", System.StringComparison.OrdinalIgnoreCase);
                }
                var left = Mathf.RoundToInt(sprite.rect.xMin);
                var right = Mathf.RoundToInt(sprite.rect.xMax) - 1;
                var bottom = Mathf.RoundToInt(sprite.rect.yMin);
                var top = Mathf.RoundToInt(sprite.rect.yMax) - 1;
                for (var inset = 0; inset < 2; inset++)
                {
                    var sampleLeft = Mathf.Clamp(left + inset, 0, texture.width - 1);
                    var sampleRight = Mathf.Clamp(right - inset, 0, texture.width - 1);
                    var sampleBottom = Mathf.Clamp(bottom + inset, 0, texture.height - 1);
                    var sampleTop = Mathf.Clamp(top - inset, 0, texture.height - 1);
                    for (var x = sampleLeft; x <= sampleRight; x++)
                        if (pixels[sampleBottom * texture.width + x].a > 8 ||
                            pixels[sampleTop * texture.width + x].a > 8) return false;
                    for (var y = sampleBottom; y <= sampleTop; y++)
                        if (pixels[y * texture.width + sampleLeft].a > 8 ||
                            pixels[y * texture.width + sampleRight].a > 8) return false;
                }
                return true;
            }
        }
        public bool UsesBossEntrance => usesBossEntrance;
        public bool HasWorldHealthBar => healthFill != null;
        public bool HasDirectionalBackPresentation =>
            IsBoss ? bossBackSprite != null : normalBackSprite != null;
        public bool UsesAuthoredVariantArt => body != null && body.sprite != null &&
                                              body.sprite.texture != null &&
                                              (body.sprite.texture.name.Contains("enemy-golem-variants-v2") ||
                                               body.sprite.texture.name.Contains("enemy-abyss-roster-v1"));
        public bool UsesAuthoredVariantBackArt => normalBackSprite != null && normalBackSprite.texture != null &&
                                                  (normalBackSprite.texture.name.Contains("enemy-golem-variants-back-v2") ||
                                                   normalBackSprite.texture.name.Contains("enemy-abyss-roster-back-v1"));
        public bool HasBossPresentation => !IsBoss || (barrierAura != null && bossRankLabel != null);
        public bool HasArticulatedLimbRigForQa => limbRig != null;
        public bool UsesSeamSafeWholeBodyAnimationForQa => limbRig == null && body != null;
        public float ArticulatedPoseSignatureForQa => limbRig?.PoseSignature ?? 0f;
        public float VisualScaleRatio => bodyBaseScale.x / Mathf.Max(.01f, Radius);
        public float VisualWorldHeight => body == null || body.sprite == null
            ? 0f
            : OpaqueWorldHeight(body.sprite) * Mathf.Abs(body.transform.localScale.y);
        public float ExpectedBossVisualHeightPerRadiusForQa => IsBoss
            ? BossVisualHeightPerRadius(variantProfile?.Id)
            : 0f;
        public float CurrentVisualScaleHeightRatioForQa => body == null
            ? 0f
            : Mathf.Abs(body.transform.localScale.y) / Mathf.Max(.0001f, Mathf.Abs(bodyBaseScale.y));
        public float GroundVisualOffset => body != null ? body.transform.localPosition.y : float.MaxValue;
        public float CurrentSpriteHeightCorrectionForQa => currentSpriteHeightCorrection;
        public float CurrentSpriteRenderAspectForQa => body == null || body.sprite == null ||
                                                        body.sprite.bounds.size.y <= .0001f
            ? 0f
            : body.sprite.bounds.size.x / body.sprite.bounds.size.y;
        public float CurrentSpriteOpaqueAreaForQa => body == null || body.sprite == null
            ? 0f
            : SpriteOpaqueAreaForQa(body.sprite);
        public int CurrentSpriteForeignComponentsForQa => body == null
            ? -1
            : SpriteFrameIsolationRegistry.For(body.sprite).RemainingForeignComponents;
        public int CurrentSpriteSignificantComponentsForQa => body == null
            ? 0
            : SpriteFrameIsolationRegistry.For(body.sprite).SignificantComponents;
        public bool CurrentSpriteHasIsolationAuditForQa => body != null &&
            SpriteFrameIsolationRegistry.HasAudit(body.sprite);
        public bool CurrentBodyFlipXForQa => body != null && body.flipX;
        public bool ExpectedBodyFlipForQa(Vector2 direction) =>
            ShouldFlipDirectionalBoss(EightWayFacing.FromVector(direction));
        public FacingOctant PreviewTargetLockForQa(Vector2 targetDirection)
        {
            FaceVisualTarget(Position + (targetDirection.sqrMagnitude > .0001f
                ? targetDirection.normalized
                : Vector2.down));
            velocity = Vector2.zero;
            UpdateVisualMotion();
            return visualOctant;
        }
        public void RefreshVisualMotionForQa() => UpdateVisualMotion();
        public float BossAuraAlpha => bossAura != null ? bossAura.color.a : 0f;
        public Color PresentationTint => bodyBaseColor;
        public bool IsTimeFrozen => Time.time < timeFrozenUntil;
        public float TargetableRadius => IsBoss ? Radius * 1.75f : Radius * .72f;
        public bool HasForbiddenCircularOverlay
        {
            get
            {
                foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null || renderer == body || renderer == shadow || renderer.sprite == null ||
                        renderer.sprite.texture == null) continue;
                    if (renderer.sprite.texture.name == "Runtime Circle") return true;
                }
                return false;
            }
        }

        internal static float EnemyPhysicalPenetration(EnemyClass enemyClass, int round, bool boss)
        {
            var stage = Mathf.Clamp(round, 1, 50);
            if (boss) return 7f + stage * .24f;
            return enemyClass switch
            {
                EnemyClass.Piercer => 10f + stage * .24f,
                EnemyClass.Sunderer => 7f + stage * .18f,
                EnemyClass.Brute => 4f + stage * .12f,
                EnemyClass.Siege => 3f + stage * .10f,
                EnemyClass.Runner or EnemyClass.Skeleton => 2f + stage * .07f,
                EnemyClass.Flyer => 2f + stage * .08f,
                _ => 1f + stage * .05f
            };
        }

        internal static float EnemyMagicPenetration(EnemyClass enemyClass, int round, bool boss)
        {
            var stage = Mathf.Clamp(round, 1, 50);
            if (boss) return 7f + stage * .23f;
            return enemyClass switch
            {
                EnemyClass.Cursebinder => 9f + stage * .22f,
                EnemyClass.Silencer => 8f + stage * .20f,
                EnemyClass.Shaman => 7f + stage * .18f,
                EnemyClass.Mage => 6f + stage * .16f,
                EnemyClass.Wisp => 5f + stage * .18f,
                EnemyClass.Siege => 4f + stage * .12f,
                EnemyClass.Flyer => 3f + stage * .10f,
                _ => 1f + stage * .04f
            };
        }
        public bool HasPersistentRoleBlock
        {
            get
            {
                foreach (var renderer in roleSilhouette)
                    if (renderer != null && renderer.enabled && renderer.sprite == game?.SquareSprite)
                        return true;
                return false;
            }
        }

        public void Initialize(JellyGateGame owner, int spawnIndex, float startingHealth, bool boss,
            int pathLane, bool mage = false)
        {
            Initialize(owner, spawnIndex, startingHealth, boss, pathLane,
                boss ? EnemyClass.Boss : mage ? EnemyClass.Mage : EnemyClass.Melee);
        }

        public void Initialize(JellyGateGame owner, int spawnIndex, float startingHealth, bool boss,
            int pathLane, EnemyClass enemyClass)
        {
            Initialize(owner, spawnIndex, startingHealth, boss, pathLane, enemyClass, null);
        }

        public void Initialize(JellyGateGame owner, int spawnIndex, float startingHealth, bool boss,
            int pathLane, EnemyClass enemyClass, EnemyVariantProfile profile, bool playSpawnVoice = true)
        {
            StopAllCoroutines();
            var requestedPoolKey = PoolKeyFor(profile, boss, enemyClass);
            var reusePresentation = presentationInitialized && presentationPoolKey == requestedPoolKey;
            ResetRuntimeStateForReuse();
            game = owner;
            IsBoss = boss;
            variantProfile = profile;
            bossSkillLabel = GameLocalization.Text("접근 중", "APPROACHING");
            // A boss keeps the combat modifiers below but retains its chapter's visual family:
            // jelly, skeleton, goblin, or golem.  Boss rounds therefore never inject a random
            // purple jelly into an otherwise different monster set.
            Class = profile?.CombatClass ?? (enemyClass == EnemyClass.Boss ? EnemyClass.Melee : enemyClass);
            VisualClass = profile?.FamilyClass ?? Class;
            lane = Mathf.Clamp(pathLane, 0, Mathf.Max(0, game.LaneCount - 1));
            var healthMultiplier = IsBoss ? 1f : Class switch
            {
                EnemyClass.Skeleton => .72f,
                EnemyClass.Runner => .58f,
                EnemyClass.Brute => 1.34f,
                EnemyClass.Shaman => .92f,
                EnemyClass.Siege => 1.18f,
                EnemyClass.Piercer => .86f,
                EnemyClass.Wisp => .72f,
                EnemyClass.Flyer => .68f,
                EnemyClass.Silencer => .76f,
                EnemyClass.Cursebinder => .91f,
                EnemyClass.Sunderer => 1.08f,
                _ => 1f
            };
            maxHealth = startingHealth * healthMultiplier;
            health = maxHealth;
            Radius = IsBoss ? Class switch
            {
                EnemyClass.Brute or EnemyClass.Siege => .76f,
                EnemyClass.Wisp or EnemyClass.Flyer => .62f,
                EnemyClass.Runner or EnemyClass.Piercer => .66f,
                _ => .70f
            } : Class switch
            {
                EnemyClass.Boss => .52f,
                EnemyClass.Brute => .37f,
                EnemyClass.Siege => .34f,
                EnemyClass.Runner => .19f,
                EnemyClass.Flyer => .19f,
                EnemyClass.Wisp => .21f,
                EnemyClass.Piercer => .23f,
                EnemyClass.Silencer => .23f,
                EnemyClass.Cursebinder => .26f,
                EnemyClass.Sunderer => .31f,
                _ => .25f
            };
            speed = IsBoss ? .48f : Class switch
            {
                EnemyClass.Boss => .48f,
                EnemyClass.Runner => 1.08f,
                EnemyClass.Skeleton => .82f,
                EnemyClass.Brute => .52f,
                EnemyClass.Shaman => .61f,
                EnemyClass.Siege => .48f,
                EnemyClass.Mage => .67f,
                EnemyClass.Piercer => .78f,
                EnemyClass.Wisp => .76f,
                EnemyClass.Flyer => .94f,
                EnemyClass.Silencer => .72f,
                EnemyClass.Cursebinder => .60f,
                EnemyClass.Sunderer => .58f,
                _ => .76f
            };
            var baseAttack = 8f + game.Round * .55f;
            var baseMagic = 17f + game.Round * 1.15f;
            attackPower = IsBoss ? 30f + game.Round * 1.35f : Class switch
            {
                EnemyClass.Boss => 30f + game.Round * 1.35f,
                EnemyClass.Brute => baseAttack * 1.42f,
                EnemyClass.Siege => baseAttack * 1.12f,
                EnemyClass.Runner => baseAttack * .68f,
                EnemyClass.Skeleton => baseAttack * .86f,
                EnemyClass.Mage or EnemyClass.Shaman => baseAttack * .72f,
                EnemyClass.Piercer => baseAttack * 1.08f,
                EnemyClass.Flyer => baseAttack * .78f,
                EnemyClass.Silencer => baseAttack * .54f,
                EnemyClass.Cursebinder => baseAttack * .58f,
                EnemyClass.Sunderer => baseAttack * 1.04f,
                _ => baseAttack
            };
            magicPower = IsBoss ? 25f + game.Round * 1.1f : Class switch
            {
                EnemyClass.Boss => 25f + game.Round * 1.1f,
                EnemyClass.Shaman => baseMagic * 1.35f,
                EnemyClass.Siege => baseMagic * .58f,
                EnemyClass.Mage => baseMagic,
                EnemyClass.Wisp => baseMagic * .96f,
                EnemyClass.Silencer => baseMagic * .96f,
                EnemyClass.Cursebinder => baseMagic * 1.02f,
                EnemyClass.Sunderer => baseMagic * .26f,
                _ => 3f + game.Round * .18f
            };
            contactDamage = attackPower;
            armor = IsBoss ? 50f + game.Round * 1.50f : Class switch
            {
                EnemyClass.Boss => 28f + game.Round * 1.15f,
                EnemyClass.Brute => 13f + game.Round * 1.18f,
                EnemyClass.Skeleton => 11f + game.Round * 1.15f,
                EnemyClass.Siege => 16f + game.Round * 1.18f,
                EnemyClass.Wisp => 62f + game.Round * .22f,
                EnemyClass.Piercer => 5f + game.Round,
                EnemyClass.Silencer => 7f + game.Round * .84f,
                EnemyClass.Cursebinder => 9f + game.Round * 1.08f,
                EnemyClass.Sunderer => 18f + game.Round * 1.24f,
                _ => 6f + game.Round * 1.3f
            };
            magicResistance = IsBoss ? 46f + game.Round * 1.35f : Class switch
            {
                EnemyClass.Boss => 26f + game.Round,
                EnemyClass.Shaman => 31f + game.Round * 1.7f,
                EnemyClass.Siege => 16f + game.Round * 1.3f,
                EnemyClass.Mage => 24f + game.Round * 1.4f,
                EnemyClass.Wisp => 5f + game.Round * .6f,
                EnemyClass.Silencer => 34f + game.Round * 1.55f,
                EnemyClass.Cursebinder => 28f + game.Round * 1.45f,
                EnemyClass.Sunderer => 12f + game.Round * 1.04f,
                _ => 4f + game.Round * 1.1f
            };
            physicalPenetration = EnemyPhysicalPenetration(Class, game.Round, IsBoss);
            magicPenetration = EnemyMagicPenetration(Class, game.Round, IsBoss);
            // The boss barrier must be derived from the final, variant-scaled health.
            // Initialising it here made large boss variants receive a much smaller
            // effective barrier than the UI and balance sheet promised.
            barrier = 0f;
            nextBossSkillAt = Time.time + 4.8f;
            nextSkillAt = Time.time + Random.Range(3.8f, 5.8f);
            GateDamage = IsBoss ? 40f + game.Round * 3f : Class switch
            {
                EnemyClass.Boss => 40f + game.Round * 3f,
                EnemyClass.Siege => 20f + game.Round * 1.55f,
                EnemyClass.Brute => 16f + game.Round * 1.35f,
                EnemyClass.Runner => 8f + game.Round * .9f,
                EnemyClass.Mage or EnemyClass.Shaman => 11f + game.Round * 1.25f,
                EnemyClass.Flyer => 10f + game.Round * 1.1f,
                EnemyClass.Piercer => 13f + game.Round * 1.4f,
                EnemyClass.Silencer => 9f + game.Round * 1.05f,
                EnemyClass.Cursebinder => 10f + game.Round * 1.12f,
                EnemyClass.Sunderer => 13f + game.Round * 1.28f,
                _ => 14f + game.Round * 1.5f
            };
            if (variantProfile != null)
            {
                maxHealth *= variantProfile.HealthMultiplier;
                health = maxHealth;
                attackPower *= variantProfile.AttackMultiplier;
                contactDamage = attackPower;
                magicPower *= variantProfile.MagicMultiplier;
                speed *= variantProfile.SpeedMultiplier;
                Radius *= variantProfile.ScaleMultiplier;
                GateDamage *= Mathf.Lerp(variantProfile.AttackMultiplier,
                    Mathf.Max(variantProfile.AttackMultiplier, variantProfile.MagicMultiplier), .45f);
            }
            if (IsBoss)
                barrier = maxHealth * .23f;
            var roundDamagePressure = game.EnemyRoundDamageMultiplier;
            attackPower *= roundDamagePressure;
            magicPower *= roundDamagePressure;
            contactDamage = attackPower;
            GateDamage *= roundDamagePressure;
            lateralOffset = SpawnLateralOffsets[spawnIndex % SpawnLateralOffsets.Length];
            wriggle = Random.value * Mathf.PI * 2f;
            var start = CurrentPathTarget(0, lateralOffset, wriggle);
            transform.position = game.ActorWorldPosition(start, true);
            if (CurrentPathCount > 1)
                facingDirection = (CurrentPathTarget(1, lateralOffset, wriggle) - start).normalized;
            visualFacingDirection = facingDirection;
            lastVisualWorldPosition = start;
            lastMovementPosition = start;
            lastMovementAt = Time.time;
            nextRecoveryAt = Time.time + .75f;
            name = variantProfile?.Name ?? EnemyDisplayName();

            if (reusePresentation)
            {
                RestorePooledPresentation();
                if (playSpawnVoice) game.PlayEnemyVoice(transform, Class, VoiceCue.Spawn);
                return;
            }

            shadow = game.CreateSpriteChild(transform, "Ground Shadow", game.CircleSprite,
                new Color(.035f, .025f, .06f, boss ? .48f : .36f), 1f, 1);
            // Enemy sprite pivots are authored at their feet. Keep the contact shadow directly
            // under that line instead of a third of a body below it (the old floating look).
            shadow.transform.localPosition = new Vector3(0f, boss ? 0f : -Radius * .055f, .05f);
            shadow.transform.localScale = new Vector3(Radius * (boss ? 2.64f : 2.4f),
                Radius * (boss ? .62f : .72f), 1f);
            shadowBaseScale = shadow.transform.localScale;

            if (boss)
            {
                barrierAura = game.CreateSpriteChild(transform, "Boss Barrier Crest", game.SparkSprite,
                    new Color(.34f, .82f, 1f, .18f), Radius * 1.15f, 2);
                barrierAura.transform.localPosition = new Vector3(0f, Radius * 1.35f, .02f);
            }

            animationFrames = variantProfile != null
                ? game.GetEnemyAnimationFrames(variantProfile, boss)
                : boss ? System.Array.Empty<Sprite>() : game.GetEnemyAnimationFrames(VisualClass);
            directionalAnimation = boss
                ? game.GetBossDirectionalAnimation(variantProfile)
                : game.GetEnemyVariantDirectionalAnimation(variantProfile);
            var staticSprite = variantProfile != null
                ? game.GetEnemyVariantSprite(variantProfile, boss)
                : game.GetEnemyStaticSprite(VisualClass, boss);
            var authoredVariantBody = !boss && variantProfile != null && staticSprite != null &&
                                      staticSprite != game.GetEnemyStaticSprite(
                                          variantProfile.FamilyClass, false);
            var authoredDirectionalBody = directionalAnimation != null;
            var monsterSprite = animationFrames.Length > 0 ? animationFrames[0] : staticSprite;
            if (boss)
            {
                bossFrontSprite = monsterSprite;
                bossBackSprite = game.GetEnemyBossBackSprite(variantProfile);
            }
            else
            {
                normalBackSprite = variantProfile != null
                    ? game.GetEnemyVariantBackSprite(variantProfile)
                    : game.GetEnemyBackSprite(VisualClass);
            }
            if (animationFrames.Length == 0 && monsterSprite != null)
            {
                animationFrames = BuildStaticExtendedTimeline(monsterSprite);
                usesStaticTimeline = true;
            }
            visualReferenceOpaqueHeight = RobustReferenceOpaqueHeight(monsterSprite,
                animationFrames, directionalAnimation);
            // Combat radii stay unchanged for balance; only the art grows so enemies remain
            // readable on the enlarged battlefield without changing collision or attack range.
            // Late heavy bodies must read as heavy without changing their combat collision. The
            // art-only multiplier keeps runners compact while brutes, siege units and sundering
            // elites retain the larger silhouette promised by their design.
            var regularReadabilityScale = Class switch
            {
                EnemyClass.Brute => 4.48f,
                EnemyClass.Siege => 4.42f,
                EnemyClass.Sunderer => 4.38f,
                EnemyClass.Shaman or EnemyClass.Cursebinder => 4.22f,
                _ => 4.08f
            };
            var chapterReadabilityScale = boss ? 1f : Class switch
            {
                EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Sunderer =>
                    1f + Mathf.Clamp((game.Round - 1) / 5, 0, 9) * .022f,
                EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Cursebinder =>
                    1f + Mathf.Clamp((game.Round - 1) / 5, 0, 9) * .010f,
                _ => 1f
            };
            var visualScale = monsterSprite != null
                ? Radius * (boss ? 5.32f : regularReadabilityScale) * chapterReadabilityScale
                : Radius * 2.24f;
            var typeColor = variantProfile?.Accent ?? EnemyColor();
            body = game.CreateSpriteChild(transform, "Monster", monsterSprite ?? game.CircleSprite,
                authoredVariantBody || authoredDirectionalBody || boss && monsterSprite != null ? Color.white :
                animationFrames.Length > 0 ? Color.Lerp(Color.white, typeColor, .32f) :
                staticSprite != null && staticSprite != game.GetEnemySprite(false) &&
                (!boss || VisualClass != EnemyClass.Melee) && animationFrames.Length == 0
                    ? Color.Lerp(Color.white, typeColor, .12f)
                    : monsterSprite != null ? typeColor : boss ? new Color(.48f, .14f, .63f) : typeColor,
                visualScale, 3);
            bodyBaseScale = body.transform.localScale;
            if (directionalAnimation != null)
            {
                // Direction sheets do not give every octant the same canvas occupancy. Measure
                // each walking body and keep one correction through walk, attack and skill. This
                // applies to regular enemies too: turning the Silence Shroud or Jelly Mage must
                // never make the actor visibly grow or shrink.
                var desiredWorldHeight = boss
                    ? Radius * BossVisualHeightPerRadius(variantProfile?.Id)
                    : visualReferenceOpaqueHeight * Mathf.Abs(bodyBaseScale.y);
                foreach (FacingOctant octant in System.Enum.GetValues(typeof(FacingOctant)))
                {
                    var walkBodyHeight = RobustWalkOpaqueHeight(directionalAnimation.FramesFor(octant));
                    bossDirectionalReferenceOpaqueHeights[(int)octant] = Mathf.Max(.01f, walkBodyHeight);
                    bossDirectionalScaleCorrections[(int)octant] = walkBodyHeight > .01f
                        ? Mathf.Clamp(desiredWorldHeight /
                                      (walkBodyHeight * Mathf.Abs(bodyBaseScale.y)), .24f, 3.25f)
                        : 1f;
                }
                var initialCorrection = bossDirectionalScaleCorrections[(int)visualOctant];
                body.transform.localScale = new Vector3(bodyBaseScale.x * initialCorrection,
                    bodyBaseScale.y * initialCorrection, bodyBaseScale.z);
            }
            bodyBaseColor = body.color;
            // Enemy art is authored as complete silhouettes. Splitting those painted bodies into
            // procedural torso/leg pieces creates a visible seam during walk cycles and can pull
            // a neighbouring lineup fragment away from the body. Keep enemy and boss frames
            // whole; locomotion and action timing are already supplied by the 72-frame timeline
            // plus the grounded root pose below. Defender rigs remain articulated separately.
            limbRig = null;
            CaptureVisualGroundLine();
            if (boss) BuildBossPresentation();
            // Every regular profile gets a compact role silhouette.  It uses only curved crest,
            // spark and round-shield primitives; the old rectangular slabs are forbidden.
            else if (!authoredVariantBody && !authoredDirectionalBody) BuildVariantPresentation(typeColor);
            if (game.Use2p5DPresentation)
            {
                body.enabled = false;
                visualRig = KayKit2p5DUnitVisual.CreateEnemy(transform, VisualClass, typeColor, Radius, boss);
            }

            // Static roster portraits still receive a distinct in-world animation language.
            // Casters orbit runes while brutes carry a heated core that flares before a slam.
            if (Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege or EnemyClass.Wisp or
                EnemyClass.Silencer or EnemyClass.Cursebinder)
            {
                var runeColor = Class switch
                {
                    EnemyClass.Siege => new Color(.65f, .3f, 1f, .9f),
                    EnemyClass.Shaman => new Color(.18f, 1f, .66f, .86f),
                    EnemyClass.Wisp => new Color(.22f, .9f, 1f, .86f),
                    EnemyClass.Silencer => new Color(.24f, .88f, 1f, .9f),
                    EnemyClass.Cursebinder => new Color(.26f, .84f, .94f, .9f),
                    _ => new Color(.45f, .8f, 1f, .86f)
                };
                motionAccentA = game.CreateSpriteChild(transform, "Casting Spark A", game.SparkSprite,
                    runeColor, Radius * .33f, 2);
                motionAccentB = game.CreateSpriteChild(transform, "Casting Spark B", game.SparkSprite,
                    Color.Lerp(runeColor, Color.white, .55f), Radius * .19f, 2);
            }
            else if (!IsBoss && Class == EnemyClass.Brute)
            {
                motionAccentA = game.CreateSpriteChild(transform, "Heavy Core Flare", game.SparkSprite,
                    new Color(1f, .36f, .11f, .66f), Radius * .5f, 2);
            }
            else if (Class is EnemyClass.Runner or EnemyClass.Piercer or EnemyClass.Flyer)
            {
                var trailColor = Class switch
                {
                    EnemyClass.Runner => new Color(.38f, 1f, .42f, .52f),
                    EnemyClass.Piercer => new Color(1f, .2f, .12f, .58f),
                    _ => new Color(.72f, .34f, 1f, .52f)
                };
                motionAccentA = game.CreateSpriteChild(transform, "Weapon Motion Accent", game.SparkSprite,
                    trailColor, Radius * .34f, 2);
            }

            if (monsterSprite == null)
            {
                var face = new GameObject("Face");
                face.transform.SetParent(transform, false);
                face.transform.localPosition = new Vector3(0f, -.01f, -0.2f);
                var text = face.AddComponent<TextMesh>();
                text.text = "••";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 42;
                text.characterSize = .045f;
                text.color = new Color(.2f, .07f, .25f);
                text.GetComponent<MeshRenderer>().sortingOrder = 5;
            }

            if (!IsBoss)
            {
                var back = game.CreateSpriteChild(transform, "Health Back", game.SquareSprite,
                    new Color(.08f, .05f, .12f, .95f), 1f, 6).transform;
                var healthBarWidth = Radius * 2.1f;
                back.localPosition = new Vector3(0f, -Radius - .12f, -0.1f);
                back.localScale = new Vector3(healthBarWidth, .06f, 1f);
                healthFill = game.CreateSpriteChild(transform, "Health Fill", game.SquareSprite,
                    new Color(1f, .32f, .28f), 1f, 7).transform;
                healthFill.localPosition = new Vector3(0f, -Radius - .12f, -0.2f);
                healthFill.localScale = new Vector3(healthBarWidth, .045f, 1f);
            }
            presentationInitialized = true;
            presentationPoolKey = requestedPoolKey;
            if (playSpawnVoice) game.PlayEnemyVoice(transform, Class, VoiceCue.Spawn);
        }

        private void ResetRuntimeStateForReuse()
        {
            IsAlive = true;
            velocity = Vector2.up;
            facingDirection = Vector2.up;
            visualFacingDirection = Vector2.up;
            visualOctant = FacingOctant.North;
            bossMorphScale = Vector2.one;
            lastAttackAt = 0f;
            nextDustAt = 0f;
            lastFootstepPhase = int.MinValue;
            stunnedUntil = timeFrozenUntil = 0f;
            nextPathReanchorAt = nextTerrainAuditAt = nextVisualUpdateAt = lastVisualUpdateAt = 0f;
            nextTargetScanAt = nextBlockerScanAt = ignoredTargetUntil = nextDetectionAllowedAt = 0f;
            bossSilhouetteSeparationUntil = nextPursuitApproachScanAt = 0f;
            moveSpeedFactor = .65f;
            hitMotion = attackMotion = skillMotion = 0f;
            skillMotionSpeed = 1f;
            visibleAnimationFrame = pathIndex = bossFormationLagSamples = 0;
            usesBossEntrance = castingBossSkill = enraged = false;
            attackingGate = engagingDefender = avoidsBossSilhouette = false;
            detectedTarget = cachedBlocker = temporarilyIgnoredTarget = pursuitApproachTarget = null;
            cachedPursuitApproachIndex = -1;
            stallRecoveryCount = unreachableTargetRejectCount = corridorPursuitStepCount = 0;
            bossSkillCastCount = bossPassiveHitCount = bossMomentumStacks = 0;
            lastBossSkillId = lastSpecialQaState = string.Empty;
            bossPassiveReadyAt = armorBreakUntil = armorBreakAmount = 0f;
            lastBossPassiveHitAt = -10f;
            damageContributors.Clear();
        }

        private void RestorePooledPresentation()
        {
            if (body != null)
            {
                body.enabled = !game.Use2p5DPresentation;
                body.color = bodyBaseColor;
                body.flipX = false;
                body.transform.localPosition = Vector3.zero;
                body.transform.localEulerAngles = Vector3.zero;
                var correction = directionalAnimation != null
                    ? bossDirectionalScaleCorrections[(int)visualOctant]
                    : 1f;
                body.transform.localScale = new Vector3(bodyBaseScale.x * correction,
                    bodyBaseScale.y * correction, bodyBaseScale.z);
                if (animationFrames != null && animationFrames.Length > 0)
                    body.sprite = animationFrames[0];
            }
            if (visualRig != null) visualRig.gameObject.SetActive(game.Use2p5DPresentation);
            if (shadow != null)
            {
                shadow.enabled = true;
                shadow.color = new Color(.035f, .025f, .06f, IsBoss ? .48f : .36f);
                shadow.transform.localScale = shadowBaseScale;
            }
            if (barrierAura != null) barrierAura.enabled = IsBoss;
            if (bossAura != null) bossAura.enabled = IsBoss;
            if (healthFill != null)
            {
                var scale = healthFill.localScale;
                scale.x = Radius * 2.1f;
                healthFill.localScale = scale;
            }
            CaptureVisualGroundLine();
        }

        public void PrepareForPool()
        {
            StopAllCoroutines();
            IsAlive = false;
            detectedTarget = cachedBlocker = temporarilyIgnoredTarget = pursuitApproachTarget = null;
            velocity = Vector2.zero;
            gameObject.SetActive(false);
        }

        private void BuildVariantPresentation(Color accent)
        {
            var dark = Color.Lerp(accent, new Color(.04f, .035f, .07f), .55f);
            roleSilhouetteRoot = new GameObject("Variant Role Silhouette").transform;
            roleSilhouetteRoot.SetParent(transform, false);

            void AddRolePart(string partName, Sprite sprite, Color color, Vector2 position,
                Vector2 scale, float rotation = 0f)
            {
                var renderer = game.CreateSpriteChild(roleSilhouetteRoot, partName, sprite, color, 1f, 6);
                renderer.transform.localPosition = new Vector3(position.x, position.y, -.20f);
                renderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                renderer.transform.localEulerAngles = new Vector3(0f, 0f, rotation);
                roleSilhouette.Add(renderer);
            }

            switch (Class)
            {
                case EnemyClass.Mage:
                case EnemyClass.Shaman:
                case EnemyClass.Wisp:
                    if (VariantId.Contains("archer") || VariantId.Contains("slinger"))
                    {
                        AddRolePart("Bow Upper Limb", game.SparkSprite, dark,
                            new Vector2(-Radius * .78f, Radius * .38f),
                            new Vector2(Radius * .35f, Radius * 1.05f), 24f);
                        AddRolePart("Bow Lower Limb", game.SparkSprite, dark,
                            new Vector2(-Radius * .78f, -Radius * .25f),
                            new Vector2(Radius * .35f, Radius * 1.05f), 156f);
                        AddRolePart("Quiver", game.SparkSprite, Color.Lerp(accent, Color.white, .34f),
                            new Vector2(Radius * .56f, Radius * .2f),
                            new Vector2(Radius * .24f, Radius * 1.18f), -18f);
                    }
                    else
                    {
                        AddRolePart("Caster Staff", game.SparkSprite, dark,
                            new Vector2(Radius * .92f, Radius * .03f),
                            new Vector2(Radius * .12f, Radius * 1.75f), -12f);
                        AddRolePart("Caster Focus", game.SparkSprite, Color.Lerp(accent, Color.white, .55f),
                            new Vector2(Radius * .74f, Radius * .82f), Vector2.one * Radius * .52f, 45f);
                    }
                    break;
                case EnemyClass.Siege:
                    AddRolePart("Siege Barrel", game.SparkSprite, Color.Lerp(accent, Color.white, .2f),
                        new Vector2(Radius * .7f, Radius * .18f), new Vector2(Radius * .16f, Radius * 1.55f), -68f);
                    AddRolePart("Siege Fuse", game.SparkSprite, new Color(1f, .55f, .12f),
                        new Vector2(-Radius * .45f, Radius * .72f), Vector2.one * Radius * .32f, 35f);
                    break;
                case EnemyClass.Piercer:
                    AddRolePart("Piercer Lance", game.SparkSprite, Color.Lerp(accent, Color.white, .42f),
                        new Vector2(Radius * .78f, Radius * .14f), new Vector2(Radius * .10f, Radius * 2.1f), -24f);
                    AddRolePart("Piercer Blade", game.SparkSprite, Color.white,
                        new Vector2(Radius * .56f, Radius * .92f), Vector2.one * Radius * .43f, -24f);
                    break;
                case EnemyClass.Runner:
                    AddRolePart("Runner Fin Left", game.SparkSprite, accent,
                        new Vector2(-Radius * .7f, Radius * .1f), new Vector2(Radius * .5f, Radius * .9f), 145f);
                    AddRolePart("Runner Fin Right", game.SparkSprite, accent,
                        new Vector2(Radius * .7f, Radius * .1f), new Vector2(Radius * .5f, Radius * .9f), 35f);
                    break;
                case EnemyClass.Brute:
                    AddRolePart("Brute Pauldron Left", game.SparkSprite, dark,
                        new Vector2(-Radius * .78f, Radius * .34f), Vector2.one * Radius * .7f, 90f);
                    AddRolePart("Brute Pauldron Right", game.SparkSprite, dark,
                        new Vector2(Radius * .78f, Radius * .34f), Vector2.one * Radius * .7f, -90f);
                    break;
            }

            if (VariantId.Contains("bomber") || VariantId.Contains("cannon"))
            {
                AddRolePart("Fuse Charge", game.SparkSprite, new Color(1f, .28f, .08f),
                    new Vector2(-Radius * .72f, Radius * .66f), Vector2.one * Radius * .48f, 45f);
                AddRolePart("Fuse Spark", game.SparkSprite, new Color(1f, .88f, .25f),
                    new Vector2(-Radius * .82f, Radius * 1.02f), Vector2.one * Radius * .34f);
            }
            if (VariantId.Contains("scout") || VariantId.Contains("hound") || VariantId.Contains("crawler"))
            {
                AddRolePart("Scout Blade Left", game.SparkSprite, accent,
                    new Vector2(-Radius * .62f, -Radius * .2f),
                    new Vector2(Radius * .34f, Radius * .88f), 135f);
                AddRolePart("Scout Blade Right", game.SparkSprite, accent,
                    new Vector2(Radius * .62f, -Radius * .2f),
                    new Vector2(Radius * .34f, Radius * .88f), 45f);
            }
        }

        private void BuildBossPresentation()
        {
            var labelObject = new GameObject("Boss Rank Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, Radius * 5.1f, -.28f);
            bossRankLabel = labelObject.AddComponent<TextMesh>();
            bossRankLabel.text = "BOSS";
            bossRankLabel.anchor = TextAnchor.MiddleCenter;
            bossRankLabel.alignment = TextAlignment.Center;
            bossRankLabel.fontSize = 64;
            bossRankLabel.characterSize = .03f;
            bossRankLabel.fontStyle = FontStyle.Bold;
            bossRankLabel.color = new Color(1f, .84f, .28f);
            bossRankLabel.GetComponent<MeshRenderer>().sortingOrder = 14;

            bossCrownProngs = System.Array.Empty<SpriteRenderer>();
        }

        public void ConfigureBossEntrance(float formationOffset, float backRowDistance)
        {
            if (game == null || !IsAlive || game.GetBossEntrancePathCount() == 0) return;
            usesBossEntrance = true;
            // Honour guards start outside the boss silhouette. The separation expires after the
            // entrance settles so a wide boss can never become a permanent navigation collider.
            avoidsBossSilhouette = !IsBoss;
            bossSilhouetteSeparationUntil = Time.time + 1.25f;
            attackingGate = false;
            engagingDefender = false;
            detectedTarget = null;
            pathIndex = 0;
            lateralOffset = formationOffset;
            bossFormationLagSamples = Mathf.RoundToInt(Mathf.Clamp(backRowDistance, 0f, 2.1f) * 11f);
            var start = game.GetBossEntrancePathTarget(0, lateralOffset, wriggle);
            var ahead = game.GetBossEntrancePathTarget(Mathf.Min(4, game.GetBossEntrancePathCount() - 1),
                lateralOffset, wriggle);
            var forward = (ahead - start).normalized;
            var position = start - forward * Mathf.Clamp(backRowDistance, 0f, 2.1f);
            transform.position = game.ActorWorldPosition(position, true);
            lastMovementPosition = position;
            lastMovementAt = Time.time;
            nextRecoveryAt = Time.time + .75f;
            velocity = forward;
            facingDirection = forward;
            visualFacingDirection = forward;
            lastVisualWorldPosition = position;
        }

        private int CurrentPathCount => usesBossEntrance
            ? game.GetBossEntrancePathCount()
            : game.GetPathCount(lane);

        private Vector2 CurrentPathTarget(int index, float lateral, float phase)
        {
            if (!usesBossEntrance) return game.GetPathTarget(lane, index, lateral, phase);
            if (bossFormationLagSamples <= 0)
                return game.GetBossEntrancePathTarget(index, lateral, phase);
            var last = Mathf.Max(0, game.GetBossEntrancePathCount() - 1);
            // Preserve the honour guard's back rows throughout the approach. The lag eases out
            // only in the final sector so every member can eventually reach its own gate point.
            var lagWeight = Mathf.Clamp01((last - index) / 34f);
            var lag = Mathf.RoundToInt(bossFormationLagSamples * lagWeight);
            return game.GetBossEntrancePathTarget(Mathf.Max(0, index - lag), lateral, phase);
        }

        private string EnemyDisplayName() => Class switch
        {
            EnemyClass.Skeleton => "뼈다귀 병사",
            EnemyClass.Runner => "질주 슬라임",
            EnemyClass.Brute => "바위 거한",
            EnemyClass.Shaman => "저주 주술사",
            EnemyClass.Siege => "공성 골렘",
            EnemyClass.Piercer => "관통 전사",
            EnemyClass.Wisp => "비전 위습",
            EnemyClass.Flyer => "비행 젤리",
            EnemyClass.Silencer => "침묵 수의령",
            EnemyClass.Cursebinder => "봉인 수의사제",
            EnemyClass.Sunderer => "갑주 파쇄기",
            EnemyClass.Mage => "젤리 마도사",
            EnemyClass.Boss => "성벽 파괴자",
            _ => "젤리 병사"
        };

        private Color EnemyColor() => Class switch
        {
            EnemyClass.Skeleton => new Color(.72f, .78f, .72f),
            EnemyClass.Runner => new Color(.46f, 1f, .43f),
            EnemyClass.Brute => new Color(1f, .55f, .24f),
            EnemyClass.Shaman => new Color(.78f, .4f, 1f),
            EnemyClass.Siege => new Color(.44f, .6f, .7f),
            EnemyClass.Piercer => new Color(1f, .35f, .25f),
            EnemyClass.Wisp => new Color(.38f, .86f, 1f),
            EnemyClass.Flyer => new Color(1f, .76f, .38f),
            EnemyClass.Silencer => new Color(.28f, .78f, .94f),
            EnemyClass.Cursebinder => new Color(.28f, .82f, .94f),
            EnemyClass.Sunderer => new Color(.94f, .60f, .18f),
            EnemyClass.Mage => new Color(.7f, .58f, 1f),
            EnemyClass.Boss => new Color(.76f, .42f, 1f),
            _ => Color.white
        };

        public void ForceAtGateForQa()
        {
            if (game == null || !IsAlive) return;
            pathIndex = Mathf.Max(0, CurrentPathCount - 1);
            attackingGate = true;
            castingBossSkill = false;
            velocity = Vector2.zero;
            var face = game.GetGateAttackPoint(lane, lateralOffset);
            transform.position = game.ActorWorldPosition(face, true);
            facingDirection = (game.GatePosition - face).normalized;
            visualFacingDirection = facingDirection;
            lastVisualWorldPosition = face;
            bossSkillLabel = GameLocalization.Text("성벽 지속 공격", "ATTACKING GATE");
            lastAttackAt = Time.time - .8f;
        }

        public void ForceNearGateForQa(float distance)
        {
            if (game == null || !IsAlive) return;
            pathIndex = Mathf.Max(0, CurrentPathCount - 4);
            attackingGate = false;
            castingBossSkill = false;
            velocity = Vector2.zero;
            var face = game.GetGateAttackPoint(lane, lateralOffset);
            var outward = (face - game.GatePosition).normalized;
            var nearGate = new Vector2(face.x + outward.x * Mathf.Max(.2f, distance),
                face.y + outward.y * Mathf.Max(.2f, distance));
            transform.position = game.ActorWorldPosition(nearGate, true);
            facingDirection = (face - nearGate).normalized;
            visualFacingDirection = facingDirection;
            lastVisualWorldPosition = nearGate;
            lastAttackAt = Time.time - 1.5f;
        }

        public void ForcePositionForQa(Vector2 position)
        {
            if (game == null || !IsAlive) return;
            attackingGate = false;
            castingBossSkill = false;
            velocity = Vector2.zero;
            transform.position = game.ActorWorldPosition(position, true);
            lastVisualWorldPosition = position;
            lastMovementPosition = position;
            lastMovementAt = Time.time;
            lastAttackAt = Time.time - 1.5f;
        }

        public bool ForceSpecialSkillForQa(PlayerUnit target)
        {
            if (target == null || !target.IsAlive) return false;
            lastSpecialQaState = $"started:{Vector2.Distance(Position, target.Position):0.000}";
            switch (Class)
            {
                case EnemyClass.Silencer:
                case EnemyClass.Cursebinder:
                    StartCoroutine(DebuffSkillRoutine(target));
                    return true;
                case EnemyClass.Sunderer:
                    StartCoroutine(ArmorRendSkillRoutine(target));
                    return true;
                default:
                    return false;
            }
        }

        public int PreviewAnimationForQa(int mode)
        {
            if (body == null || animationFrames.Length == 0) return -1;
            castingBossSkill = false;
            attackMotion = 0f;
            skillMotion = 0f;
            velocity = Vector2.zero;
            switch (mode)
            {
                case 0:
                    velocity = Vector2.right;
                    wriggle = 2.05f;
                    break;
                case 1:
                    attackMotion = .74f;
                    break;
                default:
                    skillMotion = .24f;
                    skillMotionSpeed = 0f;
                    castingBossSkill = true;
                    break;
            }
            UpdateVisualMotion();
            castingBossSkill = false;
            return visibleAnimationFrame;
        }

        public float PreviewDirectionHeightForQa(Vector2 direction)
        {
            if (body == null) return 0f;
            velocity = Vector2.zero;
            attackMotion = 0f;
            skillMotion = 0f;
            castingBossSkill = false;
            facingDirection = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.down;
            UpdateVisualMotion();
            return VisualWorldHeight;
        }

        public Vector4 PreviewMotionPoseForQa(Vector2 direction, int state, float normalizedPhase)
        {
            if (body == null) return Vector4.zero;
            facingDirection = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.down;
            velocity = state == 0 ? facingDirection : Vector2.zero;
            attackMotion = 0f;
            skillMotion = 0f;
            castingBossSkill = false;
            hitMotion = 0f;
            normalizedPhase = Mathf.Clamp01(normalizedPhase);
            wriggle = normalizedPhase * Mathf.PI * 2f;
            if (state == 1) attackMotion = Mathf.Max(.02f, 1f - normalizedPhase);
            if (state == 2)
            {
                skillMotion = Mathf.Max(.02f, 1f - normalizedPhase);
                skillMotionSpeed = 0f;
                castingBossSkill = true;
            }
            UpdateVisualMotion();
            castingBossSkill = false;
            var position = body.transform.localPosition / Mathf.Max(.01f, Radius);
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) / 45f;
            var scaleRatio = body.transform.localScale.x / Mathf.Max(.001f, bodyBaseScale.x);
            return new Vector4(position.x, position.y, angle, scaleRatio);
        }

        public Vector4 PreviewPresentationStateForQa(Vector2 direction, int state, float normalizedPhase)
        {
            // 0 idle, 1 walk, 2 attack, 3 skill, 4 hit, 5 stunned/frozen. The legacy preview
            // keeps state zero as walking, so this explicit presentation matrix lets release QA
            // cover every non-movement state without changing older test contracts.
            var legacyState = state switch { 2 => 1, 3 => 2, _ => 0 };
            var pose = PreviewMotionPoseForQa(direction, legacyState, normalizedPhase);
            if (state is 0 or 4 or 5)
            {
                velocity = Vector2.zero;
                attackMotion = 0f;
                skillMotion = 0f;
                castingBossSkill = false;
                hitMotion = state == 4 ? Mathf.Clamp01(1f - normalizedPhase) : 0f;
                UpdateVisualMotion();
                var position = body.transform.localPosition / Mathf.Max(.01f, Radius);
                var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) / 45f;
                var scaleRatio = body.transform.localScale.x / Mathf.Max(.001f, bodyBaseScale.x);
                pose = new Vector4(position.x, position.y, angle, scaleRatio);
            }
            return pose;
        }

        private void FaceVisualTarget(Vector2 targetPosition)
        {
            var requested = targetPosition - Position;
            if (requested.sqrMagnitude <= .0001f) return;
            facingDirection = requested.normalized;
            // Combat acquisition happens after the visual update in the frame. Synchronize the
            // presentation lock immediately so the first wind-up cell cannot use the previous
            // walking direction, then keep this direction for the whole attack/cast.
            visualFacingDirection = facingDirection;
            visualOctant = EightWayFacing.FromVector(facingDirection);
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

        public Vector2 AttackOriginFor(Vector2 targetPosition)
        {
            var requested = targetPosition - Position;
            var direction = requested.sqrMagnitude > .0001f
                ? EightWayFacing.VectorFor(EightWayFacing.FromVector(requested))
                : EightWayFacing.VectorFor(visualOctant);
            var side = new Vector2(-direction.y, direction.x);
            var forwardDistance = Radius * (Class switch
            {
                EnemyClass.Piercer => .9f,
                EnemyClass.Siege => .72f,
                EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp or EnemyClass.Silencer or
                    EnemyClass.Cursebinder => .58f,
                _ => .66f
            });
            var handHeight = Radius * (IsBoss ? 1.38f : Class is EnemyClass.Brute or EnemyClass.Siege ? .92f : .7f);
            var handSide = Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp or
                EnemyClass.Silencer or EnemyClass.Cursebinder
                ? Radius * .16f
                : -Radius * .07f;
            return Position + Vector2.up * handHeight + direction * forwardDistance + side * handSide;
        }

        private IEnumerator GateAttackContactRoutine(bool ranged)
        {
            yield return new WaitForSeconds(ranged ? .3f : Class == EnemyClass.Runner ? .2f : .28f);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle || !attackingGate) yield break;
            if (ranged)
            {
                game.LaunchEnemyGateBolt(this, AttackOriginFor(game.GatePosition));
                yield break;
            }
            if (!game.IsEnemyAtMeleeGateContact(this)) yield break;
            game.DamageGate(this);
            var face = game.GetGateAttackPoint(lane, lateralOffset);
            game.SpawnContactEffect(face, IsBoss);
            game.SpawnEnemyClassEffect(AttackOriginFor(face), face, Class, false);
            if (Class == EnemyClass.Brute || IsBoss)
                game.SpawnEnemySlam(face, IsBoss ? 1.45f : .92f,
                    IsBoss ? new Color(.92f, .22f, .16f) : new Color(1f, .42f, .16f));
        }

        private IEnumerator RangedAttackReleaseRoutine(PlayerUnit target, float damage)
        {
            yield return new WaitForSeconds(Class == EnemyClass.Wisp ? .2f : .3f);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle ||
                target == null || !target.IsAlive) yield break;
            FaceVisualTarget(target.Position);
            game.LaunchEnemyMagic(target, AttackOriginFor(target.Position), damage, false, Class,
                PhysicalPenetration, MagicPenetration);
        }

        private IEnumerator DefenderAttackContactRoutine(PlayerUnit target, float damageMultiplier)
        {
            var windup = Class switch
            {
                EnemyClass.Runner => .18f,
                EnemyClass.Piercer => .23f,
                EnemyClass.Brute => .34f,
                _ => IsBoss ? .32f : .27f
            };
            yield return new WaitForSeconds(windup);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle ||
                target == null || !target.IsAlive) yield break;
            if (Vector2.Distance(Position, target.Position) > AttackRange + target.Radius * .55f) yield break;
            FaceVisualTarget(target.Position);
            target.ReactToContact(Position, IsBoss ? .2f : .06f);
            target.TakeDamage((IsFlying ? attackPower : contactDamage) * damageMultiplier,
                IgnoresArmor ? DamageType.Pure : DamageType.Physical, true,
                PhysicalPenetration, MagicPenetration);
            if (target.Archetype == UnitArchetype.Tank && game.TankThornsDamage > 0f)
                TakeDamage(game.TankThornsDamage, target, DamageType.Physical);
            var impact = Vector2.Lerp(AttackOriginFor(target.Position), target.HitPoint, .62f);
            game.SpawnContactEffect(impact, IsBoss);
            game.SpawnEnemyClassEffect(AttackOriginFor(target.Position), impact, Class, false);
        }

        public void TakeDamage(float amount, PlayerUnit source = null, DamageType damageType = DamageType.Physical,
            bool suppressImpact = false)
        {
            if (!IsAlive) return;
            amount = ApplyBossPassiveToIncomingDamage(amount, source, damageType);
            if (amount <= 0f) return;
            if (RequiresMagicDamage && damageType == DamageType.Physical)
            {
                game.SpawnImpact(Position, new Color(.32f, .86f, 1f));
                return;
            }
            var reducedDamage = Mathf.Max(1f, CombatMath.MitigatedDamage(amount, damageType,
                Armor, magicResistance, source?.PhysicalPenetration ?? 0f,
                source?.MagicPenetration ?? 0f));
            var absorbed = Mathf.Min(barrier, reducedDamage);
            barrier = Mathf.Max(0f, barrier - absorbed);
            var healthDamage = Mathf.Max(0f, reducedDamage - absorbed);
            var appliedDamage = Mathf.Min(health, healthDamage);
            if (source != null && source.IsAlive && appliedDamage > 0f)
                damageContributors[source] = damageContributors.TryGetValue(source, out var previous)
                    ? previous + appliedDamage
                    : appliedDamage;
            health = Mathf.Max(0f, health - healthDamage);
            hitMotion = 1f;
            game.SpawnDamageNumber(Position, absorbed > 0f ? absorbed : healthDamage, damageType, absorbed > 0f);
            var incoming = source != null && source.IsAlive ? Position - source.Position : Vector2.down;
            var hitColor = absorbed > 0f
                ? new Color(.28f, .78f, 1f)
                : damageType switch
                {
                    DamageType.Magic => new Color(.72f, .28f, 1f),
                    DamageType.Pure => new Color(1f, .3f, .12f),
                    _ => new Color(1f, .72f, .18f)
                };
            var feedbackIntensity = Mathf.Clamp(reducedDamage / Mathf.Max(12f, maxHealth * .1f), .7f, IsBoss ? 1.9f : 1.45f);
            if (!suppressImpact)
            {
                if (source != null && !source.IsRangedCombatant)
                    game.SpawnMeleeImpactFeedback(Position, incoming, hitColor, feedbackIntensity);
                else
                    game.SpawnHitFeedback(Position, incoming, hitColor, feedbackIntensity);
            }
            if (IsBoss && !enraged && HealthRatio <= .5f) EnterEnrage();
            RefreshHealthBar();
            if (health > 0f) return;
            Die();
        }

        private float ApplyBossPassiveToIncomingDamage(float amount, PlayerUnit source, DamageType damageType)
        {
            if (!IsBoss || amount <= 0f) return amount;
            var profile = BossIdentityCatalog.For(VisualClass);
            var adjusted = amount;
            var triggered = false;
            switch (profile.PassiveId)
            {
                case "gelatin_crown":
                    adjusted = Mathf.Min(adjusted, maxHealth * .08f);
                    triggered = adjusted + .01f < amount;
                    break;
                case "ossuary_plate":
                    adjusted *= damageType == DamageType.Physical ? .72f : damageType == DamageType.Magic ? 1.15f : 1f;
                    triggered = damageType == DamageType.Physical;
                    break;
                case "warpath":
                    if (bossMomentumStacks < 5)
                    {
                        bossMomentumStacks++;
                        speed *= 1.035f;
                        contactDamage *= 1.025f;
                        triggered = true;
                    }
                    break;
                case "bedrock_layers":
                    bossPassiveHitCount++;
                    if (bossPassiveHitCount % 5 == 0)
                    {
                        adjusted *= .55f;
                        barrier = Mathf.Min(maxHealth * .2f, barrier + maxHealth * .035f);
                        triggered = true;
                    }
                    break;
                case "elder_sap":
                    if (Time.time >= bossPassiveReadyAt)
                    {
                        health = Mathf.Min(maxHealth, health + adjusted * .22f);
                        bossPassiveReadyAt = Time.time + 5f;
                        triggered = true;
                    }
                    break;
                case "clockwork_guard":
                    bossPassiveHitCount++;
                    if (bossPassiveHitCount % 4 == 0)
                    {
                        adjusted *= .4f;
                        triggered = true;
                    }
                    break;
                case "bloodscale_counter":
                    if (source != null && source.IsAlive && !source.IsRangedCombatant && Time.time >= bossPassiveReadyAt)
                    {
                        source.TakeDamage(Mathf.Max(8f, attackPower * .32f), DamageType.Pure);
                        bossPassiveReadyAt = Time.time + 3f;
                        triggered = true;
                    }
                    break;
                case "astral_phase":
                    var physicalPhase = Mathf.FloorToInt(Time.time / 5f) % 2 == 0;
                    if (physicalPhase && damageType == DamageType.Physical || !physicalPhase && damageType == DamageType.Magic)
                    {
                        adjusted *= .45f;
                        triggered = true;
                    }
                    break;
                case "storm_wing":
                    if (source != null && source.IsAlive && source.IsRangedCombatant && Time.time >= bossPassiveReadyAt)
                    {
                        bossPassiveReadyAt = Time.time + 4f;
                        adjusted = 0f;
                        triggered = true;
                    }
                    break;
                case "abyssal_lens":
                    if (damageType == DamageType.Magic)
                    {
                        barrier = Mathf.Min(maxHealth * .15f, barrier + adjusted * .18f);
                        triggered = true;
                    }
                    break;
            }
            lastBossPassiveHitAt = Time.time;
            if (triggered)
            {
                game.SpawnImpact(Position, profile.Accent);
                bossSkillLabel = profile.PassiveName;
            }
            return adjusted;
        }

        public void ApplyStun(float duration)
        {
            if (!IsAlive) return;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Max(0f, duration));
            hitMotion = Mathf.Max(hitMotion, .65f);
            velocity = Vector2.zero;
        }

        public void ApplyArmorBreak(float duration, float amount)
        {
            if (!IsAlive || duration <= 0f || amount <= 0f) return;
            armorBreakUntil = Mathf.Max(armorBreakUntil, Time.time + duration);
            armorBreakAmount = Mathf.Max(armorBreakAmount, amount);
            hitMotion = Mathf.Max(hitMotion, .45f);
        }

        public void ApplyTimeFreeze(float duration)
        {
            if (!IsAlive) return;
            var freezeDuration = Mathf.Max(0f, duration);
            StopAllCoroutines();
            castingBossSkill = false;
            attackMotion = 0f;
            skillMotion = 0f;
            velocity = Vector2.zero;
            timeFrozenUntil = Mathf.Max(timeFrozenUntil, Time.time + freezeDuration);
            stunnedUntil = Mathf.Max(stunnedUntil, timeFrozenUntil);
            lastAttackAt += freezeDuration;
            nextSkillAt += freezeDuration;
            nextBossSkillAt += freezeDuration;
        }

        public bool ContainsPointer(Vector2 world)
        {
            var delta = world - Position;
            if (IsBoss)
                return Mathf.Abs(delta.x) <= Radius * 2.65f &&
                       delta.y >= -Radius * 1.25f && delta.y <= Radius * 4.2f;
            return Mathf.Abs(delta.x) <= Radius * 1.32f &&
                   delta.y >= -Radius * 1.05f && delta.y <= Radius * 2.25f;
        }

        public SpriteRenderer CreateFocusOutline(Material material)
        {
            if (body == null || material == null) return null;
            var outlineObject = new GameObject("Focus Silhouette Outline");
            outlineObject.transform.SetParent(body.transform, false);
            var outline = outlineObject.AddComponent<SpriteRenderer>();
            outline.sharedMaterial = material;
            outline.color = new Color(.18f, .9f, 1f, .96f);
            SyncFocusOutline(outline);
            return outline;
        }

        public void SyncFocusOutline(SpriteRenderer outline)
        {
            if (outline == null || body == null) return;
            outline.sprite = body.sprite;
            outline.flipX = body.flipX;
            outline.sortingLayerID = body.sortingLayerID;
            outline.sortingOrder = body.sortingOrder + 1;
            outline.transform.localPosition = new Vector3(0f, 0f, -.02f);
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localScale = Vector3.one;
            outline.enabled = body.enabled && body.gameObject.activeInHierarchy;
        }

        private void Update()
        {
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle) return;
            wriggle += Time.deltaTime * 7f;
            engagingDefender = false;
            if (Time.time < stunnedUntil)
            {
                velocity = Vector2.zero;
                return;
            }
            var enteringFromBossPortal = usesBossEntrance && pathIndex == 0 &&
                                         Vector2.Distance(Position,
                                             game.GetBossEntrancePathTarget(0, lateralOffset, wriggle)) > .08f;
            if (!IsFlying && !enteringFromBossPortal && Time.time >= nextTerrainAuditAt)
            {
                nextTerrainAuditAt = Time.time + .10f + (GetInstanceID() & 7) * .006f;
                if (!game.IsWalkableWithClearance(Position, Radius * .42f) ||
                    !game.IsWithinGroundEnemyRoadCorridor(Position, Radius * .42f))
                {
                    RecoverToForwardPath();
                    return;
                }
            }

            if (attackingGate)
            {
                engagingDefender = true;
                FaceVisualTarget(game.GatePosition);
                var stillInGateRange = IsRanged
                    ? Vector2.Distance(Position, game.GatePosition) <= AttackRange + .32f
                    : game.IsEnemyAtMeleeGateContact(this);
                if (!stillInGateRange)
                {
                    attackingGate = false;
                    pathIndex = Mathf.Max(0, CurrentPathCount - 4);
                    velocity = Vector2.zero;
                    return;
                }
                var gateAttackInterval = IsRanged ? 1.45f : IsBoss ? .92f : 1.05f;
                if (Time.time >= lastAttackAt + gateAttackInterval)
                {
                    lastAttackAt = Time.time;
                    attackMotion = 1f;
                    game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                    StartCoroutine(GateAttackContactRoutine(IsRanged));
                }
                // Once an enemy has started hitting the fortress it never abandons that attack
                // for a newly detected defender. Only losing legal gate range clears this lock.
                return;
            }

            if (IsBoss && !castingBossSkill && Time.time >= nextBossSkillAt)
            {
                StartCoroutine(BossSkillRoutineV2());
                return;
            }
            if (castingBossSkill)
            {
                engagingDefender = true;
                return;
            }

            // Every enemy now owns a detection range larger than its weapon range. Ground
            // melee units chase only legal ground targets, while ranged and flying enemies may
            // acquire defenders deployed on isolated high-ground islands.
            if (Time.time >= nextDetectionAllowedAt && TryEngageDetectedDefender()) return;

            // Ranged enemies do not have to touch the gate.  Once the defenders are outside
            // their spell range, they stop on the road and visibly bombard the fortress.
            if (IsRanged)
            {
                var gateSpellRange = Class == EnemyClass.Siege ? 3.15f :
                    Class == EnemyClass.Shaman ? 2.8f : 2.5f;
                if (Vector2.Distance(Position, game.GatePosition) <= gateSpellRange + .18f)
                {
                    attackingGate = true;
                    engagingDefender = true;
                    FaceVisualTarget(game.GatePosition);
                    velocity = Vector2.zero;
                    if (Time.time >= lastAttackAt + 1.45f)
                    {
                        lastAttackAt = Time.time;
                        attackMotion = 1f;
                        game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                        StartCoroutine(GateAttackContactRoutine(true));
                    }
                    return;
                }
            }

            PlayerUnit blocker = null;
            if (!IsFlying)
            {
                if (cachedBlocker != null)
                {
                    var contactRadius = cachedBlocker.Radius + Radius + .08f;
                    if (!cachedBlocker.IsAlive ||
                        (!IsRanged && cachedBlocker.IsOnHighGround) ||
                        Vector2.SqrMagnitude(cachedBlocker.Position - Position) >= contactRadius * contactRadius)
                        cachedBlocker = null;
                }
                if (Time.time >= nextBlockerScanAt)
                {
                    cachedBlocker = game.FindBlocker(Position, Radius, IsRanged);
                    nextBlockerScanAt = Time.time + .12f + (GetInstanceID() & 7) * .009f;
                }
                blocker = cachedBlocker;
            }
            if (blocker != null)
            {
                engagingDefender = true;
                FaceVisualTarget(blocker.Position);
                if (!IsBoss && HasMeleeSignatureSkill &&
                    Time.time >= nextSkillAt)
                {
                    StartCoroutine(MeleeSkillRoutine(blocker));
                    return;
                }
                var attackInterval = IsBoss ? (enraged ? .7f : .9f) : Class switch
                {
                    EnemyClass.Runner => .46f,
                    EnemyClass.Brute => .95f,
                    EnemyClass.Skeleton => .76f,
                    _ => .72f
                };
                if (Time.time >= lastAttackAt + attackInterval)
                {
                    lastAttackAt = Time.time;
                    attackMotion = 1f;
                    game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                    StartCoroutine(DefenderAttackContactRoutine(blocker, 1f));
                }
                return;
            }

            var last = CurrentPathCount - 1;
            ReanchorToNearestForwardPath();
            while (pathIndex < last && Vector2.Distance(Position, CurrentPathTarget(pathIndex + 1, lateralOffset, wriggle)) < .14f)
                pathIndex++;

            var gate = CurrentPathTarget(last, lateralOffset, wriggle);
            if (pathIndex >= last - 1 && Vector2.Distance(Position, gate) < .17f)
            {
                ForceAtGateForQa();
                lastAttackAt = Time.time - .35f;
                return;
            }

            // Looking six samples ahead made a monster aim across the inside of a curved road.
            // The terrain correctly rejected that shortcut, but the monster then retried the
            // same invalid line forever.  A short look-ahead preserves smooth motion and drops
            // to the immediate centreline sample whenever progress has stalled.
            var stalled = Time.time - lastMovementAt > .72f;
            var lookAhead = stalled ? 1 : 3;
            var target = CurrentPathTarget(Mathf.Min(last, pathIndex + lookAhead),
                stalled ? 0f : lateralOffset, stalled ? 0f : wriggle);
            var desired = (target - Position).normalized;
            velocity = Vector2.Lerp(velocity, desired, Mathf.Clamp01(Time.deltaTime * 8f)).normalized;
            if (velocity.sqrMagnitude > .001f) facingDirection = velocity;
            moveSpeedFactor = Mathf.MoveTowards(moveSpeedFactor, enraged ? 1.27f : 1f, Time.deltaTime * 1.8f);
            var movement = speed * moveSpeedFactor * game.EnemySpeedMultiplier * Time.deltaTime;
            var next = Position + velocity * movement;
            var portalStep = enteringFromBossPortal &&
                             Vector2.Distance(next, game.GetBossEntrancePathTarget(0, lateralOffset, wriggle)) <
                             Vector2.Distance(Position, game.GetBossEntrancePathTarget(0, lateralOffset, wriggle));
            if (!IsFlying && !portalStep && !game.CanTraverseGroundEnemy(Position, next, Radius * .42f))
            {
                // First recover to the next authored sample.  If a unit has drifted to the edge
                // of a bend, test small steering fans so it slides back onto the road instead of
                // vibrating against the cliff.
                var centerTarget = CurrentPathTarget(Mathf.Min(last, pathIndex + 1), 0f, 0f);
                var centerDirection = (centerTarget - Position).normalized;
                var foundStep = false;
                foreach (var angle in RecoverySteeringAngles)
                {
                    var radians = angle * Mathf.Deg2Rad;
                    var steered = new Vector2(
                        centerDirection.x * Mathf.Cos(radians) - centerDirection.y * Mathf.Sin(radians),
                        centerDirection.x * Mathf.Sin(radians) + centerDirection.y * Mathf.Cos(radians));
                    var candidate = Position + steered * movement;
                    if (!game.CanTraverseGroundEnemy(Position, candidate, Radius * .42f)) continue;
                    velocity = steered;
                    next = candidate;
                    foundStep = true;
                    break;
                }
                if (!foundStep)
                {
                    velocity = Vector2.zero;
                    if (Time.time >= nextRecoveryAt)
                    {
                        nextRecoveryAt = Time.time + .42f;
                        stallRecoveryCount++;
                        RecoverToForwardPath();
                    }
                    return;
                }
            }
            if (avoidsBossSilhouette && Time.time < bossSilhouetteSeparationUntil &&
                game.BossExclusionActive)
                next = game.ResolveBossSilhouetteExclusion(this, next);
            transform.position = game.ActorWorldPosition(next, true);
            if (Vector2.Distance(next, lastMovementPosition) >= .045f)
            {
                lastMovementPosition = next;
                lastMovementAt = Time.time;
                stallRecoveryCount = 0;
            }
            var footstepGaitSpeed = Class switch
            {
                EnemyClass.Runner => 1.72f,
                EnemyClass.Brute or EnemyClass.Siege => .62f,
                EnemyClass.Skeleton => .9f,
                EnemyClass.Piercer => 1.18f,
                EnemyClass.Flyer => 1.42f,
                _ => 1f
            };
            var footstepPhase = Mathf.FloorToInt(wriggle * footstepGaitSpeed / Mathf.PI);
            if (footstepPhase != lastFootstepPhase && Time.time >= nextDustAt)
            {
                lastFootstepPhase = footstepPhase;
                nextDustAt = Time.time + (IsBoss ? .18f : .28f);
                game.SpawnMovementDust(Position - velocity * Radius * .42f,
                    IsBoss ? new Color(.72f, .28f, 1f) : new Color(.72f, .48f, .85f), Radius,
                    EightWayFacing.VectorFor(visualOctant));
            }
        }

        private void LateUpdate()
        {
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle) return;
            // AI resolves pursuit, recovery and attack state in Update.  Rendering the octant
            // before that decision left one stale walking frame at every sharp turn and could
            // make a monster appear to run backwards.  Commit the pose from the final movement
            // vector instead; attack/cast locks still win because those states zero velocity.
            // Movement remains continuous on the parent transform, while the expensive
            // multi-renderer pose rig is sampled at a stable 30 FPS (bosses at 45 FPS).
            // Staggering prevents a crowd from rebuilding every limb in the same frame.
            if (Time.time < nextVisualUpdateAt) return;
            var visualInterval = IsBoss ? 1f / 45f : 1f / 30f;
            nextVisualUpdateAt = Time.time + visualInterval + (GetInstanceID() & 3) * .0007f;
            UpdateVisualMotion();
        }

        private void RecoverToForwardPath()
        {
            var safe = game.GetEnemyRecoveryPoint(lane, pathIndex, usesBossEntrance,
                Position, Radius * .42f, out var recoveredIndex);
            transform.position = game.ActorWorldPosition(safe, true);
            pathIndex = Mathf.Max(pathIndex, Mathf.Max(0, recoveredIndex - 1));
            lastMovementPosition = safe;
            lastMovementAt = Time.time;
            lateralOffset = Mathf.MoveTowards(lateralOffset, 0f, .14f);
            if (stallRecoveryCount >= 2)
            {
                // An unreachable defender or a wide boss can repeatedly pull the unit back into
                // the same failed pursuit. Give the authored lane a short uncontested window so
                // the unit advances past the obstruction before detection resumes.
                detectedTarget = null;
                cachedBlocker = null;
                nextDetectionAllowedAt = Time.time + .65f;
            }
            var nextIndex = Mathf.Min(CurrentPathCount - 1, recoveredIndex + 1);
            velocity = (CurrentPathTarget(nextIndex, 0f, 0f) - safe).normalized;
            if (velocity.sqrMagnitude > .001f) facingDirection = velocity;
        }

        private bool TryEngageDetectedDefender()
        {
            // The last three bends are visible castle-front sectors.  Defenders still block by
            // physical contact, but passive aggro may not pull every flank into one centre tank.
            // Away from the castle, ordinary nearest-target detection remains unchanged.
            var restrictToGateSector = !usesBossEntrance &&
                                       pathIndex >= Mathf.Max(0, CurrentPathCount - 46);
            var targetInvalid = detectedTarget == null || !detectedTarget.IsAlive ||
                                Vector2.SqrMagnitude(Position - detectedTarget.Position) >
                                DetectionRange * DetectionRange * 1.2544f ||
                                (!CanTargetHighGround && detectedTarget.IsOnHighGround) ||
                                restrictToGateSector && !game.IsDefenderInGateSector(lane, detectedTarget);
            if (targetInvalid)
            {
                detectedTarget = game.FindDefenderTargetForEnemy(this, restrictToGateSector);
                nextTargetScanAt = Time.time + .2f + (GetInstanceID() & 7) * .014f;
            }
            else if (Time.time >= nextTargetScanAt)
            {
                var nearest = game.FindDefenderTargetForEnemy(this, restrictToGateSector);
                if (nearest != null &&
                    Vector2.SqrMagnitude(Position - nearest.Position) + .16f <
                    Vector2.SqrMagnitude(Position - detectedTarget.Position))
                    detectedTarget = nearest;
                nextTargetScanAt = Time.time + .24f + (GetInstanceID() & 7) * .016f;
            }

            if (Class == EnemyClass.Silencer && Time.time >= nextSkillAt)
            {
                var mageTarget = game.FindDefenderTargetForEnemy(this, restrictToGateSector, true);
                if (mageTarget != null) detectedTarget = mageTarget;
            }

            if (detectedTarget == null || !detectedTarget.IsAlive) return false;
            // Revalidate before both attack and movement. A defender may cross onto an isolated
            // hill after acquisition; melee units must release it instead of attacking through
            // the boundary merely because the straight-line distance is now small.
            if (!CanAcquireDetectedTarget(detectedTarget))
            {
                TemporarilyRejectDetectedTarget();
                return false;
            }
            var distance = Vector2.Distance(Position, detectedTarget.Position);
            if (distance <= AttackRange)
            {
                engagingDefender = true;
                velocity = Vector2.zero;
                FaceVisualTarget(detectedTarget.Position);
                if (IsRanged)
                {
                    if (Time.time >= nextSkillAt &&
                        (Class != EnemyClass.Silencer || detectedTarget.Archetype is
                            UnitArchetype.AreaMage or UnitArchetype.SingleMage))
                    {
                        StartCoroutine(MageSkillRoutine(detectedTarget));
                        return true;
                    }
                    var rangedInterval = Class switch
                    {
                        EnemyClass.Wisp => 1.12f,
                        EnemyClass.Silencer => 1.48f,
                        EnemyClass.Cursebinder => 1.46f,
                        _ => 1.36f
                    };
                    if (Time.time >= lastAttackAt + rangedInterval)
                    {
                        lastAttackAt = Time.time;
                        attackMotion = 1f;
                        game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                        StartCoroutine(RangedAttackReleaseRoutine(detectedTarget,
                            magicPower * (Class == EnemyClass.Wisp ? .68f :
                                Class is EnemyClass.Silencer or EnemyClass.Cursebinder ? .50f : .58f)));
                    }
                    return true;
                }

                if (IsFlying)
                {
                    if (Time.time >= nextSkillAt)
                    {
                        StartCoroutine(FlyingSkillRoutine(detectedTarget));
                        return true;
                    }
                    if (Time.time >= lastAttackAt + .92f)
                    {
                        lastAttackAt = Time.time;
                        attackMotion = 1f;
                        game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                        StartCoroutine(DefenderAttackContactRoutine(detectedTarget, .82f));
                    }
                    return true;
                }

                if (!IsBoss && HasMeleeSignatureSkill &&
                    Time.time >= nextSkillAt)
                {
                    StartCoroutine(MeleeSkillRoutine(detectedTarget));
                    return true;
                }
                var meleeInterval = IsBoss ? (enraged ? .7f : .9f) : Class switch
                {
                    EnemyClass.Runner => .46f,
                    EnemyClass.Brute => .95f,
                    EnemyClass.Skeleton => .76f,
                    _ => .72f
                };
                if (Time.time >= lastAttackAt + meleeInterval)
                {
                    lastAttackAt = Time.time;
                    attackMotion = 1f;
                    game.PlayEnemyVoice(transform, Class, VoiceCue.Attack);
                    StartCoroutine(DefenderAttackContactRoutine(detectedTarget, 1f));
                }
                return true;
            }
            var chaseSpeed = speed * (enraged ? 1.18f : 1f) * game.EnemySpeedMultiplier;
            var chaseStep = chaseSpeed * Time.deltaTime;
            if (!TryResolveDefenderPursuitStep(detectedTarget, chaseStep, out var next, out var direction,
                    out var usedCorridor))
            {
                TemporarilyRejectDetectedTarget();
                return false;
            }

            engagingDefender = true;
            velocity = Vector2.Lerp(velocity, direction, Mathf.Clamp01(Time.deltaTime * 10f)).normalized;
            facingDirection = direction;
            if (usedCorridor) corridorPursuitStepCount++;
            transform.position = game.ActorWorldPosition(next, true);
            if (Vector2.Distance(next, lastMovementPosition) >= .035f)
            {
                lastMovementPosition = next;
                lastMovementAt = Time.time;
                stallRecoveryCount = 0;
            }
            return true;
        }

        internal bool CanAcquireDetectedTarget(PlayerUnit candidate)
        {
            if (candidate == null || !candidate.IsAlive) return false;
            if (candidate == temporarilyIgnoredTarget && Time.time < ignoredTargetUntil) return false;
            if (!CanTargetHighGround && candidate.IsOnHighGround) return false;
            var distance = Vector2.Distance(Position, candidate.Position);
            if (distance > DetectionRange) return false;
            if (IsFlying || distance <= AttackRange && IsRanged) return true;
            if (distance <= AttackRange)
            {
                var contactDirection = (candidate.Position - Position).normalized;
                var contactProbe = candidate.Position - contactDirection *
                    Mathf.Max(.03f, candidate.Radius * .42f);
                return contactDirection.sqrMagnitude > .001f &&
                       game.CanTraverseGroundEnemy(Position, contactProbe, Radius * .42f);
            }

            var direction = (candidate.Position - Position).normalized;
            // Validate the complete route to a legal weapon-release point. A one-frame probe
            // accepted off-road defenders until the monster reached the verge, then caused the
            // target/path/target loop reported as spinning or freezing.
            var probeDistance = Mathf.Max(.04f, distance - Mathf.Max(.08f, AttackRange - .035f));
            var directProbe = Position + direction * probeDistance;
            if (direction.sqrMagnitude > .001f &&
                game.CanTraverseGroundEnemy(Position, directProbe, Radius * .42f)) return true;
            return TryFindCorridorAttackApproach(candidate, out _, out _);
        }

        private bool TryResolveDefenderPursuitStep(PlayerUnit target, float movement,
            out Vector2 next, out Vector2 direction, out bool usedCorridor)
        {
            next = Position;
            direction = Vector2.zero;
            usedCorridor = false;
            var direct = target.Position - Position;
            if (direct.sqrMagnitude <= .000001f) return false;
            direction = direct.normalized;
            var distance = direct.magnitude;
            var directTravel = Mathf.Min(movement,
                Mathf.Max(0f, distance - Mathf.Max(.08f, AttackRange - .035f)));
            next = Position + direction * directTravel;
            if (IsFlying) return true;
            var releaseDistance = Mathf.Max(.04f,
                distance - Mathf.Max(.08f, AttackRange - .035f));
            var releasePoint = Position + direction * releaseDistance;
            // Checking only the next few pixels lets an enemy begin a doomed straight chase,
            // hit the road verge and snap back toward the lane. Validate the whole line to the
            // legal attack point before choosing direct pursuit, so curved-road motion stays
            // continuous from its first frame.
            if (game.CanTraverseGroundEnemy(Position, releasePoint, Radius * .42f)) return true;

            if (!TryFindCorridorAttackApproach(target, out var approachIndex, out var approachPoint)) return false;
            ReanchorToNearestForwardPath();
            while (pathIndex < approachIndex &&
                   Vector2.Distance(Position, CurrentPathTarget(pathIndex + 1, 0f, 0f)) < .14f)
                pathIndex++;
            var waypointIndex = approachIndex >= pathIndex
                ? Mathf.Min(approachIndex, pathIndex + 2)
                : Mathf.Max(approachIndex, pathIndex - 1);
            var waypoint = Mathf.Abs(approachIndex - pathIndex) <= 1
                ? approachPoint
                : CurrentPathTarget(waypointIndex, 0f, 0f);
            direction = waypoint - Position;
            if (direction.sqrMagnitude <= .0004f)
            {
                if (Vector2.Distance(Position, target.Position) <= AttackRange + .04f) return true;
                return false;
            }
            direction.Normalize();
            next = Position + direction * Mathf.Min(movement,
                Mathf.Max(.001f, Vector2.Distance(Position, waypoint)));
            if (game.CanTraverseGroundEnemy(Position, next, Radius * .42f))
            {
                usedCorridor = true;
                return true;
            }

            // A monster can be sitting on the outside shoulder of a tight bend.  Use the same
            // small steering fan as normal lane motion, but keep every candidate inside the
            // authored enemy corridor.  This prevents target/path oscillation without allowing
            // a shortcut across an island or cliff.
            foreach (var angle in RecoverySteeringAngles)
            {
                var radians = angle * Mathf.Deg2Rad;
                var steered = new Vector2(
                    direction.x * Mathf.Cos(radians) - direction.y * Mathf.Sin(radians),
                    direction.x * Mathf.Sin(radians) + direction.y * Mathf.Cos(radians));
                var candidate = Position + steered * Mathf.Min(movement,
                    Mathf.Max(.001f, Vector2.Distance(Position, waypoint)));
                if (!game.CanTraverseGroundEnemy(Position, candidate, Radius * .42f)) continue;
                direction = steered;
                next = candidate;
                usedCorridor = true;
                return true;
            }
            return false;
        }

        private bool TryFindCorridorAttackApproach(PlayerUnit target, out int approachIndex,
            out Vector2 approachPoint)
        {
            if (target == pursuitApproachTarget &&
                Vector2.SqrMagnitude(target.Position - pursuitApproachTargetPosition) <= .0064f &&
                Time.time < nextPursuitApproachScanAt)
            {
                approachIndex = cachedPursuitApproachIndex;
                approachPoint = cachedPursuitApproachPoint;
                return approachIndex >= 0;
            }
            approachIndex = -1;
            approachPoint = Position;
            if (target == null || CurrentPathCount <= 0) return false;
            var first = Mathf.Max(0, pathIndex - 2);
            var last = Mathf.Min(CurrentPathCount - 1, pathIndex + 72);
            var bestScore = float.MaxValue;
            var reach = Mathf.Max(.08f, AttackRange - .035f);
            for (var index = first; index <= last; index++)
            {
                // Centre and shallow shoulders cover broad road mouths without ever admitting
                // grass, river shelves or high-ground islands as pursuit destinations.
                foreach (var lateral in PursuitLateralOffsets)
                {
                    var candidate = CurrentPathTarget(index, lateral, 0f);
                    if (Vector2.Distance(candidate, target.Position) > reach ||
                        !game.IsWithinGroundEnemyRoadCorridor(candidate, Radius * .42f) ||
                        !game.IsWalkableWithClearance(candidate, Radius * .42f)) continue;
                    var backwards = Mathf.Max(0, pathIndex - index) * .12f;
                    var score = Vector2.Distance(Position, candidate) + backwards;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    approachIndex = index;
                    approachPoint = candidate;
                }
            }
            pursuitApproachTarget = target;
            pursuitApproachTargetPosition = target.Position;
            cachedPursuitApproachIndex = approachIndex;
            cachedPursuitApproachPoint = approachPoint;
            nextPursuitApproachScanAt = Time.time + .34f + (GetInstanceID() & 3) * .025f;
            return approachIndex >= 0;
        }

        private void TemporarilyRejectDetectedTarget()
        {
            if (detectedTarget == null) return;
            temporarilyIgnoredTarget = detectedTarget;
            ignoredTargetUntil = Time.time + .72f + (GetInstanceID() & 3) * .06f;
            detectedTarget = null;
            engagingDefender = false;
            velocity = Vector2.zero;
            unreachableTargetRejectCount++;
            // Do not face the rejected target. The normal lane update in the same frame assigns
            // the real movement vector, so the rendered octant cannot alternate by 180 degrees.
        }

        private void ReanchorToNearestForwardPath(bool force = false)
        {
            var count = CurrentPathCount;
            if (count <= 1 || (!force && Time.time < nextPathReanchorAt)) return;
            nextPathReanchorAt = Time.time + .18f;

            var last = count - 1;
            var nearestIndex = pathIndex;
            var nearestDistance = Vector2.Distance(Position, CurrentPathTarget(pathIndex, 0f, 0f));
            // Defender pursuit can pull a monster several metres away from its road. Resuming
            // the old sample made it walk backwards around an entire bend. Search only forward
            // and rejoin the furthest genuinely-near sample, preserving castleward progress.
            for (var index = Mathf.Max(0, pathIndex - 2);
                 index <= Mathf.Min(last, pathIndex + 64); index++)
            {
                var distance = Vector2.Distance(Position, CurrentPathTarget(index, 0f, 0f));
                if (distance > nearestDistance + .001f) continue;
                nearestDistance = distance;
                nearestIndex = index;
            }
            pathIndex = Mathf.Max(pathIndex, Mathf.Max(0, nearestIndex - 1));
        }

        private void UpdateVisualMotion()
        {
            var now = Time.time;
            var motionDelta = lastVisualUpdateAt <= 0f
                ? Mathf.Min(Time.deltaTime, .05f)
                : Mathf.Min(now - lastVisualUpdateAt, .05f);
            lastVisualUpdateAt = now;
            hitMotion = Mathf.Max(0f, hitMotion - motionDelta * 7.5f);
            var attackSpeed = Class switch
            {
                EnemyClass.Runner => 2.45f,
                EnemyClass.Brute or EnemyClass.Siege => 1.55f,
                EnemyClass.Piercer => 2.15f,
                _ => 1.9f
            };
            attackMotion = Mathf.Max(0f, attackMotion - motionDelta * attackSpeed);
            skillMotion = Mathf.Max(0f, skillMotion - motionDelta * skillMotionSpeed);
            var isMoving = velocity.sqrMagnitude > .01f && !attackingGate && !castingBossSkill;
            var gaitSpeed = Class switch
            {
                EnemyClass.Runner => 1.72f,
                EnemyClass.Brute or EnemyClass.Siege => .62f,
                EnemyClass.Skeleton => .9f,
                EnemyClass.Piercer => 1.18f,
                EnemyClass.Flyer => 1.42f,
                _ => 1f
            };
            var gait = Mathf.Sin(wriggle * gaitSpeed);
            var pulse = Mathf.Sin(wriggle * (.72f + gaitSpeed * .28f));
            var step = isMoving ? Mathf.Abs(gait) : 0f;
            var attackT = 1f - attackMotion;
            var lunge = attackMotion > 0f ? Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI) : 0f;
            var skillT = 1f - skillMotion;
            var skillPulse = skillMotion > 0f || castingBossSkill
                ? Mathf.Sin(Mathf.Clamp01(skillT) * Mathf.PI)
                : 0f;
            var articulatedWalkPhase = Mathf.Repeat(wriggle * gaitSpeed, Mathf.PI * 2f) /
                                       (Mathf.PI * 2f);
            var hitSquash = Mathf.Sin(hitMotion * Mathf.PI) * .18f;
            var visualWorld = Position;
            var visualDisplacement = visualWorld - lastVisualWorldPosition;
            lastVisualWorldPosition = visualWorld;
            // Moving bosses face their actual displacement, not whichever defender happened to
            // win the detection scan that frame. This removes distracting 180-degree snaps while
            // preserving deliberate target-facing during attacks and casts.
            var requestedVisualFacing = isMoving && visualDisplacement.sqrMagnitude > .0000005f
                ? visualDisplacement.normalized
                : isMoving && velocity.sqrMagnitude > .001f
                    ? velocity.normalized
                    : facingDirection.sqrMagnitude > .001f ? facingDirection.normalized : visualFacingDirection;
            if (requestedVisualFacing.sqrMagnitude > .001f)
                visualFacingDirection = requestedVisualFacing;
            var travel = visualFacingDirection.sqrMagnitude > .001f ? visualFacingDirection : Vector2.up;
            visualOctant = EightWayFacing.FromVector(travel);
            var poseForward = EightWayFacing.VectorFor(visualOctant);
            var diagonalFacing = EightWayFacing.IsDiagonal(visualOctant);
            // A chapter boss can use Brute combat numbers while retaining a Flyer visual and
            // navigation family (for example the tempest dragon).  Lift follows the resolved
            // mobility/visual property, never the damage-stat class.
            var baseLift = IsFlying
                ? IsBoss
                    ? .035f + Mathf.Sin(Time.time * 6.2f) * .008f
                    : .2f + Mathf.Sin(Time.time * 8.5f) * .075f
                : Class switch
                {
                    // Keep the wisp visibly airborne through the complete bob cycle. The former
                    // .075 minimum let its silhouette graze the road at unlucky frame samples.
                    EnemyClass.Wisp => .17f + Mathf.Sin(Time.time * 5.5f) * .045f,
                    EnemyClass.Runner => 0f,
                    EnemyClass.Skeleton or EnemyClass.Piercer => 0f,
                    EnemyClass.Brute or EnemyClass.Siege => 0f,
                    _ => 0f
                };
            if (IsBoss && !IsFlying) baseLift = 0f;
            if (directionalAnimation != null)
                animationFrames = directionalAnimation.FramesFor(visualOctant);
            if (animationFrames.Length >= 4)
            {
                var cinematicTimeline = animationFrames.Length >= 72;
                var completeTimeline = animationFrames.Length >= 40;
                var extendedTimeline = animationFrames.Length >= 16;
                var walkFrameCount = cinematicTimeline ? 24 : completeTimeline ? 12 :
                    extendedTimeline ? 8 : animationFrames.Length >= 7 ? 4 : 2;
                var walkPhase = Mathf.Repeat(wriggle * gaitSpeed, Mathf.PI * 2f) / (Mathf.PI * 2f);
                var strideTransfer = Mathf.Sin(walkPhase * Mathf.PI * 2f);
                var doubleSupport = Mathf.Abs(Mathf.Cos(walkPhase * Mathf.PI * 2f));
                var poseSide = new Vector2(-poseForward.y, poseForward.x);
                // The articulated legs own the readable stride.  Root motion is deliberately
                // restrained so a static boss painting no longer moves as one bouncing card.
                var authoredMotionBoost = usesStaticTimeline ? .55f : .42f;
                var hipShift = isMoving
                    ? poseSide * (strideTransfer * Radius * (diagonalFacing ? .046f : .038f) * authoredMotionBoost)
                    : Vector2.zero;
                var strideTravel = isMoving
                    ? poseForward * (strideTransfer * Radius *
                                     (Class == EnemyClass.Runner ? .045f :
                                         Class is EnemyClass.Brute or EnemyClass.Siege ? .036f :
                                         diagonalFacing ? .03f : .034f) * authoredMotionBoost)
                    : Vector2.zero;
                var footSettle = isMoving && !IsFlying
                    ? -poseForward * (doubleSupport * Radius * .009f)
                    : Vector2.zero;
                var groundedStepLift = 0f;
                var frameIndex = isMoving
                    ? Mathf.Clamp(Mathf.FloorToInt(walkPhase * walkFrameCount), 0, walkFrameCount - 1)
                    : 0;
                if (skillMotion > 0f || castingBossSkill)
                    frameIndex = cinematicTimeline
                        ? 48 + Mathf.Clamp(Mathf.FloorToInt(skillT * 24f), 0, 23)
                        : completeTimeline
                        ? 24 + Mathf.Clamp(Mathf.FloorToInt(skillT * 16f), 0, 15)
                        : extendedTimeline
                        ? 12 + Mathf.Clamp(Mathf.FloorToInt(skillT * 4f), 0, 3)
                        : animationFrames.Length >= 8
                        ? (skillT < .55f ? 6 : 7)
                        : animationFrames.Length >= 7 ? (skillT < .55f ? 5 : 6) : (skillT < .55f ? 2 : 3);
                else if (attackMotion > 0f)
                    frameIndex = cinematicTimeline
                        ? 24 + Mathf.Clamp(Mathf.FloorToInt(attackT * 24f), 0, 23)
                        : completeTimeline
                        ? 12 + Mathf.Clamp(Mathf.FloorToInt(attackT * 12f), 0, 11)
                        : extendedTimeline
                        ? 8 + Mathf.Clamp(Mathf.FloorToInt(attackT * 4f), 0, 3)
                        : animationFrames.Length >= 6
                        ? (attackT < .52f ? 4 : 5)
                        : (attackT < .52f ? 2 : 3);
                visibleAnimationFrame = Mathf.Clamp(frameIndex, 0, animationFrames.Length - 1);
                body.sprite = animationFrames[visibleAnimationFrame];

                // Staged wind-up -> contact -> recovery keeps the attack readable instead of
                // teleporting forward and immediately snapping back.
                var stagedLunge = attackMotion <= 0f ? 0f :
                    attackT < .32f ? -Mathf.SmoothStep(0f, 1f, attackT / .32f) * .34f :
                    attackT < .58f ? Mathf.Lerp(-.34f, 1f, Mathf.SmoothStep(0f, 1f, (attackT - .32f) / .26f)) :
                    Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (attackT - .58f) / .42f));
                var attackTravel = Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege or EnemyClass.Wisp
                    ? poseForward * stagedLunge * Radius * .11f
                    : poseForward * stagedLunge * Radius *
                      (Class is EnemyClass.Runner or EnemyClass.Piercer ? .32f : .22f);
                var classSquashX = Class is EnemyClass.Brute or EnemyClass.Siege ? step * .003f :
                    Class == EnemyClass.Runner ? step * .007f : step * .005f;
                var classSquashY = Class is EnemyClass.Brute or EnemyClass.Siege ? step * .002f :
                    Class == EnemyClass.Runner ? step * .006f : step * .004f;
                var attackBrace = attackMotion > 0f ? Mathf.Max(0f, stagedLunge) : 0f;
                var attackWindupCompression = attackMotion > 0f ? Mathf.Max(0f, -stagedLunge) : 0f;
                // Action readability comes from authored frames, weapon travel and impact VFX.
                // Large whole-card squash/stretch made the same monster change size between
                // directions and was especially obvious on bosses. Keep only a restrained,
                // sub-pixel breathing response for legacy non-directional silhouettes.
                var castScale = 1f + skillPulse *
                    (Class is EnemyClass.Wisp or EnemyClass.Flyer ? .032f : .022f);
                body.transform.localScale = new Vector3(
                    bodyBaseScale.x * (castScale + hitSquash * .12f + classSquashX +
                                       attackBrace * .024f - attackWindupCompression * .015f) * bossMorphScale.x,
                    bodyBaseScale.y * (castScale - hitSquash * .08f - classSquashY -
                                       attackBrace * .017f + attackWindupCompression * .012f) * bossMorphScale.y, 1f);
                body.transform.localPosition = new Vector3(
                    attackTravel.x + hipShift.x + strideTravel.x + footSettle.x,
                    baseLift + attackTravel.y + hipShift.y + strideTravel.y + footSettle.y + groundedStepLift +
                    (Class == EnemyClass.Brute ? skillPulse * .07f : 0f), -.15f);
                var locomotionLean = Class switch
                {
                    EnemyClass.Runner => -poseForward.x * (isMoving ? 6.2f : 0f),
                    EnemyClass.Piercer => -poseForward.x * (isMoving ? 4.1f : 0f),
                    EnemyClass.Flyer => -poseForward.x * (isMoving ? 7.4f : 0f),
                    EnemyClass.Brute or EnemyClass.Siege => -poseForward.x * (isMoving ? 1.6f : 0f),
                    _ => -poseForward.x * (isMoving ? 2.8f : 0f)
                };
                var gaitLean = isMoving ? -strideTransfer * (diagonalFacing ? 1.05f : 1.28f) : 0f;
                var attackFacingSign = Mathf.Abs(poseForward.x) > .2f
                    ? -poseForward.x
                    : poseForward.y >= 0f ? -.58f : .58f;
                var attackLean = attackMotion > 0f ? attackFacingSign * stagedLunge * 8.5f : 0f;
                var castLean = skillMotion > 0f || castingBossSkill
                    ? Mathf.Sin(skillT * Mathf.PI * 2f) *
                      (Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp ? 8f : 4f)
                    : 0f;
                body.transform.localEulerAngles = new Vector3(0f, 0f,
                    locomotionLean + gaitLean + attackLean + castLean +
                    (Class == EnemyClass.Skeleton ? lunge * 6f : 0f));
                if (directionalAnimation != null)
                {
                    // Authored boss sheets own locomotion, anticipation, contact and recovery.
                    // Never stack the legacy root-scale/rotation synthesizer on top of them: it
                    // reintroduced the whole-body bob, stretch and skating the artwork replaced.
                    var directionScale = bossDirectionalScaleCorrections[(int)visualOctant];
                    var poseScale = 1f;
                    if (body.sprite != null)
                    {
                        // Attack and skill paintings can occupy far more of the source canvas
                        // than their walk pose. Keep the actor's measured opaque height stable
                        // inside each direction instead of letting one crouch, raised weapon or
                        // connected spell shape make the whole boss suddenly double in size.
                        // This is a renderer correction only; combat radius and VFX scale remain
                        // unchanged. Extremely thin/corrupt frames are rejected by QA before this
                        // correction can hide them.
                        var currentOpaqueHeight = OpaqueWorldHeight(body.sprite);
                        var referenceOpaqueHeight =
                            bossDirectionalReferenceOpaqueHeights[(int)visualOctant];
                        if (currentOpaqueHeight > .01f && referenceOpaqueHeight > .01f)
                            poseScale = Mathf.Clamp(referenceOpaqueHeight / currentOpaqueHeight,
                                IsBoss ? .30f : .24f, IsBoss ? 1.72f : 3.25f);
                    }
                    body.transform.localScale = new Vector3(
                        bodyBaseScale.x * bossMorphScale.x * directionScale * poseScale,
                        bodyBaseScale.y * bossMorphScale.y * directionScale * poseScale, 1f);
                    // Whole-body paintings still receive restrained sub-frame weight transfer.
                    // It adds readable anticipation/recovery between sparse authored cells while
                    // grounding below removes any vertical skating or hover.
                    var authoredStrideX = isMoving ? strideTransfer * Radius * .024f : 0f;
                    var authoredTravel = attackMotion > 0f ? attackTravel * .24f : Vector2.zero;
                    body.transform.localPosition = new Vector3(
                        poseSide.x * authoredStrideX + authoredTravel.x,
                        baseLift + authoredTravel.y, -.15f);
                    body.transform.localEulerAngles = new Vector3(0f, 0f,
                        (isMoving ? gaitLean * .24f : 0f) + attackLean * .16f + castLean * .12f);
                }
                // Enemy side sheets, like the player sheets, are painted facing screen-left.
                body.flipX = ShouldFlipDirectionalBoss(visualOctant);
                body.color = Color.Lerp(bodyBaseColor, Color.white, Mathf.Sin(hitMotion * Mathf.PI) * .85f);
            }
            else
            {
                var heavy = Class is EnemyClass.Brute or EnemyClass.Siege ? 1.45f : 1f;
                var pulseX = IsBoss ? .012f : .052f;
                var pulseY = IsBoss ? .009f : .046f;
                body.transform.localScale = new Vector3(
                    bodyBaseScale.x * (1f + pulse * pulseX / heavy + hitSquash) * bossMorphScale.x,
                    bodyBaseScale.y * (1f - pulse * pulseY / heavy - hitSquash * .55f) * bossMorphScale.y, 1f);
                body.transform.localPosition = new Vector3(
                    poseForward.x * lunge * .12f,
                    baseLift + poseForward.y * lunge * .11f, -.15f);
                body.transform.localEulerAngles = new Vector3(0f, 0f,
                    -poseForward.x * (isMoving ? (IsBoss ? 1.4f : 4.5f) : 0f) +
                    pulse * (IsBoss ? .32f : 1.2f) + (Class == EnemyClass.Skeleton ? lunge * 8f : 0f));
                body.flipX = EightWayFacing.IsRight(visualOctant);
                body.color = Color.Lerp(bodyBaseColor, Color.white, Mathf.Sin(hitMotion * Mathf.PI) * .85f);
            }
            var showingBack = EightWayFacing.IsBack(visualOctant);
            if (IsBoss && directionalAnimation == null && bossFrontSprite != null)
                body.sprite = showingBack && bossBackSprite != null ? bossBackSprite : bossFrontSprite;
            else if (!IsBoss && directionalAnimation == null && showingBack && normalBackSprite != null)
                body.sprite = normalBackSprite;
            NormalizeCurrentSpriteHeight();
            var intentionalLift = IsFlying || Class == EnemyClass.Wisp
                ? baseLift
                : !IsBoss && !isMoving && (skillMotion > 0f || castingBossSkill) &&
                  (Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege)
                    ? skillPulse * Radius * .055f
                    : 0f;
            AnchorCurrentSpriteToGround(intentionalLift);
            if (limbRig != null)
            {
                var actionActive = attackMotion > 0f || skillMotion > 0f || castingBossSkill;
                var actionProgress = skillMotion > 0f || castingBossSkill ? skillT : attackT;
                var casting = skillMotion > 0f || castingBossSkill ||
                              Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege or EnemyClass.Wisp;
                limbRig.Animate(body.sprite, body.color, body.flipX, visualRig == null,
                    isMoving, articulatedWalkPhase, actionActive, actionProgress, casting,
                    Class is EnemyClass.Brute or EnemyClass.Siege ? 1.3f :
                    Class is EnemyClass.Runner or EnemyClass.Piercer ? .68f : IsBoss ? 1.18f : .9f);
            }
            foreach (var part in roleSilhouette)
                if (part != null) part.enabled = IsBoss || !showingBack || IsRanged;
            if (roleSilhouetteRoot != null)
            {
                // Equipment silhouettes are part of the body, not HUD decorations.  They follow
                // the exact grounded gait/attack transform so a staff or shield never floats a
                // frame behind the monster while the painted body lunges.
                roleSilhouetteRoot.localPosition = new Vector3(body.transform.localPosition.x,
                    body.transform.localPosition.y, 0f);
                roleSilhouetteRoot.localEulerAngles = body.transform.localEulerAngles;
                roleSilhouetteRoot.localScale = new Vector3(body.flipX ? -1f : 1f, 1f, 1f);
            }
            UpdateMotionAccents(gait, lunge, isMoving);
            if (bossAura != null)
            {
                var auraPulse = 1f + Mathf.Sin(Time.time * (enraged ? 7f : 3.6f)) * .11f;
                bossAura.transform.localScale = new Vector3(Radius * 3.6f * auraPulse,
                    Radius * .9f * auraPulse, 1f);
                bossAura.transform.Rotate(0f, 0f, Time.deltaTime * (enraged ? 92f : 46f));
                bossAura.color = enraged
                    ? new Color(1f, .18f, .08f, .22f + Mathf.Sin(Time.time * 8f) * .035f)
                    : new Color(1f, .67f, .18f, .14f + Mathf.Sin(Time.time * 3.4f) * .025f);
            }
            if (barrierAura != null)
            {
                barrierAura.enabled = barrier > 0f;
                if (barrierAura.enabled)
                {
                    var barrierPulse = 1f + Mathf.Sin(Time.time * 5f) * .07f;
                    barrierAura.transform.localScale = new Vector3(Radius * 2.82f * barrierPulse,
                        Radius * .74f * barrierPulse, 1f);
                    barrierAura.transform.Rotate(0f, 0f, -Time.deltaTime * 72f);
                }
            }
            if (bossRankLabel != null)
            {
                var labelColor = bossRankLabel.color;
                labelColor.a = .82f + Mathf.Sin(Time.time * 5.5f) * .18f;
                bossRankLabel.color = labelColor;
                bossRankLabel.transform.localPosition = new Vector3(0f,
                    Radius * 5.1f + Mathf.Sin(Time.time * 3.2f) * .035f, -.28f);
                for (var i = 0; i < bossCrownProngs.Length; i++)
                {
                    if (bossCrownProngs[i] == null) continue;
                    var crownColor = bossCrownProngs[i].color;
                    crownColor.a = .72f + Mathf.Sin(Time.time * 7f + i * .7f) * .28f;
                    bossCrownProngs[i].color = crownColor;
                }
            }
            if (shadow != null)
            {
                var compression = 1f - step * .12f - lunge * .08f;
                shadow.transform.localScale = new Vector3(shadowBaseScale.x * (2f - compression),
                    shadowBaseScale.y * compression, 1f);
                var shadowColor = shadow.color;
                shadowColor.a = IsFlying || Class == EnemyClass.Wisp
                    ? (IsBoss ? .43f : .18f) * compression
                    : (IsBoss ? .48f : .36f) * compression;
                shadow.color = shadowColor;
            }
            if (visualRig != null)
                visualRig.Animate(poseForward, isMoving, attackMotion > 0f ? attackT : 0f,
                    hitMotion, IsBoss, IsFlying);
        }

        private bool ShouldFlipDirectionalBoss(FacingOctant octant)
        {
            if (directionalAnimation == null) return EightWayFacing.IsRight(octant);
            var horizontal = octant is FacingOctant.SouthWest or FacingOctant.West or
                FacingOctant.NorthWest or FacingOctant.NorthEast or FacingOctant.East or
                FacingOctant.SouthEast;
            if (!horizontal) return false;
            var movingRight = EightWayFacing.IsRight(octant);
            return directionalAnimation.SideFacesRight ? !movingRight : movingRight;
        }

        private static float OpaqueHeightRatio(Sprite front, Sprite back)
        {
            if (front == null || back == null) return 1f;
            var frontHeight = OpaqueWorldHeight(front);
            var backHeight = OpaqueWorldHeight(back);
            if (frontHeight <= .01f || backHeight <= .01f) return 1f;
            return Mathf.Clamp(frontHeight / backHeight, .82f, 1.22f);
        }

        private static Sprite[] BuildStaticExtendedTimeline(Sprite sprite)
        {
            var timeline = new Sprite[72];
            for (var i = 0; i < timeline.Length; i++) timeline[i] = sprite;
            return timeline;
        }

        private static float RobustReferenceOpaqueHeight(Sprite fallback, Sprite[] timeline,
            DirectionalAnimationSet directional)
        {
            var heights = new List<float>(384);
            void Add(Sprite sprite)
            {
                if (sprite == null) return;
                var height = OpaqueWorldHeight(sprite);
                if (height > .01f && !float.IsNaN(height) && !float.IsInfinity(height)) heights.Add(height);
            }
            if (timeline != null)
                foreach (var sprite in timeline) Add(sprite);
            if (directional != null)
            {
                foreach (var sprite in directional.Down) Add(sprite);
                foreach (var sprite in directional.DownDiagonal) Add(sprite);
                foreach (var sprite in directional.Side) Add(sprite);
                foreach (var sprite in directional.UpDiagonal) Add(sprite);
                foreach (var sprite in directional.Up) Add(sprite);
            }
            if (heights.Count == 0) return Mathf.Max(.01f, OpaqueWorldHeight(fallback));
            heights.Sort();
            // A median reference is immune to one torso-only or neighbouring composite frame.
            // It also preserves intentional boss size while all five directions normalize to one
            // perceived height.
            var middle = heights.Count / 2;
            return heights.Count % 2 == 0
                ? (heights[middle - 1] + heights[middle]) * .5f
                : heights[middle];
        }

        private static float RobustWalkOpaqueHeight(Sprite[] frames)
        {
            if (frames == null) return 0f;
            var heights = new List<float>(24);
            for (var index = 0; index < Mathf.Min(24, frames.Length); index++)
            {
                var height = OpaqueWorldHeight(frames[index]);
                if (height > .01f && !float.IsNaN(height) && !float.IsInfinity(height))
                    heights.Add(height);
            }
            if (heights.Count == 0) return 0f;
            heights.Sort();
            var middle = heights.Count / 2;
            return heights.Count % 2 == 0
                ? (heights[middle - 1] + heights[middle]) * .5f
                : heights[middle];
        }

        private static float BossVisualHeightPerRadius(string bossId) => bossId switch
        {
            "jelly_king" => 3.90f,
            "lich" => 3.25f,
            "goblin_warchief" => 3.45f,
            "mountain_titan" => 4.05f,
            "ancient_ent" => 4.15f,
            "iron_colossus" => 4.00f,
            "crimson_tyrant" => 3.45f,
            "wraith_king" => 3.60f,
            "tempest_dragon" => 3.90f,
            "abyss_sovereign" => 4.25f,
            _ => 3.65f
        };

        private void NormalizeCurrentSpriteHeight()
        {
            if (body == null || body.sprite == null || visualReferenceOpaqueHeight <= .01f) return;
            // Directional combat atlases share one pixels-per-unit contract. Their attack and
            // skill frames intentionally contain arrows, flames, capes and spell silhouettes
            // whose opaque bounds are taller than the actor. Scaling the whole frame by those
            // bounds shrank the body during one action and enlarged it on the next. Preserve the
            // authored actor scale; extraction and transparent gutters own clipping safety.
            if (directionalAnimation != null)
            {
                currentSpriteHeightCorrection = 1f;
                return;
            }
            var currentHeight = OpaqueWorldHeight(body.sprite);
            if (currentHeight <= .01f) return;
            // Back sheets from several monster families have much tighter crops than their
            // animated fronts. A wider guard range is intentional: the target is a measured
            // identical opaque world height, while combat radius remains unchanged.
            // Some dedicated rear portraits use a deliberately looser crop than the roster
            // sheet.  The previous .55 lower clamp left the jelly mage 32% larger from behind.
            // Keep a broad corruption guard, but normalize all valid authored silhouettes to
            // the exact same opaque world height so turning never changes perceived size.
            currentSpriteHeightCorrection = Mathf.Clamp(visualReferenceOpaqueHeight / currentHeight,
                .38f, 2.65f);
            var scale = body.transform.localScale;
            body.transform.localScale = new Vector3(scale.x * currentSpriteHeightCorrection,
                scale.y * currentSpriteHeightCorrection, scale.z);
        }

        private void CaptureVisualGroundLine()
        {
            if (body == null || body.sprite == null) return;
            var foot = OpaqueFootAnchor(body.sprite);
            // Boss contact is authored against the arena floor even for the one winged boss.
            // Its tiny intentional hover is applied later; inheriting the sprite pivot here and
            // adding lift again was what made the entire boss roster look detached from shadows.
            visualGroundLineY = IsBoss ? 0f : IsFlying || Class == EnemyClass.Wisp
                ? body.transform.localPosition.y + foot.y * Mathf.Abs(bodyBaseScale.y)
                : 0f;
        }

        private void AnchorCurrentSpriteToGround(float intentionalLift)
        {
            if (body == null || body.sprite == null) return;
            var foot = OpaqueFootAnchor(body.sprite);
            if (body.flipX) foot.x = -foot.x;
            var scale = body.transform.localScale;
            var scaled = new Vector2(foot.x * Mathf.Abs(scale.x), foot.y * Mathf.Abs(scale.y));
            var angle = Mathf.DeltaAngle(0f, body.transform.localEulerAngles.z) * Mathf.Deg2Rad;
            var rotatedFootY = scaled.x * Mathf.Sin(angle) + scaled.y * Mathf.Cos(angle);
            var position = body.transform.localPosition;
            position.y = visualGroundLineY - rotatedFootY + intentionalLift;
            body.transform.localPosition = position;
        }

        private static float OpaqueWorldHeight(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return 0f;
            if (OpaqueHeightCache.TryGetValue(sprite, out var cached)) return cached;
            OpaqueMetricCacheMisses++;
            try
            {
                var rect = sprite.textureRect;
                var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, sprite.texture.width - 1);
                var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, sprite.texture.width);
                var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, sprite.texture.height - 1);
                var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, sprite.texture.height);
                var pixels = sprite.texture.GetPixels32();
                var minY = top;
                var maxY = bottom - 1;
                for (var y = bottom; y < top; y++)
                for (var x = left; x < right; x++)
                {
                    if (pixels[y * sprite.texture.width + x].a <= 12) continue;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                cached = maxY < minY ? 0f : (maxY - minY + 1f) / sprite.pixelsPerUnit;
                OpaqueHeightCache[sprite] = cached;
                return cached;
            }
            catch (System.Exception)
            {
                cached = sprite.bounds.size.y;
                OpaqueHeightCache[sprite] = cached;
                return cached;
            }
        }

        private static Vector2 OpaqueFootAnchor(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f) return Vector2.zero;
            if (OpaqueFootAnchorCache.TryGetValue(sprite, out var cached)) return cached;
            try
            {
                var rect = sprite.textureRect;
                var left = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, sprite.texture.width - 1);
                var right = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), left + 1, sprite.texture.width);
                var bottom = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, sprite.texture.height - 1);
                var top = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), bottom + 1, sprite.texture.height);
                var pixels = sprite.texture.GetPixels32();
                var opaqueMinX = right;
                var opaqueMaxX = left;
                for (var y = bottom; y < top; y++)
                for (var x = left; x < right; x++)
                {
                    if (pixels[y * sprite.texture.width + x].a <= 12) continue;
                    opaqueMinX = Mathf.Min(opaqueMinX, x);
                    opaqueMaxX = Mathf.Max(opaqueMaxX, x);
                }
                if (opaqueMaxX < opaqueMinX) return Vector2.zero;
                var center = (opaqueMinX + opaqueMaxX) * .5f;
                var halfBodyWidth = Mathf.Max(2f, (opaqueMaxX - opaqueMinX + 1f) * .28f);
                var centralLeft = Mathf.Max(left, Mathf.FloorToInt(center - halfBodyWidth));
                var centralRight = Mathf.Min(right, Mathf.CeilToInt(center + halfBodyWidth));
                var footY = top;
                for (var y = bottom; y < top && footY == top; y++)
                for (var x = centralLeft; x < centralRight; x++)
                    if (pixels[y * sprite.texture.width + x].a > 12) { footY = y; break; }
                if (footY == top) footY = bottom;
                var xSum = 0f;
                var count = 0;
                for (var y = footY; y <= Mathf.Min(top - 1, footY + 2); y++)
                for (var x = centralLeft; x < centralRight; x++)
                {
                    if (pixels[y * sprite.texture.width + x].a <= 12) continue;
                    xSum += x + .5f;
                    count++;
                }
                var anchorX = count > 0 ? xSum / count : center;
                cached = new Vector2(
                    (anchorX - rect.xMin - sprite.pivot.x) / sprite.pixelsPerUnit,
                    (footY + .5f - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit);
                OpaqueFootAnchorCache[sprite] = cached;
                return cached;
            }
            catch (System.Exception)
            {
                return new Vector2(0f, sprite.bounds.min.y);
            }
        }

        private void UpdateMotionAccents(float gait, float lunge, bool isMoving)
        {
            if (motionAccentA != null &&
                Class is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Siege or EnemyClass.Wisp or
                    EnemyClass.Silencer or EnemyClass.Cursebinder)
            {
                var orbitSpeed = Class switch
                {
                    EnemyClass.Siege => 3.2f,
                    EnemyClass.Wisp => 7.6f,
                    EnemyClass.Shaman => 4.7f,
                    EnemyClass.Silencer => 4.8f,
                    EnemyClass.Cursebinder => 4.35f,
                    _ => 5.8f
                };
                var orbit = Time.time * orbitSpeed;
                var radius = Radius * (Class == EnemyClass.Siege ? 1.45f :
                    Class == EnemyClass.Wisp ? 1.55f : 1.18f);
                motionAccentA.transform.localPosition = new Vector3(Mathf.Cos(orbit) * radius,
                    .06f + Mathf.Sin(orbit) * radius * .55f, -.19f);
                motionAccentB.transform.localPosition = new Vector3(Mathf.Cos(orbit + Mathf.PI) * radius * .76f,
                    .05f + Mathf.Sin(orbit + Mathf.PI) * radius * .42f, -.2f);
                var castFlare = Mathf.Max(lunge, skillMotion > 0f ? 1f - skillMotion : 0f);
                motionAccentA.transform.localScale = Vector3.one * (Radius * (.30f + castFlare * .24f));
                motionAccentB.transform.localScale = Vector3.one * (Radius * (.17f + castFlare * .17f));
            }
            else if (motionAccentA != null && Class is EnemyClass.Runner or EnemyClass.Piercer or EnemyClass.Flyer)
            {
                var flare = Mathf.Max(lunge, skillMotion > 0f ? Mathf.Sin((1f - skillMotion) * Mathf.PI) : 0f);
                motionAccentA.transform.localPosition = new Vector3(
                    Class == EnemyClass.Flyer ? -.06f : .08f,
                    Class == EnemyClass.Flyer ? .1f : -.02f, -.18f);
                motionAccentA.transform.localEulerAngles = new Vector3(0f, 0f,
                    Class == EnemyClass.Piercer ? -18f : Class == EnemyClass.Runner ? 12f : Time.time * 160f);
                motionAccentA.transform.localScale = new Vector3(
                    Radius * (.25f + flare * 1.8f), Radius * (.12f + flare * .3f), 1f);
                var color = motionAccentA.color;
                color.a = .05f + flare * .8f;
                motionAccentA.color = color;
            }
            else if (motionAccentA != null)
            {
                var flare = .34f + Mathf.Max(0f, gait) * .18f + lunge * .44f;
                motionAccentA.transform.localPosition = new Vector3(0f, Radius * .05f, .05f);
                motionAccentA.transform.localScale = Vector3.one * (Radius * (1.12f + flare));
                var color = motionAccentA.color;
                color.a = IsBoss ? .48f + flare * .25f : .22f + flare * .35f;
                motionAccentA.color = color;
            }
        }

        private IEnumerator MeleeSkillRoutine(PlayerUnit target)
        {
            if (Class == EnemyClass.Sunderer)
            {
                yield return ArmorRendSkillRoutine(target);
                yield break;
            }
            castingBossSkill = true;
            velocity = Vector2.zero;
            if (target != null) FaceVisualTarget(target.Position);
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            var radius = Class switch
            {
                EnemyClass.Brute => 1.02f,
                EnemyClass.Runner => .62f,
                EnemyClass.Piercer => .66f,
                EnemyClass.Melee => .78f,
                _ => .72f
            };
            var windup = Class switch
            {
                EnemyClass.Brute => .48f,
                EnemyClass.Runner => .24f,
                EnemyClass.Piercer => .31f,
                EnemyClass.Melee => .38f,
                _ => .34f
            };
            BeginSkillVisual(windup);
            game.SpawnBossTelegraph(Position, radius, windup);
            yield return new WaitForSeconds(windup);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle)
            {
                castingBossSkill = false;
                yield break;
            }
            var multiplier = Class switch
            {
                EnemyClass.Brute => 1.92f,
                EnemyClass.Runner => 1.52f,
                EnemyClass.Piercer => 1.7f,
                EnemyClass.Melee => 1.28f,
                _ => 1.36f
            };
            var damageType = Class == EnemyClass.Piercer ? DamageType.Pure : DamageType.Physical;
            game.DamageDefenders(Position, radius, attackPower * multiplier, damageType, false,
                PhysicalPenetration, MagicPenetration);
            var impactColor = Class switch
            {
                EnemyClass.Brute => new Color(1f, .42f, .16f),
                EnemyClass.Runner => new Color(.36f, 1f, .38f),
                EnemyClass.Piercer => new Color(1f, .18f, .1f),
                EnemyClass.Melee => new Color(.72f, .32f, 1f),
                _ => new Color(.88f, .82f, .62f)
            };
            game.SpawnCombatImpact(Position, UnitArchetype.Melee, impactColor, radius);
            if (Class == EnemyClass.Brute) game.SpawnEnemySlam(Position, radius, impactColor);
            var meleeImpact = target != null && target.IsAlive ? target.HitPoint : Position + Vector2.up * .25f;
            game.SpawnEnemyClassEffect(AttackOriginFor(meleeImpact), meleeImpact, Class, true);
            nextSkillAt = Time.time + Random.Range(5.4f, 6.8f);
            lastAttackAt = Time.time;
            castingBossSkill = false;
        }

        private IEnumerator ArmorRendSkillRoutine(PlayerUnit target)
        {
            castingBossSkill = true;
            velocity = Vector2.zero;
            if (target != null) FaceVisualTarget(target.Position);
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            const float windup = .46f;
            BeginSkillVisual(windup);
            var point = target != null ? target.Position : Position;
            game.SpawnBossTelegraph(point, .58f, windup);
            yield return new WaitForSeconds(windup);
            var contactDistance = target != null ? Vector2.Distance(Position, target.Position) : -1f;
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle ||
                target == null || !target.IsAlive ||
                contactDistance > AttackRange + target.Radius * .58f)
            {
                lastSpecialQaState = $"aborted:enemy={IsAlive}:game={game != null}:phase={game?.Phase}:" +
                                     $"target={target != null}:{target?.IsAlive}:distance={contactDistance:0.000}:" +
                                     $"limit={AttackRange + (target != null ? target.Radius * .58f : 0f):0.000}";
                castingBossSkill = false;
                yield break;
            }
            FaceVisualTarget(target.Position);
            target.ReactToContact(Position, .12f);
            target.TakeDamage(attackPower * 1.18f, DamageType.Physical, true,
                PhysicalPenetration, MagicPenetration);
            target.ApplyArmorShred(9f + game.Round * .30f, 5.0f);
            lastSpecialQaState = $"applied:{contactDistance:0.000}:{9f + game.Round * .30f:0.0}";
            game.SpawnCombatImpact(target.HitPoint, UnitArchetype.Melee,
                new Color(1f, .58f, .12f), .84f, target.Position - Position, CombatVfxTier.Skill);
            game.SpawnEnemyClassEffect(AttackOriginFor(target.Position), target.HitPoint, Class, true);
            nextSkillAt = Time.time + Random.Range(7.2f, 8.6f);
            lastAttackAt = Time.time;
            castingBossSkill = false;
        }

        private IEnumerator MageSkillRoutine(PlayerUnit target)
        {
            if (Class is EnemyClass.Silencer or EnemyClass.Cursebinder)
            {
                yield return DebuffSkillRoutine(target);
                yield break;
            }
            castingBossSkill = true;
            velocity = Vector2.zero;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            var targetPosition = target != null ? target.Position : Position;
            FaceVisualTarget(targetPosition);
            var radius = Class switch
            {
                EnemyClass.Siege => 1.16f,
                EnemyClass.Shaman => 1.02f,
                EnemyClass.Wisp => .74f,
                _ => .86f
            };
            var windup = Class switch
            {
                EnemyClass.Siege => .78f,
                EnemyClass.Shaman => .64f,
                EnemyClass.Wisp => .42f,
                _ => .56f
            };
            BeginSkillVisual(windup);
            game.SpawnBossTelegraph(targetPosition, radius, windup);
            yield return new WaitForSeconds(windup);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle)
            {
                castingBossSkill = false;
                yield break;
            }
            if (target != null && target.IsAlive)
                game.LaunchEnemyMagic(target, AttackOriginFor(target.Position),
                    magicPower * (Class == EnemyClass.Siege ? 1.42f : Class == EnemyClass.Wisp ? 1.34f : 1.22f),
                    true, Class, PhysicalPenetration, MagicPenetration);
            game.DamageDefenders(targetPosition, radius,
                magicPower * (Class == EnemyClass.Siege ? .76f : Class == EnemyClass.Shaman ? .68f : .62f),
                DamageType.Magic, true, PhysicalPenetration, MagicPenetration);
            var skillColor = Class switch
            {
                EnemyClass.Shaman => new Color(.18f, 1f, .62f),
                EnemyClass.Siege => new Color(.72f, .28f, 1f),
                EnemyClass.Wisp => new Color(.22f, .92f, 1f),
                _ => new Color(.52f, .42f, 1f)
            };
            game.SpawnCombatImpact(targetPosition, UnitArchetype.SingleMage, skillColor, radius * 1.15f);
            game.SpawnEnemyClassEffect(AttackOriginFor(targetPosition), targetPosition, Class, true);
            if (Class == EnemyClass.Siege) game.SpawnEnemySlam(targetPosition, radius, skillColor);
            nextSkillAt = Time.time + Random.Range(6.2f, 7.8f);
            lastAttackAt = Time.time;
            castingBossSkill = false;
        }

        private IEnumerator DebuffSkillRoutine(PlayerUnit target)
        {
            castingBossSkill = true;
            velocity = Vector2.zero;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            var silence = Class == EnemyClass.Silencer;
            var targetPosition = target != null ? target.Position : Position;
            FaceVisualTarget(targetPosition);
            var radius = silence ? .78f : .82f;
            var windup = silence ? .68f : .70f;
            BeginSkillVisual(windup);
            game.SpawnBossTelegraph(targetPosition, radius, windup);
            yield return new WaitForSeconds(windup);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle)
            {
                castingBossSkill = false;
                yield break;
            }

            if (target != null && target.IsAlive)
            {
                FaceVisualTarget(target.Position);
                game.LaunchEnemyMagic(target, AttackOriginFor(target.Position),
                    magicPower * (silence ? .42f : .66f), true, Class,
                    PhysicalPenetration, MagicPenetration);
                if (silence)
                    target.ApplyMagicSeal(2.1f + Mathf.Min(.8f, game.Round * .012f));
                else
                    target.ApplyResistanceCurse(10f + game.Round * .42f, 5.2f);
            }

            var color = silence ? new Color(.28f, .88f, 1f) : new Color(.23f, .86f, .96f);
            game.SpawnCombatImpact(targetPosition,
                UnitArchetype.SingleMage,
                color, radius * 1.42f, (targetPosition - Position).normalized, CombatVfxTier.Skill);
            game.SpawnEnemyClassEffect(AttackOriginFor(targetPosition), targetPosition, Class, true);
            nextSkillAt = Time.time + (silence ? Random.Range(8.2f, 9.6f) : Random.Range(7.6f, 9.0f));
            lastAttackAt = Time.time;
            castingBossSkill = false;
        }

        private IEnumerator FlyingSkillRoutine(PlayerUnit target)
        {
            castingBossSkill = true;
            velocity = Vector2.zero;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            var targetPosition = target != null ? target.Position : Position;
            FaceVisualTarget(targetPosition);
            const float windup = .36f;
            BeginSkillVisual(windup);
            game.SpawnBossTelegraph(targetPosition, .78f, windup);
            yield return new WaitForSeconds(windup);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle)
            {
                castingBossSkill = false;
                yield break;
            }
            game.DamageDefenders(targetPosition, .8f, attackPower * 1.45f, DamageType.Physical, true,
                PhysicalPenetration, MagicPenetration);
            game.SpawnEnemyClassEffect(AttackOriginFor(targetPosition), targetPosition, Class, true);
            game.SpawnCombatImpact(targetPosition, UnitArchetype.Archer, new Color(.76f, .34f, 1f), .92f);
            nextSkillAt = Time.time + Random.Range(5.8f, 7.1f);
            lastAttackAt = Time.time;
            castingBossSkill = false;
        }

        private bool HasMeleeSignatureSkill => Class is EnemyClass.Melee or EnemyClass.Skeleton or
            EnemyClass.Runner or EnemyClass.Brute or EnemyClass.Piercer or EnemyClass.Sunderer;

        private void BeginSkillVisual(float duration)
        {
            attackMotion = 0f;
            skillMotion = 1f;
            skillMotionSpeed = 1f / Mathf.Max(.18f, duration);
        }

        private IEnumerator RoyalSplitMorphRoutine(float duration)
        {
            bossMorphScale = Vector2.one;
            var compressDuration = duration * .48f;
            for (var elapsed = 0f; elapsed < compressDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, compressDuration));
                bossMorphScale = new Vector2(Mathf.Lerp(1f, 1.34f, t), Mathf.Lerp(1f, .54f, t));
                yield return null;
            }
            var expandDuration = duration * .34f;
            for (var elapsed = 0f; elapsed < expandDuration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Sin(Mathf.Clamp01(elapsed / Mathf.Max(.01f, expandDuration)) * Mathf.PI);
                bossMorphScale = new Vector2(1.34f + t * .54f, .54f + t * .86f);
                yield return null;
            }
            bossMorphScale = Vector2.one;
        }

        private IEnumerator BossSkillRoutine()
        {
            castingBossSkill = true;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            bossSkillLabel = GameLocalization.Text("성벽 분쇄 준비", "PREPARING GATE BREAKER");
            game.ShowBossWarning(GameLocalization.Text("보스 스킬 · 성벽 분쇄", "BOSS SKILL · GATE BREAKER"));
            game.SpawnBossTelegraph(Position, 1.85f, 1.05f);
            yield return new WaitForSeconds(1.05f);
            if (!IsAlive || game == null || game.Phase != GamePhase.Battle) yield break;
            attackMotion = 1f;
            game.BossGroundSlam(Position, 1.85f, 38f + game.Round * 3f,
                PhysicalPenetration, MagicPenetration);
            barrier = Mathf.Min(maxHealth * .16f, barrier + maxHealth * .06f);
            bossSkillLabel = barrier > 0f
                ? GameLocalization.Text("마력 방벽", "ARCANE BARRIER")
                : GameLocalization.Text("재사용 대기", "COOLDOWN");
            nextBossSkillAt = Time.time + (enraged ? 5.8f : 7.4f);
            castingBossSkill = false;
        }

        private IEnumerator BossSkillRoutineV2()
        {
            castingBossSkill = true;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Skill);
            lastBossSkillId = BossSkillIdForClass(VisualClass);
            bossSkillCastCount++;

            switch (lastBossSkillId)
            {
                case "slime_royal_split":
                {
                    bossSkillLabel = GameLocalization.Text("왕의 분열", "ROYAL DIVISION");
                    game.ShowBossWarning(GameLocalization.Text("젤리 왕 · 왕의 분열", "JELLY KING · ROYAL DIVISION"));
                    const float splitWindup = .86f;
                    BeginSkillVisual(splitWindup);
                    StartCoroutine(RoyalSplitMorphRoutine(splitWindup));
                    game.SpawnBossTelegraph(Position, 1.72f, splitWindup);
                    game.SpawnBossSignatureEffect(Position, new Color(.64f, .24f, 1f), 1.22f, 0);
                    yield return new WaitForSeconds(splitWindup * .55f);
                    if (!CanFinishBossSkill()) yield break;
                    game.SpawnBossSignatureEffect(Position, new Color(.92f, .42f, 1f), 1.58f, 0);
                    yield return new WaitForSeconds(splitWindup * .45f);
                    if (!CanFinishBossSkill()) yield break;
                    attackMotion = 1f;
                    game.SpawnBossMinions(this, EnemyClass.Runner, enraged ? 5 : 3);
                    barrier = Mathf.Min(maxHealth * .2f, barrier + maxHealth * .055f);
                    game.SpawnBossSignatureEffect(Position, new Color(.72f, .26f, 1f), 2.35f, 0);
                    game.SpawnEnemySlam(Position, 1.42f, new Color(.76f, .28f, 1f));
                    break;
                }
                case "lich_legion":
                {
                    bossSkillLabel = GameLocalization.Text("망자의 군단", "LEGION OF THE DEAD");
                    game.ShowBossWarning(GameLocalization.Text("리치 · 망자의 군단 소환", "LICH · SUMMON LEGION"));
                    BeginSkillVisual(.9f);
                    game.SpawnBossTelegraph(Position, 1.72f, .9f);
                    yield return new WaitForSeconds(.9f);
                    if (!CanFinishBossSkill()) yield break;
                    attackMotion = 1f;
                    game.SpawnBossMinions(this, EnemyClass.Skeleton, enraged ? 5 : 3);
                    health = Mathf.Min(maxHealth, health + maxHealth * .07f);
                    RefreshHealthBar();
                    game.SpawnBossSignatureEffect(Position, new Color(.68f, .26f, 1f), 2.05f, 1);
                    break;
                }
                case "warlord_charge":
                {
                    bossSkillLabel = GameLocalization.Text("붉은 선봉 돌진", "CRIMSON VANGUARD");
                    game.ShowBossWarning(GameLocalization.Text("대족장 · 붉은 선봉 돌진", "WARCHIEF · CRIMSON VANGUARD"));
                    var chargeTarget = game.FindDefenderTarget(Position, DetectionRange * 2.2f, false);
                    var impact = chargeTarget != null ? chargeTarget.Position : Position + Vector2.up * 1.2f;
                    FaceVisualTarget(impact);
                    BeginSkillVisual(.58f);
                    game.SpawnBossTelegraph(impact, .96f, .58f);
                    game.SpawnBossSignatureEffect(Position, new Color(1f, .38f, .12f), 1.45f, 2);
                    yield return new WaitForSeconds(.58f);
                    if (!CanFinishBossSkill()) yield break;
                    attackMotion = 1f;
                    game.DamageDefenders(impact, .96f, attackPower * 2.15f, DamageType.Physical, false,
                        PhysicalPenetration, MagicPenetration);
                    game.SpawnEnemySlam(impact, 1.05f, new Color(1f, .32f, .08f));
                    game.SpawnBossSignatureEffect(impact, new Color(1f, .68f, .16f), 1.62f, 2);
                    break;
                }
                case "titan_quake":
                {
                    bossSkillLabel = GameLocalization.Text("대지 분쇄", "EARTH SHATTER");
                    game.ShowBossWarning(GameLocalization.Text("산맥 거신 · 대지 분쇄", "MOUNTAIN TITAN · EARTH SHATTER"));
                    BeginSkillVisual(1.05f);
                    game.SpawnBossTelegraph(Position, 2.3f, 1.05f);
                    yield return new WaitForSeconds(1.05f);
                    if (!CanFinishBossSkill()) yield break;
                    attackMotion = 1f;
                    game.BossGroundSlam(Position, 2.3f, 42f + game.Round * 3.2f,
                        PhysicalPenetration, MagicPenetration);
                    barrier = Mathf.Min(maxHealth * .2f, barrier + maxHealth * .075f);
                    game.SpawnBossSignatureEffect(Position, new Color(1f, .48f, .12f), 2.5f, 3);
                    break;
                }
                case "arcane_prism":
                {
                    bossSkillLabel = GameLocalization.Text("비전 프리즘", "ARCANE PRISM");
                    game.ShowBossWarning(GameLocalization.Text("심연 군주 · 비전 프리즘", "ABYSS SOVEREIGN · ARCANE PRISM"));
                    var centerTarget = game.FindDefenderTarget(Position, 99f, true);
                    var center = centerTarget != null ? centerTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(center);
                    var prismPoints = new[]
                    {
                        center,
                        center + new Vector2(-.58f, .34f),
                        center + new Vector2(.58f, -.3f)
                    };
                    BeginSkillVisual(.76f);
                    foreach (var point in prismPoints) game.SpawnBossTelegraph(point, .68f, .76f);
                    yield return new WaitForSeconds(.76f);
                    if (!CanFinishBossSkill()) yield break;
                    foreach (var point in prismPoints)
                    {
                        game.DamageDefenders(point, .7f, magicPower * 1.08f, DamageType.Magic, true,
                            PhysicalPenetration, MagicPenetration);
                        game.SpawnEnemyClassEffect(AttackOriginFor(point), point, Class, true);
                        game.SpawnBossSignatureEffect(point, new Color(.58f, .36f, 1f), 1.08f, 4);
                    }
                    break;
                }
                case "ancestral_hex":
                {
                    bossSkillLabel = GameLocalization.Text("선조의 저주", "ANCESTRAL HEX");
                    game.ShowBossWarning(GameLocalization.Text("고대 나무거신 · 선조의 저주", "ANCIENT ENT · ANCESTRAL HEX"));
                    var hexTarget = game.FindDefenderTarget(Position, 99f, true);
                    var hexCenter = hexTarget != null ? hexTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(hexCenter);
                    BeginSkillVisual(.88f);
                    game.SpawnBossTelegraph(hexCenter, 1.42f, .88f);
                    yield return new WaitForSeconds(.88f);
                    if (!CanFinishBossSkill()) yield break;
                    game.DamageDefenders(hexCenter, 1.44f, magicPower * 1.28f, DamageType.Magic, true,
                        PhysicalPenetration, MagicPenetration);
                    health = Mathf.Min(maxHealth, health + maxHealth * .055f);
                    RefreshHealthBar();
                    game.SpawnEnemyClassEffect(AttackOriginFor(hexCenter), hexCenter, Class, true);
                    game.SpawnBossSignatureEffect(hexCenter, new Color(.18f, 1f, .62f), 1.72f, 1);
                    break;
                }
                case "void_barrage":
                {
                    bossSkillLabel = GameLocalization.Text("공허 포격", "VOID BARRAGE");
                    game.ShowBossWarning(GameLocalization.Text("철갑 거신 · 공허 포격", "IRON COLOSSUS · VOID BARRAGE"));
                    var barrageTarget = game.FindDefenderTarget(Position, 99f, true);
                    var barrageCenter = barrageTarget != null ? barrageTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(barrageCenter);
                    var barragePoints = new[]
                    {
                        barrageCenter,
                        barrageCenter + new Vector2(-.82f, -.18f),
                        barrageCenter + new Vector2(.82f, .2f)
                    };
                    BeginSkillVisual(1.02f);
                    foreach (var point in barragePoints) game.SpawnBossTelegraph(point, .9f, 1.02f);
                    yield return new WaitForSeconds(1.02f);
                    if (!CanFinishBossSkill()) yield break;
                    foreach (var point in barragePoints)
                    {
                        game.DamageDefenders(point, .92f, magicPower * 1.34f, DamageType.Magic, true,
                            PhysicalPenetration, MagicPenetration);
                        game.SpawnEnemySlam(point, 1.02f, new Color(.74f, .24f, 1f));
                        game.SpawnEnemyClassEffect(AttackOriginFor(point), point, Class, true);
                    }
                    barrier = Mathf.Min(maxHealth * .22f, barrier + maxHealth * .08f);
                    break;
                }
                case "bloodline_impale":
                {
                    bossSkillLabel = GameLocalization.Text("고룡의 꿰뚫기", "ANCIENT DRAGON IMPALE");
                    game.ShowBossWarning(GameLocalization.Text("도마뱀 장로 · 고룡의 관통술", "LIZARD ELDER · ANCIENT DRAGON IMPALE"));
                    var impaleTarget = game.FindDefenderTarget(Position, DetectionRange * 2.4f, false);
                    var impalePoint = impaleTarget != null ? impaleTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(impalePoint);
                    BeginSkillVisual(.52f);
                    game.SpawnBossTelegraph(impalePoint, .72f, .52f);
                    yield return new WaitForSeconds(.52f);
                    if (!CanFinishBossSkill()) yield break;
                    game.DamageDefenders(impalePoint, .76f, attackPower * 2.35f, DamageType.Pure, false,
                        PhysicalPenetration, MagicPenetration);
                    game.SpawnEnemyClassEffect(AttackOriginFor(impalePoint), impalePoint, Class, true);
                    game.SpawnBossSignatureEffect(impalePoint, new Color(1f, .16f, .08f), 1.38f, 2);
                    break;
                }
                case "skyfall_hunt":
                {
                    bossSkillLabel = GameLocalization.Text("공중 사냥", "SKYFALL HUNT");
                    game.ShowBossWarning(GameLocalization.Text("폭풍 비룡 · 공중 사냥", "TEMPEST DRAGON · SKYFALL HUNT"));
                    var huntTarget = game.FindDefenderTarget(Position, 99f, true);
                    var huntCenter = huntTarget != null ? huntTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(huntCenter);
                    var huntPoints = new[]
                    {
                        huntCenter,
                        huntCenter + new Vector2(-.7f, .4f),
                        huntCenter + new Vector2(.68f, .34f),
                        huntCenter + new Vector2(0f, -.62f)
                    };
                    BeginSkillVisual(.66f);
                    foreach (var point in huntPoints) game.SpawnBossTelegraph(point, .62f, .66f);
                    yield return new WaitForSeconds(.66f);
                    if (!CanFinishBossSkill()) yield break;
                    foreach (var point in huntPoints)
                    {
                        game.DamageDefenders(point, .64f, attackPower * 1.22f, DamageType.Physical, true,
                            PhysicalPenetration, MagicPenetration);
                        game.SpawnEnemyClassEffect(AttackOriginFor(point), point, Class, true);
                    }
                    game.SpawnBossSignatureEffect(huntCenter, new Color(.74f, .3f, 1f), 1.65f, 4);
                    break;
                }
                case "astral_tempest":
                {
                    bossSkillLabel = GameLocalization.Text("성운 폭풍", "ASTRAL TEMPEST");
                    game.ShowBossWarning(GameLocalization.Text("망령왕 · 성운 폭풍", "WRAITH KING · ASTRAL TEMPEST"));
                    var stormTarget = game.FindDefenderTarget(Position, 99f, true);
                    var stormCenter = stormTarget != null ? stormTarget.Position : Position + Vector2.up;
                    FaceVisualTarget(stormCenter);
                    var stormPoints = new[]
                    {
                        stormCenter,
                        stormCenter + new Vector2(-.72f, .28f),
                        stormCenter + new Vector2(.72f, -.24f)
                    };
                    BeginSkillVisual(.82f);
                    foreach (var point in stormPoints) game.SpawnBossTelegraph(point, .82f, .82f);
                    yield return new WaitForSeconds(.82f);
                    if (!CanFinishBossSkill()) yield break;
                    attackMotion = 1f;
                    foreach (var point in stormPoints)
                    {
                        game.DamageDefenders(point, .84f, magicPower * 1.18f, DamageType.Magic, true,
                            PhysicalPenetration, MagicPenetration);
                        game.SpawnBossSignatureEffect(point, new Color(.3f, .82f, 1f), 1.38f, 4);
                    }
                    break;
                }
                default:
                {
                    BeginSkillVisual(.7f);
                    game.SpawnBossTelegraph(Position, 1.2f, .7f);
                    yield return new WaitForSeconds(.7f);
                    if (!CanFinishBossSkill()) yield break;
                    game.BossGroundSlam(Position, 1.2f, 28f + game.Round * 2f,
                        PhysicalPenetration, MagicPenetration);
                    break;
                }
            }

            bossSkillLabel = lastBossSkillId switch
            {
                "slime_royal_split" => GameLocalization.Text("왕의 분열", "ROYAL DIVISION"),
                "arcane_prism" => GameLocalization.Text("비전 프리즘", "ARCANE PRISM"),
                "lich_legion" => GameLocalization.Text("망자의 군단", "LEGION OF THE DEAD"),
                "warlord_charge" => GameLocalization.Text("붉은 선봉 돌진", "CRIMSON VANGUARD"),
                "titan_quake" => GameLocalization.Text("대지 분쇄", "EARTH SHATTER"),
                "ancestral_hex" => GameLocalization.Text("선조의 저주", "ANCESTRAL HEX"),
                "void_barrage" => GameLocalization.Text("공허 포격", "VOID BARRAGE"),
                "bloodline_impale" => GameLocalization.Text("진홍 꿰뚫기", "CRIMSON IMPALE"),
                "skyfall_hunt" => GameLocalization.Text("공중 사냥", "SKYFALL HUNT"),
                _ => GameLocalization.Text("성운 폭풍", "ASTRAL TEMPEST")
            };
            nextBossSkillAt = Time.time + (enraged ? 5.8f : 7.4f);
            castingBossSkill = false;
        }

        private bool CanFinishBossSkill()
        {
            if (IsAlive && game != null && game.Phase == GamePhase.Battle) return true;
            castingBossSkill = false;
            return false;
        }

        private void RefreshHealthBar()
        {
            if (healthFill == null) return;
            var ratio = Mathf.Clamp01(health / Mathf.Max(1f, maxHealth));
            var width = Radius * (IsBoss ? 3.55f : 2.1f);
            healthFill.localScale = new Vector3(width * ratio, IsBoss ? .085f : .045f, 1f);
            healthFill.localPosition = new Vector3(-width * .5f * (1f - ratio),
                -Radius - (IsBoss ? .18f : .12f), -.2f);
        }

        private static string BossSkillIdForClass(EnemyClass enemyClass) => BossIdentityCatalog.For(enemyClass).SkillId;

        public void ForceBossSkillReadyForQa()
        {
            if (!IsBoss || !IsAlive) return;
            attackingGate = false;
            nextBossSkillAt = Time.time - .01f;
        }

        public bool ForceBossSkillCastForQa()
        {
            if (!IsBoss || !IsAlive || game == null || castingBossSkill) return false;
            attackingGate = false;
            engagingDefender = false;
            velocity = Vector2.zero;
            StartCoroutine(BossSkillRoutineV2());
            return true;
        }

        public void SetSummonedPosition(Vector2 position, int inheritedPathIndex, bool inheritBossEntrance = false)
        {
            if (game == null || !IsAlive) return;
            avoidsBossSilhouette = true;
            bossSilhouetteSeparationUntil = Time.time + .95f;
            usesBossEntrance = inheritBossEntrance;
            attackingGate = false;
            castingBossSkill = false;
            detectedTarget = null;
            pathIndex = Mathf.Clamp(inheritedPathIndex, 0, Mathf.Max(0, CurrentPathCount - 2));
            // Summoned escorts and split minions must inherit the authored assault corridor.
            // Generic nearest-walkable projection can choose a visually walkable flower island
            // or river shelf, which is valid for player deployment but never for ground enemies.
            var safe = IsFlying ? position :
                game.NearestGroundEnemyRoadPosition(position, Radius * .42f);
            if (!IsFlying) safe = game.ResolveBossSilhouetteExclusion(this, safe);
            transform.position = game.ActorWorldPosition(safe, true);
            ReanchorToNearestForwardPath(true);
            lastMovementPosition = safe;
            lastMovementAt = Time.time;
        }

        public void ForcePathReanchorForQa() => ReanchorToNearestForwardPath(true);

        public void ForceDetectedTargetForQa(PlayerUnit target)
        {
            detectedTarget = target;
            temporarilyIgnoredTarget = null;
            ignoredTargetUntil = 0f;
            nextTargetScanAt = Time.time + .5f;
        }

        private void EnterEnrage()
        {
            enraged = true;
            contactDamage *= 1.12f;
            armor += 6f;
            magicResistance += 5f;
            bossSkillLabel = GameLocalization.Text("격노 · 공격 가속", "ENRAGED · ATTACK SPEED UP");
            game.ShowBossWarning(GameLocalization.Text("보스 격노 · 방어력과 공격 속도 상승",
                "BOSS ENRAGED · DEFENCE AND ATTACK SPEED UP"));
            game.SpawnCombatImpact(Position, UnitArchetype.AreaMage, new Color(1f, .2f, .18f), 1.35f);
            nextBossSkillAt = Mathf.Min(nextBossSkillAt, Time.time + 3.2f);
        }

        private void Die()
        {
            if (!IsAlive) return;
            IsAlive = false;
            game.PlayEnemyVoice(transform, Class, VoiceCue.Defeat);
            game.AwardExperience(damageContributors, IsBoss);
            game.NotifyEnemyDefeated(Class, IsBoss);
            game.RemoveEnemy(this);
            game.SpawnImpact(Position, new Color(.85f, .52f, 1f));
            game.RecycleEnemy(this);
        }
    }
}
