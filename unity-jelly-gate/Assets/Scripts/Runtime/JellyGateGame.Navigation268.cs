using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JellyGate
{
    public sealed partial class JellyGateGame
    {
        private const float WalkGridCell268 = .16f;
        private const float WalkGridMinX268 = -4.8f;
        private const float WalkGridMinY268 = -7.36f;
        private const int WalkGridWidth268 = 61;
        private const int WalkGridHeight268 = 93;
        private const int WalkGridCount268 = WalkGridWidth268 * WalkGridHeight268;

        // Clearance sampling is the expensive part of a movement order: each cell fans out into
        // nine navigation-mask probes. It is immutable after the map is built, so cache it per
        // unit radius instead of rebuilding 5,673 cells for every selected shield soldier.
        private readonly Dictionary<int, bool[]> walkGridPassabilityCache268 = new();
        private int[] walkGridRegions268 = Array.Empty<int>();
        private float[] walkGridCosts268 = Array.Empty<float>();
        private int[] walkGridParents268 = Array.Empty<int>();
        private int[] walkGridSeenStamp268 = Array.Empty<int>();
        private int[] walkGridClosedStamp268 = Array.Empty<int>();
        private int walkGridSearchStamp268;
        private readonly GridMinHeap268 walkGridOpen268 = new(WalkGridCount268);
        private int walkGridCacheBuildCount268;
        private int walkGridCacheHitCount268;
        private int lastWalkGridExpandedNodes268;

        public int WalkGridCacheBuildCountForQa268 => walkGridCacheBuildCount268;
        public int WalkGridCacheHitCountForQa268 => walkGridCacheHitCount268;
        public int LastWalkGridExpandedNodesForQa268 => lastWalkGridExpandedNodes268;

        private Vector2 WalkGridWorld268(int index) => new(
            WalkGridMinX268 + index % WalkGridWidth268 * WalkGridCell268,
            WalkGridMinY268 + index / WalkGridWidth268 * WalkGridCell268);

        private bool[] GetWalkGridPassability268(float clearance)
        {
            EnsureWalkGridRegions268();
            var key = Mathf.RoundToInt(Mathf.Max(.01f, clearance) * 10000f);
            if (walkGridPassabilityCache268.TryGetValue(key, out var cached))
            {
                walkGridCacheHitCount268++;
                return cached;
            }
            var result = new bool[WalkGridCount268];
            for (var index = 0; index < result.Length; index++)
                result[index] = IsWalkableWithClearance(WalkGridWorld268(index), clearance);
            walkGridPassabilityCache268[key] = result;
            walkGridCacheBuildCount268++;
            return result;
        }

        private void EnsureWalkGridRegions268()
        {
            if (walkGridRegions268.Length == WalkGridCount268) return;
            walkGridRegions268 = new int[WalkGridCount268];
            for (var index = 0; index < walkGridRegions268.Length; index++)
                walkGridRegions268[index] = GetNavigationRegion(WalkGridWorld268(index));
        }

        private bool HasCachedWalkGridLine268(Vector2 start, Vector2 end, float clearance, int region)
        {
            var passable = GetWalkGridPassability268(clearance);
            EnsureWalkGridRegions268();
            var samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / .075f));
            for (var sample = 1; sample <= samples; sample++)
            {
                var point = Vector2.Lerp(start, end, sample / (float)samples);
                var x = Mathf.Clamp(Mathf.RoundToInt((point.x - WalkGridMinX268) / WalkGridCell268),
                    0, WalkGridWidth268 - 1);
                var y = Mathf.Clamp(Mathf.RoundToInt((point.y - WalkGridMinY268) / WalkGridCell268),
                    0, WalkGridHeight268 - 1);
                var index = y * WalkGridWidth268 + x;
                if (!passable[index] || walkGridRegions268[index] != region) return false;
            }
            return true;
        }

        private int BeginWalkGridSearch268()
        {
            if (walkGridCosts268.Length != WalkGridCount268)
            {
                walkGridCosts268 = new float[WalkGridCount268];
                walkGridParents268 = new int[WalkGridCount268];
                walkGridSeenStamp268 = new int[WalkGridCount268];
                walkGridClosedStamp268 = new int[WalkGridCount268];
            }
            if (++walkGridSearchStamp268 == int.MaxValue)
            {
                Array.Clear(walkGridSeenStamp268, 0, walkGridSeenStamp268.Length);
                Array.Clear(walkGridClosedStamp268, 0, walkGridClosedStamp268.Length);
                walkGridSearchStamp268 = 1;
            }
            walkGridOpen268.Clear();
            lastWalkGridExpandedNodes268 = 0;
            return walkGridSearchStamp268;
        }

        private void ClearPathfindingCaches268()
        {
            walkGridPassabilityCache268.Clear();
            walkGridRegions268 = Array.Empty<int>();
            walkGridCacheBuildCount268 = 0;
            walkGridCacheHitCount268 = 0;
        }

        private void PrimePlayerPathfinding268()
        {
            foreach (var clearance in definitions.Values.Select(definition => definition.Radius * .55f).Distinct())
                GetWalkGridPassability268(clearance);
        }

        private sealed class GridMinHeap268
        {
            private int[] nodes;
            private float[] priorities;
            private int count;

            public int Count => count;

            public GridMinHeap268(int capacity)
            {
                nodes = new int[Mathf.Max(16, capacity)];
                priorities = new float[nodes.Length];
            }

            public void Clear() => count = 0;

            public void Push(int node, float priority)
            {
                if (count >= nodes.Length)
                {
                    Array.Resize(ref nodes, nodes.Length * 2);
                    Array.Resize(ref priorities, priorities.Length * 2);
                }
                var index = count++;
                while (index > 0)
                {
                    var parent = (index - 1) >> 1;
                    if (priorities[parent] <= priority) break;
                    nodes[index] = nodes[parent];
                    priorities[index] = priorities[parent];
                    index = parent;
                }
                nodes[index] = node;
                priorities[index] = priority;
            }

            public int Pop(out float priority)
            {
                var result = nodes[0];
                priority = priorities[0];
                var lastNode = nodes[--count];
                var lastPriority = priorities[count];
                if (count == 0) return result;
                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= count) break;
                    var right = left + 1;
                    var child = right < count && priorities[right] < priorities[left] ? right : left;
                    if (priorities[child] >= lastPriority) break;
                    nodes[index] = nodes[child];
                    priorities[index] = priorities[child];
                    index = child;
                }
                nodes[index] = lastNode;
                priorities[index] = lastPriority;
                return result;
            }
        }
    }
}
