using UnityEngine;

namespace JellyGate
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            if (Object.FindFirstObjectByType<JellyGateGame>() != null) return;
            var root = new GameObject("CROWNFRONT Runtime");
            root.AddComponent<CrownfrontBootLoader>();
            Object.DontDestroyOnLoad(root);
        }
    }
}
