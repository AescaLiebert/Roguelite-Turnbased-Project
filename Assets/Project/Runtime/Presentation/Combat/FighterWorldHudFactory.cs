using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Creates world-following fighter HUDs from Core battle projections.</summary>
    public sealed class FighterWorldHudFactory : MonoBehaviour
    {
        [SerializeField] private CoreFighterHud unitHUDPrefab;
        [SerializeField] private Transform hudContainer;

        public CoreFighterHud CreateFighterHud(Transform target, string fighterName, int currentHealth,
            int maxHealth, int shield, int powerGauge, string attributeId = null, int level = 1)
        {
            if (unitHUDPrefab == null || target == null) return null;
            if (hudContainer == null) hudContainer = transform;
            var hud = Instantiate(unitHUDPrefab, hudContainer);
            hud.InitializeCore(target, fighterName, currentHealth, maxHealth, shield, powerGauge, attributeId, level);
            hud.gameObject.name = "HUD_Core_" + fighterName;
            return hud;
        }

        public CoreFighterHud CreateFighterHud(Transform target, string fighterName, int currentHealth,
            int maxHealth, int shield, int powerGauge) =>
            CreateFighterHud(target, fighterName, currentHealth, maxHealth, shield, powerGauge, null, 1);
    }
}
