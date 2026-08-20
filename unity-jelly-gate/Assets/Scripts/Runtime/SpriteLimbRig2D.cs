using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    /// <summary>
    /// Splits a painted character at the waist and drives two independently planted legs plus
    /// a counter-moving upper body. This is intentionally a presentation-only rig: tactical
    /// radius, hit position and navigation remain owned by the unit component.
    /// </summary>
    public sealed class SpriteLimbRig2D
    {
        private sealed class SliceSet
        {
            public Sprite Torso;
            public Sprite LeftLeg;
            public Sprite RightLeg;
            public Vector2 Waist;
            public Vector2 TorsoOffset;
            public Vector2 LeftHip;
            public Vector2 RightHip;
            public Vector2 LeftLegOffset;
            public Vector2 RightLegOffset;
        }

        private static readonly Dictionary<Sprite, SliceSet> Cache = new();
        private readonly SpriteRenderer source;
        private readonly Transform root;
        private readonly Transform torsoPivot;
        private readonly Transform leftLegPivot;
        private readonly Transform rightLegPivot;
        private readonly SpriteRenderer torso;
        private readonly SpriteRenderer leftLeg;
        private readonly SpriteRenderer rightLeg;
        private Sprite currentSprite;
        private SliceSet currentSlices;
        private bool usable;

        public bool IsUsable => usable;
        public float PoseSignature => torsoPivot == null ? 0f :
            Mathf.Abs(Mathf.DeltaAngle(0f, torsoPivot.localEulerAngles.z)) +
            Mathf.Abs(Mathf.DeltaAngle(0f, leftLegPivot.localEulerAngles.z)) +
            Mathf.Abs(Mathf.DeltaAngle(0f, rightLegPivot.localEulerAngles.z));

        public static SpriteLimbRig2D Create(SpriteRenderer sourceRenderer)
        {
            return sourceRenderer == null ? null : new SpriteLimbRig2D(sourceRenderer);
        }

        private SpriteLimbRig2D(SpriteRenderer sourceRenderer)
        {
            source = sourceRenderer;
            root = new GameObject("Articulated Sprite Rig").transform;
            root.SetParent(source.transform, false);

            torsoPivot = NewPivot("Upper Body Pivot");
            leftLegPivot = NewPivot("Left Leg Pivot");
            rightLegPivot = NewPivot("Right Leg Pivot");
            torso = NewRenderer(torsoPivot, "Upper Body", 1);
            leftLeg = NewRenderer(leftLegPivot, "Left Leg", 0);
            rightLeg = NewRenderer(rightLegPivot, "Right Leg", 0);
        }

        private Transform NewPivot(string name)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            return pivot;
        }

        private SpriteRenderer NewRenderer(Transform parent, string name, int orderOffset)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + orderOffset;
            return renderer;
        }

        public void SetVisible(bool visible)
        {
            if (torso != null) torso.enabled = visible && usable;
            if (leftLeg != null) leftLeg.enabled = visible && usable;
            if (rightLeg != null) rightLeg.enabled = visible && usable;
            if (source != null && visible && usable) source.enabled = false;
        }

        public void Animate(Sprite sprite, Color color, bool flipX, bool visible, bool moving,
            float gaitPhase, bool actionActive, float actionT, bool casting, float roleWeight = 1f,
            bool linearThrust = false)
        {
            if (source == null) return;
            if (!visible)
            {
                SetVisible(false);
                return;
            }
            EnsureSlices(sprite);
            if (!usable)
            {
                source.enabled = true;
                SetVisible(false);
                return;
            }

            source.enabled = false;
            torso.enabled = leftLeg.enabled = rightLeg.enabled = true;
            root.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);
            SyncRenderer(torso, currentSlices.Torso, color, source.sortingOrder + 1);
            SyncRenderer(leftLeg, currentSlices.LeftLeg, color, source.sortingOrder);
            SyncRenderer(rightLeg, currentSlices.RightLeg, color, source.sortingOrder);

            var cycle = gaitPhase * Mathf.PI * 2f;
            var gait = moving ? Mathf.Sin(cycle) : 0f;
            var plant = moving ? Mathf.Abs(Mathf.Cos(cycle)) : 1f;
            var weight = Mathf.Clamp(roleWeight, .55f, 1.35f);
            var legSwing = gait * Mathf.Lerp(17.5f, 10.5f, Mathf.InverseLerp(.55f, 1.35f, weight));
            var torsoCounter = -gait * Mathf.Lerp(4.8f, 2.4f, Mathf.InverseLerp(.55f, 1.35f, weight));
            var leftLift = moving ? Mathf.Max(0f, gait) : 0f;
            var rightLift = moving ? Mathf.Max(0f, -gait) : 0f;
            var stride = moving ? gait * .022f / weight : 0f;

            var staged = 0f;
            if (actionActive)
            {
                var t = Mathf.Clamp01(actionT);
                staged = linearThrust
                    ? t < .18f
                        ? 0f
                        : t < .5f
                            ? Mathf.SmoothStep(0f, 1f, (t - .18f) / .32f)
                            : t < .66f
                                ? Mathf.Lerp(1f, .72f, Mathf.SmoothStep(0f, 1f, (t - .5f) / .16f))
                                : Mathf.Lerp(.72f, 0f, Mathf.SmoothStep(0f, 1f, (t - .66f) / .34f))
                    : t < .28f
                        ? -Mathf.SmoothStep(0f, .42f, t / .28f)
                        : t < .55f
                            ? Mathf.Lerp(-.42f, 1f, Mathf.SmoothStep(0f, 1f, (t - .28f) / .27f))
                            : Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (t - .55f) / .45f));
            }

            // Keep the pelvis planted.  Locomotion comes from alternating legs and a small
            // shoulder counter-shift instead of lifting the complete painted character.
            torsoPivot.localPosition = new Vector3(currentSlices.Waist.x - stride * .22f,
                currentSlices.Waist.y, 0f);
            leftLegPivot.localPosition = new Vector3(currentSlices.LeftHip.x + stride,
                currentSlices.LeftHip.y + leftLift * .032f, .004f);
            rightLegPivot.localPosition = new Vector3(currentSlices.RightHip.x - stride,
                currentSlices.RightHip.y + rightLift * .032f, .006f);
            torso.transform.localPosition = new Vector3(currentSlices.TorsoOffset.x, currentSlices.TorsoOffset.y, -.012f);
            leftLeg.transform.localPosition = new Vector3(currentSlices.LeftLegOffset.x, currentSlices.LeftLegOffset.y, 0f);
            rightLeg.transform.localPosition = new Vector3(currentSlices.RightLegOffset.x, currentSlices.RightLegOffset.y, -.002f);

            torsoPivot.localEulerAngles = new Vector3(0f, 0f,
                torsoCounter + staged * (linearThrust ? 2.2f : casting ? 4.8f : 8.5f) / weight);
            leftLegPivot.localEulerAngles = new Vector3(0f, 0f,
                actionActive ? -staged * (linearThrust ? 1.6f : 3.2f) : legSwing);
            rightLegPivot.localEulerAngles = new Vector3(0f, 0f,
                actionActive ? staged * (linearThrust ? 1.6f : 3.2f) : -legSwing);
            leftLegPivot.localScale = new Vector3(1f + leftLift * .025f,
                1f - (1f - leftLift) * (moving ? .035f : 0f), 1f);
            rightLegPivot.localScale = new Vector3(1f + rightLift * .025f,
                1f - (1f - rightLift) * (moving ? .035f : 0f), 1f);
            var brace = actionActive ? Mathf.Abs(staged) : 0f;
            torsoPivot.localScale = new Vector3(1f + brace * .018f,
                1f - brace * .012f, 1f);
        }

        private void EnsureSlices(Sprite sprite)
        {
            if (sprite == currentSprite) return;
            currentSprite = sprite;
            currentSlices = GetOrCreateSlices(sprite);
            usable = currentSlices != null;
        }

        private static void SyncRenderer(SpriteRenderer renderer, Sprite sprite, Color color, int order)
        {
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.flipX = false;
        }

        private static SliceSet GetOrCreateSlices(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || sprite.pixelsPerUnit <= .01f ||
                sprite.texture.name == "Runtime Circle") return null;
            if (Cache.TryGetValue(sprite, out var cached)) return cached;
            try
            {
                var rect = sprite.textureRect;
                var width = Mathf.Max(4, Mathf.RoundToInt(rect.width));
                var height = Mathf.Max(6, Mathf.RoundToInt(rect.height));
                var left = Mathf.RoundToInt(rect.xMin);
                var bottom = Mathf.RoundToInt(rect.yMin);
                var split = Mathf.Clamp(Mathf.RoundToInt(height * .39f), 2, height - 3);
                var half = Mathf.Clamp(width / 2, 2, width - 2);
                const int overlap = 2;
                var lowerHeight = Mathf.Min(height, split + overlap);
                var upperStart = Mathf.Max(0, split - overlap);
                var upperHeight = height - upperStart;
                var leftWidth = Mathf.Min(width, half + overlap);
                var rightStart = Mathf.Max(0, half - overlap);
                var rightWidth = width - rightStart;
                var ppu = sprite.pixelsPerUnit;

                Sprite Make(string suffix, Rect slice)
                {
                    var created = Sprite.Create(sprite.texture, slice, new Vector2(.5f, .5f), ppu,
                        0, SpriteMeshType.FullRect);
                    created.name = sprite.name + suffix;
                    created.hideFlags = HideFlags.DontSave;
                    return created;
                }

                var torsoSprite = Make(" Upper Body",
                    new Rect(left, bottom + upperStart, width, upperHeight));
                var leftSprite = Make(" Left Leg",
                    new Rect(left, bottom, leftWidth, lowerHeight));
                var rightSprite = Make(" Right Leg",
                    new Rect(left + rightStart, bottom, rightWidth, lowerHeight));

                var pivot = sprite.pivot;
                var waist = new Vector2((width * .5f - pivot.x) / ppu, (split - pivot.y) / ppu);
                var leftHip = new Vector2((leftWidth * .5f - pivot.x) / ppu, waist.y);
                var rightHip = new Vector2((rightStart + rightWidth * .5f - pivot.x) / ppu, waist.y);
                cached = new SliceSet
                {
                    Torso = torsoSprite,
                    LeftLeg = leftSprite,
                    RightLeg = rightSprite,
                    Waist = waist,
                    TorsoOffset = new Vector2(0f, upperHeight * .5f / ppu - overlap / ppu),
                    LeftHip = leftHip,
                    RightHip = rightHip,
                    LeftLegOffset = new Vector2(0f, -lowerHeight * .5f / ppu + overlap / ppu),
                    RightLegOffset = new Vector2(0f, -lowerHeight * .5f / ppu + overlap / ppu)
                };
                Cache[sprite] = cached;
                return cached;
            }
            catch (System.Exception)
            {
                Cache[sprite] = null;
                return null;
            }
        }
    }
}
