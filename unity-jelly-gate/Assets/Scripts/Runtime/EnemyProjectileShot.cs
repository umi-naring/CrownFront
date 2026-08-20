using UnityEngine;

namespace JellyGate
{
    public sealed class EnemyProjectileShot : MonoBehaviour
    {
        private JellyGateGame game;
        private PlayerUnit target;
        private SpriteRenderer core;
        private SpriteRenderer aura;
        private SpriteRenderer trail;
        private SpriteRenderer[] orbiters = System.Array.Empty<SpriteRenderer>();
        private Transform meshCore;
        private Transform meshTrail;
        private float damage;
        private float speed;
        private float life;
        private Color color;
        private EnemyClass sourceClass;
        private float physicalPenetration;
        private float magicPenetration;
        private float baseAuraScale;
        private float baseCoreScale;

        public void Initialize(JellyGateGame owner, PlayerUnit defender, Vector2 origin,
            float hitDamage, bool empowered, EnemyClass enemyClass,
            float sourcePhysicalPenetration = 0f, float sourceMagicPenetration = 0f)
        {
            game = owner;
            target = defender;
            damage = hitDamage;
            sourceClass = enemyClass;
            physicalPenetration = sourcePhysicalPenetration;
            magicPenetration = sourceMagicPenetration;
            speed = enemyClass switch
            {
                EnemyClass.Siege => empowered ? 5.05f : 4.45f,
                EnemyClass.Wisp => empowered ? 8.4f : 7.3f,
                EnemyClass.Shaman => empowered ? 6.25f : 5.25f,
                EnemyClass.Silencer => empowered ? 6.2f : 5.3f,
                EnemyClass.Cursebinder => empowered ? 6.1f : 5.2f,
                _ => empowered ? 6.8f : 5.7f
            };
            color = enemyClass switch
            {
                EnemyClass.Shaman => empowered ? new Color(.2f, 1f, .62f) : new Color(.25f, .82f, .5f),
                EnemyClass.Siege => empowered ? new Color(.85f, .32f, 1f) : new Color(.66f, .26f, .94f),
                EnemyClass.Wisp => empowered ? new Color(.25f, .95f, 1f) : new Color(.28f, .78f, 1f),
                EnemyClass.Silencer => empowered ? new Color(.30f, .94f, 1f) : new Color(.28f, .68f, .94f),
                EnemyClass.Cursebinder => empowered ? new Color(.30f, .94f, 1f) : new Color(.28f, .66f, .92f),
                _ => empowered ? new Color(.35f, .86f, 1f) : new Color(.58f, .34f, 1f)
            };
            baseAuraScale = (empowered ? .48f : .34f) * (enemyClass == EnemyClass.Siege ? 1.42f :
                enemyClass == EnemyClass.Wisp ? .82f : 1f);
            baseCoreScale = (empowered ? .23f : .17f) * (enemyClass == EnemyClass.Siege ? 1.36f :
                enemyClass == EnemyClass.Wisp ? .76f : 1f);
            transform.position = new Vector3(origin.x, origin.y, -3.15f + origin.y * .08f);

            aura = game.CreateSpriteChild(transform, "Enemy Magic Aura", game.GlowSprite,
                new Color(color.r, color.g, color.b, .52f), baseAuraScale, 42);
            trail = game.CreateSpriteChild(transform,
                enemyClass == EnemyClass.Wisp ? "Astral Shard Trail" :
                enemyClass == EnemyClass.Siege ? "Siege Comet Trail" : "Enemy Magic Trail", game.GlowSprite,
                new Color(color.r, color.g, color.b, .62f), 1f, 41);
            trail.transform.localPosition = new Vector3(-.23f, 0f, .04f);
            trail.transform.localScale = new Vector3(
                (empowered ? .68f : .5f) * (enemyClass == EnemyClass.Siege ? 1.55f :
                    enemyClass == EnemyClass.Wisp ? 1.25f : 1f),
                enemyClass == EnemyClass.Wisp ? .085f : enemyClass == EnemyClass.Siege ? .24f : .15f, 1f);
            core = game.CreateSpriteChild(transform, "Enemy Magic Core", game.GlowSprite,
                Color.Lerp(color, Color.white, .8f), baseCoreScale, 43);
            orbiters = new SpriteRenderer[enemyClass == EnemyClass.Siege ? (empowered ? 6 : 4) :
                enemyClass == EnemyClass.Wisp ? 2 : empowered ? 4 : 3];
            for (var i = 0; i < orbiters.Length; i++)
                orbiters[i] = game.CreateSpriteChild(transform, $"Enemy Arcane Spark {i}", game.SparkSprite,
                    i % 2 == 0 ? Color.white : color, empowered ? .095f : .07f, 44);
            var scale = empowered ? .19f : .14f;
            if (game.Use2p5DPresentation)
            {
                meshTrail = Stylized2p5DFactory.CreateEffectMesh(transform, "2.5D Enemy Trail", PrimitiveType.Cube,
                    new Vector3(-.18f, 0f, .08f), new Vector3(scale * 2.8f, scale * .28f, .06f), color, .82f);
                meshCore = Stylized2p5DFactory.CreateEffectMesh(transform, "2.5D Enemy Bolt", PrimitiveType.Sphere,
                    new Vector3(.04f, 0f, -.06f), Vector3.one * scale, Color.Lerp(color, Color.white, .45f), 1.7f);
            }
        }

        private void Update()
        {
            if (target == null || !target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }
            if (game != null && game.EnemyActionsFrozen) return;

            life += Time.deltaTime;
            var position = (Vector2)transform.position;
            var targetPoint = target.HitPoint;
            if (Vector2.Distance(position, targetPoint) < .15f)
            {
                target.TakeDamage(damage, DamageType.Magic, false,
                    physicalPenetration, magicPenetration);
                var impactRadius = sourceClass == EnemyClass.Siege ? .96f :
                    sourceClass == EnemyClass.Wisp ? .52f : sourceClass == EnemyClass.Shaman ? .76f : .65f;
                game.SpawnCombatImpact(targetPoint, UnitArchetype.SingleMage, color, impactRadius,
                    targetPoint - position, CombatVfxTier.Basic);
                game.SpawnEnemyClassEffect(position, targetPoint, sourceClass, orbiters.Length > 3);
                Destroy(gameObject);
                return;
            }

            var direction = (targetPoint - position).normalized;
            transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var pulse = 1f + Mathf.Sin(life * 20f) * .16f;
            aura.transform.localScale = Vector3.one * (baseAuraScale * pulse);
            core.transform.localScale = sourceClass == EnemyClass.Wisp
                ? new Vector3(baseCoreScale * 1.75f, baseCoreScale * .72f, 1f)
                : Vector3.one * (baseCoreScale * (2f - pulse * .55f));
            for (var i = 0; i < orbiters.Length; i++)
            {
                var orbitSpeed = sourceClass == EnemyClass.Siege ? 4.2f :
                    sourceClass == EnemyClass.Wisp ? 11.5f : sourceClass == EnemyClass.Shaman ? 6.1f : 7.2f;
                var angle = life * (orbitSpeed + i * .4f) + i * Mathf.PI * 2f / orbiters.Length;
                orbiters[i].transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.02f) *
                                                       (orbiters.Length > 3 ? .29f : .21f);
                orbiters[i].transform.Rotate(0f, 0f, Time.deltaTime * (i % 2 == 0 ? 250f : -210f));
            }
            if (meshCore != null) meshCore.localScale = Vector3.one * (.14f * pulse);
            if (meshTrail != null) meshTrail.localScale = new Vector3(.39f * pulse, .05f, .06f);
            var next = Vector2.MoveTowards(position, targetPoint, speed * Time.deltaTime);
            transform.position = new Vector3(next.x, next.y, -3.15f + next.y * .08f);
        }
    }
}
