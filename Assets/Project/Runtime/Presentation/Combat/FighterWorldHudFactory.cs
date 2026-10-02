using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Creates world-following fighter HUDs from Core battle projections.</summary>
    public sealed class FighterWorldHudFactory : MonoBehaviour
    {
        [SerializeField] private CoreFighterHud unitHUDPrefab;
        [SerializeField] private Transform hudContainer;

        public CoreFighterHud CreateFighterHud(Transform target, string fighterName, int currentHealth,
            int maxHealth, int shield, int powerGauge)
        {
            if (unitHUDPrefab == null || target == null) return null;
            if (hudContainer == null) hudContainer = transform;
            var hud = Instantiate(unitHUDPrefab, hudContainer);
            hud.InitializeCore(target, fighterName, currentHealth, maxHealth, shield, powerGauge);
            hud.gameObject.name = "HUD_Core_" + fighterName;
            return hud;
        }
    }
}
