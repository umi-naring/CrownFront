using UnityEngine;
using UnityEngine.Rendering;

namespace JellyGate
{
    // Runtime bridge for the authored low-poly rigs.  It deliberately has no procedural body
    // parts: silhouettes, weapons, proportions and walk/attack motion all come from one
    // consistent rigged asset family, which prevents the old "assembled primitive" look.
    public sealed class KayKit2p5DUnitVisual : MonoBehaviour
    {
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Cast = Animator.StringToHash("Cast");
        private static readonly int Hurt = Animator.StringToHash("Hurt");
        private static readonly int Hero = Animator.StringToHash("Hero");

        private Transform facingPivot;
        private Animator animator;
        private Renderer[] modelRenderers = System.Array.Empty<Renderer>();
        private float lastAttack;
        private float lastHurt;
        private bool hero;
        private bool flying;
        private bool caster;
        private float modelScale;
        private Color teamTint;
        private UnitArchetype defenderArchetype;
        private EnemyClass enemyClass;

        public static KayKit2p5DUnitVisual CreateDefender(Transform parent, UnitArchetype archetype,
            Color teamColor, float radius, bool isHero)
        {
            var modelName = archetype switch
            {
                UnitArchetype.Tank => "Knight",
                UnitArchetype.Melee or UnitArchetype.Lancer or UnitArchetype.Bombardier => "Barbarian",
                UnitArchetype.Archer or UnitArchetype.Musketeer => "Rogue",
                UnitArchetype.AreaMage or UnitArchetype.SingleMage or UnitArchetype.Druid or UnitArchetype.Oracle => "Mage",
                _ => "RogueHooded"
            };
            var casts = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or UnitArchetype.Druid or UnitArchetype.Oracle;
            var visual = Create(parent, "Crownfront Defender", "KayKit2p5D/Characters/" + modelName,
                DesignColor(archetype, teamColor), radius, isHero, false, false, casts);
            visual.defenderArchetype = archetype;
            visual.AddSignatureKit(archetype, isHero);
            return visual;
        }

        public static KayKit2p5DUnitVisual CreateEnemy(Transform parent, EnemyClass enemyClass,
            Color enemyColor, float radius, bool isBoss)
        {
            var modelName = enemyClass switch
            {
                EnemyClass.Mage or EnemyClass.Shaman => "Skeleton_Mage",
                EnemyClass.Runner or EnemyClass.Piercer or EnemyClass.Flyer => "Skeleton_Rogue",
                EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Boss => "Skeleton_Warrior",
                _ => "Skeleton_Minion"
            };
            var casts = enemyClass is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp;
            var visual = Create(parent, "Crownfront Invader", "KayKit2p5D/Enemies/" + modelName,
                EnemyDesignColor(enemyClass, enemyColor), radius, isBoss, true, enemyClass == EnemyClass.Flyer, casts);
            visual.enemyClass = enemyClass;
            visual.AddEnemyKit(enemyClass, isBoss);
            return visual;
        }

        private static KayKit2p5DUnitVisual Create(Transform parent, string rootName, string resourcePath,
            Color tint, float radius, bool isHero, bool isEnemy, bool isFlying, bool isCaster)
        {
            var root = new GameObject(rootName).AddComponent<KayKit2p5DUnitVisual>();
            root.transform.SetParent(parent, false);
            // The real model stands toward the camera rather than on a flat billboard.  Keeping
            // it a little in front of the map gives it real limb depth without changing map
            // coordinates or turn controls.
            // The tactical board writes depth through SpriteRenderer.  A dedicated foreground
            // layer prevents only the feet from peeking through terrain, while the origin stays
            // on the board (no artificial vertical lift).
            root.transform.localPosition = new Vector3(0f, 0f, -3.12f);
            // The authored characters are physically much wider than the old sprite silhouette.
            // Keep a full tactical plaza visible at the default zoom; individual detail becomes
            // available through the new pinch/wheel zoom instead of filling an entire lane.
            root.modelScale = Mathf.Clamp(radius * (isEnemy ? 1.42f : 1.34f), .28f, isHero ? .52f : .42f);
            root.teamTint = tint;
            root.hero = isHero;
            root.flying = isFlying;
            root.caster = isCaster;

            root.facingPivot = new GameObject("Facing Pivot").transform;
            root.facingPivot.SetParent(root.transform, false);
            var source = Resources.Load<GameObject>(resourcePath);
            if (source == null)
            {
                Debug.LogWarning("Missing authored 2.5D model: " + resourcePath);
                return root;
            }

            var model = Instantiate(source, root.facingPivot);
            model.name = source.name;
            model.transform.localPosition = Vector3.zero;
            // This is a 2.5D board, not a ground-plane third-person camera: leave the rig Y-up
            // so legs and weapons read naturally against the map, then yaw the whole model into
            // the travel direction.  The prior X-axis turn laid the mesh on its side.
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * root.modelScale;
            root.animator = model.GetComponentInChildren<Animator>();
            if (root.animator != null)
            {
                root.animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("KayKit2p5D/KayKitBattle");
                root.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.animator.SetBool(Hero, isHero);
            }

            root.modelRenderers = model.GetComponentsInChildren<Renderer>(true);
            root.ApplyPresentation(isEnemy);
            return root;
        }

        public void SetHero(bool value)
        {
            hero = value;
            if (animator != null) animator.SetBool(Hero, hero);
            foreach (var renderer in modelRenderers)
                if (renderer != null) renderer.transform.localScale = value ? Vector3.one * 1.24f : Vector3.one;
        }

        public void Animate(Vector2 facing, bool moving, float attackT, float hurtT, bool isHero, bool isFlying)
        {
            if (isHero != hero) SetHero(isHero);
            flying = isFlying;
            if (facingPivot != null && facing.sqrMagnitude > .002f)
            {
                var direction = facing.normalized;
                // The source rigs face camera-forward (-Z). Map down is therefore front-facing,
                // while north/side travel presents an intentional back or profile silhouette.
                var heading = -Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
                facingPivot.localRotation = Quaternion.Euler(0f, heading, 0f);
            }

            if (animator != null)
            {
                animator.SetBool(Moving, moving);
                if (attackT > .04f && lastAttack <= .04f)
                    animator.SetTrigger(caster ? Cast : Attack);
                if (hurtT > .04f && lastHurt <= .04f)
                    animator.SetTrigger(Hurt);
            }
            lastAttack = attackT;
            lastHurt = hurtT;

            var lift = flying ? .12f + Mathf.Sin(Time.time * 6.5f) * .035f : 0f;
            transform.localPosition = new Vector3(0f, lift, -3.12f);
        }

        public void PlayCast()
        {
            if (animator != null) animator.SetTrigger(Cast);
        }

        private void ApplyPresentation(bool isEnemy)
        {
            // Character textures carry their authored color design.  A restrained property-block
            // tint differentiates allied/enemy factions without turning every unit into an AI
            // painted sprite or replacing its material.
            var tint = isEnemy ? Color.Lerp(Color.white, teamTint, .48f) : Color.Lerp(Color.white, teamTint, .42f);
            var block = new MaterialPropertyBlock();
            foreach (var renderer in modelRenderers)
            {
                if (renderer == null) continue;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.GetPropertyBlock(block);
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                    block.SetColor("_BaseColor", tint);
                else if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                    block.SetColor("_Color", tint);
                renderer.SetPropertyBlock(block);
                if (renderer.sharedMaterial != null)
                    renderer.sharedMaterial.renderQueue = (int)RenderQueue.Transparent + 24;
            }
        }

        private static Color DesignColor(UnitArchetype archetype, Color fallback) => archetype switch
        {
            UnitArchetype.Tank => new Color(.08f, .31f, .88f),
            UnitArchetype.Melee or UnitArchetype.Bombardier => new Color(.90f, .20f, .12f),
            UnitArchetype.Archer or UnitArchetype.Musketeer or UnitArchetype.Lancer => new Color(.05f, .62f, .54f),
            UnitArchetype.AreaMage or UnitArchetype.Druid => new Color(.52f, .18f, .88f),
            UnitArchetype.SingleMage or UnitArchetype.Oracle => new Color(.10f, .49f, .94f),
            _ => fallback
        };

        private static Color EnemyDesignColor(EnemyClass enemyClass, Color fallback) => enemyClass switch
        {
            EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp => new Color(.60f, .18f, .82f),
            EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Boss => new Color(.86f, .20f, .12f),
            EnemyClass.Flyer => new Color(.42f, .18f, .76f),
            _ => Color.Lerp(new Color(.25f, .72f, .30f), fallback, .22f)
        };

        private void AddSignatureKit(UnitArchetype archetype, bool isHero)
        {
            // These are small silhouette cues on top of a fully skinned, animated model; they
            // preserve the existing roster at tactical distance without replacing it with mesh
            // primitives.  The mesh itself supplies all limbs and locomotion.
            var kit = new GameObject("Crownfront Signature Kit").transform;
            kit.SetParent(facingPivot, false);
            kit.localPosition = new Vector3(0f, .34f, -.12f);
            if (archetype == UnitArchetype.Tank)
                AddBadge(kit, "Blue Gold Shield Crest", new Color(.05f, .27f, .83f), new Vector3(-.25f, -.12f, -.20f), isHero ? .34f : .28f);
            else if (archetype is UnitArchetype.Melee or UnitArchetype.Bombardier)
                AddBadge(kit, "Red Hammer Crest", new Color(.92f, .22f, .12f), new Vector3(.20f, .04f, -.18f), .22f);
            else if (archetype is UnitArchetype.Archer or UnitArchetype.Musketeer or UnitArchetype.Lancer)
                AddBadge(kit, "Mint Hood Crest", new Color(.04f, .66f, .56f), new Vector3(0f, .25f, -.18f), .24f);
            else if (archetype is UnitArchetype.AreaMage or UnitArchetype.Druid)
                AddBadge(kit, "Violet Star Focus", new Color(.76f, .35f, 1f), new Vector3(.19f, .22f, -.23f), .20f);
            else
                AddBadge(kit, "Blue Orb Focus", new Color(.12f, .75f, 1f), new Vector3(.20f, .18f, -.23f), .19f);
        }

        private void AddEnemyKit(EnemyClass enemyClass, bool boss)
        {
            var kit = new GameObject("Invader Signature Kit").transform;
            kit.SetParent(facingPivot, false);
            kit.localPosition = new Vector3(0f, .32f, -.12f);
            var color = enemyClass is EnemyClass.Mage or EnemyClass.Shaman ? new Color(.75f, .28f, 1f) :
                enemyClass is EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Boss ? new Color(1f, .24f, .10f) : new Color(.25f, .9f, .34f);
            AddBadge(kit, boss ? "Boss Core" : "Invader Core", color, new Vector3(0f, .05f, -.20f), boss ? .30f : .16f);
        }

        private static void AddBadge(Transform parent, string name, Color color, Vector3 localPosition, float scale)
        {
            var badge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            badge.name = name;
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = localPosition;
            badge.transform.localScale = new Vector3(scale, scale, scale * .28f);
            var collider = badge.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { renderQueue = (int)RenderQueue.Transparent + 26 };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * .32f);
            }
            badge.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
