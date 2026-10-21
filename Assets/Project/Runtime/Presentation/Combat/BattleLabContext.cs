using System;
using System.Collections.Generic;
using FightingAllstar.Core.Studio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Volatile lab handoff. Does not touch inventory, runs, rewards, scenes or content assets.</summary>
    public static class BattleLabContext
    {
        [Serializable]
        public sealed class VisualOverride
        {
            public GameObject Prefab;
            public float Scale = 1;
            public float Yaw;
            public ActorPresentationProfile Profile;
        }
        private static readonly Dictionary<string, VisualOverride> visuals = new Dictionary<string, VisualOverride>();
        private static BattleLabConfiguration configuration;
        public static bool IsReady => configuration != null;
        public static BattleLabMode Mode => configuration?.Mode ?? BattleLabMode.RealBattle;
        public static ulong Seed => configuration?.Seed ?? 0;
        public static BattleLabConfiguration Snapshot => configuration?.Clone();
        public static void Prepare(BattleLabConfiguration settings) => configuration = settings?.Clone() ?? throw new ArgumentNullException(nameof(settings));
        public static void SetVisual(string fighterId, GameObject prefab, float scale, float yaw, ActorPresentationProfile profile = null) => visuals[fighterId] = new VisualOverride { Prefab = prefab, Scale = scale, Yaw = yaw, Profile = profile };
        public static VisualOverride VisualFor(string fighterId) => visuals.TryGetValue(fighterId, out var value) ? value : null;
        public static void ClearVisuals() => visuals.Clear();
        public static void Clear() { configuration = null; visuals.Clear(); }
        public static void Restart()
        {
            if (!IsReady) return;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        public static void Exit()
        {
            Clear();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            SceneManager.LoadScene("MainMenu");
#endif
        }
    }
}
