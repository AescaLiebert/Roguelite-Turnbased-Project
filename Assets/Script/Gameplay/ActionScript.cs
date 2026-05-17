using UnityEngine;
using Random = UnityEngine.Random;

public class ActionScript : MonoBehaviour
{
    public GameObject owner;
    
    //เเก้เรื่องเกจอันติ
    [SerializeField] private float PGadd;
    private bool isCrit, isBlocked;
    
    [SerializeField] private string aniamtionName;
    [SerializeField] private float minAttackMultiplier;
    [SerializeField] private float maxAttackMultiplier;
    [SerializeField] private float minDefenseMultiplier;
    [SerializeField] private float maxDefenseMultiplier;
    

    private Unit attackerstats;
    private Unit targetstats;
   
    private float damage = 0.0f;

    public void Attack(GameObject victim)
    {
        isBlocked = false;
        isCrit = false;
        attackerstats = owner.GetComponent<Unit>();
        targetstats = victim.GetComponent<Unit>();

        //ต้องเเก้เรื่องเกจอันติ
        //เเก้เรื่องการโจมตีให้ติดคริ เอฟเฟคสกิล ดาเมจ เกจ บล๊อก target def-related atk-related
        //update Powergauge
        
        //attackerstats.updatePGFill(PGstatus);
            //random atk dmg
            
            //using Skill or Ultimate
            
            /*
            if (attackerstats.pg == 0)
            {
                damage = multiplier * attackerstats.attack;
                attackerstats.pg = attackerstats.pg - PGadd;
            }*/
            
            //=====PG gauge=====
            attackerstats.IncrementPowerGauge();
            
            
            //=====Random number of take dmg=====
            float atkmultiplier = Random.Range(minAttackMultiplier, maxAttackMultiplier);
            float defensemultiplier = Random.Range(minDefenseMultiplier, maxDefenseMultiplier);
            
            
            //=====calculate atk dmg=====
            damage = atkmultiplier * (attackerstats.Stats.attack + (attackerstats.Stats.Atk_PierceRate / 100f * attackerstats.Stats.attack));
            
            
            //=====Card Skill effect=====
            
            
            //=====Calculate Crit and Block=====
            int Possibility = Random.Range(1, 100);
            float critchc = attackerstats.Stats.Atk_CritChance - targetstats.Stats.Def_CritResistance;
            float critdmgamount = attackerstats.Stats.Atk_CritDamage - targetstats.Stats.Def_CritDefense;
            float BlockAmount = (targetstats.Stats.Def_BlockPower / 100f) * damage;
            float Blockchc = (targetstats.Stats.Def_BlockChance - ((attackerstats.Stats.Atk_PierceRate / 100f) / 0.375f) * targetstats.Stats.Def_BlockChance);
            
            
            if (Possibility <= Blockchc)
            {
                isBlocked = true;
                damage -= BlockAmount;
                Debug.Log($"Possibility is {Possibility} and Blocked the {BlockAmount} Block Chance {Blockchc}");
            } else if (Possibility <= critchc)
            {
                isCrit = true;
                Debug.Log($"Possibility is {Possibility} and your Crit Chance is {critchc}");
                damage *= (critdmgamount / 100f);
            }else if (Blockchc >= 100f && critchc >= 100f)
            {
                damage *= 0.97f;
            } //Debug.Log($"You not Crit or Not Blocked");
            
            
            //=====Defense Calculation=====
            damage = Mathf.Max(0, damage - (defensemultiplier * targetstats.Stats.defense + (targetstats.Stats.Def_Resistance / 100f * targetstats.Stats.defense)));
            
            
            //=====play attacker aniamtion=====
            owner.GetComponent<Animator>().Play(aniamtionName);
            
            
            //=====Take Damage send to FighterStats=====
            if (atkmultiplier >= 1.20f)
            {
                targetstats.SeriousReceiveDamage(Mathf.CeilToInt(damage),isCrit);
            }
            else
            {
                targetstats.ReceiveDamage(Mathf.CeilToInt(damage),isCrit,isBlocked);
            }
            
            //=====LifeSteal after attack=====
            Invoke("LifeDrainWait",1f);
    }
    
    public void LifeDrainWait()
    {
        attackerstats.LifeDrain(damage);
    }
}
