using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UnitHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private UIFollowWorld followScript;
    [SerializeField] private TextMeshProUGUI levelText; // Optional 7DSGC style
    // Add buff icons references here later

    private Unit targetUnit;

    private void Awake()
    {
        if (followScript == null) followScript = GetComponent<UIFollowWorld>();
    }

    public void Initialize(Unit unit)
    {
        targetUnit = unit;
        
        // Setup Follow
        if (followScript != null)
        {
            followScript.SetTarget(unit.transform);
        }

        // Setup Events
        targetUnit.OnHealthChanged += HandleHealthChanged;

        // Init visual state
        // We need to access unit stats directly since event might have fired already or we just spawned
        // Assuming Unit has public stats access or we can trigger a refresh
        if (unit.Stats.health > 0)
        {
             HandleHealthChanged(unit.Stats.health, unit.Stats.health);
        }
    }

    private void OnDestroy()
    {
        if (targetUnit != null)
        {
            targetUnit.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = max;
            healthSlider.value = current;
        }
    }
}
