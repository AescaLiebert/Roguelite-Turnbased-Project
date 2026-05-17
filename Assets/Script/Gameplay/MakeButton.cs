using UnityEngine;
using UnityEngine.UI;

public class MakeButton : MonoBehaviour
{
    private GameObject hero; 
    string temp;
    
    //Start is called before the first frame update
    void Start()
    {
        string temp = gameObject.name;
        gameObject.GetComponent<Button>().onClick.AddListener(() => AttachCallBack(temp));
        hero = GameObject.FindGameObjectWithTag("Hero");
    }

    //must fix this function
    private void AttachCallBack(string buttons)
    {
      
        if (buttons.CompareTo("skill1Btn") == 0)
        {
            hero.GetComponent<FighterAction>().SelectAttack("Skill1");
        }
        else if (buttons.CompareTo("skill2Btn") == 0)
        {
            hero.GetComponent<FighterAction>().SelectAttack("Skill2");
        }
    }
    // Update is called once per frame
}
