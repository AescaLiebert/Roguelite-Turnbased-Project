using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class ConfigureSCIIIvyKit
{
    private const string Folder = "Assets/Project/Data/Character/Pre-Release_Phase/";
    private const string CharacterPath = Folder + "scii-lvy.asset";
    private const string PassivePath = Folder + "scii-lvy_Passive.asset";
    private const string BannerPath = "Assets/Project/Data/Economy/SoulCaliburBannerConfiguration.asset";

    [MenuItem("Fighting Allstar/Content/Configure SCII Ivy Kit")]
    public static void Configure()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(CharacterPath);
        var passiveAsset = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(PassivePath);
        if (character == null || passiveAsset == null || character.Skill1 == null || character.Skill2 == null ||
            character.Ultimate == null)
            throw new InvalidOperationException("Ivy's existing character, passive, skills, and Ultimate assets are required.");

        var banner = AssetDatabase.LoadAssetAtPath<SummonBannerConfigurationSO>(BannerPath);
        var attackVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/IncreaseATK-Related.asset");
        var extortBuffVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/ExtortBuff.asset");
        var extortDebuffVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/ExtortDebuff.asset");
        if (banner == null || attackVisual == null || extortBuffVisual == null || extortDebuffVisual == null)
            throw new InvalidOperationException("The Soul Calibur banner and shared ATK/Extort status visuals are required.");

        var definition = new PassiveDefinition { Id = "fighter.scii-ivy.passive.source" };
        var soulCaliburAura = new PassiveAuraDefinition
        {
            Id = "soul-calibur-stat-bonus",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self, IncludeOwner = true }
        };
        soulCaliburAura.Modifiers.Add(Stat(StatId.MaxHealth, 1000));
        soulCaliburAura.Modifiers.Add(Stat(StatId.CritResistance, 1000));
        definition.Auras.Add(soulCaliburAura);

        var attackBonus = new PassiveReactionDefinition
        {
            Id = "soul-calibur-attack-bonus",
            Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self,
            CardOriginOnly = true,
            FilterDamageFamily = true,
            DamageFamily = DamageFamily.Normal
        };
        attackBonus.Commands.Add(new PassiveCommandDefinition
        {
            Kind = PassiveCommandKind.IncreaseCurrentAttackPercent,
            Amount = 1200
        });
        definition.Reactions.Add(attackBonus);

        var teamAttackUp = Status("status.increase.attack-related", "Increase ATK-Related", StatusPolarity.Buff, 2,
            Stat(StatId.Attack, 1000));
        var criticalBuffReaction = new PassiveReactionDefinition
        {
            Id = "critical-hit-team-attack-buff",
            Trigger = PassiveEventKind.DamageResolved,
            ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies,
            CardOriginOnly = true,
            RequireCritical = true
        };
        criticalBuffReaction.Commands.Add(new PassiveCommandDefinition
        {
            Kind = PassiveCommandKind.ExecuteEffect,
            Effect = new CardEffectOperationDefinition
            {
                Id = "ivy.critical-hit.team-attack-buff",
                Window = CardEffectWindow.AfterDamage,
                Kind = CardEffectOperationKind.ApplyStatus,
                Target = EffectTargetScope.AllAllies,
                Status = new StatusApplicationRecipe
                {
                    StatusRecipeId = teamAttackUp.Id,
                    InlineRecipe = teamAttackUp,
                    DurationOverride = 2
                }
            }
        });
        definition.Reactions.Add(criticalBuffReaction);

        var stealStats = new PassiveReactionDefinition
        {
            Id = "ultimate-steal-stats",
            Trigger = PassiveEventKind.DamageResolved,
            DamageTiming = PassiveDamageTiming.FinalHit,
            ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies,
            CardOriginOnly = true,
            FilterCategory = true,
            Category = CardCategory.Ultimate,
            LimitScope = PassiveLimitScope.RootActionTarget,
            MaximumActivations = 1
        };
        stealStats.Commands.Add(new PassiveCommandDefinition
        {
            Kind = PassiveCommandKind.ExecuteEffect,
            Amount = 1,
            Effect = new CardEffectOperationDefinition
            {
                Id = "ivy.ultimate.steal-stats",
                Window = CardEffectWindow.Damaging,
                Kind = CardEffectOperationKind.TransferStats,
                Target = EffectTargetScope.TriggerTarget,
                StatTransfer = new StatTransferRecipeDefinition
                {
                    CoefficientBp = 5000,
                    Stats = new List<StatId> { StatId.Attack, StatId.Defense },
                    SourceStatus = TransferStatus("status.buff.extort", StatusPolarity.Buff,
                        StatusDurationClock.TargetTurnStart),
                    TargetStatus = TransferStatus("status.debuff.extort", StatusPolarity.Debuff,
                        StatusDurationClock.TargetTurnEnd)
                }
            }
        });
        definition.Reactions.Add(stealStats);

        var errors = PassiveRuleValidator.Validate(definition);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        passiveAsset.SetDefinition(definition);
        passiveAsset.statusVisuals = new List<StatusVisualData> { attackVisual, extortBuffVisual, extortDebuffVisual };
        EditorUtility.SetDirty(passiveAsset);

        var skill1 = character.Skill1;
        skill1.cardName = "SCII Ivy · Whip's Edge";
        skill1.statusVisuals = new List<StatusVisualData>();
        skill1.ranks = new List<CardRankData>();
        var skill1Coefficients = new[] { 10000, 18000, 25000 };
        for (var rank = 1; rank <= skill1Coefficients.Length; rank++)
        {
            var coefficient = skill1Coefficients[rank - 1];
            skill1.ranks.Add(new CardRankData
            {
                skillType = SkillType.Attack,
                description = $"Deals {coefficient / 100}% ATK Spike damage to all enemies in 5 hits.",
                runtimeEffect = Damage($"fighter.scii-ivy.skill.1.rank.{rank}", EffectTargetScope.AllEnemies,
                    coefficient, "attack.spike", 5, AttackRange.Long)
            });
        }

        var skill2 = character.Skill2;
        skill2.cardName = "SCII Ivy · Serpent's Snare";
        skill2.statusVisuals = new List<StatusVisualData>();
        skill2.ranks = new List<CardRankData>();
        var skill2Coefficients = new[] { 15000, 25000, 35000 };
        for (var rank = 1; rank <= skill2Coefficients.Length; rank++)
        {
            var coefficient = skill2Coefficients[rank - 1];
            var effect = Damage($"fighter.scii-ivy.skill.2.rank.{rank}", EffectTargetScope.AllEnemies,
                coefficient, null, 1, AttackRange.Long);
            effect.Sequence.Add(RemoveEffect("fighter.scii-ivy.skill.2.remove-buffs", EffectKind.RemoveBuffs,
                EffectTargetScope.AllEnemies));
            effect.Sequence.Add(RemoveEffect("fighter.scii-ivy.skill.2.remove-stances", EffectKind.RemoveStance,
                EffectTargetScope.AllEnemies));
            skill2.ranks.Add(new CardRankData
            {
                skillType = SkillType.DebuffAtk,
                description = $"Deals {coefficient / 100}% ATK damage to all enemies, removes their buffs, and cancels their stances.",
                runtimeEffect = effect
            });
        }

        var ultimate = character.Ultimate;
        ultimate.ultimateName = "SCII Ivy · Extort";
        ultimate.statusVisuals = new List<StatusVisualData> { extortBuffVisual, extortDebuffVisual };
        ultimate.levels = new List<UltimateLevelData>();
        for (var tier = 0; tier <= 5; tier++)
        {
            var coefficient = 50000 + tier * 2000;
            ultimate.levels.Add(new UltimateLevelData
            {
                level = tier,
                skillType = SkillType.Ultimate,
                description = $"Deals {coefficient / 100}% ATK damage to one enemy and steals 50% of their Attack and Defense, reducing their stats and increasing Ivy's stats by 50% for 2 turns.",
                runtimeEffect = Damage($"fighter.scii-ivy.ultimate.{tier}", EffectTargetScope.SelectedEnemy,
                    coefficient, null, 1, AttackRange.Close)
            });
        }

        var serialized = new SerializedObject(character);
        serialized.FindProperty("passiveSourceDescription").stringValue =
            "When Ivy lands a critical hit with an attack, grants all allies ATK +10% for 2 turns. Soul Calibur: own Max HP +10% and Crit Resistance +10 percentage points; ATK +12% only while attacking, without stacking or a lasting bonus.";
        serialized.FindProperty("passiveDefinition").objectReferenceValue = passiveAsset;
        serialized.FindProperty("Skill1").objectReferenceValue = skill1;
        serialized.FindProperty("Skill2").objectReferenceValue = skill2;
        serialized.FindProperty("Ultimate").objectReferenceValue = ultimate;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(skill1);
        EditorUtility.SetDirty(skill2);
        EditorUtility.SetDirty(ultimate);

        AddToBanner(banner, character);
        if (!banner.Validate(out var bannerError)) throw new InvalidOperationException(bannerError);
        AssetDatabase.SaveAssets();
        Debug.Log("SCII Ivy passive, ranked skills, Iori95-style single-target Ultimate, and Soul Calibur banner configured.");
    }

    private static void AddToBanner(SummonBannerConfigurationSO banner, CharacterObject character)
    {
        var data = new SerializedObject(banner);
        var pools = data.FindProperty("rarityPools");
        for (var i = 0; i < pools.arraySize; i++)
        {
            var pool = pools.GetArrayElementAtIndex(i);
            if ((FighterRarity)pool.FindPropertyRelative("rarity").enumValueIndex != FighterRarity.SSR) continue;
            var characters = pool.FindPropertyRelative("characters");
            for (var j = 0; j < characters.arraySize; j++)
                if (characters.GetArrayElementAtIndex(j).objectReferenceValue == character) return;
            characters.InsertArrayElementAtIndex(characters.arraySize);
            characters.GetArrayElementAtIndex(characters.arraySize - 1).objectReferenceValue = character;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(banner);
            return;
        }
        throw new InvalidOperationException("Soul Calibur banner has no SSR pool.");
    }

    private static EffectDefinition Damage(string id, EffectTargetScope target, int coefficientBp, string keyword,
        int hitCount, AttackRange range) => new EffectDefinition
    {
        Id = id,
        Kind = EffectKind.Damage,
        Family = DamageFamily.Normal,
        Scaling = StatScaling.Attack,
        CoefficientBp = coefficientBp,
        KeywordId = keyword,
        KeywordFactorBp = 10000,
        Target = target,
        Attack = new DamageAttackDefinition { HitCount = hitCount, Range = range }
    };

    private static CardEffectStep RemoveEffect(string id, EffectKind kind, EffectTargetScope target) => new CardEffectStep
    {
        Timing = CardEffectTiming.AfterDamage,
        Effect = new EffectStepDefinition { Id = id, Kind = kind, Target = target }
    };

    private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition
    {
        Target = ModifierTarget.Stat,
        Stat = stat,
        Amount = amount,
        Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth
            ? ModifierOperation.PercentOfBase
            : ModifierOperation.PercentagePoints
    };

    private static StatusRecipeDefinition Status(string id, string name, StatusPolarity polarity, int turns,
        params StatModifierDefinition[] modifiers) => new StatusRecipeDefinition
    {
        Id = id,
        NameKey = name,
        Polarity = polarity,
        Behavior = StatusBehavior.Stat,
        DefaultDuration = turns,
        DurationClock = StatusDurationClock.TargetTurnEnd,
        Stacking = StatusStackingPolicy.RefreshDuration,
        MaxStacks = 1,
        Modifiers = new List<StatModifierDefinition>(modifiers)
    };

    private static StatusRecipeDefinition TransferStatus(string id, StatusPolarity polarity, StatusDurationClock clock) =>
        new StatusRecipeDefinition
        {
            Id = id,
            NameKey = id,
            Polarity = polarity,
            Behavior = StatusBehavior.Stat,
            Stacking = StatusStackingPolicy.RefreshDuration,
            DurationClock = clock,
            DefaultDuration = 2,
            MaxStacks = 1,
            Tags = new List<string> { "status.extort" }
        };
}
