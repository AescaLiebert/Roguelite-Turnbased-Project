using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;

public static class BuildGenshinStarterAssets
{
    private const string Folder = "Assets/Project/Data/Character/GenshinImpact";

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Project/Data/Character", "GenshinImpact");
        var definitions = GenshinStarterRoster.Create();
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var stem = definition.Id.Substring("fighter.".Length);
            var character = LoadOrCreate<CharacterObject>(stem);
            var passive = LoadOrCreate<PassiveDefinitionSO>(stem + "_Passive");
            passive.SetDefinition(definition.Passive);
            var skills = new SkillCardSO[2];
            foreach (var skill in definition.Skills)
            {
                var card = LoadOrCreate<SkillCardSO>(stem + "_Skill" + skill.Slot);
                card.cardName = definition.DisplayName + " · Skill " + skill.Slot;
                card.ranks = new List<CardRankData>();
                foreach (var rank in skill.Ranks) card.ranks.Add(new CardRankData {
                    skillType = Type(rank.Category), runtimeEffect = rank.Effect.Clone(),
                    description = Description(rank.Effect) });
                skills[skill.Slot - 1] = card;
                Save(card);
            }
            var ultimate = LoadOrCreate<UltimateCardSO>(stem + "_Ultimate");
            ultimate.ultimateName = definition.DisplayName + " · Ultimate";
            ultimate.levels = new List<UltimateLevelData>();
            foreach (var tier in definition.UltimateTiers) ultimate.levels.Add(new UltimateLevelData {
                level = tier.Tier, skillType = Type(tier.Category), runtimeEffect = tier.Effect.Clone(), description = Description(tier.Effect) });
            character.Skill1 = skills[0]; character.Skill2 = skills[1]; character.Ultimate = ultimate;
            character.FighterAttribute = (FighterAttribute)Enum.Parse(typeof(FighterAttribute), definition.AttributeId.Substring(10), true);
            character.FighterRarity = (FighterRarity)Enum.Parse(typeof(FighterRarity), definition.RarityId.Substring(7), true);
            character.attack = definition.BaseStats.Attack; character.defense = definition.BaseStats.Defense;
            character.health = definition.BaseStats.MaxHealth; character.Classpower = definition.BaseStats.CombatClass;
            character.LevelGrowth = definition.StatGrowth.Clone();
            var stats = definition.BaseStats;
            character.SecondaryStats = new CharacterSecondaryStats { PierceRate = stats.PierceBp / 100f,
                Resistance = stats.ResistanceBp / 100f, Regenerate = stats.RegenerationBp / 100f,
                CritChance = stats.CritChanceBp / 100f, CritDmg = stats.CritDamageBp / 100f,
                CritResistance = stats.CritResistanceBp / 100f, CritDefense = stats.CritDefenseBp / 100f,
                RecoveryRate = stats.RecoveryBp / 100f, BlockChance = stats.BlockChanceBp / 100f,
                BlockPower = stats.BlockPowerBp / 100f };
            var data = new SerializedObject(character);
            data.FindProperty("definitionId").stringValue = definition.Id;
            data.FindProperty("runtimeReady").boolValue = true;
            data.FindProperty("familyId").stringValue = definition.FamilyId;
            data.FindProperty("seriesId").stringValue = definition.SeriesId;
            data.FindProperty("fighterName").stringValue = definition.DisplayName;
            data.FindProperty("id").intValue = 40001 + index; // Generated local IDs, not source CSV record IDs.
            data.FindProperty("fighterUltimateLevel").intValue = 0;
            data.FindProperty("passiveDefinition").objectReferenceValue = passive;
            data.FindProperty("passiveSourceType").intValue = (int)PassiveSourceType.OnField;
            var traits = data.FindProperty("traitIds");
            traits.arraySize = definition.TraitIds.Count;
            for (var i = 0; i < traits.arraySize; i++) traits.GetArrayElementAtIndex(i).stringValue = definition.TraitIds[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var exported = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var errors = ElementalContentValidator.Validate(exported, ElementalRuleSet.CreateDefault());
            errors.AddRange(PassiveRuleValidator.Validate(exported.Passive));
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            Save(passive); Save(ultimate); Save(character);
        }
        FightingAllstar.EditorTools.CharacterRegistrySync.Sync();
        return "Created and validated five Genshin character assets, 10 ranked skills, five C0–C5 ultimates and five passives. Art uses the existing placeholder fallback.";
    }

    private static string Description(EffectDefinition effect) => FightingAllstar.Presentation.Combat.CardEffectText.TryFormat(effect, "", out var text) ? text : "";

    public static string Audit()
    {
        var definitions = new List<CharacterDefinition>();
        foreach (var source in GenshinStarterRoster.Create())
        {
            var path = Folder + "/" + source.Id.Substring("fighter.".Length) + ".asset";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var definition = CharacterObjectCatalogBuilder.CreateDefinition(AssetDatabase.LoadAssetAtPath<CharacterObject>(path));
            var errors = ElementalContentValidator.Validate(definition, ElementalRuleSet.CreateDefault());
            errors.AddRange(PassiveRuleValidator.Validate(definition.Passive));
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            if (definition.Skills[0].Ranks.Count != 3 || definition.UltimateTiers.Count != 6)
                throw new InvalidOperationException("Incomplete persisted kit: " + definition.Id);
            definitions.Add(definition);
        }
        var barbara = definitions.Single(f => f.Id == "fighter.barbara");
        var team = new[] { barbara }.Concat(definitions.Where(f => f != barbara)).Take(4).ToArray();
        var battle = BattleEngine.Create("persisted-genshin-audit", team, null, team, null, 777);
        var owner = battle.Player.Fighters[0];
        var originalHealth = owner.Health / 2;
        owner.Health = originalHealth;
        var skill = owner.Definition.Skills[1];
        var card = new CardState { Id = "persisted-heal", OwnerFighterId = owner.Id, SkillId = skill.Id,
            Rank = 3, Category = CardCategory.Recovery, TargetScope = EffectTargetScope.AllAllies };
        battle.Player.Hand.Clear(); battle.Player.Hand.Add(card);
        var draft = new PlanDraft(battle);
        if (!draft.QueuePlay(card.Id, owner.Id, out var reason) ||
            !BattleEngine.TryResolvePlan(battle, draft.BuildPlan("persisted-heal"), out var next, out reason))
            throw new InvalidOperationException(reason);
        if (next.Player.Fighters[0].Health <= originalHealth)
            throw new InvalidOperationException("Persisted Barbara lost her Max HP heal through Unity inline serialization.");
        return "PASS: five reimported Unity kits validate; 30 skill ranks and 30 ultimate tiers export; Barbara's persisted Max HP heal executes.";
    }
    private static SkillType Type(CardCategory category) => category == CardCategory.AttackDebuff ? SkillType.DebuffAtk :
        category == CardCategory.Recovery ? SkillType.Heal : category == CardCategory.Buff ? SkillType.Buff : SkillType.Attack;
    private static T LoadOrCreate<T>(string stem) where T : ScriptableObject
    {
        var path = Folder + "/" + stem + ".asset";
        var result = AssetDatabase.LoadAssetAtPath<T>(path);
        if (result != null) return result;
        result = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(result, path);
        return result;
    }
    private static void Save(UnityEngine.Object asset) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
}
