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
        Purple,
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
        [InspectorName("Off-Field")] Sub,
        [InspectorName("SUB, PVP-Only")] SubPvpOnly
}

public enum PassiveRestriction
{
        Unknown,
        All,
        Own,
        [InspectorName("Green attribute")] GreenAttribute,
        [InspectorName("Red attribute")] RedAttribute,
        Women,
        [InspectorName("PVP")] PvP,
        [InspectorName("PVE")] PvE
}

public static class CharacterPassiveMetadata
{
        public static PassiveSourceType ParseSourceType(string value)
        {
                switch ((value ?? string.Empty).Trim().ToUpperInvariant())
                {
                        case "ON-FIELD": return PassiveSourceType.OnField;
                        case "SUB": return PassiveSourceType.Sub;
                        case "OFF-FIELD": return PassiveSourceType.Sub;
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
                        case "PVP": return PassiveRestriction.PvP;
                        case "PVE": return PassiveRestriction.PvE;
                        default: return PassiveRestriction.Unknown;
                }
        }

        public static string SourceTypeLabel(PassiveSourceType value) => value switch
        {
                PassiveSourceType.OnField => "On-Field",
                PassiveSourceType.Sub => "Off-Field",
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
                PassiveRestriction.PvP => "PVP",
                PassiveRestriction.PvE => "PVE",
                _ => string.Empty
        };
}


[CreateAssetMenu(fileName = "New Character", menuName = "Character System/Character Object")] 
    public class CharacterObject : ScriptableObject, IEquatable<CharacterObject>
    {
        [Header("Stable Content Identity")]
        [SerializeField] private string definitionId;
        [SerializeField] private bool runtimeReady;
        [SerializeField] private string familyId;
        [SerializeField] private string seriesId;
        [SerializeField] private string role;
        [SerializeField] private string[] traitIds = Array.Empty<string>();
        [SerializeField, TextArea] private string passiveSourceDescription;
        [SerializeField] private PassiveSourceType passiveSourceType;
        [SerializeField] private PassiveRestriction passiveRestriction;
        [Header("Passive")]
        [SerializeField] private PassiveDefinitionSO passiveDefinition;
        [SerializeField, HideInInspector] private int studioSchema;
        [SerializeField, HideInInspector] private string studioMetadataJson;
        [SerializeField, HideInInspector] private string studioBundleHash;
        [SerializeField, HideInInspector] private string studioAuthoringJson, studioLibraryJson;
        public string StudioAuthoringJson => studioAuthoringJson;
        public string StudioLibraryJson => studioLibraryJson;
        public void SetStudioAuthoring(string authoring, string library)
        { studioAuthoringJson = authoring; studioLibraryJson = library; }
        public bool StudioOwned => studioSchema != 0;
        public string StudioBundleHash => studioBundleHash;
        public void SetStudioMetadata(CharacterDefinition value, string bundleHash)
        {
                if (value == null) throw new ArgumentNullException(nameof(value));
                var metadata = value.Clone(); metadata.Skills.Clear(); metadata.UltimateTiers.Clear(); metadata.Passive = null;
                studioMetadataJson = SnapshotJson.Serialize(metadata); studioSchema = 1; studioBundleHash = bundleHash;
        }
        public CharacterDefinition CreateStudioMetadata()
        {
                if (!StudioOwned) return null;
                if (studioSchema != 1) throw new InvalidOperationException("Unsupported Studio character schema: " + studioSchema);
                return SnapshotJson.Deserialize<CharacterDefinition>(studioMetadataJson) ?? throw new InvalidOperationException("Studio character metadata is missing.");
        }

        [Header("Basic Information")]
        [SerializeField] private int id;
        [SerializeField] private string fighterName;
        
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
        [Tooltip("Visual yaw correction beneath the battle actor root. Current sample models face -Z; use 0 for models authored facing +Z.")]
        [SerializeField] private float fighterModelYawOffset = 180f;

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
        [Tooltip("Starting values. Basis points of level-one stats gained each level; only these four stats grow.")]
        public CharacterStatGrowth LevelGrowth = new CharacterStatGrowth();
        
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
        public Sprite FighterFull => fighterFull;
        public Sprite FighterPassive => fighterPassive;
        public Sprite FighterCard1 => fighterCard1;
        public Sprite FighterCard2 => fighterCard2;
        public Sprite FighterUltimate => fighterUltimate;

        public GameObject Fighter3DPrefab => fighter3DPrefab;
        public float FighterModelYawOffset => fighterModelYawOffset;
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
                   && definitionId == other.definitionId;
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
                hashCode = (hashCode * 397) ^ (definitionId != null ? definitionId.GetHashCode() : 0);
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
