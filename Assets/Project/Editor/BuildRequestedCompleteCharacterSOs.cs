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

    private static readonly Dictionary<string, string> RequestedCharacterNames = new Dictionary<string, string>
    {
        ["fighter.clark94"] = "clark94",
        ["fighter.ralf94"] = "ralf94",
        ["fighter.joe96"] = "joe96"
    };

    private static readonly Dictionary<string, string> AdditionalRequestedNames = new Dictionary<string, string>
    {
        ["fighter.brian94"] = "brian94",
        ["fighter.heavyd94"] = "heavyd94",
        ["fighter.lucky94"] = "lucky94"
    };

    [MenuItem("Fighting Allstar/Content/Import Brian94, HeavyD94, and Lucky94")]
    public static void ImportRequestedAmericanTeam()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        EnsureFolder(CardPath);
        EnsureFolder(PassivePath);

        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        if (root?.characters == null) throw new InvalidOperationException("Could not read the WIP character draft source.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Shared character prefab is missing: " + ModelPath);
        foreach (var pair in AdditionalRequestedNames)
        {
            var draft = root.characters.FirstOrDefault(item => item != null && item.definitionId == pair.Key);
            if (draft == null) throw new InvalidOperationException("Draft character not found: " + pair.Key);
            if (draft.cards == null || draft.cards.Length != 2 ||
                draft.cards.Any(card => card == null || card.ranks == null || card.ranks.Length != 3))
                throw new InvalidOperationException(pair.Key + " must have both complete 3-rank skills before import.");
            if (draft.constellations == null || draft.constellations.Length != 6)
                throw new InvalidOperationException(pair.Key + " must have all six C0-C5 Ultimate tiers before import.");
            BuildCharacter(draft, pair.Value, model);
        }
        BuildRegistry();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported Brian94 (R), Heavy D! 94 (SR), and Lucky94 (R) as reviewable CharacterObject asset sets.");
    }

    [MenuItem("Fighting Allstar/Content/Import SR Brian94 Barrier Kit")]
    public static void ImportBrian94SR()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        var draft = root?.characters?.FirstOrDefault(item => item != null && item.definitionId == "fighter.brian94");
        if (draft == null || draft.cards == null || draft.cards.Length != 2 || draft.constellations == null || draft.constellations.Length != 6)
            throw new InvalidOperationException("Brian94 draft data must include two skills and all six C0-C5 Ultimate tiers.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Shared character prefab is missing: " + ModelPath);

        BuildCharacter(draft, "brian94", model);
        ConfigureBrian94Kit();
        BuildRegistry();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported SR blue Brian94 with a 40% Block Chance passive, Rupture, Attack-scaled Barrier, and C0-C5 Detonate Ultimate.");
    }

    private static void ConfigureBrian94Kit()
    {
        const string characterPath = "Assets/Project/Data/Character/brian94.asset";
        const string passivePath = "Assets/Project/Data/Character/brian94_Passive.asset";
        const string skill1Path = "Assets/Project/Data/Character/brian94_skilldata1.asset";
        const string skill2Path = "Assets/Project/Data/Character/brian94_skilldata2.asset";
        const string ultimatePath = "Assets/Project/Data/Character/brian94_ultdata1.asset";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(passivePath);
        var skill1 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill1Path);
        var skill2 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill2Path);
        var ultimate = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(ultimatePath);
        if (character == null || passiveAsset == null || skill1 == null || skill2 == null || ultimate == null)
            throw new InvalidOperationException("Brian94 character, passive, skill, or Ultimate asset is missing.");

        var passive = new FightingAllstar.Core.Content.PassiveDefinition { Id = "fighter.brian94.passive.source" };
        var blockAura = CreateAura("team-block-chance", FightingAllstar.Core.Content.PassiveRelation.Allies);
        blockAura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = FightingAllstar.Core.Content.StatId.BlockChance,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = 4000
        });
        passive.Auras.Add(blockAura);
        passiveAsset.SetDefinition(passive);
        EditorUtility.SetDirty(passiveAsset);

        for (var i = 0; i < skill1.ranks.Count; i++)
        {
            var rank = i + 1;
            var coefficient = rank == 1 ? 13333 : rank == 2 ? 26667 : 40000;
            skill1.ranks[i].description = $"Inflicts Rupture damage equal to {coefficient / 100f:0.##}% of ATK.";
            skill1.ranks[i].skillType = SkillType.Attack;
            skill1.ranks[i].runtimeEffect = new FightingAllstar.Core.Content.EffectDefinition
            {
                Id = $"fighter.brian94.skill.1.rank.{rank}",
                Kind = FightingAllstar.Core.Content.EffectKind.Damage,
                Family = FightingAllstar.Core.Content.DamageFamily.Normal,
                Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
                CoefficientBp = coefficient,
                KeywordFactorBp = 10000,
                KeywordId = "Rupture",
                Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
            };
        }
        EditorUtility.SetDirty(skill1);

        var barrierVisual = LoadOrCreate<FightingAllstar.Presentation.Combat.StatusVisualData>(
            "Assets/Project/Data/StatusData/BrianBarrierVisual.asset");
        var visualSerialized = new SerializedObject(barrierVisual);
        Set(visualSerialized, "id", "status.buff.brian94.barrier");
        Set(visualSerialized, "displayName", "Barrier");
        Set(visualSerialized, "icon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/shield.png"));
        Set(visualSerialized, "polarity", FightingAllstar.Core.Content.StatusPolarity.Buff);
        Set(visualSerialized, "keywords", new[] { "Barrier", "Shield", "status.buff.brian94.barrier" });
        Set(visualSerialized, "description", "Absorbs incoming damage.");
        visualSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(barrierVisual);
        skill2.statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData> { barrierVisual };
        for (var i = 0; i < skill2.ranks.Count; i++)
        {
            var rank = i + 1;
            var coefficient = rank * 12500;
            var duration = rank == 1 ? 1 : 2;
            var recipe = new FightingAllstar.Core.Content.StatusRecipeDefinition
            {
                Id = "status.buff.brian94.barrier",
                NameKey = "Barrier",
                Polarity = FightingAllstar.Core.Content.StatusPolarity.Buff,
                Behavior = FightingAllstar.Core.Content.StatusBehavior.Barrier,
                Stacking = FightingAllstar.Core.Content.StatusStackingPolicy.RefreshStronger,
                DurationClock = FightingAllstar.Core.Content.StatusDurationClock.TargetTurnEnd,
                DefaultDuration = duration,
                MaxStacks = 1,
                BarrierCoefficientBp = coefficient,
                Tags = new List<string> { "Barrier", "Shield" }
            };
            skill2.ranks[i].description = $"Grants all allies a barrier equal to {coefficient / 100f:0.##}% of ATK for {duration} turn{(duration == 1 ? "" : "s")}.";
            skill2.ranks[i].skillType = SkillType.Buff;
            skill2.ranks[i].runtimeEffect = new FightingAllstar.Core.Content.EffectDefinition
            {
                Id = $"fighter.brian94.skill.2.rank.{rank}",
                Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
                Target = FightingAllstar.Core.Content.EffectTargetScope.AllAllies,
                StatusRecipe = recipe,
                StatusDurationOverride = duration
            };
        }
        EditorUtility.SetDirty(skill2);

        ultimate.levels = ultimate.levels.OrderBy(level => level.level).ToList();
        for (var tier = 0; tier <= 5 && tier < ultimate.levels.Count; tier++)
        {
            var coefficient = 65000 + tier * 3250;
            var level = ultimate.levels[tier];
            level.description = $"Inflicts Detonate damage equal to {coefficient / 100f:0.##}% of ATK.";
            level.skillType = SkillType.Attack;
            level.runtimeEffect = new FightingAllstar.Core.Content.EffectDefinition
            {
                Id = $"fighter.brian94.ultimate.c{tier}",
                Kind = FightingAllstar.Core.Content.EffectKind.Damage,
                Family = FightingAllstar.Core.Content.DamageFamily.Normal,
                Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
                CoefficientBp = coefficient,
                KeywordFactorBp = 10000,
                KeywordId = "Detonate",
                Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
            };
        }
        EditorUtility.SetDirty(ultimate);

        var characterSerialized = new SerializedObject(character);
        Set(characterSerialized, "runtimeReady", true);
        Set(characterSerialized, "FighterRarity", FighterRarity.SR);
        Set(characterSerialized, "FighterAttribute", FighterAttribute.Blue);
        Set(characterSerialized, "passiveSourceDescription", "Increases allies' Block Chance by 40%.");
        characterSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    [MenuItem("Fighting Allstar/Content/Import Iori95")]
    public static void ImportIori95()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        var source = root?.characters?.FirstOrDefault(item => item != null && item.definitionId == "fighter.iori95");
        if (source == null) throw new InvalidOperationException("Iori95 draft data is missing.");
        if (source.cards == null || source.cards.Length != 2 ||
            source.cards.Any(card => card == null || card.ranks == null || card.ranks.Length != 3) ||
            source.constellations == null || source.constellations.Length != 6)
            throw new InvalidOperationException("Iori95 must have both three-rank skills and all six C0-C5 tiers before cloning.");
        if (AssetDatabase.LoadAssetAtPath<CharacterObject>(CharacterPath + "/iori95.asset") != null)
            throw new InvalidOperationException("Iori95 CharacterObject already exists; refusing to overwrite it.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Shared character prefab is missing: " + ModelPath);
        BuildCharacter(source, "iori95", model);
        ConfigureIori95Kit();
        RemoveGeneratedIori96Placeholder();
        BuildRegistry();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported SSR blue Yagami Iori 95 with the supplied runtime kit.");
    }

    [MenuItem("Fighting Allstar/Content/Import Terry96")]
    public static void ImportTerry96()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        var source = root?.characters?.FirstOrDefault(item => item != null && item.definitionId == "fighter.terry96");
        if (source == null) throw new InvalidOperationException("Terry96 draft data is missing.");
        if (source.cards == null || source.cards.Length != 2 ||
            source.cards.Any(card => card == null || card.ranks == null || card.ranks.Length != 3) ||
            source.constellations == null || source.constellations.Length != 6)
            throw new InvalidOperationException("Terry96 must have both three-rank skills and all six C0-C5 tiers before import.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Shared character prefab is missing: " + ModelPath);

        BuildCharacter(source, "terry96", model);
        ConfigureTerry96Kit();
        BuildRegistry();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported SSR blue Terry Bogard 96 with the supplied passive, skills, and Charge Ultimate.");
    }

    private static void ConfigureTerry96Kit()
    {
        const string characterPath = "Assets/Project/Data/Character/terry96.asset";
        const string passivePath = "Assets/Project/Data/Character/terry96_Passive.asset";
        const string skill1Path = "Assets/Project/Data/Character/terry96_skilldata1.asset";
        const string skill2Path = "Assets/Project/Data/Character/terry96_skilldata2.asset";
        const string ultimatePath = "Assets/Project/Data/Character/terry96_ultdata1.asset";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(passivePath);
        var skill1 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill1Path);
        var skill2 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill2Path);
        var ultimate = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(ultimatePath);
        if (character == null || passiveAsset == null || skill1 == null || skill2 == null || ultimate == null)
            throw new InvalidOperationException("Terry96 CharacterObject, passive, skills, or Ultimate asset is missing.");

        var passive = new FightingAllstar.Core.Content.PassiveDefinition { Id = "fighter.terry96.passive.source" };
        var gate = IoriGate();
        var aura = new FightingAllstar.Core.Content.PassiveAuraDefinition
        {
            Id = "enemy-attack-pierce-down-per-turn",
            Gate = gate,
            Targets = new FightingAllstar.Core.Content.PassiveTargetFilter
            {
                Relation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
                IncludeOwner = false,
                IncludeReserve = true
            },
            Scaling = FightingAllstar.Core.Content.PassiveScaling.OwnerCounter,
            ScalingKey = "terry96.turn-stacks",
            MaximumUnits = 5
        };
        aura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = FightingAllstar.Core.Content.StatId.Attack,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentOfBase,
            Amount = -600
        });
        aura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = FightingAllstar.Core.Content.StatId.Pierce,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = -600
        });
        passive.Auras.Add(aura);
        passive.Reactions.Add(new FightingAllstar.Core.Content.PassiveReactionDefinition
        {
            Id = "increase-enemy-weakening-at-turn-start",
            Gate = gate,
            Trigger = FightingAllstar.Core.Content.PassiveEventKind.TeamTurnStarted,
            OwnTeamTurnOnly = true,
            ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Any,
            TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Any,
            Commands =
            {
                new FightingAllstar.Core.Content.PassiveCommandDefinition
                {
                    Kind = FightingAllstar.Core.Content.PassiveCommandKind.IncrementCounter,
                    CounterKey = "terry96.turn-stacks",
                    CounterCap = 5,
                    Amount = 1
                }
            }
        });
        passiveAsset.SetDefinition(passive);
        EditorUtility.SetDirty(passiveAsset);

        for (var rank = 1; rank <= 3; rank++)
        {
            var skill = skill1.ranks[rank - 1];
            var damageBp = rank * 12000;
            var critReductionBp = rank == 1 ? 2667 : rank == 2 ? 5333 : 8000;
            var duration = rank == 1 ? 1 : 2;
            skill.description = $"Inflicts {damageBp / 100f:0.##}% of ATK and reduces the target's Critical Chance by {critReductionBp / 100f:0.##}% for {duration} {(duration == 1 ? "turn" : "turns")}.";
            skill.skillType = SkillType.Attack;
            skill.runtimeEffect = BuildTerry96Skill1Effect(rank, damageBp, critReductionBp, duration);
        }
        EditorUtility.SetDirty(skill1);

        for (var rank = 1; rank <= 3; rank++)
        {
            var skill = skill2.ranks[rank - 1];
            var reductionBp = rank == 1 ? 1333 : rank == 2 ? 2667 : 4000;
            skill.description = $"Reduces all enemies' defense-related stats by {reductionBp / 100f:0.##}% for {rank} {(rank == 1 ? "turn" : "turns")}.";
            skill.skillType = SkillType.Debuff;
            skill.runtimeEffect = BuildTerry96Skill2Effect(rank, reductionBp);
        }
        EditorUtility.SetDirty(skill2);

        ultimate.levels = ultimate.levels.OrderBy(level => level.level).ToList();
        for (var tier = 0; tier <= 5; tier++)
        {
            var level = ultimate.levels[tier];
            var coefficient = 45000 + tier * 2250;
            level.description = $"Inflicts Charge damage equal to {coefficient / 100f:0.##}% of ATK.";
            level.skillType = SkillType.Attack;
            level.runtimeEffect = new FightingAllstar.Core.Content.EffectDefinition
            {
                Id = $"fighter.terry96.ultimate.c{tier}",
                Kind = FightingAllstar.Core.Content.EffectKind.Damage,
                Family = FightingAllstar.Core.Content.DamageFamily.Normal,
                Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
                CoefficientBp = coefficient,
                KeywordFactorBp = 10000,
                KeywordId = "Charge",
                Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
            };
        }
        EditorUtility.SetDirty(ultimate);

        var serialized = new SerializedObject(character);
        Set(serialized, "runtimeReady", true);
        Set(serialized, "FighterRarity", FighterRarity.SSR);
        Set(serialized, "FighterAttribute", FighterAttribute.Blue);
        Set(serialized, "passiveSourceDescription", "Reduces all enemies' Attack and Pierce Rate by 6% at the start of own turn, up to 30%.");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildTerry96Skill1Effect(int rank,
        int damageBp, int critReductionBp, int duration)
    {
        var effect = new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = $"fighter.terry96.skill.1.rank.{rank}",
            Kind = FightingAllstar.Core.Content.EffectKind.Damage,
            Family = FightingAllstar.Core.Content.DamageFamily.Normal,
            Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
            CoefficientBp = damageBp,
            KeywordFactorBp = 10000,
            Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
        };
        var recipe = new FightingAllstar.Core.Content.StatusRecipeDefinition
        {
            Id = "status.debuff.terry96.critical-chance-down",
            NameKey = "status.debuff.terry96.critical-chance-down",
            Polarity = FightingAllstar.Core.Content.StatusPolarity.Debuff,
            Behavior = FightingAllstar.Core.Content.StatusBehavior.Stat,
            DurationClock = FightingAllstar.Core.Content.StatusDurationClock.TargetTurnEnd,
            DefaultDuration = duration,
            MaxStacks = 1
        };
        recipe.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = FightingAllstar.Core.Content.StatId.CritChance,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = -critReductionBp
        });
        effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
        {
            Timing = FightingAllstar.Core.Content.CardEffectTiming.AfterDamage,
            Effect = new FightingAllstar.Core.Content.EffectStepDefinition
            {
                Id = $"fighter.terry96.skill.1.critical-down.rank.{rank}",
                Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
                Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy,
                StatusRecipe = recipe,
                StatusDurationOverride = duration
            }
        });
        return effect;
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildTerry96Skill2Effect(int rank, int reductionBp)
    {
        var recipe = new FightingAllstar.Core.Content.StatusRecipeDefinition
        {
            Id = "status.debuff.terry96.defense-related-down",
            NameKey = "status.debuff.terry96.defense-related-down",
            Polarity = FightingAllstar.Core.Content.StatusPolarity.Debuff,
            Behavior = FightingAllstar.Core.Content.StatusBehavior.Stat,
            DurationClock = FightingAllstar.Core.Content.StatusDurationClock.TargetTurnEnd,
            DefaultDuration = rank,
            MaxStacks = 1
        };
        recipe.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.StatBundle,
            Bundle = FightingAllstar.Core.Content.StatBundleKind.DefenseRelated,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = -reductionBp
        });
        return new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = $"fighter.terry96.skill.2.rank.{rank}",
            Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
            Target = FightingAllstar.Core.Content.EffectTargetScope.AllEnemies,
            StatusRecipe = recipe,
            StatusDurationOverride = rank
        };
    }

    private static void RemoveGeneratedIori96Placeholder()
    {
        var oldCharacterPath = CharacterPath + "/iori96.asset";
        var oldCharacter = AssetDatabase.LoadAssetAtPath<CharacterObject>(oldCharacterPath);
        if (oldCharacter == null || oldCharacter.DefinitionId != "fighter.iori96" || oldCharacter.SourceId != 30)
            return;
        foreach (var path in new[]
        {
            oldCharacterPath,
            CharacterPath + "/iori96_Passive.asset",
            CharacterPath + "/iori96_skilldata1.asset",
            CharacterPath + "/iori96_skilldata2.asset",
            CharacterPath + "/iori96_ultdata1.asset",
            "Assets/Project/Art/Character/3D-Material/iori96.mat"
        })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
    }

    [MenuItem("Fighting Allstar/Content/Import Clark94, Ralf94, and Joe96")]
    public static void ImportRequestedCharacters()
    {
        if (!File.Exists(SourcePath)) throw new FileNotFoundException("Character draft source is missing.", SourcePath);
        EnsureFolder(CharacterPath);
        EnsureFolder(CardPath);
        EnsureFolder(PassivePath);

        var root = JsonUtility.FromJson<DraftRoot>(File.ReadAllText(SourcePath));
        if (root?.characters == null) throw new InvalidOperationException("Could not read the WIP character draft source.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) throw new InvalidOperationException("Shared character prefab is missing: " + ModelPath);
        foreach (var pair in RequestedCharacterNames)
        {
            var draft = root.characters.FirstOrDefault(item => item != null && item.definitionId == pair.Key);
            if (draft == null) throw new InvalidOperationException("Draft character not found: " + pair.Key);
            if (draft.cards == null || draft.cards.Length != 2 ||
                draft.cards.Any(card => card == null || card.ranks == null || card.ranks.Length != 3))
                throw new InvalidOperationException(pair.Key + " must have both complete 3-rank skills before import.");
            if (draft.constellations == null || draft.constellations.Length != 6)
                throw new InvalidOperationException(pair.Key + " must have all six C0-C5 Ultimate tiers before import.");
            BuildCharacter(draft, pair.Value, model);
        }
        BuildRegistry();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Imported Clark94 (SR), Ralf94 (R), and Joe96 (R) as reviewable CharacterObject asset sets.");
    }

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
        if (draft.constellations == null || draft.constellations.Length != 6)
            throw new InvalidOperationException("Mai95 must have all six C0-C5 Ultimate tiers before import.");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        BuildCharacter(draft, "mai95", model);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built Mai95 character, passive, two runtime skills, and six Ultimate tiers. Other character assets were not touched.");
    }

    [MenuItem("Fighting Allstar/Content/Configure Leona96 Passive and Ultimate")]
    public static void ConfigureLeona96PassiveAndUltimate()
    {
        const string passivePath = "Assets/Project/Data/Character/leona96_Passive.asset";
        const string characterPath = "Assets/Project/Data/Character/leona96.asset";
        const string ultimatePath = "Assets/Project/Data/Character/leona96_ultdata1.asset";
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(passivePath);
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
        var ultimate = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(ultimatePath);
        if (passiveAsset == null || character == null || ultimate == null)
            throw new InvalidOperationException("Leona96 passive, character, or Ultimate asset is missing.");

        var passive = passiveAsset.CreateDefinition() ?? new FightingAllstar.Core.Content.PassiveDefinition();
        passive.Id = "fighter.leona96.passive.source";
        passive.Triggers.Clear();
        passive.Auras.Clear();
        passive.Reactions.Clear();
        var lifestealAura = new FightingAllstar.Core.Content.PassiveAuraDefinition
        {
            Id = "lifesteal",
            Gate = new FightingAllstar.Core.Content.PassiveGate
            {
                MinimumTier = 0,
                MaximumTier = 5,
                Modes = FightingAllstar.Core.Content.BattleModeMask.All,
                Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
            },
            Targets = new FightingAllstar.Core.Content.PassiveTargetFilter
            {
                Relation = FightingAllstar.Core.Content.PassiveRelation.Self,
                IncludeOwner = true,
                IncludeReserve = true
            },
            Scaling = FightingAllstar.Core.Content.PassiveScaling.Constant,
            MaximumUnits = 1
        };
        lifestealAura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
            Stat = FightingAllstar.Core.Content.StatId.LifeSteal,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = 2500
        });
        passive.Auras.Add(lifestealAura);

        var lowHealthDamage = LeonaDamageReaction("low-health-damage-bonus");
        lowHealthDamage.Conditions.Add(new FightingAllstar.Core.Content.EffectConditionDefinition
        {
            Kind = FightingAllstar.Core.Content.EffectConditionKind.TargetHealthAtMost,
            Threshold = 3499,
            Subject = FightingAllstar.Core.Content.ConditionSubject.OperationTarget
        });
        passive.Reactions.Add(lowHealthDamage);

        var lowestHealthUltimate = LeonaDamageReaction("lowest-health-ultimate-bonus");
        lowestHealthUltimate.CardOriginOnly = true;
        lowestHealthUltimate.FilterCategory = true;
        lowestHealthUltimate.Category = FightingAllstar.Core.Content.CardCategory.Ultimate;
        lowestHealthUltimate.Conditions.Add(new FightingAllstar.Core.Content.EffectConditionDefinition
        {
            Kind = FightingAllstar.Core.Content.EffectConditionKind.TargetIsLowestHealthEnemy,
            Subject = FightingAllstar.Core.Content.ConditionSubject.OperationTarget
        });
        passive.Reactions.Add(lowestHealthUltimate);

        var killHeal = new FightingAllstar.Core.Content.PassiveReactionDefinition
        {
            Id = "ultimate-kill-heal",
            Gate = new FightingAllstar.Core.Content.PassiveGate
            {
                MinimumTier = 0,
                MaximumTier = 5,
                Modes = FightingAllstar.Core.Content.BattleModeMask.All,
                Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
            },
            Trigger = FightingAllstar.Core.Content.PassiveEventKind.FighterDefeated,
            ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Self,
            TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
            CardOriginOnly = true,
            FilterCategory = true,
            Category = FightingAllstar.Core.Content.CardCategory.Ultimate
        };
        killHeal.Commands.Add(new FightingAllstar.Core.Content.PassiveCommandDefinition
        {
            Kind = FightingAllstar.Core.Content.PassiveCommandKind.ExecuteEffect,
            Amount = 1,
            Effect = new FightingAllstar.Core.Content.CardEffectOperationDefinition
            {
                Id = "leona96.ultimate.kill-heal",
                Window = FightingAllstar.Core.Content.CardEffectWindow.Damaging,
                Kind = FightingAllstar.Core.Content.CardEffectOperationKind.Heal,
                Target = FightingAllstar.Core.Content.EffectTargetScope.Self,
                Value = new FightingAllstar.Core.Content.EffectValueDefinition
                {
                    Source = FightingAllstar.Core.Content.EffectValueSource.TargetMaxHealth,
                    CoefficientBp = 800,
                    MaximumAmount = int.MaxValue
                }
            }
        });
        passive.Reactions.Add(killHeal);
        passiveAsset.SetDefinition(passive);
        EditorUtility.SetDirty(passiveAsset);

        var serializedCharacter = new SerializedObject(character);
        serializedCharacter.FindProperty("passiveSourceDescription").stringValue =
            "Increases own LifeSteal by 25%. Deals 40% more damage to enemies below 35% HP.";
        serializedCharacter.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);

        ultimate.ultimateName = "Leona Heidern 96 · Ultimate";
        if (ultimate.levels == null || ultimate.levels.Count != 6)
            throw new InvalidOperationException("Leona96 Ultimate must have six C0-C5 tiers before updating.");
        var ultCoefficientsBp = new[] { 50000, 52500, 55000, 57500, 60000, 62500 };
        foreach (var level in ultimate.levels)
        {
            if (level == null) continue;
            if (level.level < 0 || level.level >= ultCoefficientsBp.Length)
                throw new InvalidOperationException("Leona96 Ultimate contains an invalid C0-C5 tier: " + level.level);
            var coefficientBp = ultCoefficientsBp[level.level];
            level.description = "Inflict " + (coefficientBp / 100) + "% of ATK to one enemy. Deal 40% more damage if the target has the lowest current HP. On kill, restore 8% of own max HP.";
            var effect = level.runtimeEffect ?? new FightingAllstar.Core.Content.EffectDefinition();
            level.runtimeEffect = effect;
            effect.Id = "fighter.leona96.ultimate.c" + level.level;
            effect.Kind = FightingAllstar.Core.Content.EffectKind.Damage;
            effect.Family = FightingAllstar.Core.Content.DamageFamily.Normal;
            effect.Scaling = FightingAllstar.Core.Content.StatScaling.Attack;
            effect.ScalingStat = FightingAllstar.Core.Content.StatId.Attack;
            effect.Magnitude = 0;
            effect.CoefficientBp = coefficientBp;
            effect.KeywordFactorBp = 10000;
            effect.KeywordId = string.Empty;
            effect.Conditions.Clear();
            effect.StatusRecipe = null;
            effect.StatusDurationOverride = 0;
            effect.StatusStackCount = 1;
            effect.StatusPotencyBp = 0;
            effect.StatusApplyChanceBp = 10000;
            effect.HealValue = null;
            effect.HealCoefficientBp = 0;
            effect.PowerGaugeAmount = 0;
            effect.Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy;
            effect.Attack = new FightingAllstar.Core.Content.DamageAttackDefinition { HitCount = 1 };
            effect.Sequence.Clear();
        }
        EditorUtility.SetDirty(ultimate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Updated Leona96 LifeSteal, conditional damage, single-target Ultimate, lowest-HP bonus, and kill heal.");
    }

    [MenuItem("Fighting Allstar/Content/Configure Iori95 Kit")]
    public static void ConfigureIori95Kit()
    {
        const string characterPath = "Assets/Project/Data/Character/iori95.asset";
        const string passivePath = "Assets/Project/Data/Character/iori95_Passive.asset";
        const string skill1Path = "Assets/Project/Data/Character/iori95_skilldata1.asset";
        const string skill2Path = "Assets/Project/Data/Character/iori95_skilldata2.asset";
        const string ultimatePath = "Assets/Project/Data/Character/iori95_ultdata1.asset";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(passivePath);
        var skill1 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill1Path);
        var skill2 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(skill2Path);
        var ultimate = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(ultimatePath);
        if (character == null || passiveAsset == null || skill1 == null || skill2 == null || ultimate == null)
            throw new InvalidOperationException("Iori95 character, passive, skills, or Ultimate asset is missing.");

        var passive = new FightingAllstar.Core.Content.PassiveDefinition { Id = "fighter.iori95.passive.source" };
        var burnAura = new FightingAllstar.Core.Content.PassiveAuraDefinition
        {
            Id = "burn-damage-per-stack",
            Gate = IoriGate(),
            Targets = new FightingAllstar.Core.Content.PassiveTargetFilter
            {
                Relation = FightingAllstar.Core.Content.PassiveRelation.Self,
                IncludeOwner = true,
                IncludeReserve = true
            },
            Scaling = FightingAllstar.Core.Content.PassiveScaling.FieldStatusStacks,
            ScalingKey = FightingAllstar.Core.Content.CombatTags.Ignite,
            ScalingRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies
        };
        burnAura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.AnyDamageDealt,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = 1400
        });
        passive.Auras.Add(burnAura);

        passive.Reactions.Add(new FightingAllstar.Core.Content.PassiveReactionDefinition
        {
            Id = "bonus-max-health-damage-vs-kyo-team",
            Gate = IoriGate(),
            Trigger = FightingAllstar.Core.Content.PassiveEventKind.DamageResolved,
            ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Self,
            TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
            CardOriginOnly = true,
            Conditions =
            {
                new FightingAllstar.Core.Content.EffectConditionDefinition
                {
                    Kind = FightingAllstar.Core.Content.EffectConditionKind.RosterCountAtLeast,
                    Threshold = 1,
                    RosterFilter = new FightingAllstar.Core.Content.PassiveTargetFilter
                    {
                        Relation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
                        IncludeOwner = true,
                        IncludeReserve = true,
                        TraitId = "trait.kyo"
                    }
                }
            },
            Commands =
            {
                new FightingAllstar.Core.Content.PassiveCommandDefinition
                {
                    Kind = FightingAllstar.Core.Content.PassiveCommandKind.ExecuteEffect,
                    Amount = 1,
                    Effect = new FightingAllstar.Core.Content.CardEffectOperationDefinition
                    {
                        Id = "iori95.bonus-max-health-damage",
                        Window = FightingAllstar.Core.Content.CardEffectWindow.Damaging,
                        Kind = FightingAllstar.Core.Content.CardEffectOperationKind.Damage,
                        Target = FightingAllstar.Core.Content.EffectTargetScope.TriggerTarget,
                        Damage = new FightingAllstar.Core.Content.DamageEffectRecipe
                        {
                            Family = FightingAllstar.Core.Content.DamageFamily.Additional,
                            Scaling = FightingAllstar.Core.Content.StatScaling.MaxHealth,
                            CoefficientBp = 500,
                            KeywordFactorBp = 10000,
                            CannotCrit = true,
                            CannotBlock = true,
                            ScaleFromTargetMaxHealth = true
                        }
                    }
                }
            }
        });
        passive.Reactions.Add(new FightingAllstar.Core.Content.PassiveReactionDefinition
        {
            Id = "ultimate-steal-stats",
            Gate = IoriGate(),
            Trigger = FightingAllstar.Core.Content.PassiveEventKind.DamageResolved,
            ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Self,
            TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies,
            CardOriginOnly = true,
            FilterCategory = true,
            Category = FightingAllstar.Core.Content.CardCategory.Ultimate,
            LimitScope = FightingAllstar.Core.Content.PassiveLimitScope.RootActionTarget,
            MaximumActivations = 1,
            Commands =
            {
                new FightingAllstar.Core.Content.PassiveCommandDefinition
                {
                    Kind = FightingAllstar.Core.Content.PassiveCommandKind.ExecuteEffect,
                    Amount = 1,
                    Effect = new FightingAllstar.Core.Content.CardEffectOperationDefinition
                    {
                        Id = "iori95.ultimate.steal-stats",
                        Window = FightingAllstar.Core.Content.CardEffectWindow.Damaging,
                        Kind = FightingAllstar.Core.Content.CardEffectOperationKind.TransferStats,
                        Target = FightingAllstar.Core.Content.EffectTargetScope.TriggerTarget,
                        StatTransfer = new FightingAllstar.Core.Content.StatTransferRecipeDefinition
                        {
                            CoefficientBp = 5000,
                            SourceStatus = BuildIoriTransferStatus("status.buff.iori95.extort",
                                FightingAllstar.Core.Content.StatusPolarity.Buff,
                                FightingAllstar.Core.Content.StatusDurationClock.TargetTurnStart),
                            TargetStatus = BuildIoriTransferStatus("status.debuff.iori95.extort",
                                FightingAllstar.Core.Content.StatusPolarity.Debuff,
                                FightingAllstar.Core.Content.StatusDurationClock.TargetTurnEnd)
                        }
                    }
                }
            }
        });
        passiveAsset.SetDefinition(passive);
        EditorUtility.SetDirty(passiveAsset);

        for (var rank = 1; rank <= 3; rank++)
        {
            var item = skill1.ranks[rank - 1];
            var duration = rank == 1 ? 1 : 2;
            var durationLabel = duration == 1 ? "turn" : "turns";
            item.description = $"Inflicts {rank * 500f / 3f:0.##}% of ATK and increases Lifesteal by {rank * 10}% for {duration} {durationLabel}.";
            item.runtimeEffect = BuildIori95SkillEffect(1, rank);
        }
        EditorUtility.SetDirty(skill1);

        for (var rank = 1; rank <= 3; rank++)
        {
            var item = skill2.ranks[rank - 1];
            var coefficient = rank == 1 ? 9333 : rank == 2 ? 18667 : 28000;
            var stacks = rank == 1 ? 1 : 2;
            var duration = rank;
            var stackLabel = stacks == 1 ? "stack" : "stacks";
            var durationLabel = duration == 1 ? "turn" : "turns";
            item.description = $"Inflicts {coefficient / 100f:0.##}% of ATK and inflicts {stacks} Ignite {stackLabel} for {duration} {durationLabel}.";
            item.runtimeEffect = BuildIori95SkillEffect(2, rank);
        }
        EditorUtility.SetDirty(skill2);

        ultimate.levels = ultimate.levels.OrderBy(level => level.level).ToList();
        for (var tier = 0; tier <= 5; tier++)
        {
            var item = ultimate.levels[tier];
            var coefficient = 40000 + tier * 2000;
            item.description = $"Inflicts {coefficient / 100f:0}% of ATK and steals 50% of the target's Attack and Defense, reducing their stats and increasing own stats by 50% for 2 turns.";
            item.runtimeEffect = BuildIori95UltimateEffect(tier);
        }
        foreach (var visualPath in new[]
        {
            "Assets/Project/Data/StatusData/Iori95ExtortBuff.asset",
            "Assets/Project/Data/StatusData/Iori95ExtortDebuff.asset"
        })
        {
            var visual = AssetDatabase.LoadAssetAtPath<FightingAllstar.Presentation.Combat.StatusVisualData>(visualPath);
            if (visual == null) continue;
            if (!ultimate.statusVisuals.Contains(visual)) ultimate.statusVisuals.Add(visual);
            if (!passiveAsset.statusVisuals.Contains(visual)) passiveAsset.statusVisuals.Add(visual);
        }
        EditorUtility.SetDirty(passiveAsset);
        EditorUtility.SetDirty(ultimate);

        var serialized = new SerializedObject(character);
        Set(serialized, "definitionId", "fighter.iori95");
        Set(serialized, "runtimeReady", true);
        Set(serialized, "sourceId", 6);
        Set(serialized, "sourceRecord", 7);
        Set(serialized, "id", 1006);
        Set(serialized, "fighterName", "Yagami Iori 95");
        Set(serialized, "fighterTag", "Iori");
        Set(serialized, "passiveSourceDescription",
            "Increases own damage dealt by 14% for each Ignite stack on enemies. When the enemy team has Kyo, deals bonus damage equal to 5% of the target's Max HP.");
        Set(serialized, "passiveDefinition", passiveAsset);
        Set(serialized, "traitIds", (character.TraitIds ?? Array.Empty<string>())
            .Where(trait => trait != "trait.iori" && trait != "trait.kyo-counter").ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);

        AddKyoTrait("Assets/Project/Data/Character/kyo94.asset");
        AddKyoTrait("Assets/Project/Data/Character/kyo98.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        BuildRegistry();
        Debug.Log("Configured Iori95's burn scaling, anti-Kyo Max HP damage, three-rank skills, and C0-C5 Ultimate runtime effects.");
    }

    [MenuItem("Fighting Allstar/Content/Configure Ralf94 Ultimate Damage Passive")]
    public static void ConfigureRalf94UltimateDamagePassive()
    {
        const string characterPath = "Assets/Project/Data/Character/ralf94.asset";
        const string passivePath = "Assets/Project/Data/Character/ralf94_Passive.asset";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(passivePath);
        if (character == null || passiveAsset == null)
            throw new InvalidOperationException("Ralf94 CharacterObject or PassiveDefinitionSO is missing.");

        var passive = new FightingAllstar.Core.Content.PassiveDefinition
        {
            Id = "fighter.ralf94.passive.source"
        };
        var aura = new FightingAllstar.Core.Content.PassiveAuraDefinition
        {
            Id = "ult-damage-aura",
            Gate = new FightingAllstar.Core.Content.PassiveGate
            {
                MinimumTier = 0,
                MaximumTier = 5,
                Modes = FightingAllstar.Core.Content.BattleModeMask.All,
                Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
            },
            Targets = new FightingAllstar.Core.Content.PassiveTargetFilter
            {
                Relation = FightingAllstar.Core.Content.PassiveRelation.Allies,
                IncludeOwner = true,
                IncludeReserve = true
            },
            Scaling = FightingAllstar.Core.Content.PassiveScaling.Constant,
            MaximumUnits = 1
        };
        aura.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
        {
            Target = FightingAllstar.Core.Content.ModifierTarget.UltimateDamageDealt,
            Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
            Amount = 4000
        });
        passive.Auras.Add(aura);
        passiveAsset.SetDefinition(passive);
        EditorUtility.SetDirty(passiveAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Configured Ralf94's 40% Ultimate damage aura for allies, including Ralf in reserve.");
    }

    private static FightingAllstar.Core.Content.StatusRecipeDefinition BuildIoriTransferStatus(string id,
        FightingAllstar.Core.Content.StatusPolarity polarity, FightingAllstar.Core.Content.StatusDurationClock clock) =>
        new FightingAllstar.Core.Content.StatusRecipeDefinition
        {
            Id = id, NameKey = id, Polarity = polarity,
            Behavior = FightingAllstar.Core.Content.StatusBehavior.Stat,
            Stacking = FightingAllstar.Core.Content.StatusStackingPolicy.RefreshDuration,
            DurationClock = clock, DefaultDuration = 2, MaxStacks = 1,
            Tags = new List<string> { "status.extort" }
        };

    private static FightingAllstar.Core.Content.PassiveGate IoriGate() =>
        new FightingAllstar.Core.Content.PassiveGate
        {
            MinimumTier = 0,
            MaximumTier = 5,
            Modes = FightingAllstar.Core.Content.BattleModeMask.All,
            Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
        };

    private static void AddKyoTrait(string path)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(path);
        if (character == null) return;
        var serialized = new SerializedObject(character);
        var traits = character.TraitIds ?? Array.Empty<string>();
        if (!traits.Contains("trait.kyo")) Set(serialized, "traitIds", traits.Concat(new[] { "trait.kyo" }).ToArray());
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildIori95SkillEffect(int slot, int rank)
    {
        var coefficient = slot == 1 ? (rank * 50000 + 1) / 3 : rank == 1 ? 9333 : rank == 2 ? 18667 : 28000;
        var effect = new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = $"fighter.iori95.skill.{slot}.rank.{rank}",
            Kind = FightingAllstar.Core.Content.EffectKind.Damage,
            Family = FightingAllstar.Core.Content.DamageFamily.Normal,
            Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
            CoefficientBp = coefficient,
            KeywordFactorBp = 10000,
            Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
        };
        if (slot == 1)
        {
            var recipe = new FightingAllstar.Core.Content.StatusRecipeDefinition
            {
                Id = "status.buff.iori95.lifesteal",
                NameKey = "status.buff.iori95.lifesteal",
                Polarity = FightingAllstar.Core.Content.StatusPolarity.Buff,
                Behavior = FightingAllstar.Core.Content.StatusBehavior.Stat,
                DurationClock = FightingAllstar.Core.Content.StatusDurationClock.TargetTurnEnd,
                DefaultDuration = rank == 1 ? 1 : 2,
                MaxStacks = 1
            };
            recipe.Modifiers.Add(new FightingAllstar.Core.Content.StatModifierDefinition
            {
                Target = FightingAllstar.Core.Content.ModifierTarget.Stat,
                Stat = FightingAllstar.Core.Content.StatId.LifeSteal,
                Operation = FightingAllstar.Core.Content.ModifierOperation.PercentagePoints,
                Amount = rank * 1000
            });
            effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
            {
                Timing = FightingAllstar.Core.Content.CardEffectTiming.AfterDamage,
                Effect = new FightingAllstar.Core.Content.EffectStepDefinition
                {
                    Id = $"fighter.iori95.skill.1.lifesteal.rank.{rank}",
                    Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
                    Target = FightingAllstar.Core.Content.EffectTargetScope.Self,
                    StatusRecipe = recipe,
                    StatusDurationOverride = rank == 1 ? 1 : 2
                }
            });
        }
        else
        {
            var ignite = FightingAllstar.Core.Content.StandardEffectDatabase.CreateStatusRecipes()
                .Find(item => item.Id == "status.debuff.ignite")?.Clone();
            if (ignite != null)
                effect.Sequence.Add(new FightingAllstar.Core.Content.CardEffectStep
                {
                    Timing = FightingAllstar.Core.Content.CardEffectTiming.AfterDamage,
                    Effect = new FightingAllstar.Core.Content.EffectStepDefinition
                    {
                        Id = $"fighter.iori95.skill.2.ignite.rank.{rank}",
                        Kind = FightingAllstar.Core.Content.EffectKind.ApplyStatus,
                        Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy,
                        StatusRecipe = ignite,
                        StatusDurationOverride = rank,
                        StatusStackCount = rank == 1 ? 1 : 2
                    }
                });
        }
        return effect;
    }

    private static FightingAllstar.Core.Content.EffectDefinition BuildIori95UltimateEffect(int tier)
    {
        var effect = new FightingAllstar.Core.Content.EffectDefinition
        {
            Id = $"fighter.iori95.ultimate.c{tier}",
            Kind = FightingAllstar.Core.Content.EffectKind.Damage,
            Family = FightingAllstar.Core.Content.DamageFamily.Normal,
            Scaling = FightingAllstar.Core.Content.StatScaling.Attack,
            CoefficientBp = 40000 + tier * 2000,
            KeywordFactorBp = 10000,
            Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
        };
        return effect;
    }

    private static FightingAllstar.Core.Content.PassiveReactionDefinition LeonaDamageReaction(string id)
    {
        var rule = new FightingAllstar.Core.Content.PassiveReactionDefinition
        {
            Id = id,
            Gate = new FightingAllstar.Core.Content.PassiveGate
            {
                MinimumTier = 0,
                MaximumTier = 5,
                Modes = FightingAllstar.Core.Content.BattleModeMask.All,
                Presence = FightingAllstar.Core.Content.PassivePresence.LivingRoster
            },
            Trigger = FightingAllstar.Core.Content.PassiveEventKind.BeforeDamage,
            ActorRelation = FightingAllstar.Core.Content.PassiveRelation.Self,
            TargetRelation = FightingAllstar.Core.Content.PassiveRelation.Enemies
        };
        rule.Commands.Add(new FightingAllstar.Core.Content.PassiveCommandDefinition
        {
            Kind = FightingAllstar.Core.Content.PassiveCommandKind.IncreaseCurrentDamageDealtPercent,
            Amount = 4000
        });
        return rule;
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
            if (draft.constellations == null || draft.constellations.Length < 6)
                throw new InvalidOperationException(pair.Key + " must have all six C0-C5 Ultimate tiers before import.");
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
        Set(serialized, "passiveSourceType", CharacterPassiveMetadata.ParseSourceType(draft.passive.sourceType));
        Set(serialized, "passiveRestriction", CharacterPassiveMetadata.ParseRestriction(draft.passive.restriction));
        Set(serialized, "passiveDefinition", passive);
        Set(serialized, "id", 1000 + draft.sourceId);
        Set(serialized, "fighterName", draft.name);
        Set(serialized, "fighterTag", draft.familyId);
        var portrait = LoadCharacterSprite(draft.definitionId, true);
        Set(serialized, "fighterPic", portrait);
        Set(serialized, "fighterIcon", LoadCharacterSprite(draft.definitionId, false));
        Set(serialized, "fighter3DPrefab", model);
        var sharedMeshFilter = model == null ? null : model.GetComponent<MeshFilter>();
        Set(serialized, "fighter3DMesh", sharedMeshFilter == null ? null : sharedMeshFilter.sharedMesh);
        Set(serialized, "fighter3DMaterial", LoadOrCreateCharacterMaterial(safeId, portrait));
        Set(serialized, "Skill1", skill1);
        Set(serialized, "Skill2", skill2);
        Set(serialized, "Ultimate", ultimate);
        Set(serialized, "FighterAttribute", (Enum)Enum.Parse(typeof(FighterAttribute), draft.attribute, true));
        // Preserve the authoritative R overrides for Yuri and Ryo, plus Joe96's requested R rarity.
        var rarity = safeId == "yuri94" || safeId == "ryo94" || safeId == "joe96" ? "R" : draft.rarity;
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
        if (character.definitionId == "fighter.joe96")
        {
            var joe94 = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(CardPath + "/joe94_ultdata1.asset");
            if (joe94 == null || joe94.levels == null || joe94.levels.Count != 6)
                throw new InvalidOperationException("Joe94 must have all C0-C5 Ultimate data before Joe96 can reuse it.");
            asset.levels = joe94.levels.Select(level => new UltimateLevelData
            {
                level = level.level,
                description = level.description,
                skillType = level.skillType,
                runtimeEffect = level.runtimeEffect == null ? null : level.runtimeEffect.Clone()
            }).ToList();
        }
        else
        {
            asset.levels = character.constellations.OrderBy(item => item.tier).Select(item => new UltimateLevelData
            {
                level = item.tier,
                description = item.description,
                skillType = SkillType.Attack,
                runtimeEffect = character.definitionId == "fighter.mai95"
                    ? BuildMaiUltimateEffect(item.tier) : null
            }).ToList();
        }
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static PassiveDefinitionSO BuildPassive(DraftCharacter character)
    {
        var path = PassivePath + "/" + character.definitionId.Replace("fighter.", string.Empty) + "_Passive.asset";
        var asset = LoadOrCreate<PassiveDefinitionSO>(path);
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
                : FightingAllstar.Core.Content.EffectTargetScope.AllEnemies
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
            Target = FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy
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
        "kyo98" => "6", "benimaru99" => "7", "andy94" => "8", "joe94" => "9", "joe96" => "10",
        "ralf94" => "23", "clark94" => "24",
        "brian94" => "20", "lucky94" => "21", "heavyd94" => "22",
        "iori95" => "26", "iori96" => "26", "terry96" => "11",
        _ => throw new InvalidOperationException("No art mapping for " + id)
    };
    private static string CutNumber(string id) => id switch
    {
        "chang94" => "20", "ralf94" => "24", "clark94" => "25",
        "brian94" => "21", "lucky94" => "22", "heavyd94" => "23",
        "iori95" => "27", "iori96" => "27", _ => ArtNumber(id)
    };

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
        "joe96" => "Joe96", "ralf94" => "Ralf94", "clark94" => "Clark94",
        "brian94" => "Brian94", "lucky94" => "Lucky94", "heavyd94" => "HeavyD!94",
        "iori95" => "Iori95", "iori96" => "Iori95", "terry96" => "Terry96",
        _ => throw new InvalidOperationException("No art mapping for " + id)
    };

    private static Material LoadOrCreateCharacterMaterial(string safeId, Sprite portrait)
    {
        var path = "Assets/Project/Art/Character/3D-Material/" + safeId + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Project/Art/Character/3D-Material/joe94.mat");
        if (template == null) throw new InvalidOperationException("Could not find the shared character material template.");
        material = new Material(template) { name = safeId };
        if (portrait != null) material.mainTexture = portrait.texture;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

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
