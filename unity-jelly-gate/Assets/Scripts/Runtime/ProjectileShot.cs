using UnityEngine;

namespace JellyGate
{
    public sealed class ProjectileShot : MonoBehaviour
    {
        private JellyGateGame game;
        private PlayerUnit source;
        private EnemyUnit target;
        private UnitArchetype archetype;
        private SpriteRenderer core;
        private SpriteRenderer trail;
        private SpriteRenderer aura;
        private SpriteRenderer[] afterimages = System.Array.Empty<SpriteRenderer>();
        private SpriteRenderer[] orbiters = System.Array.Empty<SpriteRenderer>();
        private SpriteRenderer[] skinSignatures = System.Array.Empty<SpriteRenderer>();
        private Transform meshCore;
        private Transform meshTrail;
        private Color effectColor;
        private Color secondaryColor;
        private Color coreColor;
        private int skinVariant;
        private float damage;
        private float splashRadius;
        private float speed;
        private float life;

        public void Initialize(JellyGateGame owner, PlayerUnit attackSource, Vector2 origin, EnemyUnit enemy, float hitDamage,
            float splash, Color color, float travelSpeed = 7.5f)
        {
            game = owner;
            source = attackSource;
            target = enemy;
            archetype = source != null ? source.Archetype : UnitArchetype.Archer;
            effectColor = color;
            skinVariant = source != null ? source.SkinVariant : 0;
            secondaryColor = skinVariant > 0 ? game.GetUnitSkinSecondary(archetype) :
                Color.Lerp(color, Color.white, .66f);
            if (secondaryColor.a <= .01f) secondaryColor = Color.Lerp(color, Color.white, .66f);
            secondaryColor.a = 1f;
            coreColor = skinVariant == 1
                ? Color.Lerp(secondaryColor, Color.white, .34f)
                : Color.Lerp(color, Color.white, skinVariant == 2 ? .86f : .72f);
            coreColor.a = 1f;
            damage = hitDamage;
            splashRadius = splash;
            speed = Mathf.Approximately(travelSpeed, 7.5f)
                ? archetype switch
                {
                    UnitArchetype.Archer => 9.4f,
                    UnitArchetype.Musketeer => 10.8f,
                    UnitArchetype.AreaMage => 6.4f,
                    UnitArchetype.SingleMage => 8.6f,
                    UnitArchetype.Oracle => 7.4f,
                    _ => travelSpeed
                }
                : travelSpeed;
            transform.position = new Vector3(origin.x, origin.y, -3f + origin.y * .08f);

            if (archetype is UnitArchetype.Archer or UnitArchetype.Musketeer)
            {
                trail = game.CreateSpriteChild(transform, "Arrow Trail", game.GlowSprite,
                    skinVariant > 0
                        ? new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, .82f)
                        : new Color(color.r, color.g, color.b, .58f), 1f, 39);
                trail.transform.localPosition = new Vector3(-.23f, 0f, .04f);
                trail.transform.localScale = skinVariant switch
                {
                    1 => new Vector3(.94f, .11f, 1f),
                    2 => new Vector3(1.08f, .2f, 1f),
                    _ => new Vector3(.78f, .14f, 1f)
                };
                core = game.CreateSpriteChild(transform, "Arrow", game.SquareSprite,
                    skinVariant > 0 ? Color.Lerp(color, coreColor, .48f) :
                    Color.Lerp(color, Color.white, .42f), 1f, 41);
                core.transform.localScale = new Vector3(.5f, .13f, 1f);
                var tip = game.CreateSpriteChild(transform, "Arrow Tip", game.SparkSprite,
                    coreColor, .135f, 42);
                tip.transform.localPosition = new Vector3(.285f, 0f, -.02f);
                var feather = game.CreateSpriteChild(transform, "Arrow Feather", game.SparkSprite,
                    new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, .92f), .145f, 41);
                feather.transform.localPosition = new Vector3(-.285f, 0f, 0f);
            }
            else
            {
                var auraColor = color;
                var areaCaster = archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle;
                auraColor.a = areaCaster ? .68f : .46f;
                aura = game.CreateSpriteChild(transform, "Magic Aura", game.GlowSprite,
                    auraColor, areaCaster ? .56f : .3f, 39);
                trail = game.CreateSpriteChild(transform, "Magic Trail", game.GlowSprite,
                    new Color(color.r, color.g, color.b, .56f), 1f, 39);
                trail.transform.localPosition = new Vector3(-.24f, 0f, .04f);
                trail.transform.localScale = new Vector3(areaCaster ? .74f : .5f, .15f, 1f);
                core = game.CreateSpriteChild(transform, "Magic Core", game.GlowSprite,
                    coreColor, areaCaster ? .28f : .17f, 42);
                orbiters = new SpriteRenderer[(areaCaster ? 4 : 2) + (skinVariant > 0 ? skinVariant : 0)];
                for (var i = 0; i < orbiters.Length; i++)
                    orbiters[i] = game.CreateSpriteChild(transform, $"Magic Orbiter {i}", game.SparkSprite,
                        i % 3 == 0 ? coreColor : i % 2 == 0 ? secondaryColor : color,
                        areaCaster ? .095f : .07f, 43);
            }
            afterimages = new SpriteRenderer[3];
            for (var i = 0; i < afterimages.Length; i++)
            {
                afterimages[i] = game.CreateSpriteChild(transform, $"Projectile Afterimage {i}", game.GlowSprite,
                    new Color(i % 2 == 0 ? color.r : secondaryColor.r,
                        i % 2 == 0 ? color.g : secondaryColor.g,
                        i % 2 == 0 ? color.b : secondaryColor.b, .3f - i * .06f), .19f, 38 - i);
                afterimages[i].transform.localPosition = new Vector3(-.24f - i * .16f, 0f, .06f);
                afterimages[i].transform.localScale = new Vector3(.4f + i * .12f, .13f, 1f);
            }
            if (skinVariant > 0)
            {
                skinSignatures = new SpriteRenderer[skinVariant == 1 ? 3 : 5];
                for (var i = 0; i < skinSignatures.Length; i++)
                {
                    skinSignatures[i] = game.CreateSpriteChild(transform,
                        $"Skin VFX Basic V{skinVariant} Projectile Signature {i + 1}",
                        skinVariant == 2 && i % 2 == 0 ? game.CommandRingSprite : game.SparkSprite,
                        i % 3 == 0 ? coreColor : i % 2 == 0 ? secondaryColor : color,
                        skinVariant == 1 ? .075f : .09f, 44);
                }
            }
            var projectileScale = archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle ? .22f : .14f;
            if (game.Use2p5DPresentation)
            {
                meshTrail = Stylized2p5DFactory.CreateEffectMesh(transform, "2.5D Projectile Trail", PrimitiveType.Cube,
                    new Vector3(-.15f, 0f, .08f), new Vector3(projectileScale * 2.6f, projectileScale * .28f, .06f),
                    new Color(color.r, color.g, color.b, 1f), .72f);
                meshCore = Stylized2p5DFactory.CreateEffectMesh(transform, "2.5D Projectile Core", PrimitiveType.Sphere,
                    new Vector3(.05f, 0f, -.06f), Vector3.one * projectileScale, Color.Lerp(color, Color.white, .48f), 1.55f);
            }
        }

        private void Update()
        {
            if (target == null || !target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }
            life += Time.deltaTime;
            var position = (Vector2)transform.position;
            var targetPoint = target.HitPoint;
            var distance = Vector2.Distance(position, targetPoint);
            if (distance < .14f)
            {
                var hitPosition = targetPoint;
                var damageType = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or
                    UnitArchetype.Druid or UnitArchetype.Oracle
                    ? DamageType.Magic
                    : DamageType.Physical;
                if (splashRadius > 0f) game.DamageEnemies(target.Position, splashRadius, damage, target, source, damageType);
                else target.TakeDamage(damage, source, damageType);
                game.ApplyRangedRicochet(source, target, damage, damageType);
                // Damage radius and artwork radius are deliberately independent. Area Mage keeps
                // its gameplay splash, but its frequent basic attack no longer blankets the lane;
                // focused physical hits receive a slightly clearer contact read instead.
                var visualRadius = archetype switch
                {
                    UnitArchetype.AreaMage => .72f,
                    UnitArchetype.Archer => .62f,
                    UnitArchetype.Musketeer => .66f,
                    UnitArchetype.Lancer => .64f,
                    UnitArchetype.SingleMage => .64f,
                    UnitArchetype.Druid or UnitArchetype.Oracle => .78f,
                    UnitArchetype.Bombardier => .84f,
                    _ => splashRadius > 0f ? Mathf.Min(.9f, Mathf.Max(.62f, splashRadius)) : .62f
                };
                game.SpawnCombatImpact(hitPosition, archetype, effectColor,
                    visualRadius,
                    hitPosition - position, CombatVfxTier.Basic, source);
                Destroy(gameObject);
                return;
            }
            var direction = (targetPoint - position).normalized;
            transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var flicker = .88f + Mathf.Sin(life * 24f) * .12f;
            if (core != null) core.transform.localScale *= flicker / Mathf.Max(.001f, .88f + Mathf.Sin((life - Time.deltaTime) * 24f) * .12f);
            if (aura != null)
            {
                var areaCaster = archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle;
                var auraScale = (areaCaster ? .56f : .3f) * (1f + Mathf.Sin(life * 18f) * .18f);
                aura.transform.localScale = Vector3.one * auraScale;
                var auraColor = effectColor;
                auraColor.a = .4f + Mathf.Sin(life * 16f) * .12f;
                aura.color = auraColor;
            }
            for (var i = 0; i < orbiters.Length; i++)
            {
                var angle = life * (8f + i * .55f) + i * Mathf.PI * 2f / orbiters.Length;
                var orbit = archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle ? .3f : .19f;
                orbiters[i].transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.02f) * orbit;
                orbiters[i].transform.Rotate(0f, 0f, Time.deltaTime * 260f);
            }
            for (var i = 0; i < afterimages.Length; i++)
            {
                var alpha = (.3f - i * .06f) * (.78f + Mathf.Sin(life * 18f - i) * .22f);
                var afterColor = afterimages[i].color;
                afterColor.a = alpha;
                afterimages[i].color = afterColor;
            }
            for (var i = 0; i < skinSignatures.Length; i++)
            {
                var angle = life * (skinVariant == 1 ? -10.5f : 12.8f) +
                            i * Mathf.PI * 2f / skinSignatures.Length;
                var orbit = skinVariant == 1 ? .17f : .23f;
                skinSignatures[i].transform.localPosition = new Vector3(
                    -.04f + Mathf.Cos(angle) * orbit,
                    Mathf.Sin(angle) * orbit, -.03f);
                skinSignatures[i].transform.localScale = skinVariant == 1
                    ? new Vector3(.13f, .045f, 1f)
                    : Vector3.one * (.065f + i % 2 * .022f);
                skinSignatures[i].transform.Rotate(0f, 0f,
                    Time.deltaTime * (skinVariant == 1 ? -420f : 520f));
                var signatureColor = i % 3 == 0 ? coreColor : i % 2 == 0
                    ? secondaryColor : effectColor;
                signatureColor.a = .78f + Mathf.Sin(life * 19f + i) * .18f;
                skinSignatures[i].color = signatureColor;
            }
            if (meshCore != null)
            {
                var meshScale = Mathf.Lerp(.88f, 1.16f, flicker);
                meshCore.localScale = Vector3.one * ((archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle ? .22f : .14f) * meshScale);
            }
            if (meshTrail != null)
                meshTrail.localScale = new Vector3((archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle ? .22f : .14f) * 2.6f,
                    (archetype is UnitArchetype.AreaMage or UnitArchetype.Druid or UnitArchetype.Oracle ? .22f : .14f) * .28f, .06f);
            var next = Vector2.MoveTowards(position, targetPoint, speed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, -3f + next.y * .08f);
        }
    }
}
