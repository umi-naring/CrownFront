using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JellyGate
{
    // Custom real-geometry character kit for CROWNFRONT.  This deliberately follows the
    // established squad silhouettes (blue crown shield, red hammer, mint hood archer,
    // violet star witch, blue orb mage) instead of substituting a generic asset-store cast.
    // Every visible part is a lit mesh and every motion is driven by a small runtime rig.
    public sealed class Crownfront3DUnitVisual : MonoBehaviour
    {
        private static readonly Dictionary<string, Material> Materials = new();

        private Transform facingRoot;
        private Transform bodyRoot;
        private Transform headRoot;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform weaponRoot;
        private Transform offhandRoot;
        private Transform magicCore;
        private Transform heroCrown;
        private Transform heroCape;
        private bool caster;
        private bool flying;
        private bool hero;
        private float lastAttack;
        private float lastHurt;
        private float phase;

        public static Crownfront3DUnitVisual CreateDefender(Transform parent, UnitArchetype archetype,
            Color teamColor, float radius, bool isHero)
        {
            var visual = CreateRoot(parent, "Crownfront Defender 3D", radius, isHero, false);
            visual.caster = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or UnitArchetype.Druid or UnitArchetype.Oracle;
            visual.BuildDefender(archetype, teamColor);
            visual.SetHero(isHero);
            return visual;
        }

        public static Crownfront3DUnitVisual CreateEnemy(Transform parent, EnemyClass enemyClass,
            Color enemyColor, float radius, bool isBoss)
        {
            var visual = CreateRoot(parent, "Crownfront Invader 3D", radius, isBoss, enemyClass == EnemyClass.Flyer);
            visual.caster = enemyClass is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp;
            visual.BuildEnemy(enemyClass, enemyColor, isBoss);
            visual.SetHero(isBoss);
            return visual;
        }

        private static Crownfront3DUnitVisual CreateRoot(Transform parent, string name, float radius, bool isHero, bool isFlying)
        {
            var root = new GameObject(name).AddComponent<Crownfront3DUnitVisual>();
            root.transform.SetParent(parent, false);
            // Ground units deliberately sit on the board.  The former negative-Z lift is gone:
            // it was the reason feet appeared to hover over the road.
            // Sprites on the tactical board write to the same depth buffer.  Keep the full
            // character rig decisively in front of that board; at the orthographic 2.5D camera
            // this changes draw depth only, not world scale or the unit's ground position.
            root.transform.localPosition = new Vector3(0f, 0f, -3.15f);
            root.transform.localScale = Vector3.one * Mathf.Clamp(radius * (isHero ? 2.75f : 2.35f), .52f, isHero ? .9f : .7f);
            root.phase = Random.value * 12f;
            root.hero = isHero;
            root.flying = isFlying;

            root.facingRoot = new GameObject("Facing Root").transform;
            root.facingRoot.SetParent(root.transform, false);
            root.bodyRoot = new GameObject("Body Rig").transform;
            root.bodyRoot.SetParent(root.facingRoot, false);
            return root;
        }

        public void SetHero(bool value)
        {
            hero = value;
            if (!hero || heroCrown != null) return;
            heroCrown = new GameObject("Hero Crown").transform;
            heroCrown.SetParent(headRoot != null ? headRoot : bodyRoot, false);
            heroCrown.localPosition = new Vector3(0f, .34f, -.01f);
            var gold = Mat(new Color(1f, .66f, .14f), .82f, .82f);
            AddCrown(heroCrown, gold, .38f);
            heroCape = CreateCloak(bodyRoot, "Hero Cape", new Color(.06f, .22f, .65f), new Vector3(0f, -.06f, .18f), .66f, .98f);
        }

        public void Animate(Vector2 facing, bool moving, float attackT, float hurtT, bool isHero, bool isFlying)
        {
            if (isHero != hero) SetHero(isHero);
            flying = isFlying;
            if (facing.sqrMagnitude > .002f)
            {
                var d = facing.normalized;
                // The rig is modelled front-facing along -Z.  Rotating around Y gives real
                // front/profile/back silhouettes instead of swapping a flat direction sprite.
                var heading = -Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
                facingRoot.localRotation = Quaternion.Euler(0f, heading, 0f);
            }

            var gait = moving ? Mathf.Sin((Time.time + phase) * 8.4f) : 0f;
            var attack = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
            var hurt = Mathf.Sin(Mathf.Clamp01(hurtT) * Mathf.PI);
            bodyRoot.localPosition = new Vector3(0f, flying ? .18f + Mathf.Sin((Time.time + phase) * 5f) * .025f : 0f, 0f);
            bodyRoot.localRotation = Quaternion.Euler(0f, 0f, hurt * 5f);

            SwingLimb(leftLeg, gait, 1f, 30f, .035f);
            SwingLimb(rightLeg, gait, -1f, 30f, .035f);
            SwingLimb(leftArm, gait, -1f, caster ? 15f : 22f, .015f);
            SwingLimb(rightArm, gait, 1f, caster ? 18f : 25f, .015f);

            if (weaponRoot != null)
            {
                var sweep = caster ? -25f * attack : 58f * attack;
                weaponRoot.localRotation = Quaternion.Euler(-attack * (caster ? 18f : 8f), 0f, sweep);
                weaponRoot.localPosition = new Vector3(0f, attack * .06f, -attack * .035f);
            }
            if (offhandRoot != null)
                offhandRoot.localRotation = Quaternion.Euler(0f, 0f, -18f * attack);
            if (magicCore != null)
            {
                var pulse = 1f + attack * .28f + Mathf.Sin((Time.time + phase) * 5f) * .045f;
                magicCore.localScale = Vector3.one * pulse;
            }
            if (heroCape != null)
                heroCape.localRotation = Quaternion.Euler(0f, Mathf.Sin((Time.time + phase) * 3.2f) * 7f, 0f);

            // Rising edges are retained for compatibility with callers that send animation
            // windows; the visible motion itself is continuous and therefore cannot freeze on
            // a two-frame sprite edge.
            lastAttack = attackT;
            lastHurt = hurtT;
        }

        public void PlayCast()
        {
            if (weaponRoot != null) weaponRoot.localRotation = Quaternion.Euler(-18f, 0f, -32f);
        }

        private void BuildDefender(UnitArchetype archetype, Color teamColor)
        {
            var blue = new Color(.055f, .26f, .70f);
            var red = new Color(.78f, .16f, .10f);
            var mint = new Color(.05f, .52f, .49f);
            var purple = new Color(.42f, .14f, .72f);
            var sky = new Color(.08f, .38f, .78f);
            var gold = Mat(new Color(1f, .62f, .11f), .9f, .76f);
            var leather = Mat(new Color(.25f, .11f, .045f), .04f, .28f);
            var skin = Mat(new Color(.96f, .65f, .43f), .02f, .52f);
            var steel = Mat(new Color(.54f, .64f, .76f), .78f, .58f);

            var main = archetype switch
            {
                UnitArchetype.Tank => blue,
                UnitArchetype.Melee or UnitArchetype.Bombardier => red,
                UnitArchetype.Archer or UnitArchetype.Musketeer or UnitArchetype.Lancer => mint,
                UnitArchetype.AreaMage or UnitArchetype.Druid => purple,
                _ => sky
            };
            var mainMat = Mat(main, .18f, .5f);
            var darkMat = Mat(Color.Lerp(main, new Color(.035f, .04f, .075f), .55f), .1f, .42f);

            CreateTorso(mainMat, darkMat, skin);
            switch (archetype)
            {
                case UnitArchetype.Tank:
                    AddHelmet(blue, gold, true);
                    AddCrestShield(offhandRoot, blue, gold, .84f);
                    AddSword(weaponRoot, steel, gold, .76f);
                    break;
                case UnitArchetype.Melee:
                case UnitArchetype.Bombardier:
                    AddHelmet(red, gold, false);
                    AddHammer(weaponRoot, red, steel, gold, archetype == UnitArchetype.Bombardier ? .88f : .74f);
                    CreateCloak(bodyRoot, "Crimson Back Cape", red, new Vector3(0f, -.05f, .18f), .54f, .72f);
                    break;
                case UnitArchetype.Archer:
                case UnitArchetype.Musketeer:
                    AddHood(mint, gold);
                    AddBow(weaponRoot, leather, gold, archetype == UnitArchetype.Musketeer);
                    CreateCloak(bodyRoot, "Mint Hood Cape", mint, new Vector3(0f, -.03f, .17f), .62f, .84f);
                    break;
                case UnitArchetype.AreaMage:
                case UnitArchetype.Druid:
                    AddWizardHat(purple, gold, true);
                    AddStaff(weaponRoot, leather, new Color(1f, .72f, .12f), true);
                    CreateCloak(bodyRoot, "Violet Star Robe", purple, new Vector3(0f, -.05f, .16f), .72f, 1.02f);
                    break;
                case UnitArchetype.SingleMage:
                case UnitArchetype.Oracle:
                    AddHood(sky, gold);
                    AddStaff(weaponRoot, leather, new Color(.15f, .82f, 1f), false);
                    CreateCloak(bodyRoot, "Azure Orb Robe", sky, new Vector3(0f, -.05f, .16f), .72f, 1.02f);
                    break;
                case UnitArchetype.Lancer:
                    AddHelmet(mint, gold, false);
                    AddLance(weaponRoot, steel, gold);
                    break;
                default:
                    AddHelmet(main, gold, false);
                    AddSword(weaponRoot, steel, gold, .72f);
                    break;
            }
        }

        private void BuildEnemy(EnemyClass enemyClass, Color enemyColor, bool boss)
        {
            var green = Color.Lerp(new Color(.25f, .65f, .24f), enemyColor, .22f);
            var bone = new Color(.79f, .75f, .61f);
            var violet = new Color(.44f, .10f, .68f);
            var ember = new Color(.92f, .21f, .10f);
            var leather = Mat(new Color(.20f, .08f, .035f), .03f, .28f);
            var boneMat = Mat(bone, .1f, .5f);
            var metal = Mat(new Color(.3f, .31f, .37f), .65f, .42f);
            var main = enemyClass is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp ? violet :
                enemyClass is EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Boss ? ember : green;
            var mainMat = Mat(main, .14f, .43f);
            var darkMat = Mat(Color.Lerp(main, Color.black, .55f), .04f, .3f);
            var skin = Mat(enemyClass == EnemyClass.Skeleton ? bone : Color.Lerp(main, Color.white, .2f), .02f, .38f);
            CreateTorso(mainMat, darkMat, skin, enemyClass == EnemyClass.Skeleton);

            if (enemyClass is EnemyClass.Mage or EnemyClass.Shaman or EnemyClass.Wisp)
            {
                AddWizardHat(violet, Mat(new Color(.92f, .36f, 1f), .38f, .65f), false);
                AddStaff(weaponRoot, leather, new Color(.82f, .28f, 1f), true);
                CreateCloak(bodyRoot, "Invoker Mantle", violet, new Vector3(0f, -.04f, .16f), .7f, .92f);
            }
            else if (enemyClass is EnemyClass.Brute or EnemyClass.Siege or EnemyClass.Boss)
            {
                AddHelmet(ember, Mat(new Color(.25f, .24f, .3f), .6f, .4f), false);
                AddHammer(weaponRoot, ember, metal, metal, boss ? 1.14f : .92f);
                AddCrestShield(offhandRoot, new Color(.22f, .08f, .06f), metal, .68f);
            }
            else if (enemyClass == EnemyClass.Flyer)
            {
                AddHood(violet, Mat(new Color(.88f, .28f, 1f), .4f, .7f));
                AddWing(bodyRoot, "Left Wing", -1f, darkMat);
                AddWing(bodyRoot, "Right Wing", 1f, darkMat);
            }
            else
            {
                AddHelmet(main, metal, false);
                AddSword(weaponRoot, metal, metal, .67f);
            }

            if (boss)
            {
                var crown = new GameObject("Boss Horn Crown").transform;
                crown.SetParent(headRoot, false);
                crown.localPosition = new Vector3(0f, .38f, 0f);
                AddCrown(crown, Mat(new Color(.72f, .12f, .92f), .3f, .7f), .44f);
            }
        }

        private void CreateTorso(Material main, Material dark, Material skin, bool skeletal = false)
        {
            CreatePart(bodyRoot, "Boot L", PrimitiveType.Sphere, new Vector3(-.18f, -.57f, .02f), new Vector3(.23f, .16f, .26f), dark);
            CreatePart(bodyRoot, "Boot R", PrimitiveType.Sphere, new Vector3(.18f, -.57f, .02f), new Vector3(.23f, .16f, .26f), dark);
            leftLeg = new GameObject("Left Leg Rig").transform;
            rightLeg = new GameObject("Right Leg Rig").transform;
            leftLeg.SetParent(bodyRoot, false); rightLeg.SetParent(bodyRoot, false);
            leftLeg.localPosition = new Vector3(-.17f, -.34f, 0f); rightLeg.localPosition = new Vector3(.17f, -.34f, 0f);
            CreatePart(leftLeg, "Left Greave", PrimitiveType.Capsule, new Vector3(0f, -.10f, 0f), new Vector3(.19f, .34f, .19f), dark);
            CreatePart(rightLeg, "Right Greave", PrimitiveType.Capsule, new Vector3(0f, -.10f, 0f), new Vector3(.19f, .34f, .19f), dark);
            CreatePart(bodyRoot, skeletal ? "Rib Armour" : "Armour Torso", PrimitiveType.Capsule, new Vector3(0f, .02f, .02f), new Vector3(.57f, .64f, .34f), main);
            CreatePart(bodyRoot, "Belt", PrimitiveType.Cylinder, new Vector3(0f, -.20f, -.02f), new Vector3(.38f, .07f, .22f), dark);

            leftArm = new GameObject("Left Arm Rig").transform;
            rightArm = new GameObject("Right Arm Rig").transform;
            leftArm.SetParent(bodyRoot, false); rightArm.SetParent(bodyRoot, false);
            leftArm.localPosition = new Vector3(-.38f, .11f, 0f); rightArm.localPosition = new Vector3(.38f, .11f, 0f);
            CreatePart(leftArm, "Left Bracer", PrimitiveType.Capsule, new Vector3(0f, -.12f, -.02f), new Vector3(.17f, .36f, .19f), main);
            CreatePart(rightArm, "Right Bracer", PrimitiveType.Capsule, new Vector3(0f, -.12f, -.02f), new Vector3(.17f, .36f, .19f), main);
            CreatePart(leftArm, "Left Glove", PrimitiveType.Sphere, new Vector3(0f, -.32f, -.04f), new Vector3(.17f, .17f, .16f), skin);
            CreatePart(rightArm, "Right Glove", PrimitiveType.Sphere, new Vector3(0f, -.32f, -.04f), new Vector3(.17f, .17f, .16f), skin);
            offhandRoot = leftArm;
            weaponRoot = rightArm;

            headRoot = new GameObject("Head Rig").transform;
            headRoot.SetParent(bodyRoot, false);
            headRoot.localPosition = new Vector3(0f, .49f, -.03f);
            CreatePart(headRoot, "Chibi Face", PrimitiveType.Sphere, new Vector3(0f, 0f, -.035f), new Vector3(.48f, .46f, .36f), skin);
            var eye = Mat(new Color(.075f, .05f, .055f), .05f, .7f);
            CreatePart(headRoot, "Eye L", PrimitiveType.Sphere, new Vector3(-.115f, .01f, -.315f), new Vector3(.07f, .09f, .035f), eye);
            CreatePart(headRoot, "Eye R", PrimitiveType.Sphere, new Vector3(.115f, .01f, -.315f), new Vector3(.07f, .09f, .035f), eye);
        }

        private void AddHelmet(Color color, Material trim, bool crown)
        {
            var shell = Mat(color, .22f, .52f);
            CreatePart(headRoot, "Helmet Shell", PrimitiveType.Sphere, new Vector3(0f, .15f, .02f), new Vector3(.53f, .38f, .39f), shell);
            CreatePart(headRoot, "Helmet Brow", PrimitiveType.Cylinder, new Vector3(0f, .11f, -.30f), new Vector3(.32f, .065f, .04f), trim);
            if (crown) AddCrown(headRoot, trim, .43f);
        }

        private void AddHood(Color color, Material trim)
        {
            var hood = Mat(color, .14f, .42f);
            CreatePart(headRoot, "Hood Cowl", PrimitiveType.Sphere, new Vector3(0f, .13f, .04f), new Vector3(.56f, .48f, .43f), hood);
            CreatePart(headRoot, "Hood Edge", PrimitiveType.Cylinder, new Vector3(0f, .02f, -.31f), new Vector3(.34f, .045f, .035f), trim);
        }

        private void AddWizardHat(Color color, Material trim, bool stars)
        {
            var hat = Mat(color, .18f, .45f);
            CreatePart(headRoot, "Hat Brim", PrimitiveType.Cylinder, new Vector3(0f, .28f, 0f), new Vector3(.42f, .055f, .34f), hat);
            CreateCone(headRoot, "Pointed Hat", new Vector3(0f, .52f, .02f), .31f, .72f, hat);
            if (stars)
            {
                magicCore = CreatePart(headRoot, "Star Emblem", PrimitiveType.Sphere, new Vector3(.02f, .44f, -.29f), new Vector3(.12f, .12f, .04f), trim);
            }
        }

        private void AddCrestShield(Transform parent, Color fill, Material trim, float size)
        {
            var root = new GameObject("Crest Shield").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(-.18f, -.02f, -.20f);
            root.localRotation = Quaternion.Euler(0f, 0f, 8f);
            CreateShield(root, "Gold Shield Rim", trim, size, .07f);
            CreateShield(root, "Blue Shield Face", Mat(fill, .2f, .48f), size * .79f, .095f);
            CreatePart(root, "Shield Gem", PrimitiveType.Sphere, new Vector3(0f, -.04f, -.12f), new Vector3(.12f, .15f, .055f),
                Mat(new Color(.08f, .68f, 1f), .45f, .72f));
        }

        private void AddSword(Transform parent, Material steel, Material gold, float scale)
        {
            var root = new GameObject("Sword").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(.08f, -.22f, -.15f);
            root.localRotation = Quaternion.Euler(0f, 0f, -22f);
            CreatePart(root, "Sword Blade", PrimitiveType.Capsule, new Vector3(0f, -.24f * scale, 0f), new Vector3(.08f * scale, .55f * scale, .07f), steel);
            CreatePart(root, "Sword Guard", PrimitiveType.Cylinder, new Vector3(0f, .08f * scale, -.02f), new Vector3(.19f * scale, .035f, .06f), gold);
        }

        private void AddHammer(Transform parent, Color main, Material steel, Material gold, float scale)
        {
            var root = new GameObject("War Hammer").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(.12f, -.25f, -.13f);
            root.localRotation = Quaternion.Euler(0f, 0f, -28f);
            CreatePart(root, "Hammer Handle", PrimitiveType.Capsule, new Vector3(0f, -.22f * scale, 0f), new Vector3(.065f, .55f * scale, .065f), Mat(new Color(.26f, .10f, .035f), .02f, .25f));
            CreatePart(root, "Hammer Head", PrimitiveType.Cylinder, new Vector3(0f, .19f * scale, 0f), new Vector3(.26f * scale, .16f * scale, .21f), steel);
            CreatePart(root, "Hammer Crown", PrimitiveType.Cylinder, new Vector3(0f, .20f * scale, -.19f), new Vector3(.15f * scale, .035f, .03f), gold);
        }

        private void AddBow(Transform parent, Material wood, Material trim, bool musket)
        {
            var root = new GameObject(musket ? "Brass Musket" : "Mint Bow").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(.08f, -.19f, -.18f);
            root.localRotation = Quaternion.Euler(0f, 0f, musket ? -78f : 14f);
            if (musket)
            {
                CreatePart(root, "Musket Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(.09f, .66f, .08f), trim);
                CreatePart(root, "Musket Stock", PrimitiveType.Capsule, new Vector3(0f, -.24f, .04f), new Vector3(.10f, .32f, .1f), wood);
                return;
            }
            CreatePart(root, "Bow Grip", PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(.06f, .42f, .055f), wood);
            CreatePart(root, "Bow Upper", PrimitiveType.Capsule, new Vector3(.13f, .20f, .02f), new Vector3(.045f, .29f, .04f), trim).localRotation = Quaternion.Euler(0f, 0f, -32f);
            CreatePart(root, "Bow Lower", PrimitiveType.Capsule, new Vector3(.13f, -.20f, .02f), new Vector3(.045f, .29f, .04f), trim).localRotation = Quaternion.Euler(0f, 0f, 32f);
        }

        private void AddStaff(Transform parent, Material wood, Color crystal, bool star)
        {
            var root = new GameObject("Focus Staff").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(.10f, -.22f, -.16f);
            root.localRotation = Quaternion.Euler(0f, 0f, -15f);
            CreatePart(root, "Staff Shaft", PrimitiveType.Capsule, new Vector3(0f, -.10f, 0f), new Vector3(.06f, .65f, .06f), wood);
            magicCore = CreatePart(root, star ? "Star Focus" : "Orb Focus", PrimitiveType.Sphere, new Vector3(0f, .27f, -.03f),
                new Vector3(star ? .16f : .18f, star ? .16f : .18f, .09f), Mat(crystal, .55f, .7f));
        }

        private void AddLance(Transform parent, Material steel, Material gold)
        {
            var root = new GameObject("Emerald Lance").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(.12f, -.17f, -.15f);
            root.localRotation = Quaternion.Euler(0f, 0f, -48f);
            CreatePart(root, "Lance Shaft", PrimitiveType.Capsule, new Vector3(0f, -.07f, 0f), new Vector3(.055f, .86f, .055f), Mat(new Color(.25f, .13f, .045f), .02f, .3f));
            CreateCone(root, "Lance Tip", new Vector3(0f, .43f, 0f), .13f, .28f, steel);
            CreatePart(root, "Lance Pennant", PrimitiveType.Cube, new Vector3(-.13f, .13f, .02f), new Vector3(.18f, .18f, .03f), gold);
        }

        private void AddCrown(Transform parent, Material gold, float width)
        {
            var crown = new GameObject("Crown Points").transform;
            crown.SetParent(parent, false);
            crown.localPosition = new Vector3(0f, .35f, -.01f);
            CreatePart(crown, "Crown Band", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(width, .055f, .28f), gold);
            for (var i = -1; i <= 1; i++)
                CreateCone(crown, "Crown Point", new Vector3(i * width * .42f, .14f + (i == 0 ? .045f : 0f), 0f), width * .12f, .28f, gold);
        }

        private static Transform CreateCloak(Transform parent, string name, Color color, Vector3 position, float width, float height)
        {
            var mesh = new Mesh { name = name + " Mesh" };
            var w = width * .5f;
            mesh.vertices = new[] { new Vector3(-w, height * .48f, 0f), new Vector3(w, height * .48f, 0f), new Vector3(w * .72f, -height * .52f, .08f), new Vector3(-w * .72f, -height * .52f, .08f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
            mesh.RecalculateNormals();
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = Mat(color, .14f, .44f);
            return node.transform;
        }

        private static void AddWing(Transform parent, string name, float side, Material material)
        {
            var wing = CreateCone(parent, name, new Vector3(side * .42f, .12f, .14f), .3f, .58f, material);
            wing.localRotation = Quaternion.Euler(0f, 0f, side * -62f);
        }

        private static void SwingLimb(Transform limb, float gait, float sign, float degrees, float lift)
        {
            if (limb == null) return;
            limb.localRotation = Quaternion.Euler(gait * sign * degrees, 0f, gait * sign * 4f);
            limb.localPosition = new Vector3(limb.localPosition.x, limb.localPosition.y + Mathf.Max(0f, -gait * sign) * lift, limb.localPosition.z);
        }

        private static Transform CreatePart(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var node = GameObject.CreatePrimitive(primitive);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.transform.localScale = scale;
            var collider = node.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var renderer = node.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return node.transform;
        }

        private static Transform CreateCone(Transform parent, string name, Vector3 position, float radius, float height, Material material)
        {
            const int sides = 12;
            var vertices = new List<Vector3> { new(0f, height * .5f, 0f), new(0f, -height * .5f, 0f) };
            for (var i = 0; i < sides; i++)
            {
                var angle = i / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -height * .5f, Mathf.Sin(angle) * radius));
            }
            var triangles = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var next = 2 + (i + 1) % sides;
                triangles.Add(0); triangles.Add(2 + i); triangles.Add(next);
                triangles.Add(1); triangles.Add(next); triangles.Add(2 + i);
            }
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node.transform;
        }

        private static Transform CreateShield(Transform parent, string name, Material material, float size, float depth)
        {
            var outline = new[] { new Vector2(-.43f, .48f), new Vector2(.43f, .48f), new Vector2(.47f, .13f), new Vector2(.32f, -.36f), new Vector2(0f, -.56f), new Vector2(-.32f, -.36f), new Vector2(-.47f, .13f) };
            var vertices = new List<Vector3>();
            foreach (var p in outline) vertices.Add(new Vector3(p.x * size, p.y * size, -depth * .5f));
            foreach (var p in outline) vertices.Add(new Vector3(p.x * size, p.y * size, depth * .5f));
            var triangles = new List<int>();
            for (var i = 1; i < outline.Length - 1; i++) { triangles.Add(0); triangles.Add(i + 1); triangles.Add(i); }
            var offset = outline.Length;
            for (var i = 1; i < outline.Length - 1; i++) { triangles.Add(offset); triangles.Add(offset + i); triangles.Add(offset + i + 1); }
            for (var i = 0; i < outline.Length; i++)
            {
                var n = (i + 1) % outline.Length;
                triangles.Add(i); triangles.Add(n); triangles.Add(offset + n);
                triangles.Add(i); triangles.Add(offset + n); triangles.Add(offset + i);
            }
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node.transform;
        }

        private static Material Mat(Color color, float metallic, float smoothness)
        {
            var key = $"{ColorUtility.ToHtmlStringRGBA(color)}:{Mathf.RoundToInt(metallic * 20f)}:{Mathf.RoundToInt(smoothness * 20f)}";
            if (Materials.TryGetValue(key, out var existing) && existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { name = "Crownfront " + key, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            material.renderQueue = (int)RenderQueue.Transparent + 20;
            Materials[key] = material;
            return material;
        }
    }
}
