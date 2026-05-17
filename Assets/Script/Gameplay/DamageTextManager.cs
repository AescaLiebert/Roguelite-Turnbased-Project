using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SCTTYPE {DAMAGE,HEAL,CRITICAL,BLOCKED}

public class DamageTextManager : MonoBehaviour
{
    private static DamageTextManager instance;

    public static DamageTextManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<DamageTextManager>();
            }

            return instance;
        }
    }

    [SerializeField] private GameObject CombatPrefab;

    public void CreateText(Vector3 position, string text, SCTTYPE type)
    {
        position.y += 1.7f;
        position.z += 1f;
        TextMeshProUGUI sct = Instantiate(CombatPrefab, transform).GetComponent<TextMeshProUGUI>();
        sct.transform.position = position;
        
        switch (type)
        {
            case SCTTYPE.DAMAGE:
                break;
            case SCTTYPE.HEAL:
                sct.color = Color.green;
                break;
            case SCTTYPE.CRITICAL:
                sct.color = new Color(1f,0.5f,0f);
                break;
            case SCTTYPE.BLOCKED:
                sct.color = Color.yellow;
                break;
        }

        sct.text = text;
    }
}
