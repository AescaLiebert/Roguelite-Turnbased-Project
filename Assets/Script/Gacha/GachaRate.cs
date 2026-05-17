using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GachaRate
{
    public string rateName;
    [Range(1, 101)] public float rate;
    public CharacterObject[] reward;
    public string rarity;
}
