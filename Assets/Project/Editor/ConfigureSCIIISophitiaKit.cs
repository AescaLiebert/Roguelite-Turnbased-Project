using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class ConfigureSCIIISophitiaKit
{
    private const string Folder = "Assets/Project/Data/Character/Pre-Release_Phase/";
    private const string Id = "fighter.sciii-sophitia";
    private const string BannerPath = "Assets/Project/Data/Economy/SoulCaliburBannerConfiguration.asset";
    private const string SplashPath = "Assets/Project/Art/Character/SplashArt/sa-scii-og-sophitia.png";
    private const string IconPath = "Assets/Project/Art/Character/Icon/icon-scii-og-sophitia.png";

    [MenuItem("Fighting Allstar/Content/Configure SCII Sophitia Kit")]
    public static void Configure()
    {
        EnsureFolder("Assets/Project/Data/Character/Pre-Release_Phase");
        var character = LoadOrCreate<CharacterObject>(Folder + "sciii-sophitia.asset");
        var passive = LoadOrCreate<PassiveDefinitionSO>(Folder + "sciii-sophitia_Passive.asset");
        var skill1 = LoadOrCreate<SkillCardSO>(Folder + "sciii-sophitia_skilldata1.asset");
        var skill2 = LoadOrCreate<SkillCardSO>(Folder + "sciii-sophitia_skilldata2.asset");
        var ultimate = LoadOrCreate<UltimateCardSO>(Folder + "sciii-sophitia_ultdata1.asset");
        var banner = AssetDatabase.LoadAssetAtPath<SummonBannerConfigurationSO>(BannerPath);
        var splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var tauntVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/Stance.Taunt.asset");
        var critChanceVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/IncreaseCritChance.asset");
        var critDamageVisual = AssetDatabase.LoadAssetAtPath<StatusVisualData>("Assets/Project/Data/StatusData/IncreaseCritDamage.asset");
        if (banner == null || splash == null || icon == null || tauntVisual == null || critChanceVisual == null || critDamageVisual == null)
            throw new InvalidOperationException("Soul Calibur banner, Sophitia art and shared Taunt/Crit status visuals are required.");

        var definition = new PassiveDefinition { Id = Id + ".passive.source" };
        var blueStats = new PassiveAuraDefinition { Id = "blue-team-basic-stats",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Allies, IncludeOwner = true,
                IncludeReserve = true, AttributeId = "attribute.blue" } };
        blueStats.Modifiers.Add(new StatModifierDefinition { Target = ModifierTarget.StatBundle,
            Bundle = StatBundleKind.BasicStats, Operation = ModifierOperation.PercentOfBase, Amount = 1200 });
        definition.Auras.Add(blueStats);

        var soulStats = new PassiveAuraDefinition { Id = "soul-calibur-stat-bonus",
            Targets = new PassiveTargetFilter { Relation = PassiveRelation.Self } };
        soulStats.Modifiers.Add(Stat(StatId.MaxHealth, 1000));
        soulStats.Modifiers.Add(Stat(StatId.CritResistance, 1000));
        definition.Auras.Add(soulStats);

        var soulAttack = new PassiveReactionDefinition { Id = "soul-calibur-attack-bonus",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        soulAttack.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 1200 });
        definition.Reactions.Add(soulAttack);

        var nonCriticalBonus = new PassiveReactionDefinition { Id = "bonus-damage-on-non-critical-attack",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        nonCriticalBonus.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent,
            Amount = 1, ScalingStat = StatId.CritChance, ScalingCoefficientBp = 10000 });
        definition.Reactions.Add(nonCriticalBonus);

        var stanceChance = new[] { 700, 1000, 1300 };
        var stanceCritDamage = new[] { 1500, 1900, 2300 };
        for (var rank = 1; rank <= 3; rank++)
        {
            var stanceReaction = new PassiveReactionDefinition { Id = "taunt-hit-builds-critical-stats-r" + rank,
                Trigger = PassiveEventKind.DamageResolved, DamageTiming = PassiveDamageTiming.FinalHit,
                ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
                CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
            stanceReaction.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag,
                Tag = "sophitia.stance.rank." + rank });
            stanceReaction.Commands.Add(ApplyBuff("taunt-hit-crit-chance-r" + rank, "status.increase.crit-chance",
                "Increase Crit Chance", StatId.CritChance, stanceChance[rank - 1]));
            stanceReaction.Commands.Add(ApplyBuff("taunt-hit-crit-damage-r" + rank, "status.increase.crit-damage",
                "Increase Crit Damage", StatId.CritDamage, stanceCritDamage[rank - 1]));
            definition.Reactions.Add(stanceReaction);
        }
        var errors = PassiveRuleValidator.Validate(definition);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        passive.SetDefinition(definition);
        passive.statusVisuals = new List<StatusVisualData> { tauntVisual, critChanceVisual, critDamageVisual };
        EditorUtility.SetDirty(passive);

        skill1.cardName = "SCII Sophitia · Aegis Blade";
        skill1.cardIcon = icon;
        skill1.statusVisuals = new List<StatusVisualData>();
        skill1.ranks = new List<CardRankData>();
        var skill1Coefficients = new[] { 20000, 36000, 50000 };
        for (var i = 0; i < skill1Coefficients.Length; i++)
        {
            var rank = i + 1;
            var coefficient = skill1Coefficients[i];
            skill1.ranks.Add(new CardRankData { skillType = SkillType.Attack,
                description = $"Deals {coefficient / 100}% ATK Flood damage to all enemies in 2 hits.",
                runtimeEffect = Damage(Id + ".skill.1.rank." + rank, EffectTargetScope.AllEnemies,
                    coefficient, "attack.flood", 2, AttackRange.Close) });
        }

        skill2.cardName = "SCII Sophitia · Aegis Guard";
        skill2.cardIcon = icon;
        skill2.statusVisuals = new List<StatusVisualData> { tauntVisual, critChanceVisual, critDamageVisual };
        skill2.ranks = new List<CardRankData>();
        var stanceTurns = new[] { 1, 2, 2 };
        var critChance = new[] { 700, 1000, 1300 };
        var critDamage = new[] { 1500, 1900, 2300 };
        var statusDuration = 2;
        for (var i = 0; i < stanceTurns.Length; i++)
        {
            var rank = i + 1;
            var turns = stanceTurns[i];
            var chance = critChance[i];
            var damage = critDamage[i];
            var stance = new StatusRecipeDefinition { Id = "status.stance.taunt", NameKey = "Taunt",
                Polarity = StatusPolarity.Neutral, Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
                DefaultDuration = turns, DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.RefreshDuration, MaxStacks = 1,
                Tags = new List<string> { CombatTags.Stance, "status.stance.taunt", "sophitia.stance.rank." + rank } };
            var effect = new EffectDefinition { Id = Id + ".skill.2.rank." + rank,
                Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.Self,
                StatusRecipe = stance, StatusDurationOverride = turns };
            skill2.ranks.Add(new CardRankData { skillType = SkillType.Stance,
                description = $"Enters a Taunt stance for {turns} turn{(turns == 1 ? "" : "s")}. Each time Sophitia is attacked while taunting, she gains {chance / 100f:0.##}% Crit Chance and {damage / 100f:0.##}% Crit Damage for {statusDuration} turns; these buffs stack.",
                runtimeEffect = effect });
        }

        ultimate.ultimateName = "SCII Sophitia · Celestial Cross";
        ultimate.icon = icon;
        ultimate.statusVisuals = new List<StatusVisualData>();
        ultimate.levels = new List<UltimateLevelData>();
        for (var tier = 0; tier <= 5; tier++)
        {
            var coefficient = 40000 + tier * 6000;
            ultimate.levels.Add(new UltimateLevelData { level = tier, skillType = SkillType.Ultimate,
                description = $"Deals {coefficient / 100}% ATK Spike damage to all enemies.",
                runtimeEffect = Damage(Id + ".ultimate.c" + tier, EffectTargetScope.AllEnemies,
                    coefficient, "attack.spike", 1, AttackRange.Close) });
        }

        var data = new SerializedObject(character);
        Set(data, "definitionId", Id);
        Set(data, "runtimeReady", true);
        Set(data, "familyId", "family.sophitia");
        Set(data, "seriesId", "series.sc");
        Set(data, "role", "Defender");
        var traits = data.FindProperty("traitIds");
        traits.arraySize = 2;
        traits.GetArrayElementAtIndex(0).stringValue = "trait.female";
        traits.GetArrayElementAtIndex(1).stringValue = "trait.soul-calibur";
        Set(data, "passiveSourceDescription", "Increases the Basic Stats of Blue allies by 12%. When one of Sophitia's attacks does not crit, she deals additional damage based on her Crit Chance. Soul Calibur: own Max HP +10% and Crit Resistance +10 percentage points; ATK +12% only while attacking, without stacking or a lasting bonus.");
        Set(data, "passiveSourceType", (int)PassiveSourceType.OnField);
        Set(data, "passiveRestriction", (int)PassiveRestriction.All);
        Set(data, "passiveDefinition", passive);
        Set(data, "id", 2306);
        Set(data, "fighterName", "[Soul Calibur II] Sophitia");
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
            throw new InvalidOperationException("Sophitia's authored assets did not produce a complete runtime definition.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SCII Sophitia character, passive, ranked skills, C0-C5 Ultimate, and Soul Calibur banner configured.");
    }

    private static PassiveCommandDefinition ApplyBuff(string suffix, string id, string name, StatId stat, int amount) =>
        new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect, Effect = new CardEffectOperationDefinition {
            Id = Id + "." + suffix, Window = CardEffectWindow.AfterDamage, Kind = CardEffectOperationKind.ApplyStatus,
            Target = EffectTargetScope.TriggerTarget, Status = new StatusApplicationRecipe { StatusRecipeId = id,
                DurationOverride = 2, StackCount = 1,
                InlineRecipe = new StatusRecipeDefinition { Id = id, NameKey = name,
                    Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stat,
                    DefaultDuration = 2, DurationClock = StatusDurationClock.TargetTurnEnd,
                    Stacking = StatusStackingPolicy.AddStacks, MaxStacks = int.MaxValue,
                    Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition {
                        Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
                        Operation = ModifierOperation.PercentagePoints } } } } } };

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

    private static StatModifierDefinition Stat(StatId stat, int amount) => new StatModifierDefinition {
        Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
        Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth
            ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints };
}
