using UnityEngine;
using System;
using FightingAllstar.Core.Content;


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

public enum PassiveSourceType
{
        Unknown,
        [InspectorName("On-Field")] OnField,
        [InspectorName("SUB")] Sub,
        [InspectorName("SUB, PVP-Only")] SubPvpOnly
}

public enum PassiveRestriction
{
        Unknown,
        All,
        Own,
        [InspectorName("Green attribute")] GreenAttribute,
        [InspectorName("Red attribute")] RedAttribute,
        Women
}

public static class CharacterPassiveMetadata
{
        public static PassiveSourceType ParseSourceType(string value)
        {
                switch ((value ?? string.Empty).Trim().ToUpperInvariant())
                {
                        case "ON-FIELD": return PassiveSourceType.OnField;
                        case "SUB": return PassiveSourceType.Sub;
                        case "SUB, PVP-ONLY": return PassiveSourceType.SubPvpOnly;
                        default: return PassiveSourceType.Unknown;
                }
        }

        public static PassiveRestriction ParseRestriction(string value)
        {
                switch ((value ?? string.Empty).Trim().ToUpperInvariant())
                {
                        case "ALL": return PassiveRestriction.All;
                        case "OWN": return PassiveRestriction.Own;
                        case "GREEN ATTRIBUTE": return PassiveRestriction.GreenAttribute;
                        case "RED ATTRIBUTE": return PassiveRestriction.RedAttribute;
                        case "WOMEN": return PassiveRestriction.Women;
                        default: return PassiveRestriction.Unknown;
                }
        }

        public static string SourceTypeLabel(PassiveSourceType value) => value switch
        {
                PassiveSourceType.OnField => "On-Field",
                PassiveSourceType.Sub => "SUB",
                PassiveSourceType.SubPvpOnly => "SUB, PVP-Only",
                _ => string.Empty
        };

        public static string RestrictionLabel(PassiveRestriction value) => value switch
        {
                PassiveRestriction.All => "All",
                PassiveRestriction.Own => "Own",
                PassiveRestriction.GreenAttribute => "Green attribute",
                PassiveRestriction.RedAttribute => "Red attribute",
                PassiveRestriction.Women => "Women",
                _ => string.Empty
        };
}


[CreateAssetMenu(fileName = "New Character", menuName = "Character System/Character Object")] 
    public class CharacterObject : ScriptableObject, IEquatable<CharacterObject>
    {
        [Header("Stable Content Identity")]
        [SerializeField] private string definitionId;
        [SerializeField] private bool runtimeReady;
        [SerializeField] private int sourceId;
        [SerializeField] private int sourceRecord;
        [SerializeField] private string familyId;
        [SerializeField] private string seriesId;
        [SerializeField] private string role;
        [SerializeField] private string[] traitIds = Array.Empty<string>();
        [SerializeField, TextArea] private string passiveSourceDescription;
        [SerializeField] private PassiveSourceType passiveSourceType;
        [SerializeField] private PassiveRestriction passiveRestriction;
        [Header("Passive")]
        [SerializeField] private PassiveDefinitionSO passiveDefinition;

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
        [SerializeField] private Mesh fighter3DMesh;
        [SerializeField] private Material fighter3DMaterial;

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
        public string DefinitionId => definitionId;
        public bool RuntimeReady => runtimeReady;
        public int SourceId => sourceId;
        public int SourceRecord => sourceRecord;
        public string FamilyId => familyId;
        public string SeriesId => seriesId;
        public string Role => role;
        public string[] TraitIds => traitIds;
        public string PassiveSourceDescription => passiveSourceDescription;
        public PassiveSourceType PassiveSourceType => passiveSourceType;
        public PassiveRestriction PassiveRestriction => passiveRestriction;
        public PassiveDefinitionSO PassiveDefinition => passiveDefinition;
        public PassiveDefinition GetPassiveDefinition() => passiveDefinition == null ? null : passiveDefinition.CreateDefinition();
        public string FighterName => fighterName;
        public Sprite FighterPic => fighterPic;
        public Sprite FighterIcon => fighterIcon;

        public GameObject Fighter3DPrefab => fighter3DPrefab;
        public Mesh Fighter3DMesh => fighter3DMesh;
        public Material Fighter3DMaterial => fighter3DMaterial;
        public int FighterLevel => fighterLevel;

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
        public float AvoidanceRate = 100f;
        public float EvadeRate;
        public float ControlRate = 100f;
        public float PerceptionRate = 100f;
    }
