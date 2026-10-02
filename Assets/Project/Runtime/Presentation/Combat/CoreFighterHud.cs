using UnityEngine;
using UnityEngine.UI;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Core adapter for the authored UnitUI prefab.</summary>
    public sealed class CoreFighterHud : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private WorldBillboardFollower followScript;

        private Image _powerGaugeFill;
        private Image _shieldFill;
        private GameObject _shieldRoot;

        private void Awake()
        {
            if (followScript == null) followScript = GetComponent<WorldBillboardFollower>();
            BindAuthoredUnitUi();
        }

        public void InitializeCore(Transform target, string fighterName, int currentHealth, int maxHealth,
            int shield, int powerGauge)
        {
            if (followScript == null) followScript = GetComponent<WorldBillboardFollower>();
            if (followScript == null) followScript = gameObject.AddComponent<WorldBillboardFollower>();
            followScript.SetTarget(target);
            BindAuthoredUnitUi();
            SetCoreHealth(currentHealth, maxHealth);
            SetShield(shield, maxHealth);
            SetPowerGauge(powerGauge);
        }

        public void SetCoreHealth(int currentHealth, int maxHealth)
        {
            if (healthSlider == null) return;
            healthSlider.maxValue = Mathf.Max(1, maxHealth);
            healthSlider.value = Mathf.Clamp(currentHealth, 0, healthSlider.maxValue);
        }

        public void SetPowerGauge(int powerGauge)
        {
            if (_powerGaugeFill == null) return;
            _powerGaugeFill.type = Image.Type.Filled;
            _powerGaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _powerGaugeFill.fillOrigin = 0;
            _powerGaugeFill.fillAmount = Mathf.Clamp01(powerGauge / 5f);
        }

        public void SetShield(int shield, int maxHealth)
        {
            if (_shieldRoot != null) _shieldRoot.SetActive(shield > 0);
            if (_shieldFill == null) return;
            _shieldFill.type = Image.Type.Filled;
            _shieldFill.fillMethod = Image.FillMethod.Horizontal;
            _shieldFill.fillOrigin = 0;
            _shieldFill.fillAmount = Mathf.Clamp01(shield / (float)Mathf.Max(1, maxHealth));
        }

        private void BindAuthoredUnitUi()
        {
            var unitUi = transform.Find("UnitUI");
            if (unitUi == null) return;
            unitUi.gameObject.SetActive(true);
            if (healthSlider == null) healthSlider = unitUi.Find("HealthBar")?.GetComponent<Slider>();
            _powerGaugeFill = unitUi.Find("GaugePanel/Gauge/Fill Area/Fill")?.GetComponent<Image>();
            var shield = unitUi.Find("HealthPanel/Ex-Healthbar");
            _shieldRoot = shield == null ? null : shield.gameObject;
            _shieldFill = shield?.Find("Fill Area/Fill")?.GetComponent<Image>();
        }
    }
}
