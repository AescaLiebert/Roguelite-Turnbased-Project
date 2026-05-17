using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//For Identify Character with Button (I thinks will delete later)
public class Character : MonoBehaviour
{
    public CharacterObject myCharacter;

    public void ShowFighter(CharacterSelectionManager u)
    {
        Debug.Log("Fighter show!");
        //CharacterSelectionManager.instance.ShowFighterInfo(myCharacter);
    }
}
