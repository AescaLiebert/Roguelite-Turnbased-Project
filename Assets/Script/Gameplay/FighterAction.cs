using UnityEngine;

public class FighterAction : MonoBehaviour
{
    private GameObject enemy;
    private GameObject hero;

    [SerializeField] 
    private GameObject Skill1prefab;
    
    [SerializeField] 
    private GameObject Skill2prefab;
    
    private GameObject currentAttack;

    //need fix for instance object

    private void Awake()
    {
        hero = GameObject.FindGameObjectWithTag("Hero");
        enemy = GameObject.FindGameObjectWithTag("Enemy");
    }

    public void SelectAttack(string buttons)
    {
        GameObject victim = hero;
        if (tag == "Hero")
        {
            victim = enemy;
        }

        if (buttons.CompareTo("Skill1") == 0)
        {
            GameObject skillObject = Instantiate(Skill1prefab);
            var actionScript = skillObject.GetComponent<ActionScript>();
            actionScript.owner = this.gameObject;
            actionScript.Attack(victim);
            
            // Destroy after use if it's just a logic carrier, or let it handle its own destruction
            Destroy(skillObject, 2f); // Safety destroy
        }
        else if (buttons.CompareTo("Skill2") == 0)
        {
            GameObject skillObject = Instantiate(Skill2prefab);
            var actionScript = skillObject.GetComponent<ActionScript>();
            actionScript.owner = this.gameObject;
            actionScript.Attack(victim);
            
            Destroy(skillObject, 2f); // Safety destroy
        }else
        {
            Debug.Log("Ultimate Cast!");
        }
    }
}
