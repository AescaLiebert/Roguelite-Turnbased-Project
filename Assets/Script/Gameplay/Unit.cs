using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public enum UnitState
{
    Enter,
    Idle,
    ReadyToAction,
    AttackUnit,
    ReadyToUltimate,
    Die,
    TakeDamage,
}

[Serializable]
public struct UnitStats
{
    public float classPower;
    public float attack;
    public float defense;
    public float health;
    
    // Combat Stats
    public float Atk_PierceRate;
    public float Def_Resistance;
    public float Hp_RecoveryRate;
    public float Atk_CritChance;
    public float Atk_CritDamage;
    public float Def_CritResistance;
    public float Def_CritDefense;
    public float Hp_Regeneration;
    public float Def_BlockChance;
    public float Def_BlockPower;
    public float Hp_LifeSteal;
}

[Serializable]
public struct AttackConfiguration
{
    public string AnimationName;
    public float PowerGaugeAdd;
    public float MinAttackMultiplier;
    public float MaxAttackMultiplier;
    public float MinDefenseMultiplier;
    public float MaxDefenseMultiplier;
}

[RequireComponent(typeof(Animator))]
public class Unit : MonoBehaviour, IComparable<Unit>
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private RectTransform healthBarFill;
    [SerializeField] private GameObject[] powerGaugeFills;
    
    [Header("Animation Settings")]
    [SerializeField] private string takeDamageAnimationTrigger = "TakeDamage";
    [SerializeField] private string criticalDamageAnimationTrigger = "CriticalDamage";
    
    public int SlotPosition { get; set; }

    [Header("Unit Stats")]
    [SerializeField] private UnitStats stats;
    public UnitStats Stats => stats;

    [Header("Card Data Runtime")]
    private SkillCardSO _skill1;
    private SkillCardSO _skill2;
    private UltimateCardSO _ultimate;

    public void InitializeCardData(SkillCardSO s1, SkillCardSO s2, UltimateCardSO ult)
    {
        _skill1 = s1;
        _skill2 = s2;
        _ultimate = ult;
    }

    public SkillCardSO GetSkill1() => _skill1;
    public SkillCardSO GetSkill2() => _skill2;
    public UltimateCardSO GetUltimate() => _ultimate;
    
    private GameManager gameManager;
    private Vector2 initialHealthScale;
    private float currentHealth;
    private float maxHealth;
    private float currentPowerGauge;
    private const float MAX_POWER_GAUGE = 5f;
    private const float DAMAGE_DISPLAY_DURATION = 1.5f;

    public bool IsDead { get; private set; }
    public int NextActionTurn { get; private set; }
    public UnitState CurrentState { get; private set; }

    [Header("Data")]
    [SerializeField] private CharacterObject characterData; // Store source SO
    public CharacterObject CharacterData => characterData;

    [Header("Visuals")]
    [SerializeField] private Transform modelHolder; // Optional: specific point to parent model

    public bool IsSubUnit { get; set; } = false;

    void Awake()
    {
        // Don't auto-initialize components in Awake if we are waiting for a model
        // InitializeComponents(); 
        InitializeStats();
    }

    public void Initialize(CharacterObject data, GameObject modelInstance = null)
    {
        characterData = data;

        // 1. Setup Stats
        stats.classPower = data.Classpower;
        stats.attack = data.attack;
        stats.defense = data.defense;
        stats.health = data.health;
        
        // Copy Secondary Stats
        if (data.SecondaryStats != null)
        {
             stats.Atk_PierceRate = data.SecondaryStats.PierceRate;
             stats.Def_Resistance = data.SecondaryStats.Resistance;
             stats.Hp_RecoveryRate = data.SecondaryStats.RecoveryRate;
             stats.Atk_CritChance = data.SecondaryStats.CritChance;
             stats.Atk_CritDamage = data.SecondaryStats.CritDmg;
             stats.Def_CritResistance = data.SecondaryStats.CritResistance;
             stats.Def_CritDefense = data.SecondaryStats.CritDefense;
             stats.Hp_Regeneration = data.SecondaryStats.Regenerate;
             stats.Def_BlockChance = data.SecondaryStats.BlockChance;
             stats.Def_BlockPower = data.SecondaryStats.BlockPower;
             stats.Hp_LifeSteal = data.SecondaryStats.LifeSteal;
        }

        // 2. Setup Components (Animator, etc.) form the model
        if (modelInstance != null)
        {
            animator = modelInstance.GetComponent<Animator>();
            if (animator == null) animator = modelInstance.GetComponentInChildren<Animator>();
        }
        else
        {
            // Try to find it in children if not passed
            animator = GetComponentInChildren<Animator>();
        }

        if (gameManager == null)
        {
             gameManager = GameObject.Find("GameControllerObject")?.GetComponent<GameManager>();
        }
        
        if (healthBarFill != null && initialHealthScale == Vector2.zero)
        {
            initialHealthScale = healthBarFill.localScale;
        }

        // Re-run initialization to apply new health etc.
        InitializeStats();
    }



    public event Action<float, float> OnHealthChanged;

    private void InitializeStats()
    {
        maxHealth = stats.health;
        currentHealth = maxHealth;
        IsDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    public async Task ReceiveDamage(float damage, bool isCritical, bool isBlocked)
    {
        if (IsDead) return;
        
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        PlayDamageAnimation(isCritical);
        UpdateHealthBar();
        
        //Death Animation
        if (currentHealth <= 0)
        {
            await HandleDeath();
        }

        await Task.Delay(TimeSpan.FromSeconds(DAMAGE_DISPLAY_DURATION));
        
        //delay continue game
        StartCoroutine(DelayedContinueGame());
    }
    
    private void PlayDamageAnimation(bool isCritical)
    {
        string triggerName = isCritical ? criticalDamageAnimationTrigger : takeDamageAnimationTrigger;
        animator.SetTrigger(triggerName);
    }

    private async Task HandleDeath()
    {
        IsDead = true;
        gameObject.tag = "Dead";
        CurrentState = UnitState.Die;
        
        // Animate death if needed
        // await PlayDeathAnimation();

        if (healthBarFill != null)
        {
            Destroy(healthBarFill.gameObject);
        }
        
        // Destroy(gameObject); // OLD LOGIC
        
        // NEW LOGIC: Notify Manager
        if (TeamDataManager.Instance != null)
        {
            TeamDataManager.Instance.OnUnitDead(this);
        }
        else
        {
            // Fallback
             Destroy(gameObject);
        }
    }

    public bool GetDead()
    {
        return IsDead;
    }

    private void DisplayDamageEffect(float damage, bool isCritical, bool isBlocked)
    {
        if (damage <= 0)
        {
            DisplayPatientEffect();
            return;
        }

        if (gameManager != null)
        {
            gameManager.DamageText.gameObject.SetActive(true);
            gameManager.DamageText.text = damage.ToString();
        }

        var damageType = DetermineDamageTextType(isCritical, isBlocked);
        DamageTextManager.MyInstance.CreateText(transform.position, damage.ToString(), damageType);
    }

    private SCTTYPE DetermineDamageTextType(bool isCritical, bool isBlocked)
    {
        if (isCritical) return SCTTYPE.CRITICAL;
        if (isBlocked) return SCTTYPE.BLOCKED;
        return SCTTYPE.DAMAGE;
    }
    
    private void DisplayPatientEffect()
    {
        if (gameManager != null)
        {
            gameManager.DamageText.gameObject.SetActive(true);
            gameManager.DamageText.text = "Patient";
        }
        DamageTextManager.MyInstance.CreateText(transform.position, "Patient", SCTTYPE.DAMAGE);
    }

    private void UpdateHealthBar()
    {
        // Keep existing logic for local health bar if needed, or deprecate later
        if (healthBarFill == null) return;

        float healthPercentage = currentHealth / maxHealth;
        float newXScale = initialHealthScale.x * healthPercentage;
        healthBarFill.localScale = new Vector2(newXScale, initialHealthScale.y);
    }
    
    //wait for PG Ultimate System
    public void IncrementPowerGauge()
    {
        if (currentPowerGauge < MAX_POWER_GAUGE)
        {
            currentPowerGauge++;
            UpdatePowerGaugeVisual();
        }
    }
    
    private void UpdatePowerGaugeVisual()
    {
        // Implement power gauge UI update logic
    }
    
    public void SeriousReceiveDamage(float damage, bool isCrit)
    {
        ReceiveDamage(damage,isCrit,false);
    }

    public void LifeDrain(float damageDealt)
    {
        float healingMultiplier = (stats.Hp_RecoveryRate / 100f) * stats.Hp_LifeSteal;
        float healAmount = (healingMultiplier / 100f) * damageDealt;


        HealUnit(healAmount);
        DisplayHealingEffect(healAmount);
    }
    
    private void HealUnit(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        UpdateHealthBar();
    }
    
    private void DisplayHealingEffect(float amount)
    {
        float roundedAmount = Mathf.Round(amount);
        DamageTextManager.MyInstance.CreateText(transform.position, roundedAmount.ToString(), SCTTYPE.HEAL);
    }

    public IEnumerator RecoveryEachTurn()
    {
        float healthDeficit = maxHealth - currentHealth;
        float regenAmount = ((stats.Hp_Regeneration / 100f) * (stats.Hp_RecoveryRate / 400f)) * healthDeficit;

        if (regenAmount > 0)
        {
            HealUnit(regenAmount);
            DisplayHealingEffect(regenAmount);
        }

        yield return new WaitForSecondsRealtime(1f);
    }

    private void ContinueGame()
    {
        gameManager?.NextTurn();
    }
    
    private IEnumerator DelayedContinueGame()
    {
        yield return new WaitForSeconds(1.5f);
        ContinueGame(); 
    }
    
    //need to fix the calculator turn with CC calculate
    public void CalculateNextTurn(int currentTurn)
    {
        // Implement turn calculation logic based on speed or other factors
        NextActionTurn = currentTurn + 1;
    }
    
    
    public int CompareTo(Unit other)
    {
        if (other == null) return 1;
        return NextActionTurn.CompareTo(other.NextActionTurn);
    }
    
    /*public void SetupHealthBar(Canvas canvas, Camera Camera)
    {
        Charui.transform.SetParent(canvas.transform);
        if (Charui.TryGetComponent<FaceCamera>(out FaceCamera faceCamera))
        {
            FaceCamera.Camera = Camera;
        }
    }*/
}
