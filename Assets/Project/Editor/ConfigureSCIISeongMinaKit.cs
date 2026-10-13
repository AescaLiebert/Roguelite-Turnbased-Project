using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class ConfigureSCIISeongMinaKit
{
    private const string Folder = "Assets/Project/Data/Character/Pre-Release_Phase/";
    private const string Id = "fighter.scii-seong";
    private const string BannerPath = "Assets/Project/Data/Economy/SoulCaliburBannerConfiguration.asset";
    private const string SplashPath = "Assets/Project/Art/Character/SplashArt/sa-scii-seong.png";
    private const string IconPath = "Assets/Project/Art/Character/Icon/icon-sa-scii-seong.png";

    [MenuItem("Fighting Allstar/Content/Configure SCII Seong Mi-Na Kit")]
    public static void Configure()
    {
        EnsureFolder("Assets/Project/Data/Character/Pre-Release_Phase");
        var character = LoadOrCreate<CharacterObject>(Folder + "scii-seong.asset");
        var passive = LoadOrCreate<PassiveDefinitionSO>(Folder + "scii-seong_Passive.asset");
        var skill1 = LoadOrCreate<SkillCardSO>(Folder + "scii-seong_skilldata1.asset");
        var skill2 = LoadOrCreate<SkillCardSO>(Folder + "scii-seong_skilldata2.asset");
        var ultimate = LoadOrCreate<UltimateCardSO>(Folder + "scii-seong_ultdata1.asset");
        var banner = AssetDatabase.LoadAssetAtPath<SummonBannerConfigurationSO>(BannerPath);
        var splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var bleedVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/Bleed.asset");
        var critVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/IncreaseCritChance.asset");
        if (banner == null || splash == null || icon == null || bleedVisual == null || critVisual == null)
            throw new InvalidOperationException("Soul Calibur banner, Seong art, Bleed and Crit Chance status visuals are required.");

        var definition = new PassiveDefinition { Id = Id + ".passive.source" };
        var selfStats = new PassiveAuraDefinition { Id = "soul-calibur-stat-bonus",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self } };
        selfStats.Modifiers.Add(Stat(StatId.MaxHealth, 1000));
        selfStats.Modifiers.Add(Stat(StatId.CritResistance, 1000));
        definition.Auras.Add(selfStats);

        var attackBonus = new PassiveReactionDefinition { Id = "soul-calibur-attack-bonus",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        attackBonus.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 1200 });
        definition.Reactions.Add(attackBonus);

        var debuffDamage = new PassiveReactionDefinition { Id = "damage-vs-debuffed-enemies",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
            FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        debuffDamage.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasDebuff });
        debuffDamage.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 3000 });
        definition.Reactions.Add(debuffDamage);

        var bleedCritDefense = new PassiveReactionDefinition { Id = "ignore-crit-defense-vs-bleeding",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
            FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        bleedCritDefense.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Bleed });
        bleedCritDefense.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent, Amount = 3000 });
        definition.Reactions.Add(bleedCritDefense);
        var errors = PassiveRuleValidator.Validate(definition);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        passive.SetDefinition(definition);
        passive.statusVisuals = new List<StatusVisualData> { bleedVisual, critVisual };
        EditorUtility.SetDirty(passive);

        var bleed = StandardEffectDatabase.CreateStatusRecipes().Find(recipe => recipe.Id == "status.debuff.bleed");
        if (bleed == null) throw new InvalidOperationException("The standard Bleed recipe is missing.");
        bleed = bleed.Clone();
        bleed.DefaultDuration = 2;
        var skill1Coefficients = new[] { 12000, 18000, 21000 };
        var critAmounts = new[] { 2000, 3000, 3000 };
        var critDurations = new[] { 2, 2, 3 };
        skill1.cardName = "SCII Seong Mi-Na · Dragon's Breath";
        skill1.cardIcon = icon;
        skill1.statusVisuals = new List<StatusVisualData> { bleedVisual, critVisual };
        skill1.ranks = new List<CardRankData>();
        for (var index = 0; index < skill1Coefficients.Length; index++)
        {
            var rank = index + 1;
            var coefficient = skill1Coefficients[index];
            var critAmount = critAmounts[index];
            var duration = critDurations[index];
            var effect = Damage(Id + ".skill.1.rank." + rank, EffectTargetScope.AllEnemies, coefficient, null, 10, AttackRange.Close);
            effect.Sequence.Add(ApplyStatus(Id + ".skill.1.bleed." + rank, EffectTargetScope.AllEnemies, CardEffectTiming.AfterDamage, bleed));
            var critStatus = Status("status.increase.crit-chance", "Increase Crit Chance", StatusPolarity.Buff, duration,
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.CritChance,
                    Operation = ModifierOperation.PercentagePoints, Amount = critAmount });
            effect.Sequence.Add(ApplyStatus(Id + ".skill.1.crit-chance." + rank, EffectTargetScope.Self, CardEffectTiming.AfterAction, critStatus));
            skill1.ranks.Add(new CardRankData { skillType = SkillType.DebuffAtk,
                description = $"Deals {coefficient / 100}% ATK damage to all enemies in 10 hits, inflicts Bleed for 2 turns, and increases Seong Mi-Na's Crit Chance by {critAmount / 100}% for {duration} turns.",
                runtimeEffect = effect });
        }

        skill2.cardName = "SCII Seong Mi-Na · Dragon's Roar";
        skill2.cardIcon = icon;
        skill2.statusVisuals = new List<StatusVisualData>();
        skill2.ranks = new List<CardRankData>();
        var skill2Coefficients = new[] { 16000, 24000, 40000 };
        for (var index = 0; index < skill2Coefficients.Length; index++)
        {
            var rank = index + 1;
            var coefficient = skill2Coefficients[index];
            skill2.ranks.Add(new CardRankData { skillType = SkillType.Attack,
                description = $"Deals {coefficient / 100}% ATK Spike damage to one enemy.",
                runtimeEffect = Damage(Id + ".skill.2.rank." + rank, EffectTargetScope.SelectedEnemy,
                    coefficient, "attack.spike", 1, AttackRange.Close) });
        }

        ultimate.ultimateName = "SCII Seong Mi-Na · Dragon's Fury";
        ultimate.icon = icon;
        ultimate.statusVisuals = new List<StatusVisualData>();
        ultimate.levels = new List<UltimateLevelData>();
        for (var tier = 0; tier <= 5; tier++)
        {
            var coefficient = 45000 + tier * 5000;
            ultimate.levels.Add(new UltimateLevelData { level = tier, skillType = SkillType.Ultimate,
                description = $"Deals {coefficient / 100}% ATK Power Strike damage to all enemies.",
                runtimeEffect = Damage(Id + ".ultimate.c" + tier, EffectTargetScope.AllEnemies,
                    coefficient, "attack.power-strike", 1, AttackRange.Close) });
        }

        var data = new SerializedObject(character);
        Set(data, "definitionId", Id);
        Set(data, "runtimeReady", true);
        Set(data, "familyId", "family.seong-mina");
        Set(data, "seriesId", "series.sc");
        Set(data, "role", "Attacker");
        var traits = data.FindProperty("traitIds");
        traits.arraySize = 2;
        traits.GetArrayElementAtIndex(0).stringValue = "trait.female";
        traits.GetArrayElementAtIndex(1).stringValue = "trait.soul-calibur";
        Set(data, "passiveSourceDescription", "When Seong Mi-Na attacks a debuffed enemy, her damage dealt increases by 30%. Against a bleeding enemy, her attacks ignore 30% of its Crit Defense. Soul Calibur: own Max HP +10% and Crit Resistance +10 percentage points; ATK +12% only while attacking, without stacking or a lasting bonus.");
        Set(data, "passiveSourceType", (int)PassiveSourceType.OnField);
        Set(data, "passiveRestriction", (int)PassiveRestriction.All);
        Set(data, "passiveDefinition", passive);
        Set(data, "id", 2305);
        Set(data, "fighterName", "[Soul Calibur II] Seong Mi-Na");
        Set(data, "fighterPic", splash);
        Set(data, "fighterIcon", icon);
        Set(data, "fighterFull", splash);
        Set(data, "fighterPassive", (UnityEngine.Object)null);
        Set(data, "fighterCard1", icon);
        Set(data, "fighterCard2", icon);
        Set(data, "fighterUltimate", icon);
        Set(data, "Skill1", skill1);
        Set(data, "Skill2", skill2);
        Set(data, "Ultimate", ultimate);
        Set(data, "fighter3DPrefab", (UnityEngine.Object)null);
        Set(data, "fighter3DMesh", (UnityEngine.Object)null);
        Set(data, "fighter3DMaterial", (UnityEngine.Object)null);
        Set(data, "FighterAttribute", (int)FighterAttribute.Blue);
        Set(data, "FighterRarity", (int)FighterRarity.SSR);
        Set(data, "fighterUltimateLevel", 0);
        data.ApplyModifiedPropertiesWithoutUndo();
        character.attack = 450;
        character.defense = 360;
        character.health = 6900;
        character.Classpower = 3750;
        character.LevelGrowth = new CharacterStatGrowth { AttackBp = 1935, DefenseBp = 2035, HealthBp = 2135 };
        character.SecondaryStats = new CharacterSecondaryStats { PierceRate = 35, Resistance = 5, Regenerate = 10,
            CritChance = 20, CritDmg = 135, CritResistance = 35, CritDefense = 20, RecoveryRate = 110,
            BlockChance = 20, BlockPower = 25, LifeSteal = 17, AvoidanceRate = 100, ControlRate = 100, PerceptionRate = 100 };
        EditorUtility.SetDirty(character);
        EditorUtility.SetDirty(skill1);
        EditorUtility.SetDirty(skill2);
        EditorUtility.SetDirty(ultimate);
        AddToBanner(banner, character);
        if (!banner.Validate(out var bannerError)) throw new InvalidOperationException(bannerError);
        var runtimeDefinition = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.CreateDefinition(character);
        if (runtimeDefinition.Skills[0].Ranks.Count != 3 || runtimeDefinition.Skills[1].Ranks.Count != 3 ||
            runtimeDefinition.UltimateTiers.Count != 6 || PassiveRuleValidator.Validate(runtimeDefinition.Passive).Count > 0)
            throw new InvalidOperationException("Seong Mi-Na's authored assets did not produce a complete runtime definition.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SCII Seong Mi-Na character, passive, ranked skills, C0-C5 Ultimate, and Soul Calibur banner configured.");
    }

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
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Set(SerializedObject data, string propertyName, string value) => data.FindProperty(propertyName).stringValue = value;
    private static void Set(SerializedObject data, string propertyName, bool value) => data.FindProperty(propertyName).boolValue = value;
    private static void Set(SerializedObject data, string propertyName, int value) => data.FindProperty(propertyName).intValue = value;
    private static void Set(SerializedObject data, string propertyName, UnityEngine.Object value) => data.FindProperty(propertyName).objectReferenceValue = value;

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
        int hitCount, AttackRange range) => new EffectDefinition { Id = id, Kind = EffectKind.Damage,
            Family = DamageFamily.Normal, Scaling = StatScaling.Attack, CoefficientBp = coefficientBp,
            KeywordId = keyword, KeywordFactorBp = 10000, Target = target,
            Attack = new DamageAttackDefinition { HitCount = hitCount, Range = range } };

    private static CardEffectStep ApplyStatus(string id, EffectTargetScope target, CardEffectTiming timing,
        StatusRecipeDefinition recipe) => new CardEffectStep { Timing = timing,
            Effect = new EffectStepDefinition { Id = id, Kind = EffectKind.ApplyStatus, Target = target,
                StatusRecipe = recipe.Clone(), StatusDurationOverride = recipe.DefaultDuration } };

    private static StatusRecipeDefinition Status(string id, string name, StatusPolarity polarity, int turns,
        params StatModifierDefinition[] modifiers) => new StatusRecipeDefinition { Id = id, NameKey = name,
            Polarity = polarity, Behavior = StatusBehavior.Stat, DefaultDuration = turns,
            DurationClock = StatusDurationClock.TargetTurnEnd, Stacking = StatusStackingPolicy.RefreshDuration,
            MaxStacks = 1, Modifiers = new List<StatModifierDefinition>(modifiers) };

    private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition {
        Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
        Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth
            ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints };
}

