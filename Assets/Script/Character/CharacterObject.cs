using UnityEngine;
using System;


[Serializable]
public enum FighterAttribute
{
        Red,
        Green,
        Blue,
        Yellow,
        Darkness,
        Light,
        Infinity
}

public enum FighterRarity
{
        R,
        SR,
        SSR,
        FES,
        EX,
        UE,
        LR
}


[CreateAssetMenu(fileName = "New Character", menuName = "Character System/Character Object")] 
public class CharacterObject : ScriptableObject, IEquatable<CharacterObject>
    {
        [Header("Basic Information")]
        [SerializeField] private int id;
        [SerializeField] private string fighterName;
        [SerializeField] private string fighterTag;
        
        [Header("Visual Assets")]
        [SerializeField] private Sprite fighterPic;
        [SerializeField] private Sprite fighterIcon;
        [SerializeField] private Sprite fighterFull;
        [SerializeField] private Sprite fighterPassive;
        [SerializeField] private Sprite fighterCard1;
        [SerializeField] private Sprite fighterCard2;
        [SerializeField] private Sprite fighterUltimate;

        [Header("3D Model")] 
        [SerializeField] private GameObject fighter3DPrefab;

        [Header("Card Data")]
        public SkillCardSO Skill1;
        public SkillCardSO Skill2;
        public UltimateCardSO Ultimate;

        public FighterAttribute FighterAttribute;
        public FighterRarity FighterRarity;
        
        [Header("Progression")]
        [SerializeField] private int fighterLevel = 1;
        [SerializeField] private int fighterAwakening = 0;
        [SerializeField] private int fighterUltimateLevel = 1;
        
        [Header("Primary Stats")]
        [SerializeField] public float Classpower;
        [SerializeField] public float attack;
        [SerializeField] public float defense;
        [SerializeField] public float health;

        [Header("Secondary Stats")] public CharacterSecondaryStats SecondaryStats;
        
        // Properties with public getters for read-only access
        public int ID => id;
        public string FighterName => fighterName;
        public Sprite FighterPic => fighterPic;
        public Sprite FighterIcon => fighterIcon;

        public GameObject Fighter3DPrefab => fighter3DPrefab;

        // Implement IEquatable for more robust comparison
        public bool Equals(CharacterObject other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            
            return id == other.id 
                   && fighterName == other.fighterName 
                   && fighterTag == other.fighterTag;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            
            return Equals((CharacterObject)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = id;
                hashCode = (hashCode * 397) ^ (fighterName != null ? fighterName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (fighterTag != null ? fighterTag.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    // Separate class for secondary stats for better organization
    [Serializable]
    public class CharacterSecondaryStats
    {
        public float PierceRate;
        public float Resistance;
        public float Regenerate;
        public float CritChance;
        public float CritDmg;
        public float CritResistance;
        public float CritDefense;
        public float RecoveryRate;
        public float BlockChance;
        public float BlockPower;
        public float LifeSteal;
    }

