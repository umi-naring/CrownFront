using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace JellyGate
{
    // Runtime rig for the bespoke Blender defender roster.  The body is a real hierarchy of
    // bevelled meshes; locomotion moves leg/arm/weapon pivots rather than swapping a card frame.
    public sealed class CrownfrontModelUnitVisual : MonoBehaviour
    {
        private static readonly Dictionary<Material, Material> LitMaterialCache = new();
        private Transform facingRoot;
        private Transform modelRoot;
        private Transform body;
        private Transform head;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform weapon;
        private Transform offhand;
        private Vector3 bodyBase;
        private Quaternion bodyBaseRotation = Quaternion.identity;
        private Quaternion headBaseRotation = Quaternion.identity;
        private Quaternion leftLegBaseRotation = Quaternion.identity;
        private Quaternion rightLegBaseRotation = Quaternion.identity;
        private Quaternion leftArmBaseRotation = Quaternion.identity;
        private Quaternion rightArmBaseRotation = Quaternion.identity;
        private Quaternion weaponBaseRotation = Quaternion.identity;
        private Quaternion offhandBaseRotation = Quaternion.identity;
        private bool caster;
        private bool hero;
        private float phase;
        private float tacticalRadius;
        private UnitArchetype archetype;
        private Renderer[] heroRenderers = Array.Empty<Renderer>();

        public bool HasCompleteRig => body != null && head != null &&
                                      leftLeg != null && rightLeg != null &&
                                      leftArm != null && rightArm != null &&
                                      weapon != null;
        public bool HasAuthoredCollider => GetComponentInChildren<MeshCollider>(true) != null;
        public int HeroPartCount => heroRenderers.Length;
        public float PoseSignature => RotationSignature(leftLeg) + RotationSignature(rightLeg) +
                                      RotationSignature(leftArm) + RotationSignature(rightArm) +
                                      RotationSignature(weapon) + RotationSignature(offhand);
        public float VisualHeight => VisibleBounds(out var bounds) ? bounds.size.y : 0f;
        public float FootOffset => VisibleBounds(out var bounds) ? bounds.min.y - transform.position.y : 99f;

        public static int ImportedActionCount(UnitArchetype archetype)
        {
            var resource = ProductionResource(archetype);
            if (string.IsNullOrEmpty(resource)) return 0;
            return Resources.LoadAll<AnimationClip>("CrownfrontProduction/" + resource)
                .Count(clip => clip != null &&
                               !clip.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasProductionModel(UnitArchetype archetype)
        {
            // Roles move to this list only after their custom topology, bone actions and
            // collision meshes pass the production art review.  No proxy mesh is a fallback.
            var resource = ProductionResource(archetype);
            return !string.IsNullOrEmpty(resource) &&
                   Resources.Load<GameObject>("CrownfrontProduction/" + resource) != null;
        }

        public static CrownfrontModelUnitVisual CreateDefender(Transform parent, UnitArchetype archetype,
            Color unusedTeamColor, float radius, bool isHero)
        {
            var visual = new GameObject("Crownfront Bespoke Defender").AddComponent<CrownfrontModelUnitVisual>();
            visual.transform.SetParent(parent, false);
            // The unit GameObject already owns tactical depth.  Pushing the whole mesh several
            // units toward the camera made it behave like a foreground sticker instead of an
            // object standing on the board.  Keep it on the actor's 2.5D depth plane and size
            // it against the lane width, not the full screen.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            visual.phase = UnityEngine.Random.value * 10f;
            visual.tacticalRadius = radius;
            visual.hero = isHero;
            visual.archetype = archetype;
            visual.caster = archetype is UnitArchetype.AreaMage or UnitArchetype.SingleMage or UnitArchetype.Druid or UnitArchetype.Oracle;

            var resource = ProductionResource(archetype);
            var source = string.IsNullOrEmpty(resource)
                ? null
                : Resources.Load<GameObject>("CrownfrontProduction/" + resource);
            if (source == null)
            {
                Debug.LogWarning("Missing production defender model: " + archetype);
                return visual;
            }
            visual.facingRoot = new GameObject("Facing").transform;
            visual.facingRoot.SetParent(visual.transform, false);
            var model = Instantiate(source, visual.facingRoot);
            visual.modelRoot = model.transform;
            model.name = resource + " Model";
            model.transform.localPosition = Vector3.zero;
            // Blender's authored Z-up figures need one import-space turn to stand on the
            // tactical XY board.  Without this, every detail is flattened into a coloured blob.
            // The exported Blender figures face their authored camera.  Flip that local forward
            // once so an idle defender looking toward the keep presents its face, shield and
            // weapon to the tactical camera rather than its unarmoured back.
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            visual.CacheRig(model.transform);
            visual.CacheHeroParts(model.transform);
            ConfigureRenderers(model.transform);
            ConfigureCollisionMeshes(model.transform);
            visual.SetHeroParts(isHero);
            visual.NormalizeToMap(radius);
            visual.SetHero(isHero);
            return visual;
        }

        public void SetHero(bool value)
        {
            hero = value;
            SetHeroParts(value);
            if (facingRoot != null) facingRoot.localScale = value ? Vector3.one * 1.28f : Vector3.one;
            GroundBoots();
        }

        public void Animate(Vector2 facing, bool moving, float attackT, float skillT, float ultimateT,
            float hurtT, float levelUpT, bool isHero, bool isFlying)
        {
            if (isHero != hero) SetHero(isHero);
            if (facingRoot != null && facing.sqrMagnitude > .002f)
            {
                var d = facing.normalized;
                facingRoot.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg, 0f);
            }
            if (body == null) return;

            var gait = moving ? Mathf.Sin((Time.time + phase) * 8.2f) : 0f;
            var strike = Mathf.Sin(Mathf.Clamp01(attackT) * Mathf.PI);
            var skill = Mathf.Sin((1f - Mathf.Clamp01(skillT)) * Mathf.PI);
            var ultimate = Mathf.Sin((1f - Mathf.Clamp01(ultimateT)) * Mathf.PI);
            var hurt = Mathf.Sin(Mathf.Clamp01(hurtT) * Mathf.PI);
            var levelUp = Mathf.Sin((1f - Mathf.Clamp01(levelUpT)) * Mathf.PI);
            body.localPosition = bodyBase + new Vector3(0f,
                (moving ? Mathf.Abs(gait) * .006f : 0f) + ultimate * .09f + levelUp * .08f, 0f);
            body.localRotation = bodyBaseRotation * Quaternion.Euler(-hurt * 8f + ultimate * 4f, 0f,
                hurt * 7f + Mathf.Sin(Time.time * 7f) * levelUp * 3f);
            Swing(leftLeg, leftLegBaseRotation, gait, 1f, 34f);
            Swing(rightLeg, rightLegBaseRotation, gait, -1f, 34f);
            Swing(leftArm, leftArmBaseRotation, gait, -1f, caster ? 12f : 20f);
            Swing(rightArm, rightArmBaseRotation, gait, 1f, caster ? 16f : 23f);
            ApplyRoleAction(strike, skill, ultimate);
            if (head != null)
                head.localRotation = headBaseRotation *
                                     Quaternion.Euler(0f, Mathf.Sin((Time.time + phase) * 2.2f) * 2.2f, 0f);
        }

        private void ApplyRoleAction(float strike, float skill, float ultimate)
        {
            var weaponPitch = 0f;
            var weaponYaw = 0f;
            var weaponRoll = 0f;
            var offhandPitch = 0f;
            var offhandRoll = 0f;
            var rightArmPitch = 0f;
            var leftArmPitch = 0f;

            switch (archetype)
            {
                case UnitArchetype.Tank:
                    weaponPitch = -strike * 45f - ultimate * 28f;
                    offhandPitch = -strike * 18f + skill * 58f + ultimate * 72f;
                    offhandRoll = -skill * 12f;
                    leftArmPitch = skill * 26f + ultimate * 38f;
                    rightArmPitch = strike * 18f + ultimate * 25f;
                    break;
                case UnitArchetype.Melee:
                    weaponPitch = -strike * 96f - skill * 145f - ultimate * 185f;
                    weaponRoll = strike * 22f + skill * 44f;
                    rightArmPitch = strike * 34f + skill * 48f + ultimate * 70f;
                    break;
                case UnitArchetype.Archer:
                    weaponPitch = -strike * 18f - skill * 28f;
                    weaponYaw = strike * 18f;
                    rightArmPitch = -strike * 42f - skill * 64f;
                    leftArmPitch = strike * 28f + skill * 42f + ultimate * 62f;
                    break;
                case UnitArchetype.Musketeer:
                    weaponPitch = strike * 12f + skill * 7f;
                    weaponYaw = -strike * 8f;
                    weaponRoll = strike * 7f;
                    rightArmPitch = -strike * 26f - skill * 34f;
                    leftArmPitch = -strike * 18f - skill * 26f;
                    break;
                case UnitArchetype.Lancer:
                    weaponPitch = strike * 72f + skill * 104f + ultimate * 122f;
                    rightArmPitch = strike * 32f + skill * 54f;
                    leftArmPitch = strike * 18f + ultimate * 42f;
                    break;
                case UnitArchetype.Bombardier:
                    weaponPitch = -strike * 18f - skill * 32f;
                    weaponRoll = Mathf.Sin(Time.time * 38f) * (strike + skill) * 3f;
                    rightArmPitch = -strike * 26f - skill * 38f;
                    leftArmPitch = -strike * 16f - skill * 24f;
                    break;
                case UnitArchetype.AreaMage:
                case UnitArchetype.Druid:
                    weaponPitch = -strike * 34f - skill * 64f - ultimate * 92f;
                    weaponRoll = strike * 20f + skill * 42f + ultimate * 90f;
                    rightArmPitch = -strike * 22f - skill * 46f - ultimate * 72f;
                    leftArmPitch = skill * 38f + ultimate * 76f;
                    break;
                case UnitArchetype.SingleMage:
                case UnitArchetype.Oracle:
                    weaponPitch = -strike * 28f - skill * 52f - ultimate * 88f;
                    weaponYaw = skill * 24f;
                    weaponRoll = -strike * 18f - ultimate * 82f;
                    rightArmPitch = -strike * 24f - skill * 42f - ultimate * 66f;
                    leftArmPitch = strike * 18f + skill * 35f + ultimate * 70f;
                    break;
            }

            if (weapon != null)
                weapon.localRotation = weaponBaseRotation * Quaternion.Euler(weaponPitch, weaponYaw, weaponRoll);
            if (offhand != null)
                offhand.localRotation = offhandBaseRotation * Quaternion.Euler(offhandPitch, 0f, offhandRoll);
            if (rightArm != null) rightArm.localRotation *= Quaternion.Euler(rightArmPitch, 0f, 0f);
            if (leftArm != null) leftArm.localRotation *= Quaternion.Euler(leftArmPitch, 0f, 0f);
        }

        public void PlayCast()
        {
            if (weapon != null) weapon.localRotation = Quaternion.Euler(-25f, 0f, -18f);
        }

        private void CacheRig(Transform model)
        {
            // Blender exports the authored armature object as CrownfrontArmature and each
            // deform/control bone as a Transform below it.  An older prototype searched for
            // "CrownfrontRig", which is not present in these production FBXs and silently left
            // the entire roster static.
            var rig = FindDeep(model, "CrownfrontArmature") ?? model;
            body = FindDeep(rig, "Body");
            head = FindDeep(rig, "Head");
            leftLeg = FindDeep(rig, "Leg.L");
            rightLeg = FindDeep(rig, "Leg.R");
            leftArm = FindDeep(rig, "Arm.L");
            rightArm = FindDeep(rig, "Arm.R");
            weapon = FindDeep(rig, "Weapon");
            offhand = FindDeep(rig, "Offhand");
            if (body != null)
            {
                bodyBase = body.localPosition;
                bodyBaseRotation = body.localRotation;
            }
            if (head != null) headBaseRotation = head.localRotation;
            if (leftLeg != null) leftLegBaseRotation = leftLeg.localRotation;
            if (rightLeg != null) rightLegBaseRotation = rightLeg.localRotation;
            if (leftArm != null) leftArmBaseRotation = leftArm.localRotation;
            if (rightArm != null) rightArmBaseRotation = rightArm.localRotation;
            if (weapon != null) weaponBaseRotation = weapon.localRotation;
            if (offhand != null) offhandBaseRotation = offhand.localRotation;
        }

        private void CacheHeroParts(Transform root)
        {
            var all = root.GetComponentsInChildren<Renderer>(true);
            var found = new List<Renderer>();
            foreach (var renderer in all)
                if (renderer != null && renderer.name.StartsWith("HERO_", StringComparison.OrdinalIgnoreCase))
                    found.Add(renderer);
            heroRenderers = found.ToArray();
        }

        private void SetHeroParts(bool visible)
        {
            foreach (var renderer in heroRenderers)
                if (renderer != null) renderer.enabled = visible;
        }

        private void NormalizeToMap(float radius)
        {
            if (modelRoot == null) return;
            var bodyCollider = modelRoot.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter => filter != null && filter.sharedMesh != null &&
                                          filter.name.Equals("COL_Body", StringComparison.OrdinalIgnoreCase));
            var measuredHeight = bodyCollider != null
                ? MeasureWorldHeight(bodyCollider)
                : MeasureVisibleWorldHeight(modelRoot);
            if (measuredHeight < .01f) return;

            // Body size follows tactical footprint rather than the longest piece of equipment.
            // A lancer's spear and a mage's hat may extend beyond this envelope, but their head,
            // torso and feet now remain the same readable size as the rest of the squad.
            var targetBodyHeight = Mathf.Clamp(radius * 1.45f, .40f, .54f);
            var uniform = Mathf.Clamp(targetBodyHeight / measuredHeight, .10f, .72f);
            transform.localScale = Vector3.one * uniform;
            GroundBoots();
        }

        private void GroundBoots()
        {
            if (modelRoot == null || tacticalRadius <= 0f) return;
            var min = float.PositiveInfinity;
            foreach (var filter in modelRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null || filter.sharedMesh == null ||
                    filter.name.IndexOf("Boot", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var bounds = filter.sharedMesh.bounds;
                for (var x = 0; x < 2; x++)
                for (var y = 0; y < 2; y++)
                for (var z = 0; z < 2; z++)
                {
                    var local = new Vector3(
                        x == 0 ? bounds.min.x : bounds.max.x,
                        y == 0 ? bounds.min.y : bounds.max.y,
                        z == 0 ? bounds.min.z : bounds.max.z);
                    min = Mathf.Min(min, filter.transform.TransformPoint(local).y);
                }
            }
            if (float.IsPositiveInfinity(min)) return;
            var desiredBootBottom = transform.position.y - tacticalRadius * .72f;
            modelRoot.position += Vector3.up * (desiredBootBottom - min);
        }

        private static float MeasureVisibleWorldHeight(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            var found = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled ||
                    renderer.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) ||
                    renderer.name.StartsWith("HERO_", StringComparison.OrdinalIgnoreCase)) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found ? bounds.size.y : 0f;
        }

        private bool VisibleBounds(out Bounds bounds)
        {
            bounds = default;
            if (modelRoot == null) return false;
            var found = false;
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled ||
                    renderer.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static float MeasureWorldHeight(MeshFilter filter)
        {
            var bounds = filter.sharedMesh.bounds;
            var min = float.PositiveInfinity;
            var max = float.NegativeInfinity;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var local = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);
                var worldY = filter.transform.TransformPoint(local).y;
                min = Mathf.Min(min, worldY);
                max = Mathf.Max(max, worldY);
            }
            return max - min;
        }

        private static string ProductionResource(UnitArchetype archetype)
        {
            return archetype switch
            {
                UnitArchetype.Tank => "Tank",
                UnitArchetype.Melee => "Melee",
                UnitArchetype.Archer => "Archer",
                UnitArchetype.AreaMage => "AreaMage",
                UnitArchetype.SingleMage => "SingleMage",
                UnitArchetype.Bombardier => "Bombardier",
                UnitArchetype.Lancer => "Lancer",
                UnitArchetype.Druid => "Druid",
                UnitArchetype.Musketeer => "Musketeer",
                UnitArchetype.Oracle => "Oracle",
                _ => string.Empty
            };
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void Swing(Transform limb, Quaternion baseRotation, float gait, float sign, float degrees)
        {
            if (limb == null) return;
            limb.localRotation = baseRotation *
                                 Quaternion.Euler(gait * sign * degrees, 0f, gait * sign * 3f);
        }

        private static float RotationSignature(Transform target)
        {
            if (target == null) return 0f;
            var rotation = target.localRotation;
            return rotation.x * 17f + rotation.y * 29f + rotation.z * 43f + rotation.w * 7f;
        }

        private static void ConfigureRenderers(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                var source = renderer.sharedMaterial;
                if (source == null) continue;
                if (!LitMaterialCache.TryGetValue(source, out var lit) || lit == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    lit = new Material(shader) { name = "Crownfront Lit " + source.name };
                    var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                    if (lit.HasProperty("_BaseColor")) lit.SetColor("_BaseColor", color);
                    else lit.color = color;
                    if (lit.HasProperty("_Metallic")) lit.SetFloat("_Metallic", Mathf.Clamp01(source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : .18f));
                    if (lit.HasProperty("_Smoothness")) lit.SetFloat("_Smoothness", .42f);
                    LitMaterialCache[source] = lit;
                }
                renderer.sharedMaterial = lit;
            }
        }

        private static void ConfigureCollisionMeshes(Transform root)
        {
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null || filter.sharedMesh == null ||
                    !filter.name.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
                var collider = filter.GetComponent<MeshCollider>() ?? filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = true;
                collider.isTrigger = true;
            }
        }
    }
}
