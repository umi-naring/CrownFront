using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    /// <summary>
    /// Records the result of frame ownership checks while runtime atlases are still readable.
    /// The generated textures are uploaded as non-readable on mobile, so QA must not try to
    /// reconstruct this information later from the GPU resource.
    /// </summary>
    public static class SpriteFrameIsolationRegistry
    {
        public readonly struct Audit
        {
            public Audit(int removedForeignComponents, int remainingForeignComponents,
                int significantComponents, string source, bool bodySeamClosed)
            {
                RemovedForeignComponents = removedForeignComponents;
                RemainingForeignComponents = remainingForeignComponents;
                SignificantComponents = significantComponents;
                Source = source ?? string.Empty;
                BodySeamClosed = bodySeamClosed;
            }

            public int RemovedForeignComponents { get; }
            public int RemainingForeignComponents { get; }
            public int SignificantComponents { get; }
            public string Source { get; }
            public bool BodySeamClosed { get; }
        }

        private static readonly Dictionary<Sprite, Audit> Audits = new();

        public static void Register(Sprite sprite, int removedForeignComponents,
            int remainingForeignComponents, int significantComponents, string source,
            bool bodySeamClosed = true)
        {
            if (sprite == null) return;
            Audits[sprite] = new Audit(Mathf.Max(0, removedForeignComponents),
                Mathf.Max(0, remainingForeignComponents), Mathf.Max(0, significantComponents), source,
                bodySeamClosed);
        }

        public static Audit For(Sprite sprite) =>
            sprite != null && Audits.TryGetValue(sprite, out var audit)
                ? audit
                : new Audit(0, -1, 0, string.Empty, true);

        public static bool HasAudit(Sprite sprite) => sprite != null && Audits.ContainsKey(sprite);
    }
}
