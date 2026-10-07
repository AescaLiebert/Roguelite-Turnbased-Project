using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports the six requested WIP roster entries as complete ScriptableObject authoring sets.
/// Mai95 has a supported runtime kit; the other draft entries remain review-only.
/// </summary>
public static class BuildRequestedCompleteCharacterSOs
{
    private static readonly string SourcePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
        "Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/Specs/CharacterData/wip-character-drafts.json");
    private const string CharacterPath = "Assets/Project/Data/Character";
    private const string CardPath = "Assets/Project/Data/Character";
    private const string PassivePath = "Assets/Project/Data/Character";
    private const string RegistryPath = "Assets/Resources/CharacterObjectRegistry.asset";
    private const string ModelPath = "Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab";

    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        ["fighter.goro94"] = "goro94",
        ["fighter.mai95"] = "mai95",
        ["fighter.yuri94"] = "yuri94",
        ["fighter.ryo94"] = "ryo94",
        ["fighter.choi94"] = "choi94",
        ["fighter.chang94"] = "chang94"
    };

    // Keep this import separate: rebuilding the original six would replace any later edits
    // to their card runtime effects.
    private static readonly Dictionary<string, string> AdditionalWipNames = new Dictionary<string, string>
    {
        ["fighter.joe94"] = "joe94",
        ["fighter.andy94"] = "andy94",
        ["fighter.kyo98"] = "kyo98",
        ["fighter.benimaru99"] = "benimaru99"
    };

    [MenuItem("Fighting Allstar/Content/Build Mai95 Only")]
    public static void BuildMai95Only()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        var draft = root?.characters?.FirstOrDefault(item => item != null && item.definitionId == "fighter.mai95");
        if (draft == null) throw new InvalidOperationException("Draft character not found: fighter.mai95");
        if (draft.cards == null || draft.cards.Length != 2 ||
            draft.cards.Any(card => card == null || card.ranks == null || card.ranks.Length != 3))
            throw new InvalidOperationException("Mai95 must have both complete 3-rank skills before import.");
        if (draft.constellations == null || draft.constellations.Length != 7)
            throw new InvalidOperationException("Mai95 must have all seven C0-C6 Ultimate tiers before import.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        BuildCharacter(draft, "mai95", model);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built Mai95 character, passive, two runtime skills, and seven Ultimate tiers. Other character assets were not touched.");
    }

    [MenuItem("Fighting Allstar/Content/Build Requested Complete Character SOs")]
    public static void Build()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        EnsureFolder(CardPath);
        EnsureFolder(PassivePath);

        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        if (root?.characters == null) throw new InvalidOperationException("Could not read the WIP character draft source.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach (var pair in Names)
        {
            var draft = root.characters.FirstOrDefault(item => item != null && item.definitionId == pair.Key);
            if (draft == null) throw new InvalidOperationException("Draft character not found: " + pair.Key);
            BuildCharacter(draft, pair.Value, model);
        }
        BuildRegistry();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built six character, skill, ultimate, and passive SO sets. Mai95 is runtime-ready; other WIP fighters remain review-only.");
    }

    [MenuItem("Fighting Allstar/Content/Import WIP Joe94, Andy94, Kyo98, Benimaru99")]
    public static void BuildAdditionalWipCharacters()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        EnsureFolder(CardPath);
        EnsureFolder(PassivePath);

        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        if (root?.characters == null) throw new InvalidOperationException("Could not read the WIP character draft source.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach (var pair in AdditionalWipNames)
        {
            var draft = root.characters.FirstOrDefault(item => item != null && item.definitionId == pair.Key);
            if (draft == null) throw new InvalidOperationException("Draft character not found: " + pair.Key);
            if (draft.cards == null || draft.cards.Length < 2 || draft.cards.Any(card => card == null || card.ranks == null || card.ranks.Length < 3))
                throw new InvalidOperationException(pair.Key + " must have both complete 3-rank skills before import.");
            if (draft.constellations == null || draft.constellations.Length < 7)
                throw new InvalidOperationException(pair.Key + " must have all seven Ultimate tiers before import.");
            BuildCharacter(draft, pair.Value, model);
        }
        BuildRegistry();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported Joe94, Andy94, Kyo98, and Benimaru99 from WIP_Char as reviewable character SO sets with complete skill ranks and Ultimate tiers. Runtime effects remain unassigned because the source drafts are not runtime-ready.");
    }

    private static void BuildRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<CharacterObjectRegistrySO>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<CharacterObjectRegistrySO>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }
        var characters = AssetDatabase.FindAssets("t:CharacterObject", new[] { CharacterPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadAssetAtPath<CharacterObject>(path))
            .Where(character => character != null)
            .OrderBy(character => character.SourceId)
            .ToList();
        registry.SetCharacters(characters);
        EditorUtility.SetDirty(registry);
    }

    private static void BuildCharacter(DraftCharacter draft, string assetName, GameObject model)
    {
        var safeId = draft.definitionId.Replace("fighter.", string.Empty);
        var skill1 = BuildSkill(draft, draft.cards.First(item => item.slot == 1));
        var skill2 = BuildSkill(draft, draft.cards.First(item => item.slot == 2));
        var ultimate = BuildUltimate(draft);
        var passive = BuildPassive(draft);

        // Keep the original Yuri asset path (and its GUID) while correcting its requested rarity.
        var path = CharacterPath + "/" + assetName + ".asset";
        var character = LoadOrCreate<CharacterObject>(path);
        character.name = assetName;
        var serialized = new SerializedObject(character);
        Set(serialized, "definitionId", draft.definitionId);
        Set(serialized, "runtimeReady", draft.definitionId == "fighter.mai95");
        Set(serialized, "sourceId", draft.sourceId);
        Set(serialized, "sourceRecord", draft.sourceRecord);
        Set(serialized, "familyId", "family." + Slug(draft.familyId));
        Set(serialized, "seriesId", "series.kof");
        Set(serialized, "role", draft.role);
        Set(serialized, "traitIds", draft.traits.Select(item => "trait." + Slug(item)).Concat(new[] { "trait.series-kof" }).ToArray());
        Set(serialized, "passiveSourceDescription", draft.passive.description);
        Set(serialized, "passiveSourceType", draft.passive.sourceType);
        Set(serialized, "passiveRestriction", draft.passive.restriction);
        Set(serialized, "passiveDefinition", passive);
        Set(serialized, "id", 1000 + draft.sourceId);
        Set(serialized, "fighterName", draft.name);
        Set(serialized, "fighterTag", draft.familyId);
        Set(serialized, "fighterPic", LoadCharacterSprite(draft.definitionId, true));
        Set(serialized, "fighterIcon", LoadCharacterSprite(draft.definitionId, false));
        Set(serialized, "fighter3DPrefab", model);
        Set(serialized, "Skill1", skill1);
        Set(serialized, "Skill2", skill2);
        Set(serialized, "Ultimate", ultimate);
        Set(serialized, "FighterAttribute", (Enum)Enum.Parse(typeof(FighterAttribute), draft.attribute, true));
        // Rarity in this request is authoritative for Yuri and Ryo, even though the draft lists SR.
        var rarity = safeId == "yuri94" || safeId == "ryo94" ? "R" : draft.rarity;
        Set(serialized, "FighterRarity", (Enum)Enum.Parse(typeof(FighterRarity), rarity, true));
        Set(serialized, "fighterLevel", 1);
        Set(serialized, "fighterAwakening", 0);
        Set(serialized, "fighterUltimateLevel", 0);
        Set(serialized, "Classpower", draft.stats.Class_Combat.value);
        Set(serialized, "attack", draft.stats.Attack.value);
        Set(serialized, "defense", draft.stats.Defense.value);
        Set(serialized, "health", draft.stats.Health.value);
        var secondary = serialized.FindProperty("SecondaryStats");
        Set(secondary, "PierceRate", draft.stats.Pierce_Rate.value);
        Set(secondary, "Resistance", draft.stats.Resistance.value);
        Set(secondary, "Regenerate", draft.stats.Regeneration.value);
        Set(secondary, "CritChance", draft.stats.Critical_Chance.value);
        Set(secondary, "CritDmg", draft.stats.Critical_Damage.value);
        Set(secondary, "CritResistance", draft.stats.Critical_Resistance.value);
        Set(secondary, "CritDefense", draft.stats.Critical_Defense.value);
        Set(secondary, "RecoveryRate", draft.stats.Recovery_Rate.value);
        Set(secondary, "BlockChance", draft.stats.Block_Chance.value);
        Set(secondary, "BlockPower", draft.stats.Block_Power.value);
        Set(secondary, "LifeSteal", draft.stats.Lifesteal.value);
        Set(secondary, "AvoidanceRate", draft.stats.Avoidance_Rate.value);
        Set(secondary, "EvadeRate", draft.stats.Evade_Rate.value);
        Set(secondary, "ControlRate", draft.stats.Control_Rate.value);
        Set(secondary, "PerceptionRate", draft.stats.Perception_Rate.value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static SkillCardSO BuildSkill(DraftCharacter character, DraftCard source)
    {
        var path = CardPath + "/" + character.definitionId.Replace("fighter.", string.Empty) + "_skilldata" + source.slot + ".asset";
        var asset = LoadOrCreate<SkillCardSO>(path);
        asset.cardName = character.name + " · Skill " + source.slot;
        asset.cardIcon = LoadCharacterSprite(character.definitionId, false);
        asset.ranks = source.ranks.OrderBy(item => item.rank).Select(item => new CardRankData
        {
            rankLevel = item.rank,
            description = item.description,
            skillType = character.definitionId == "fighter.mai95" && source.slot == 1
                ? SkillType.DebuffAtk : ParseSkillType(source.sourceType),
            runtimeEffect = character.definitionId == "fighter.mai95"
                ? BuildMaiSkillEffect(source, item) : null
        }).ToList();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static UltimateCardSO BuildUltimate(DraftCharacter character)
    {
        var path = CardPath + "/" + character.definitionId.Replace("fighter.", string.Empty) + "_ultdata1.asset";
        var asset = LoadOrCreate<UltimateCardSO>(path);
        asset.ultimateName = character.name + " · Ultimate";
        var safeId = character.definitionId.Replace("fighter.", string.Empty);
        asset.icon = LoadCharacterSprite(character.definitionId, false);
        asset.levels = character.constellations.OrderBy(item => item.tier).Select(item => new UltimateLevelData
        {
            level = item.tier,
            description = item.description,
            skillType = SkillType.Attack,
            runtimeEffect = character.definitionId == "fighter.mai95"
                ? BuildMaiUltimateEffect(item.tier) : null
        }).ToList();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static PassiveDefinitionSO BuildPassive(DraftCharacter character)
    {
        var path = PassivePath + "/" + character.definitionId.Replace("fighter.", string.Empty) + "_Passive.asset";
        var asset = LoadOrCreate<PassiveDefinitionSO>(path);
        asset.SetSourceDraft(character.passive.description, character.passive.sourceType, character.passive.restriction);
        asset.SetDefinition(BuildPassiveDefinition(character));
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static FightingAllstar.Core.Content.PassiveDefinition BuildPassiveDefinition(DraftCharacter character)
    {
        var definition = new FightingAllstar.Core.Content.PassiveDefinition { Id = character.definitionId + ".passive.source" };
        var aura = default(FightingAllstar.Core.Content.PassiveAuraDefinition);
        switch (character.definitionId)
        {
            case "fighter.yuri94":
                aura = CreateAura("green-attack", FightingAllstar.Core.Content.PassiveRelation.Allies);
                aura.Targets.AttributeId = "attribute.green";
                AddAttackRelatedModifiers(aura, 1000);
                break;
            case "fighter.choi94":
                aura = CreateAura("women-attack-down", FightingAllstar.Core.Content.PassiveRelation.Enemies);
                aura.Targets.TraitId = "trait.women";
                AddAttackRelatedModifiers(aura, -1000);
                break;
            case "fighter.chang94":
                aura = CreateAura("green-def", FightingAllstar.Core.Content.PassiveRelation.Allies);
                aura.Targets.AttributeId = "attribute.green";
                aura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
                {
                    Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
                    Stat = FightingAllstar.Core.Content.StatId.Defense,
                    Operation = FightingAllstar.Core.Content.ModifierOperation.PercentOfBase,
                    Amount = 6000
                });
                break;
            case "fighter.mai95":
                definition.Reactions.Add(new FightingAllstar.Core.Content.PassiveReactionDefinition
                {
                    Id = "ignite-hit-bonus",
                    Gate = new FightingAllstar.Core.Content.PassiveGate
                    {
                        Presence = FightingAllstar.Core.Content.PassivePresence.LivingActive
                    },
                    Trigger = FightingAllstar.Core.Content.PassiveEventKind.BeforeDamage,
                    ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Self,
                    TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
                    Conditions =
                    {
                        new FightingAllstar.Core.Content.EffectConditionDefinition
                        {
                            Kind = FightingAllstar.Core.Content.EffectConditionKind.TargetHasStatusTag,
                            Tag = FightingAllstar.Core.Content.CombatTags.Ignite,
                            Threshold = 1
                        }
                    },
                    Commands =
                    {
                        new FightingAllstar.Core.Content.PassiveCommandDefinition
                        {
                            Kind = FightingAllstar.Core.Content.PassiveCommandKind.IncreaseCurrentAttackPercent,
                            Amount = 2000
                        },
                        new FightingAllstar.Core.Content.PassiveCommandDefinition
                        {
                            Kind = FightingAllstar.Core.Content.PassiveCommandKind.IncreaseCurrentDamageDealtPercent,
                            Amount = 2000
                        }
                    }
                });
                break;
        }

        if (aura != null) definition.Auras.Add(aura);
        return definition;
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildMaiSkillEffect(DraftCard card, DraftRank rank)
    {
        var coefficient = card.slot == 1 ? rank.rank * 12000 : rank.rank * 12500;
        var effect = new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = "fighter.mai95.skill." + card.slot + ".rank." + rank.rank,
            Kind = FightingAllstar.Core.Content.EffectKind.Damage,
            Family = FightingAllstar.Core.Content.DamageFamily.Normal,
            Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
            CoefficientBp = coefficient,
            KeywordFactorBp = 10000,
            Target = card.slot == 1
                ? FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
                : FightingAllstar.Core.Content.EffectTargetScope.AllEnemies,
            Provenance = rank.provenance == "source-rank3"
                ? FightingAllstar.Core.Content.SourceProvenance.Supplied
                : FightingAllstar.Core.Content.SourceProvenance.GeneratedStartingValue
        };

        if (card.slot == 1)
        {
            effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
            {
                Timing = FightingAllstar.Core.Content.CardEffectTiming.Damaging,
                Effect = new FightingAllstar.Core.Content.EffectStepDefinition
                {
                    Id = "fighter.mai95.skill.1.remove-buffs",
                    Kind = FightingAllstar.Core.Content.EffectKind.RemoveBuffs,
                    Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
                }
            });
            var ignite = FightingAllstar.Core.Content.StandardEffectDatabase.CreateStatusRecipes()
                .Find(item => item.Id == "status.debuff.ignite");
            effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
            {
                Timing = FightingAllstar.Core.Content.CardEffectTiming.AfterDamage,
                Effect = new FightingAllstar.Core.Content.EffectStepDefinition
                {
                    Id = "fighter.mai95.skill.1.ignite",
                    Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
                    Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy,
                    StatusRecipe = ignite,
                    StatusDurationOverride = rank.rank,
                    StatusStackCount = rank.rank
                }
            });
        }
        return effect;
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildMaiUltimateEffect(int tier)
    {
        var effect = new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = "fighter.mai95.ultimate.c" + tier,
            Kind = FightingAllstar.Core.Content.EffectKind.Damage,
            Family = FightingAllstar.Core.Content.DamageFamily.Normal,
            Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
            CoefficientBp = 37500 + tier * 1875,
            KeywordFactorBp = 10000,
            Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy,
            Provenance = tier == 0
                ? FightingAllstar.Core.Content.SourceProvenance.Supplied
                : FightingAllstar.Core.Content.SourceProvenance.GeneratedStartingValue
        };
        effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
        {
            Timing = FightingAllstar.Core.Content.CardEffectTiming.AfterAction,
            Effect = new FightingAllstar.Core.Content.EffectStepDefinition
            {
                Id = "fighter.mai95.ultimate.increase-own-card-rank",
                Kind = FightingAllstar.Core.Content.EffectKind.ModifyCardRank,
                Target = FightingAllstar.Core.Content.EffectTargetScope.Self,
                Magnitude = 1
            }
        });
        return effect;
    }

    private static FightingAllstar.Core.Content.PassiveAuraDefinition CreateAura(string id,
        FightingAllstar.Core.Content.PassiveRelation relation)
    {
        return new FightingAllstar.Core.Content.PassiveAuraDefinition
        {
            Id = id,
            Gate = new FightingAllstar.Core.Content.PassiveGate
            {
                Modes = FightingAllstar.Core.Content.BattleModeMask.All,
                Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
            },
            Targets = new FightingAllstar.Core.Content.PassiveTargetFilter
            {
                Relation = relation,
                IncludeOwner = true,
                IncludeReserve = true
            },
            Scaling = FightingAllstar.Core.Content.PassiveScaling.Constant,
            MaximumUnits = 1
        };
    }

    private static void AddAttackRelatedModifiers(FightingAllstar.Core.Content.PassiveAuraDefinition aura, int amount)
    {
        AddModifier(aura, FightingAllstar.Core.Content.StatId.Attack, amount,
            FightingAllstar.Core.Content.ModifierOperation.PercentOfBase);
        AddModifier(aura, FightingAllstar.Core.Content.StatId.Pierce, amount,
            FightingAllstar.Core.Content.ModifierOperation.PercentagePoints);
        AddModifier(aura, FightingAllstar.Core.Content.StatId.CritChance, amount,
            FightingAllstar.Core.Content.ModifierOperation.PercentagePoints);
        AddModifier(aura, FightingAllstar.Core.Content.StatId.CritDamage, amount,
            FightingAllstar.Core.Content.ModifierOperation.PercentagePoints);
    }

    private static void AddModifier(FightingAllstar.Core.Content.PassiveAuraDefinition aura,
        FightingAllstar.Core.Content.StatId stat, int amount, FightingAllstar.Core.Content.ModifierOperation operation)
    {
        aura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = stat,
            Operation = operation,
            Amount = amount
        });
    }

    private static SkillType ParseSkillType(string value) => Enum.TryParse(value, true, out SkillType result) ? result : SkillType.Attack;
    private static string Slug(string value) => System.Text.RegularExpressions.Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static string ArtNumber(string id) => id switch
    {
        "goro94" => "3", "mai95" => "5", "yuri94" => "13", "ryo94" => "12", "choi94" => "19", "chang94" => "19",
        "kyo98" => "6", "benimaru99" => "7", "andy94" => "8", "joe94" => "9",
        _ => throw new InvalidOperationException("No art mapping for " + id)
    };
    private static string CutNumber(string id) => id == "chang94" ? "20" : ArtNumber(id);

    private static Sprite LoadCharacterSprite(string definitionId, bool portrait)
    {
        var id = definitionId.Replace("fighter.", string.Empty);
        var folder = portrait ? "Cut" : "Icon";
        var number = portrait ? CutNumber(id) : ArtNumber(id);
        var path = "Assets/Project/Art/Character/" + folder + "/" + folder + "_" + number + "_" + ArtName(id) + ".png";
        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
    private static string ArtName(string id) => id switch
    {
        "goro94" => "Goro94", "mai95" => "Mai95", "yuri94" => "Yuri94", "ryo94" => "Ryo94", "choi94" => "Choi94", "chang94" => "Chang94",
        "kyo98" => "Kyo98", "benimaru99" => "Benimaru99", "andy94" => "Andy94", "joe94" => "Joe94",
        _ => throw new InvalidOperationException("No art mapping for " + id)
    };

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
    private static void EnsureFolder(string path)
    {
        var current = string.Empty;
        foreach (var part in path.Split('/'))
        {
            current = string.IsNullOrEmpty(current) ? part : current + "/" + part;
            if (current == "Assets") continue;
            var parent = current.Substring(0, current.LastIndexOf('/'));
            if (!AssetDatabase.IsValidFolder(current)) AssetDatabase.CreateFolder(parent, part);
        }
    }
    private static void Set(SerializedObject obj, string field, string value) => Set(obj.FindProperty(field), value);
    private static void Set(SerializedObject obj, string field, int value) => Set(obj.FindProperty(field), value);
    private static void Set(SerializedObject obj, string field, float value) => Set(obj.FindProperty(field), value);
    private static void Set(SerializedObject obj, string field, bool value) => Set(obj.FindProperty(field), value);
    private static void Set(SerializedObject obj, string field, Enum value) { var p = obj.FindProperty(field); if (p != null) p.enumValueIndex = Convert.ToInt32(value); }
    private static void Set(SerializedObject obj, string field, UnityEngine.Object value) { var p = obj.FindProperty(field); if (p != null) p.objectReferenceValue = value; }
    private static void Set(SerializedObject obj, string field, string[] values)
    {
        var p = obj.FindProperty(field); if (p == null) return;
        p.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).stringValue = values[i];
    }
    private static void Set(SerializedProperty property, string value) { if (property != null) property.stringValue = value ?? string.Empty; }
    private static void Set(SerializedProperty property, int value) { if (property != null) property.intValue = value; }
    private static void Set(SerializedProperty property, float value) { if (property != null) property.floatValue = value; }
    private static void Set(SerializedProperty property, bool value) { if (property != null) property.boolValue = value; }
    private static void Set(SerializedProperty parent, string field, float value) { var p = parent?.FindPropertyRelative(field); if (p != null) p.floatValue = value; }

    [Serializable] private sealed class DraftRoot { public DraftCharacter[] characters; }
    [Serializable] private sealed class DraftCharacter
    {
        public string definitionId, name, familyId, rarity, attribute, role;
        public int sourceId, sourceRecord;
        public string[] traits;
        public DraftStats stats;
        public DraftPassive passive;
        public DraftCard[] cards;
        public DraftTier[] constellations;
    }
    [Serializable] private sealed class DraftStats
    {
        public DraftStat Class_Combat, Attack, Defense, Health, Pierce_Rate, Resistance, Regeneration, Critical_Chance,
            Critical_Damage, Critical_Resistance, Critical_Defense, Recovery_Rate, Block_Chance, Block_Power,
            Lifesteal, Avoidance_Rate, Evade_Rate, Control_Rate, Perception_Rate;
    }
    [Serializable] private sealed class DraftStat { public float value; }
    [Serializable] private sealed class DraftPassive { public string sourceType, description, restriction; }
    [Serializable] private sealed class DraftCard { public int slot; public string sourceType; public DraftRank[] ranks; }
    [Serializable] private sealed class DraftRank { public int rank; public string description, provenance; }
    [Serializable] private sealed class DraftTier { public int tier; public string description; }
}
