using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private List<Unit> fighterStats;
    [SerializeField] private GameObject battleMenu;
    public TextMeshProUGUI DamageText;
    [HideInInspector] public int nextActTurn;

    //[SerializeField] private Transform[] characterSpawnPos;
    //[SerializeField] private GameObject GameoverScreen;
    
    // Start is called before the first frame update
    void Start()
    {
        fighterStats = new List<Unit>();
        Invoke(nameof(TeamDataManager.Instance.InitializeTeam), 0.1f);
        
        
        GameObject hero = GameObject.FindGameObjectWithTag("Hero");
        Unit currectFighterStats = hero.GetComponent<Unit>();
        currectFighterStats.CalculateNextTurn(0);
        fighterStats.Add(currectFighterStats);
        
        
        GameObject enemy = GameObject.FindGameObjectWithTag("Enemy");
        Unit currentEnemyStats = enemy.GetComponent<Unit>();
        currectFighterStats.CalculateNextTurn(0);
        fighterStats.Add(currentEnemyStats);
        
        fighterStats.Sort();
        this.battleMenu.SetActive(false);
        
       NextTurn();
    }

    public void NextTurn()
    {
        this.battleMenu.SetActive(false);
        DamageText.gameObject.SetActive(false);
        
        Unit currentFighterStats = fighterStats[0];
        fighterStats.Remove(currentFighterStats);

        StartCoroutine(currentFighterStats.RecoveryEachTurn());
        
        if (!currentFighterStats.GetDead())
        {
            GameObject currentUnit = currentFighterStats.gameObject;
            currentFighterStats.CalculateNextTurn(nextActTurn);
            fighterStats.Add(currentFighterStats);
            fighterStats.Sort();
            if (currentUnit.tag == "Hero")
            {
                this.battleMenu.SetActive(true);
            }
            else //ต้องเปลี่ยน
            {
                string attackType = Random.Range(0, 2) == 1 ? "Skill1" : "Skill2";
                currentUnit.GetComponent<FighterAction>().SelectAttack(attackType);
            }
        }else
        {
            NextTurn();
        } 
    }
}
