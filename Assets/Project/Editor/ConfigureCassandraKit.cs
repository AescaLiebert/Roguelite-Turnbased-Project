using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using UnityEditor;
using UnityEngine;

public static class ConfigureCassandraKit
{
    private const string Folder = "Assets/Project/Data/Character/Pre-Release_Phase/";
    private const string Id = "fighter.scii-cassandra";

    [MenuItem("Fighting Allstar/Content/Configure SCII Cassandra")]
    public static void Configure()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "scii-cassandra.asset");
        var passive = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(Folder + "scii-cassandra_Passive.asset");
        if (character == null || passive == null || character.Skill1 == null || character.Skill2 == null || character.Ultimate == null)
            throw new InvalidOperationException("Cassandra's existing authoring assets are required.");

        var defenseAttack = Status("status.buff.cassandra.defense-attack", "Defense to Attack", StatusPolarity.Buff, 2,
            new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Attack,
                Operation = ModifierOperation.Flat, ScaleByStatusPotency = true, PotencyCoefficientBp = 5000 });
        var definition = new PassiveDefinition { Id = Id + ".passive.source" };
        var soul = new PassiveAuraDefinition { Id = "soul-calibur", Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self } };
        soul.Modifiers.Add(Stat(StatId.MaxHealth, 1000));
        soul.Modifiers.Add(Stat(StatId.CritResistance, 1000));
        definition.Auras.Add(soul);
        var attackBonus = new PassiveReactionDefinition { Id = "soul-calibur-attack", Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self, CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        attackBonus.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 1200 });
        definition.Reactions.Add(attackBonus);
        var reaction = new PassiveReactionDefinition { Id = "on-attack", Trigger = PassiveEventKind.DamageResolved,
            ActorRelation = PassiveRelation.Self, CardOriginOnly = true, LimitScope = PassiveLimitScope.RootAction,
            MaximumActivations = 1 };
        reaction.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
            Effect = new CardEffectOperationDefinition { Id = "defense-attack", Window = CardEffectWindow.AfterAction,
                Kind = CardEffectOperationKind.ApplyStatus, Target = EffectTargetScope.Self,
                Status = new StatusApplicationRecipe { StatusRecipeId = defenseAttack.Id, InlineRecipe = defenseAttack,
                    DurationOverride = 2, PotencySource = StatusPotencySource.EffectOwnerDefense } } });
        definition.Reactions.Add(reaction);
        var errors = PassiveRuleValidator.Validate(definition);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        passive.SetDefinition(definition);
        EditorUtility.SetDirty(passive);

        var vulnerability = Status("status.debuff.cassandra.electrified", "Electrified", StatusPolarity.Debuff, 3,
            new StatModifierDefinition { Target = ModifierTarget.AnyDamageReceived, Operation = ModifierOperation.PercentagePoints, Amount = 4000 });
        vulnerability.Tags.Add("status.electrified");
        var defense = Status("status.buff.stat.defense", "Increase Defense", StatusPolarity.Buff, 2, Stat(StatId.Defense, 6000));
        var ultimateBuff = Status("status.buff.cassandra.ultimate", "Soul Calibur Power", StatusPolarity.Buff, 2,
            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 5000 },
            new StatModifierDefinition { Target = ModifierTarget.AnyDamageReceived, Operation = ModifierOperation.PercentagePoints, Amount = -5000 });
        var seal = Status("status.debuff.disable-debuff", "Disable Debuff", StatusPolarity.Debuff, 2);
        seal.Behavior = StatusBehavior.Disable;
        seal.DisableMask = CardCategoryMask.Debuff;
        character.Skill1.cardName = "SCII Cassandra · Sword Spin";
        character.Skill2.cardName = "SCII Cassandra · Thunder Force";
        character.Ultimate.ultimateName = "SCII Cassandra · Ultimate";
        character.Ultimate.icon = character.Skill2.cardIcon;
        passive.statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData> {
            Visual(defenseAttack, "Status_StatUp.png", "ATK equal to 50% of DEF for 2 turns. Refreshes without stacking.") };
        character.Skill1.statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData> {
            Visual(vulnerability, "Status_Shock.png", "Increases damage received by 40% for 3 turns.") };
        character.Ultimate.statusVisuals = new List<FightingAllstar.Presentation.Combat.StatusVisualData> {
            Visual(ultimateBuff, "Status_StatUp.png", "Damage dealt +50%; damage received -50% for 2 turns.") };

        character.Skill1.ranks.Clear(); character.Skill2.ranks.Clear(); character.Ultimate.levels.Clear();
        for (int rank = 1; rank <= 3; rank++)
        {
            var first = Damage(Id + ".skill.1.rank." + rank, EffectTargetScope.SelectedEnemy);
            first.Sequence.Add(Step(vulnerability, EffectTargetScope.SelectedEnemy, CardEffectTiming.AfterDamage));
            character.Skill1.ranks.Add(new CardRankData { skillType = SkillType.DebuffAtk,
                description = "Deals 300% ATK damage to one enemy. Electrifies the target, increasing damage received by 40% for 3 turns.", runtimeEffect = first });
            var second = Damage(Id + ".skill.2.rank." + rank, EffectTargetScope.AllEnemies);
            second.Sequence.Add(Step(defense, EffectTargetScope.Self, CardEffectTiming.AfterAction));
            character.Skill2.ranks.Add(new CardRankData { skillType = SkillType.Attack,
                description = "Deals 300% ATK damage to all enemies. Increases own DEF by 60% for 2 turns.", runtimeEffect = second });
        }
        for (int tier = 0; tier <= 5; tier++)
        {
            var ultimate = Damage(Id + ".ultimate." + tier, EffectTargetScope.AllEnemies);
            ultimate.Sequence.Add(Step(ultimateBuff, EffectTargetScope.Self, CardEffectTiming.BeforeAction));
            ultimate.Sequence.Add(Step(seal, EffectTargetScope.AllEnemies, CardEffectTiming.AfterDamage));
            character.Ultimate.levels.Add(new UltimateLevelData { level = tier, skillType = SkillType.DebuffAtk,
                description = "Increases own damage dealt by 50% and reduces damage received by 50% for 2 turns, then deals 300% ATK damage to all enemies and disables their Debuff cards for 2 turns.", runtimeEffect = ultimate });
        }
        var serialized = new SerializedObject(character);
        serialized.FindProperty("passiveSourceDescription").stringValue = "After attacking, gain ATK equal to 50% of current DEF for 2 turns (refreshes, no cooldown). Soul Calibur: own Max HP +10%, Crit Resistance +10 percentage points; ATK +12% only while attacking (no stacking or lasting bonus).";
        serialized.FindProperty("fighterFull").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Character/Cut/cassandra-character-only-4x3.png");
        serialized.FindProperty("fighterCard1").objectReferenceValue = character.Skill1.cardIcon;
        serialized.FindProperty("fighterCard2").objectReferenceValue = character.Skill2.cardIcon;
        serialized.FindProperty("fighterUltimate").objectReferenceValue = character.Ultimate.icon;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character); EditorUtility.SetDirty(character.Skill1);
        EditorUtility.SetDirty(character.Skill2); EditorUtility.SetDirty(character.Ultimate);
        AssetDatabase.SaveAssets();
        Debug.Log("Cassandra kit configured: SSR Blue; both 3-rank skills and all C0-C5 Ultimates use 300% ATK.");
    }

    private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition {
        Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
        Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth
            ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints };

    private static FightingAllstar.Presentation.Combat.StatusVisualData Visual(StatusRecipeDefinition status, string icon, string description)
    {
        var path = Folder + "scii-cassandra_" + status.Id.Substring(status.Id.LastIndexOf('.') + 1) + "_Visual.asset";
        var visual = AssetDatabase.LoadAssetAtPath<FightingAllstar.Presentation.Combat.StatusVisualData>(path);
        if (visual == null) { visual = ScriptableObject.CreateInstance<FightingAllstar.Presentation.Combat.StatusVisualData>(); AssetDatabase.CreateAsset(visual, path); }
        var so = new SerializedObject(visual);
        so.FindProperty("id").stringValue = status.Id;
        so.FindProperty("displayName").stringValue = status.NameKey;
        so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/" + icon);
        so.FindProperty("polarity").enumValueIndex = (int)status.Polarity;
        so.FindProperty("description").stringValue = description;
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(visual);
        return visual;
    }

    private static StatusRecipeDefinition Status(string id, string name, StatusPolarity polarity, int turns, params StatModifierDefinition[] modifiers)
        => new StatusRecipeDefinition { Id = id, NameKey = name, Polarity = polarity,
            Behavior = StatusBehavior.Stat, DefaultDuration = turns, DurationClock = StatusDurationClock.TargetTurnEnd,
            Stacking = StatusStackingPolicy.RefreshDuration, MaxStacks = 1, Modifiers = new List<StatModifierDefinition>(modifiers) };

    private static EffectDefinition Damage(string id, EffectTargetScope target) => new EffectDefinition {
        Id = id, Kind = EffectKind.Damage, Target = target, Scaling = StatScaling.Attack, CoefficientBp = 30000 };

    private static CardEffectStep Step(StatusRecipeDefinition status, EffectTargetScope target, CardEffectTiming timing)
        => new CardEffectStep { Timing = timing, Effect = new EffectStepDefinition {
            Id = status.Id, Kind = EffectKind.ApplyStatus, Target = target, StatusRecipe = status,
            StatusDurationOverride = status.DefaultDuration } };
}
