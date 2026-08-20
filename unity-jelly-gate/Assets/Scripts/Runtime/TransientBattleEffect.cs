using System.Collections.Generic;
using UnityEngine;

namespace JellyGate
{
    /// <summary>
    /// Marks short-lived battle presentation objects so a state transition can hide and dispose
    /// every projectile, impact and telegraph before its owning coroutine is stopped.
    /// </summary>
    public sealed class TransientBattleEffect : MonoBehaviour
    {
        private static readonly HashSet<GameObject> Active = new();

        public static int ActiveCount => Active.Count;

        public static GameObject Create(string objectName)
        {
            var value = new GameObject(objectName);
            Mark(value);
            return value;
        }

        public static void Mark(GameObject value)
        {
            if (value != null && value.GetComponent<TransientBattleEffect>() == null)
                value.AddComponent<TransientBattleEffect>();
        }

        public static void ClearAll()
        {
            if (Active.Count == 0) return;
            var snapshot = new GameObject[Active.Count];
            Active.CopyTo(snapshot);
            Active.Clear();
            foreach (var value in snapshot)
            {
                if (value == null) continue;
                // Disable immediately so no stale renderer can survive into the first menu frame.
                value.SetActive(false);
                Destroy(value);
            }
        }

        private void OnEnable() => Active.Add(gameObject);
        private void OnDisable() => Active.Remove(gameObject);
        private void OnDestroy() => Active.Remove(gameObject);
    }
}
