using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    // Fixed-camera 2.5D presentation.  Gameplay keeps its precise top-down coordinates, while
    // characters, cliffs and the citadel are real lit meshes with depth, limbs and animation.
    // This avoids using a baked map image as either a collision mask or a fake character model.
    public sealed class Stylized2p5DUnitRig : MonoBehaviour
    {
        private Transform torso;
        private Transform head;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform weapon;
        private Transform offhand;
        private Transform halo;
        private Transform cape;
        private readonly List<Renderer> renderers = new();
        private float seed;
        private bool hero;
        private bool flying;

        public static Stylized2p5DUnitRig CreateDefender(Transform parent, UnitArchetype archetype,
            Color teamColor, float radius, bool isHero)
        {
            var root = new GameObject("2.5D Defender Rig").AddComponent<Stylized2p5DUnitRig>();
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, -.32f);
            root.transform.localScale = Vector3.one * Mathf.Max(.42f, radius * 2.05f);
            root.seed = Random.value * 8f;
            root.BuildDefender(archetype, teamColor);
            if (isHero) root.SetHero(true);
            return root;
        }

        public static Stylized2p5DUnitRig CreateEnemy(Transform parent, EnemyClass enemyClass,
            Color enemyColor, float radius, bool isBoss)
        {
            var root = new GameObject("2.5D Enemy Rig").AddComponent<Stylized2p5DUnitRig>();
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, -.31f);
            root.transform.localScale = Vector3.one * Mathf.Max(.35f, radius * (isBoss ? 3.05f : 2.22f));
            root.seed = Random.value * 8f;
            root.flying = enemyClass == EnemyClass.Flyer;
            root.BuildEnemy(enemyClass, enemyColor, isBoss);
            if (isBoss) root.SetHero(true);
            return root;
        }

        public void SetHero(bool value)
        {
            if (hero == value) return;
            hero = value;
            if (!hero) return;
            var gold = new Color(1f, .69f, .16f);
            Part("Hero Crown", PrimitiveType.Cube, new Vector3(0f, 1.04f, -.1f), new Vector3(.48f, .13f, .21f), gold, .78f);
            cape = Part("Hero Cape", PrimitiveType.Cube, new Vector3(0f, -.12f, .18f), new Vector3(.56f, .92f, .08f),
                new Color(.12f, .35f, .84f), .1f);
            halo = Part("Hero Ground Aura", PrimitiveType.Sphere, new Vector3(0f, -.9f, .09f),
                new Vector3(1.18f, .14f, .06f), new Color(.2f, .86f, 1f), 1.2f);
        }

        public void Animate(Vector2 facing, bool moving, float attackT, float hurtT, bool isHero, bool isFlying)
        {
            if (torso == null) return;
            if (isHero && !hero) SetHero(true);
            flying |= isFlying;
            var time = Time.time + seed;
            var gait = moving ? Mathf.Sin(time * 11f) : 0f;
            var strike = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
            var hurt = Mathf.Sin(Mathf.Clamp01(hurtT) * Mathf.PI);
            var forward = facing.sqrMagnitude > .01f ? facing.normalized : Vector2.up;
            var sideTilt = Mathf.Clamp(forward.x, -1f, 1f);
            var lift = flying ? .22f + Mathf.Sin(time * 7f) * .11f : Mathf.Abs(gait) * .055f;

            transform.localPosition = new Vector3(forward.x * strike * .13f,
                lift + strike * .08f + hurt * .03f, -.32f);
            torso.localScale = new Vector3(1f + hurt * .1f + strike * .035f,
                1f - hurt * .075f - strike * .045f, 1f);
            torso.localRotation = Quaternion.Euler(0f, 0f, -sideTilt * (moving ? 5f : 1.4f) + strike * sideTilt * 8f);
            if (head != null)
            {
                head.localPosition = new Vector3(0f, .57f + Mathf.Abs(gait) * .038f + strike * .04f, -.04f);
                head.localRotation = Quaternion.Euler(0f, sideTilt * 14f, 0f);
            }
            AnimateLimb(leftLeg, -1f, gait, .78f, 23f);
            AnimateLimb(rightLeg, 1f, gait, .78f, 23f);
            AnimateLimb(leftArm, -1f, gait, .36f, 17f + strike * 20f);
            AnimateLimb(rightArm, 1f, gait, .36f, 17f + strike * 32f);
            if (weapon != null)
            {
                weapon.localRotation = Quaternion.Euler(0f, 0f,
                    -sideTilt * 22f + Mathf.Lerp(-18f, 54f, strike) * Mathf.Sign(sideTilt == 0f ? 1f : sideTilt));
                weapon.localPosition = new Vector3(.42f + forward.x * strike * .12f,
                    .08f + forward.y * strike * .11f, -.2f);
            }
            if (offhand != null)
                offhand.localRotation = Quaternion.Euler(0f, 0f, sideTilt * -10f - strike * 12f);
            if (cape != null)
                cape.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 4.2f) * 6f + sideTilt * 5f);
            if (halo != null)
            {
                var pulse = 1f + Mathf.Sin(time * 4.8f) * .09f;
                halo.localScale = new Vector3(1.18f * pulse, .14f * pulse, .06f);
            }
        }

        private void AnimateLimb(Transform limb, float side, float gait, float amplitude, float degrees)
        {
            if (limb == null) return;
            limb.localRotation = Quaternion.Euler(0f, 0f, gait * side * degrees * amplitude);
            limb.localPosition = new Vector3(limb.localPosition.x, limb.localPosition.y + Mathf.Abs(gait * side) * .035f, limb.localPosition.z);
        }

        private void BuildDefender(UnitArchetype archetype, Color teamColor)
        {
            var dark = Color.Lerp(teamColor, new Color(.055f, .07f, .12f), .58f);
            var metal = new Color(.62f, .73f, .84f);
            torso = Part("Armored Torso", PrimitiveType.Capsule, new Vector3(0f, -.02f, 0f), new Vector3(.58f, .78f, .3f), teamColor, .48f);
            head = Part("Chibi Head", PrimitiveType.Sphere, new Vector3(0f, .57f, -.04f), new Vector3(.56f, .56f, .38f), new Color(1f, .72f, .5f), .06f);
            leftLeg = Part("Left Leg", PrimitiveType.Cube, new Vector3(-.18f, -.62f, .01f), new Vector3(.2f, .46f, .22f), dark, .25f);
            rightLeg = Part("Right Leg", PrimitiveType.Cube, new Vector3(.18f, -.62f, .01f), new Vector3(.2f, .46f, .22f), dark, .25f);
            leftArm = Part("Left Arm", PrimitiveType.Capsule, new Vector3(-.42f, .03f, -.02f), new Vector3(.16f, .48f, .19f), teamColor, .35f);
            rightArm = Part("Right Arm", PrimitiveType.Capsule, new Vector3(.42f, .03f, -.02f), new Vector3(.16f, .48f, .19f), teamColor, .35f);
            Part("Left Eye", PrimitiveType.Sphere, new Vector3(-.11f, .62f, -.27f), new Vector3(.075f, .095f, .04f), new Color(.06f, .08f, .12f), .05f);
            Part("Right Eye", PrimitiveType.Sphere, new Vector3(.11f, .62f, -.27f), new Vector3(.075f, .095f, .04f), new Color(.06f, .08f, .12f), .05f);

            switch (archetype)
            {
                case UnitArchetype.Tank:
                    offhand = Part("Tower Shield", PrimitiveType.Cube, new Vector3(-.48f, .03f, -.25f), new Vector3(.22f, .7f, .1f),
                        new Color(.08f, .25f, .7f), .65f);
                    Part("Shield Crest", PrimitiveType.Sphere, new Vector3(-.48f, .05f, -.33f), new Vector3(.1f, .12f, .03f), new Color(1f, .72f, .16f), .9f);
                    weapon = Part("Tank Mace", PrimitiveType.Capsule, new Vector3(.44f, .06f, -.2f), new Vector3(.08f, .56f, .08f), metal, .65f);
                    break;
                case UnitArchetype.Archer:
                case UnitArchetype.Musketeer:
                    weapon = Part(archetype == UnitArchetype.Archer ? "Bow" : "Musket", PrimitiveType.Cube,
                        new Vector3(.46f, .1f, -.2f), archetype == UnitArchetype.Archer ? new Vector3(.08f, .66f, .08f) : new Vector3(.68f, .1f, .1f),
                        archetype == UnitArchetype.Archer ? new Color(.5f, .25f, .08f) : metal, .5f);
                    break;
                case UnitArchetype.AreaMage:
                case UnitArchetype.SingleMage:
                case UnitArchetype.Druid:
                case UnitArchetype.Oracle:
                    Part("Wizard Hat", PrimitiveType.Cylinder, new Vector3(0f, .93f, -.03f), new Vector3(.48f, .65f, .3f),
                        archetype == UnitArchetype.AreaMage ? new Color(.46f, .12f, .78f) : new Color(.14f, .54f, .82f), .28f);
                    weapon = Part("Focus Staff", PrimitiveType.Capsule, new Vector3(.43f, .03f, -.2f), new Vector3(.07f, .78f, .07f),
                        new Color(.38f, .2f, .08f), .2f);
                    Part("Focus Crystal", PrimitiveType.Sphere, new Vector3(.43f, .48f, -.25f), new Vector3(.17f, .17f, .09f),
                        new Color(.38f, .9f, 1f), 1.3f);
                    break;
                case UnitArchetype.Lancer:
                    weapon = Part("Lance", PrimitiveType.Capsule, new Vector3(.45f, .14f, -.2f), new Vector3(.07f, 1.05f, .07f), metal, .58f);
                    break;
                case UnitArchetype.Bombardier:
                    weapon = Part("Bomb Cannon", PrimitiveType.Cylinder, new Vector3(.4f, .04f, -.2f), new Vector3(.34f, .24f, .24f),
                        new Color(.16f, .2f, .28f), .62f);
                    break;
                default:
                    weapon = Part("Sword", PrimitiveType.Capsule, new Vector3(.44f, .07f, -.2f), new Vector3(.08f, .72f, .08f), metal, .78f);
                    break;
            }
        }

        private void BuildEnemy(EnemyClass enemyClass, Color enemyColor, bool boss)
        {
            var dark = Color.Lerp(enemyColor, new Color(.05f, .04f, .08f), .62f);
            var size = boss ? 1.22f : 1f;
            torso = Part("Enemy Torso", enemyClass is EnemyClass.Wisp or EnemyClass.Flyer ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                new Vector3(0f, -.02f, 0f), new Vector3(.64f * size, .76f * size, .36f), enemyColor, .34f);
            head = Part("Enemy Head", PrimitiveType.Sphere, new Vector3(0f, .55f * size, -.05f), new Vector3(.58f * size, .52f * size, .38f),
                Color.Lerp(enemyColor, Color.white, .18f), .18f);
            leftLeg = Part("Enemy Left Leg", PrimitiveType.Cube, new Vector3(-.19f * size, -.62f * size, .01f), new Vector3(.2f, .45f, .21f), dark, .18f);
            rightLeg = Part("Enemy Right Leg", PrimitiveType.Cube, new Vector3(.19f * size, -.62f * size, .01f), new Vector3(.2f, .45f, .21f), dark, .18f);
            leftArm = Part("Enemy Left Arm", PrimitiveType.Capsule, new Vector3(-.44f * size, .03f, -.02f), new Vector3(.16f, .46f, .18f), enemyColor, .2f);
            rightArm = Part("Enemy Right Arm", PrimitiveType.Capsule, new Vector3(.44f * size, .03f, -.02f), new Vector3(.16f, .46f, .18f), enemyColor, .2f);
            Part("Enemy Left Eye", PrimitiveType.Sphere, new Vector3(-.12f * size, .59f * size, -.29f), new Vector3(.09f, .1f, .035f),
                new Color(1f, .85f, .24f), 1.4f);
            Part("Enemy Right Eye", PrimitiveType.Sphere, new Vector3(.12f * size, .59f * size, -.29f), new Vector3(.09f, .1f, .035f),
                new Color(1f, .85f, .24f), 1.4f);

            if (enemyClass is EnemyClass.Mage or EnemyClass.Shaman)
            {
                Part("Enemy Mage Hat", PrimitiveType.Cylinder, new Vector3(0f, .92f * size, -.02f), new Vector3(.54f, .68f, .32f),
                    new Color(.3f, .08f, .55f), .32f);
                weapon = Part("Enemy Staff", PrimitiveType.Capsule, new Vector3(.43f * size, .04f, -.2f), new Vector3(.07f, .8f, .07f),
                    new Color(.2f, .09f, .04f), .2f);
                Part("Enemy Orb", PrimitiveType.Sphere, new Vector3(.43f * size, .48f, -.26f), new Vector3(.18f, .18f, .08f),
                    new Color(.78f, .22f, 1f), 1.45f);
            }
            else if (enemyClass == EnemyClass.Siege || enemyClass == EnemyClass.Brute || boss)
            {
                torso.localScale *= 1.22f;
                weapon = Part("Heavy Hammer", PrimitiveType.Cube, new Vector3(.47f * size, .04f, -.2f), new Vector3(.22f, .65f, .16f),
                    new Color(.22f, .24f, .31f), .6f);
                Part("Heavy Core", PrimitiveType.Sphere, new Vector3(0f, .02f, -.25f), new Vector3(.2f, .2f, .07f),
                    new Color(1f, .28f, .1f), 1.5f);
            }
            else if (enemyClass == EnemyClass.Flyer)
            {
                Part("Left Wing", PrimitiveType.Cube, new Vector3(-.65f, .15f, .08f), new Vector3(.72f, .16f, .08f), dark, .12f);
                Part("Right Wing", PrimitiveType.Cube, new Vector3(.65f, .15f, .08f), new Vector3(.72f, .16f, .08f), dark, .12f);
            }
            else
            {
                weapon = Part("Enemy Blade", PrimitiveType.Capsule, new Vector3(.44f, .06f, -.2f), new Vector3(.08f, .7f, .08f),
                    new Color(.67f, .7f, .74f), .62f);
            }
        }

        private Transform Part(string partName, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale,
            Color color, float emission)
        {
            var piece = GameObject.CreatePrimitive(primitive);
            piece.name = partName;
            piece.transform.SetParent(transform, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localScale = localScale;
            var collider = piece.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = piece.GetComponent<Renderer>();
            renderer.material = Stylized2p5DFactory.Material(color, emission);
            renderers.Add(renderer);
            return piece.transform;
        }
    }

    public static class Stylized2p5DFactory
    {
        private static readonly Dictionary<string, Material> CachedMaterials = new();

        public static Material Material(Color color, float emission = 0f)
        {
            var color32 = (Color32)color;
            var key = $"{color32.r}:{color32.g}:{color32.b}:{color32.a}:{Mathf.RoundToInt(emission * 20f)}";
            if (CachedMaterials.TryGetValue(key, out var cached) && cached != null) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.color = color;
            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            material.enableInstancing = true;
            CachedMaterials[key] = material;
            return material;
        }

        public static Transform CreateEffectMesh(Transform parent, string name, PrimitiveType type, Vector3 localPosition,
            Vector3 localScale, Color color, float emission = 0f)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = localScale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            value.GetComponent<Renderer>().material = Material(color, emission);
            return value.transform;
        }

        public static void BuildBattlefield(Transform parent)
        {
            var root = new GameObject("2.5D Battlefield Volumes");
            root.transform.SetParent(parent, false);
            CreateLight(root.transform);
            CreateCitadel(root.transform);
            CreateHill(root.transform, new Vector2(-1.86f, 4.36f), new Vector2(.88f, .72f));
            CreateHill(root.transform, new Vector2(1.86f, 4.36f), new Vector2(.88f, .72f));
            CreateHill(root.transform, new Vector2(-1.94f, -3.58f), new Vector2(.92f, .78f));
            CreateHill(root.transform, new Vector2(1.94f, -3.58f), new Vector2(.92f, .78f));
        }

        private static void CreateLight(Transform parent)
        {
            var lightObject = new GameObject("2.5D Key Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = new Vector3(-3.5f, 6f, -5f);
            lightObject.transform.rotation = Quaternion.Euler(25f, -20f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, .88f, .7f);
            light.intensity = 1.35f;
            var fillObject = new GameObject("2.5D Fill Light");
            fillObject.transform.SetParent(parent, false);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.34f, .56f, 1f);
            fill.intensity = .42f;
            fillObject.transform.rotation = Quaternion.Euler(-20f, 35f, 0f);
        }

        private static void CreateCitadel(Transform parent)
        {
            var stone = new Color(.32f, .38f, .43f);
            var stoneLight = new Color(.58f, .61f, .6f);
            var blue = new Color(.08f, .24f, .68f);
            var gold = new Color(1f, .65f, .14f);
            Block(parent, "Citadel Base", new Vector3(0f, 0f, -1.94f), new Vector3(2.82f, 2.55f, .36f), stone, .12f);
            Block(parent, "Citadel Gate", new Vector3(0f, -.22f, -2.16f), new Vector3(.7f, .92f, .12f), new Color(.09f, .075f, .06f), .04f);
            Block(parent, "Citadel Crown", new Vector3(0f, 1.04f, -2.18f), new Vector3(1.18f, .32f, .16f), stoneLight, .16f);
            Block(parent, "Citadel Banner", new Vector3(0f, .88f, -2.28f), new Vector3(.25f, .66f, .06f), blue, .45f);
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            {
                var tower = Primitive(parent, "Citadel Tower", PrimitiveType.Cylinder,
                    new Vector3(x * 1.04f, y * .76f, -2.16f), new Vector3(.42f, .68f, .2f), stoneLight, .15f);
                Primitive(tower, "Blue Roof", PrimitiveType.Sphere, new Vector3(0f, .56f, -.05f), new Vector3(.45f, .28f, .16f), blue, .24f);
                Primitive(tower, "Gold Tip", PrimitiveType.Sphere, new Vector3(0f, .79f, -.08f), new Vector3(.11f, .11f, .05f), gold, .9f);
            }
        }

        private static void CreateHill(Transform parent, Vector2 position, Vector2 radius)
        {
            var rock = new Color(.19f, .23f, .23f);
            var grass = new Color(.19f, .48f, .2f);
            Primitive(parent, "Cliff Rock Volume", PrimitiveType.Sphere, new Vector3(position.x, position.y, -.38f),
                new Vector3(radius.x * 2.25f, radius.y * 2.25f, .32f), rock, .06f);
            Primitive(parent, "Raised Grass Crown", PrimitiveType.Sphere, new Vector3(position.x, position.y + .08f, -.57f),
                new Vector3(radius.x * 1.85f, radius.y * 1.82f, .21f), grass, .08f);
            for (var i = 0; i < 3; i++)
            {
                var offset = new Vector2(Mathf.Sin(i * 2.4f + position.x) * radius.x * .52f,
                    Mathf.Cos(i * 1.8f + position.y) * radius.y * .45f);
                var trunk = Primitive(parent, "Tree Trunk", PrimitiveType.Cylinder,
                    new Vector3(position.x + offset.x, position.y + offset.y - .03f, -.67f), new Vector3(.07f, .2f, .06f),
                    new Color(.22f, .11f, .04f), .03f);
                Primitive(trunk, "Tree Crown", PrimitiveType.Sphere, new Vector3(0f, .18f, -.05f), new Vector3(.26f, .34f, .12f),
                    new Color(.08f, .34f, .12f), .05f);
            }
        }

        private static Transform Block(Transform parent, string name, Vector3 position, Vector3 scale, Color color, float emission)
        {
            return Primitive(parent, name, PrimitiveType.Cube, position, scale, color, emission);
        }

        private static Transform Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Color color, float emission)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            value.GetComponent<Renderer>().material = Material(color, emission);
            return value.transform;
        }
    }

    // Mesh effects sit in the same depth space as the rigs.  The retained sprite particles add
    // readability on small phones; these meshes add volume, light and debris to each impact.
    public sealed class Stylized2p5DMeshEffect : MonoBehaviour
    {
        private readonly List<Transform> fragments = new();
        private Transform core;
        private Transform ring;
        private float duration;
        private float elapsed;
        private float radius;
        private EffectKind kind;

        private enum EffectKind { Impact, Slam, Dust, Contact }

        public static void SpawnImpact(Vector2 position, Color color, float effectRadius)
        {
            Spawn(position, color, effectRadius, EffectKind.Impact);
        }

        public static void SpawnSlam(Vector2 position, Color color, float effectRadius)
        {
            Spawn(position, color, effectRadius, EffectKind.Slam);
        }

        public static void SpawnDust(Vector2 position, Color color, float effectRadius)
        {
            Spawn(position, Color.Lerp(new Color(.56f, .38f, .18f), color, .18f), effectRadius, EffectKind.Dust);
        }

        public static void SpawnContact(Vector2 position, Color color, float effectRadius)
        {
            Spawn(position, color, effectRadius, EffectKind.Contact);
        }

        private static void Spawn(Vector2 position, Color color, float effectRadius, EffectKind effectKind)
        {
            var root = TransientBattleEffect.Create($"2.5D {effectKind} Effect");
            var value = root.AddComponent<Stylized2p5DMeshEffect>();
            value.transform.position = new Vector3(position.x, position.y, -3.15f + position.y * .08f);
            value.kind = effectKind;
            value.radius = Mathf.Max(.2f, effectRadius);
            value.duration = effectKind switch
            {
                EffectKind.Slam => .42f,
                EffectKind.Dust => .3f,
                EffectKind.Contact => .24f,
                _ => .32f
            };
            value.Build(color);
        }

        private void Build(Color color)
        {
            core = Piece("Core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .18f, Color.Lerp(color, Color.white, .55f), 1.6f);
            ring = Piece("Ring", PrimitiveType.Sphere, new Vector3(0f, 0f, .08f), new Vector3(.22f, .08f, .04f), color, .85f);
            var count = kind == EffectKind.Slam ? 9 : kind == EffectKind.Impact ? 6 : 4;
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + Random.Range(-.18f, .18f);
                var part = Piece($"Fragment {i + 1}", kind == EffectKind.Dust ? PrimitiveType.Sphere : PrimitiveType.Cube,
                    Vector3.zero, Vector3.one * (kind == EffectKind.Dust ? .1f : .075f),
                    i % 2 == 0 ? Color.Lerp(color, Color.white, .35f) : color, .55f);
                part.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                part.gameObject.AddComponent<EffectFragmentDirection>().Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                fragments.Add(part);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var ease = 1f - Mathf.Pow(1f - t, 3f);
            if (core != null)
            {
                core.localScale = Vector3.one * Mathf.Lerp(radius * .45f, .01f, ease);
                core.localPosition = new Vector3(0f, Mathf.Sin(t * Mathf.PI) * radius * .14f, 0f);
            }
            if (ring != null)
                ring.localScale = new Vector3(radius * Mathf.Lerp(.3f, 2.8f, ease), radius * Mathf.Lerp(.1f, .42f, ease), .04f);
            foreach (var part in fragments)
            {
                if (part == null) continue;
                var direction = part.GetComponent<EffectFragmentDirection>();
                var offset = direction != null ? direction.Direction : Vector2.zero;
                part.localPosition = new Vector3(offset.x * radius * ease, offset.y * radius * ease + Mathf.Sin(t * Mathf.PI) * .1f, -.05f);
                part.localScale = Vector3.one * Mathf.Lerp(kind == EffectKind.Dust ? .08f : .11f, .01f, t);
                part.localRotation *= Quaternion.Euler(0f, 0f, 720f * Time.deltaTime);
            }
            if (t >= 1f) Destroy(gameObject);
        }

        private Transform Piece(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, float emission)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(transform, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            var collider = value.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            value.GetComponent<Renderer>().material = Stylized2p5DFactory.Material(color, emission);
            return value.transform;
        }
    }

    public sealed class EffectFragmentDirection : MonoBehaviour
    {
        public Vector2 Direction;
    }
}
