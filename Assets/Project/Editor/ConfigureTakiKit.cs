using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class ConfigureTakiKit
{
    private const string Folder = "Assets/Project/Data/Character/Pre-Release_Phase/";
    private const string Id = "fighter.scii-taki";
    private const string BannerPath = "Assets/Project/Data/Economy/SoulCaliburBannerConfiguration.asset";
    private const string SplashPath = "Assets/Project/Art/Character/SplashArt/sa-scii-taki.png";
    private const string IconPath = "Assets/Project/Art/Character/Icon/icon-sa-scii-og-taki.png";

    [MenuItem("Fighting Allstar/Content/Configure SCII Taki")]
    public static void Configure()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(Folder + "scii-taki.asset");
        var passive = AssetDatabase.LoadAssetAtPath<PassiveDefinitionSO>(Folder + "scii-taki_Passive.asset");
        var skill1 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(Folder + "scii-taki_skilldata1.asset");
        var skill2 = AssetDatabase.LoadAssetAtPath<SkillCardSO>(Folder + "scii-taki_skilldata2.asset");
        var ultimateCard = AssetDatabase.LoadAssetAtPath<UltimateCardSO>(Folder + "scii-taki_ultdata1.asset");
        var banner = AssetDatabase.LoadAssetAtPath<SummonBannerConfigurationSO>(BannerPath);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
        if (character == null || passive == null || skill1 == null || skill2 == null || ultimateCard == null ||
            banner == null || icon == null || splash == null)
            throw new InvalidOperationException("Taki's character, cards, passive, banner, and art assets are required.");

        var hpDown = Status("status.debuff.taki.max-health-down", "Decrease Max HP", StatusPolarity.Debuff, 2,
            new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.MaxHealth,
                Operation = ModifierOperation.PercentOfBase, Amount = -3000 });

        var definition = new PassiveDefinition { Id = Id + ".passive.source" };
        var pvpAura = new PassiveAuraDefinition
        {
            Id = "pvp-enemy-attack-reduction",
            Gate = new PassiveGate { Modes = BattleModeMask.PvP },
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Enemies, IncludeOwner = false }
        };
        pvpAura.Modifiers.Add(Stat(StatId.Attack, -1600));
        definition.Auras.Add(pvpAura);

        var soulCaliburAura = new PassiveAuraDefinition
        {
            Id = "soul-calibur-stat-bonus",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self }
        };
        soulCaliburAura.Modifiers.Add(Stat(StatId.MaxHealth, 1000));
        soulCaliburAura.Modifiers.Add(Stat(StatId.CritResistance, 1000));
        definition.Auras.Add(soulCaliburAura);

        var attackBonus = new PassiveReactionDefinition
        {
            Id = "soul-calibur-attack-bonus", Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self, CardOriginOnly = true,
            FilterDamageFamily = true, DamageFamily = DamageFamily.Normal
        };
        attackBonus.Commands.Add(new PassiveCommandDefinition
        {
            Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 1200
        });
        definition.Reactions.Add(attackBonus);

        var passiveErrors = PassiveRuleValidator.Validate(definition);
        if (passiveErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", passiveErrors));
        passive.SetDefinition(definition);
        passive.statusVisuals = new List<StatusVisualData>();
        EditorUtility.SetDirty(passive);

        var visual = AssetDatabase.LoadAssetAtPath<StatusVisualData>(
            "Assets/Project/Data/StatusData/DecreaseHP-Related.asset");
        if (visual == null)
            throw new InvalidOperationException("The existing Decrease HP-Related status visual is required.");

        skill1.cardName = "SCII Taki · Shadow Strike";
        skill1.cardIcon = icon;
        skill1.statusVisuals = new List<StatusVisualData> { visual };
        skill1.ranks = new List<CardRankData>();
        var skill1Coefficients = new[] { 12000, 24000, 36000 };
        for (var rank = 1; rank <= skill1Coefficients.Length; rank++)
        {
            var coefficient = skill1Coefficients[rank - 1];
            var effect = Damage(Id + ".skill.1.rank." + rank, EffectTargetScope.SelectedEnemy, coefficient);
            effect.Sequence.Add(Step(hpDown, EffectTargetScope.SelectedEnemy, CardEffectTiming.AfterDamage));
            skill1.ranks.Add(new CardRankData { skillType = SkillType.DebuffAtk,
                description = $"Deals {coefficient / 100}% ATK damage to one enemy and reduces their Max HP by 30% for 2 turns.",
                runtimeEffect = effect });
        }

        skill2.cardName = "SCII Taki · Shadow Dance";
        skill2.cardIcon = icon;
        skill2.statusVisuals = new List<StatusVisualData>();
        skill2.ranks = new List<CardRankData>();
        var skill2Coefficients = new[] { 18000, 27000, 45000 };
        for (var rank = 1; rank <= skill2Coefficients.Length; rank++)
        {
            var coefficient = skill2Coefficients[rank - 1];
            var effect = Damage(Id + ".skill.2.rank." + rank, EffectTargetScope.SelectedEnemy, coefficient);
            effect.Sequence.Add(new CardEffectStep { Timing = CardEffectTiming.AfterDamage, Effect = new EffectStepDefinition {
                Id = Id + ".skill.2.rank." + rank + ".additional", Kind = EffectKind.Damage, Target = EffectTargetScope.SelectedEnemy,
                DamageRecipe = new DamageEffectRecipe { Family = DamageFamily.Additional, Scaling = StatScaling.MaxHealth,
                    StatSource = DamageStatSource.Target, CoefficientBp = 2000, CannotCrit = true, CannotBlock = true } } });
            skill2.ranks.Add(new CardRankData { skillType = SkillType.Attack,
                description = $"Deals {coefficient / 100}% ATK damage to one enemy, plus additional damage equal to 20% of the target's Max HP.", runtimeEffect = effect });
        }

        ultimateCard.ultimateName = "SCII Taki · Severing Slash";
        ultimateCard.icon = icon;
        ultimateCard.statusVisuals = new List<StatusVisualData>();
        ultimateCard.levels = new List<UltimateLevelData>();
        for (var tier = 0; tier <= 5; tier++)
        {
            var coefficient = 63000 + tier * 6000;
            ultimateCard.levels.Add(new UltimateLevelData { level = tier, skillType = SkillType.Ultimate,
                description = $"Slashes one enemy for {coefficient / 100}% ATK damage.",
                runtimeEffect = Damage(Id + ".ultimate." + tier, EffectTargetScope.SelectedEnemy, coefficient) });
        }

        var data = new SerializedObject(character);
        data.FindProperty("definitionId").stringValue = Id;
        data.FindProperty("runtimeReady").boolValue = true;
        data.FindProperty("familyId").stringValue = "family.taki";
        data.FindProperty("seriesId").stringValue = "series.sc";
        data.FindProperty("role").stringValue = "Assassin";
        data.FindProperty("traitIds").arraySize = 2;
        data.FindProperty("traitIds").GetArrayElementAtIndex(0).stringValue = "trait.female";
        data.FindProperty("traitIds").GetArrayElementAtIndex(1).stringValue = "trait.soul-calibur";
        data.FindProperty("passiveSourceDescription").stringValue =
            "PvP only: reduces enemy ATK by 16%. Soul Calibur: own Max HP +10% and Crit Resistance +10 percentage points; ATK +12% only while attacking, without stacking or a lasting bonus.";
        data.FindProperty("passiveSourceType").enumValueIndex = (int)PassiveSourceType.OnField;
        data.FindProperty("passiveRestriction").enumValueIndex = (int)PassiveRestriction.All;
        data.FindProperty("passiveDefinition").objectReferenceValue = passive;
        data.FindProperty("id").intValue = 2304;
        data.FindProperty("fighterName").stringValue = "[Soul Calibur II] Taki";
        data.FindProperty("fighterPic").objectReferenceValue = splash;
        data.FindProperty("fighterIcon").objectReferenceValue = icon;
        data.FindProperty("fighterFull").objectReferenceValue = splash;
        data.FindProperty("fighterPassive").objectReferenceValue = null;
        data.FindProperty("fighterCard1").objectReferenceValue = icon;
        data.FindProperty("fighterCard2").objectReferenceValue = icon;
        data.FindProperty("fighterUltimate").objectReferenceValue = icon;
        data.FindProperty("Skill1").objectReferenceValue = skill1;
        data.FindProperty("Skill2").objectReferenceValue = skill2;
        data.FindProperty("Ultimate").objectReferenceValue = ultimateCard;
        data.FindProperty("fighter3DPrefab").objectReferenceValue = null;
        data.FindProperty("fighter3DMesh").objectReferenceValue = null;
        data.FindProperty("fighter3DMaterial").objectReferenceValue = null;
        data.FindProperty("FighterAttribute").enumValueIndex = (int)FighterAttribute.Blue;
        data.FindProperty("FighterRarity").enumValueIndex = (int)FighterRarity.SSR;
        data.FindProperty("fighterUltimateLevel").intValue = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);

        var skillDefinition = new SkillDefinition { Id = Id + ".skill.2", Slot = 2, Category = CardCategory.Attack };
        for (var i = 0; i < skill2.ranks.Count; i++) skillDefinition.Ranks.Add(new SkillRankDefinition {
            Rank = i + 1, Description = skill2.ranks[i].description, Effect = skill2.ranks[i].runtimeEffect.Clone() });
        skill2.SetStudioDefinition(skillDefinition);
        EditorUtility.SetDirty(skill1);
        EditorUtility.SetDirty(skill2);
        EditorUtility.SetDirty(ultimateCard);
        AddToBanner(banner, character);
        if (!banner.Validate(out var bannerError)) throw new InvalidOperationException(bannerError);
        AssetDatabase.SaveAssets();
        Debug.Log("All SCII Taki ScriptableObjects configured and Soul Calibur banner validated.");
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

    private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition
    {
        Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
        Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth
            ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints
    };

    private static StatusRecipeDefinition Status(string id, string name, StatusPolarity polarity, int turns,
        params StatModifierDefinition[] modifiers) => new StatusRecipeDefinition
    {
        Id = id, NameKey = name, Polarity = polarity, Behavior = StatusBehavior.Stat,
        DefaultDuration = turns, DurationClock = StatusDurationClock.TargetTurnEnd,
        Stacking = StatusStackingPolicy.RefreshDuration, MaxStacks = 1,
        Modifiers = new List<StatModifierDefinition>(modifiers)
    };

    private static EffectDefinition Damage(string id, EffectTargetScope target, int coefficientBp) => new EffectDefinition
    {
        Id = id, Kind = EffectKind.Damage, Target = target, Scaling = StatScaling.Attack,
        CoefficientBp = coefficientBp
    };

    private static CardEffectStep Step(StatusRecipeDefinition status, EffectTargetScope target, CardEffectTiming timing) =>
        new CardEffectStep { Timing = timing, Effect = new EffectStepDefinition
        {
            Id = status.Id, Kind = EffectKind.ApplyStatus, Target = target,
            StatusRecipe = status, StatusDurationOverride = status.DefaultDuration
        } };
}
