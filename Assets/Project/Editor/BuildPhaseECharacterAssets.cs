using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Combat;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class BuildPhaseECharacterAssets
{
    private const string DraftCatalogPath = "Assets/Project/Content/WipCharacterCatalog.json";
    private const string CharacterOutput = "Assets/Resources/Character_WIP-Phase";
    private const string CardOutput = "Assets/Project/Content/Authoring/WipPhaseCharacterCards";
    private const string PublishedCatalogOutput = "Assets/Project/Content/Generated/wip-phase-character-catalog.json";
    private const string ModelPath = "Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab";
    private static readonly string[] PhaseEIds =
    {
        "fighter.kyo94", "fighter.chin94", "fighter.kensou94", "fighter.king94",
        "fighter.mai94", "fighter.shingo97", "fighter.benimaru94", "fighter.athena94"
    };

    [MenuItem("Fighting Allstar/Content/Build WIP Phase Character Assets")]
    public static void Build()
    {
        if (!File.Exists(DraftCatalogPath)) throw new FileNotFoundException("The preserved source catalog is missing.", DraftCatalogPath);
        EnsureFolder(CharacterOutput);
        EnsureFolder(CardOutput);

        var draft = JsonUtility.FromJson<ContentCatalog>(File.ReadAllText(DraftCatalogPath));
        if (draft == null || draft.Characters == null) throw new InvalidOperationException("The source character catalog could not be read.");
        var definitions = new Dictionary<string, CharacterObject>(StringComparer.Ordinal);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        foreach (var id in PhaseEIds)
        {
            var source = draft.Characters.Find(character => character != null && character.Id == id);
            if (source == null) throw new InvalidOperationException("Source character is missing: " + id);
            definitions.Add(id, BuildCharacter(source, model));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built eight WIP Phase Character definitions from the WIP catalog: " + string.Join(", ", PhaseEIds));
    }

    [MenuItem("Fighting Allstar/Content/Migrate Phase E Effects Into WIP Character Catalog")]
    public static void MigrateRuntimeContentIntoWipCatalog()
    {
        if (!File.Exists(DraftCatalogPath)) throw new FileNotFoundException("The WIP character catalog is missing.", DraftCatalogPath);
        var catalog = JsonUtility.FromJson<ContentCatalog>(File.ReadAllText(DraftCatalogPath));
        if (catalog?.Characters == null) throw new InvalidOperationException("The WIP character catalog could not be read.");
        foreach (var id in PhaseEIds)
        {
            var character = catalog.Characters.Find(item => item != null && item.Id == id);
            if (character == null) throw new InvalidOperationException("WIP character is missing: " + id);
            if (character.Passive == null || string.IsNullOrWhiteSpace(character.Passive.Id))
                character.Passive = StandardCharacterPassives.Create(id);
            foreach (var skill in character.Skills)
            {
                skill.Category = ResolvePhaseECategory(character, skill);
                skill.TargetScope = ResolvePhaseETargetScope(character, skill);
                foreach (var rank in skill.Ranks)
                {
                    if (rank.Effect != null && (rank.Provenance != SourceProvenance.Proposal ||
                        rank.Effect.Provenance != SourceProvenance.Proposal)) continue;
                    rank.Effect = CreatePhaseESkillEffect(character, skill, rank.Rank);
                    rank.Provenance = SourceProvenance.Proposal;
                }
            }
            foreach (var tier in character.UltimateTiers)
                if (tier.Effect == null || (tier.Provenance == SourceProvenance.Proposal &&
                    tier.Effect.Provenance == SourceProvenance.Proposal))
                {
                    tier.Effect = CreatePhaseEUltimateEffect(character, tier.Tier);
                    tier.Provenance = SourceProvenance.Proposal;
                }
        }
        catalog.StatusRecipes = catalog.StatusRecipes != null && catalog.StatusRecipes.Count > 0
            ? catalog.StatusRecipes : StandardEffectDatabase.CreateStatusRecipes();
        catalog.AttackEffectRecipes = catalog.AttackEffectRecipes != null && catalog.AttackEffectRecipes.Count > 0
            ? catalog.AttackEffectRecipes : StandardEffectDatabase.CreateAttackEffects();
        catalog.ContentVersion = "wip-phase-character-content-v2";
        catalog.ContentHash = string.Empty;
        var canonical = JsonUtility.ToJson(catalog);
        using (var sha = SHA256.Create())
            catalog.ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty).ToLowerInvariant();
        File.WriteAllText(DraftCatalogPath, JsonUtility.ToJson(catalog, true));
        AssetDatabase.ImportAsset(DraftCatalogPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("Migrated eight Phase E kits, skill-rank effects, status recipes, attack recipes, and Core passives into WipCharacterCatalog.json.");
    }

    private static CardCategory ResolvePhaseECategory(CharacterDefinition character, SkillDefinition skill)
    {
        var type = (skill.SourceType ?? string.Empty).Trim();
        if (string.Equals(type, "Heal", StringComparison.OrdinalIgnoreCase)) return CardCategory.Recovery;
        if (string.Equals(type, "Buff", StringComparison.OrdinalIgnoreCase)) return CardCategory.Buff;
        if (string.Equals(type, "Stance", StringComparison.OrdinalIgnoreCase)) return CardCategory.Stance;
        if (string.Equals(type, "DebuffAtk", StringComparison.OrdinalIgnoreCase)) return CardCategory.AttackDebuff;
        if (string.Equals(type, "Debuff", StringComparison.OrdinalIgnoreCase) && SkillMultiplier(character.Id, skill.Slot, 1) > 0)
            return CardCategory.AttackDebuff;
        return CardCategory.Attack;
    }

    private static EffectTargetScope ResolvePhaseETargetScope(CharacterDefinition character, SkillDefinition skill)
    {
        if (string.Equals(skill.SourceTarget, "AOE", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.AllEnemies;
        if (string.Equals(skill.SourceTarget, "Self", StringComparison.OrdinalIgnoreCase)) return EffectTargetScope.Self;
        if (string.Equals(skill.SourceTarget, "AllAllies", StringComparison.OrdinalIgnoreCase) ||
            character.Id == "fighter.kensou94" && skill.Slot == 2 ||
            character.Id == "fighter.athena94" && skill.Slot == 2) return EffectTargetScope.AllAllies;
        return EffectTargetScope.SelectedEnemy;
    }

    private static EffectDefinition CreatePhaseESkillEffect(CharacterDefinition character, SkillDefinition skill, int rank)
    {
        var authored = AuthorSkillEffects(character.Id, skill.Slot, rank);
        var scope = ResolvePhaseETargetScope(character, skill);
        var multiplier = SkillMultiplier(character.Id, skill.Slot, rank);
        if (string.Equals(skill.SourceType, "Heal", StringComparison.OrdinalIgnoreCase))
        {
            var heal = authored.Find(item => item.kind == CharacterCardEffectKind.HealAttackMultiplier ||
                item.kind == CharacterCardEffectKind.HealMissingHealthPercent || item.kind == CharacterCardEffectKind.HealMaxHealthPercent);
            if (heal != null)
            {
                authored.Remove(heal);
                var rootHeal = ConvertPhaseEUtility(heal, scope);
                AppendUtilitySequence(rootHeal, authored, CardEffectTiming.AfterAction, scope);
                return rootHeal;
            }
        }
        var effect = new EffectDefinition { Kind = EffectKind.Damage, Family = DamageFamily.Normal,
            Scaling = StatScaling.Attack, CoefficientBp = Mathf.RoundToInt(multiplier * 10000f),
            KeywordFactorBp = 10000, KeywordId = ResolveAttackKeyword(skill.SourceEffectTags),
            Target = ScopeName(scope), Provenance = SourceProvenance.Proposal };
        AppendUtilitySequence(effect, authored, CardEffectTiming.AfterDamage, scope);
        return effect;
    }

    private static EffectDefinition CreatePhaseEUltimateEffect(CharacterDefinition character, int tier)
    {
        var authored = AuthorUltimateEffects(character.Id, tier);
        if (character.Id == "fighter.athena94")
        {
            var area = new EffectDefinition { Kind = EffectKind.Damage, Family = DamageFamily.Normal,
                Scaling = StatScaling.Attack, CoefficientBp = Mathf.RoundToInt(UltimateMultiplier(character.Id, tier) * 10000f),
                Target = "AllEnemies", KeywordId = "ultimate", Provenance = SourceProvenance.Proposal };
            AppendUtilitySequence(area, authored, CardEffectTiming.AfterAction, EffectTargetScope.AllAllies);
            return area;
        }
        var scope = character.Id == "fighter.kensou94" ? EffectTargetScope.SelectedAlly : EffectTargetScope.SelectedEnemy;
        var multiplier = UltimateMultiplier(character.Id, tier);
        var heal = authored.Find(item => item.kind == CharacterCardEffectKind.HealAttackMultiplier ||
            item.kind == CharacterCardEffectKind.HealMissingHealthPercent || item.kind == CharacterCardEffectKind.HealMaxHealthPercent);
        EffectDefinition effect;
        if (heal != null)
        {
            authored.Remove(heal);
            effect = ConvertPhaseEUtility(heal, scope);
            AppendUtilitySequence(effect, authored, CardEffectTiming.AfterAction, scope);
        }
        else
        {
            effect = new EffectDefinition { Kind = EffectKind.Damage, Family = DamageFamily.Normal,
                Scaling = StatScaling.Attack, CoefficientBp = Mathf.RoundToInt(multiplier * 10000f),
                KeywordId = "ultimate", Target = ScopeName(scope), Provenance = SourceProvenance.Proposal };
            AppendUtilitySequence(effect, authored, CardEffectTiming.AfterDamage, scope);
        }
        return effect;
    }

    private static void AppendUtilitySequence(EffectDefinition root, List<CharacterCardEffect> authored,
        CardEffectTiming timing, EffectTargetScope defaultScope)
    {
        foreach (var source in authored)
        {
            var effect = ConvertPhaseEUtility(source, defaultScope);
            if (effect != null) root.Sequence.Add(new CardEffectStep { Timing = timing, Effect = effect });
        }
    }

    private static EffectDefinition ConvertPhaseEUtility(CharacterCardEffect source, EffectTargetScope defaultScope)
    {
        if (source == null) return null;
        var target = source.targetType == SkillTargetType.AllAllies ? EffectTargetScope.AllAllies :
            source.targetType == SkillTargetType.AOE ? EffectTargetScope.AllEnemies :
            source.targetType == SkillTargetType.Self ? EffectTargetScope.Self : defaultScope;
        var result = new EffectDefinition { Target = ScopeName(target), StatusStackCount = Math.Max(1, source.stackCount),
            StatusDurationOverride = Math.Max(0, source.durationTurns), Provenance = SourceProvenance.Proposal };
        switch (source.kind)
        {
            case CharacterCardEffectKind.ApplyStatus:
                result.Kind = EffectKind.ApplyStatus;
                result.StatusRecipe = CreatePhaseEStatusRecipe(source.statusId, source.stackCap);
                break;
            case CharacterCardEffectKind.HealAttackMultiplier:
                result.Kind = EffectKind.Heal;
                result.HealValue = new EffectValueDefinition { Source = EffectValueSource.SourceAttack,
                    CoefficientBp = Mathf.RoundToInt(source.magnitude * 10000f) };
                break;
            case CharacterCardEffectKind.HealMissingHealthPercent:
                result.Kind = EffectKind.Heal;
                result.HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMissingHealth,
                    CoefficientBp = Mathf.RoundToInt(source.magnitude * 100f) };
                break;
            case CharacterCardEffectKind.HealMaxHealthPercent:
                result.Kind = EffectKind.Heal;
                result.HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth,
                    CoefficientBp = Mathf.RoundToInt(source.magnitude * 100f) };
                break;
            case CharacterCardEffectKind.CleanseDebuffs:
                result.Kind = EffectKind.Cleanse;
                break;
            case CharacterCardEffectKind.RemoveBuffs:
                result.Kind = EffectKind.RemoveBuffs;
                break;
            case CharacterCardEffectKind.DisableCardType:
                result.Kind = EffectKind.ApplyStatus;
                result.StatusRecipe = DisableCardRecipe(source.disabledCardType);
                break;
            case CharacterCardEffectKind.DrainPowerGauge:
                result.Kind = EffectKind.ChangePowerGauge;
                result.PowerGaugeAmount = -Mathf.RoundToInt(source.magnitude);
                break;
            case CharacterCardEffectKind.ModifyStat:
                result.Kind = EffectKind.ApplyStatus;
                result.StatusRecipe = StatCardRecipe(source.statId, source.magnitude);
                break;
            case CharacterCardEffectKind.IncreaseCardRank:
                result.Kind = EffectKind.ModifyCardRank;
                result.Magnitude = 1;
                break;
            default:
                return null;
        }
        return result;
    }

    private static StatusRecipeDefinition CreatePhaseEStatusRecipe(string statusId, int stackCap)
    {
        var normalized = (statusId ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized == "ignite")
            return StandardEffectDatabase.CreateStatusRecipes().Find(item => item.Id == "status.debuff.ignite");
        if (normalized == "poison")
            return StandardEffectDatabase.CreateStatusRecipes().Find(item => item.Id == "status.debuff.poison");
        if (normalized == "paralyze")
            return StandardEffectDatabase.CreateStatusRecipes().Find(item => item.Id == "status.debuff.paralyze");
        var polarity = normalized == "debuffimmunity" || normalized == "rejuvenation"
            ? StatusPolarity.Buff : StatusPolarity.Debuff;
        var maxStacks = normalized == "rejuvenation" ? 3 : Math.Max(1, stackCap);
        var recipe = new StatusRecipeDefinition { Id = "status." + polarity.ToString().ToLowerInvariant() + "." + normalized,
            Polarity = polarity, Behavior = StatusBehavior.Stat,
            Stacking = maxStacks > 1 ? StatusStackingPolicy.AddStacks : StatusStackingPolicy.RefreshStronger,
            MaxStacks = maxStacks, DefaultDuration = 2,
            Tags = new List<string> { "status." + normalized } };
        if (normalized == "debuffimmunity") recipe.DebuffImmunity = true;
        if (normalized == "rejuvenation")
        {
            recipe.Color = StatusColor.Blue;
            recipe.Modifiers.Add(new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Regeneration,
                Operation = ModifierOperation.PercentagePoints, Amount = 1000 });
        }
        return recipe;
    }

    private static StatusRecipeDefinition DisableCardRecipe(SkillType disabledType)
    {
        var mask = disabledType switch
        {
            SkillType.Attack => CardCategoryMask.Attack,
            SkillType.Buff => CardCategoryMask.Buff,
            SkillType.Debuff => CardCategoryMask.Debuff,
            SkillType.DebuffAtk => CardCategoryMask.Attack | CardCategoryMask.Debuff,
            SkillType.Heal => CardCategoryMask.Recovery,
            SkillType.Stance => CardCategoryMask.Stance | CardCategoryMask.ReceiveStances,
            _ => CardCategoryMask.None
        };
        return new StatusRecipeDefinition { Id = "status.debuff.disable-" + disabledType.ToString().ToLowerInvariant(),
            Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.Disable, DisableMask = mask,
            DefaultDuration = 1, Tags = new List<string> { "status.disable" } };
    }

    private static StatusRecipeDefinition StatCardRecipe(string statName, float magnitude)
    {
        if (!Enum.TryParse(ContentAliases.NormalizeStat(statName), true, out StatId stat)) return null;
        var statBase = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth;
        var amount = Mathf.RoundToInt(magnitude * 100f);
        return new StatusRecipeDefinition { Id = "status." + (magnitude < 0 ? "debuff" : "buff") + "." + stat.ToString().ToLowerInvariant(),
            Polarity = magnitude < 0 ? StatusPolarity.Debuff : StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat, DefaultDuration = 2, Tags = new List<string> { "status.stat" },
            Modifiers = { new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = stat,
                Operation = statBase ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints,
                Amount = amount } } };
    }

    private static string ResolveAttackKeyword(string sourceTags)
    {
        if (string.IsNullOrWhiteSpace(sourceTags)) return string.Empty;
        var available = StandardEffectDatabase.CreateAttackEffects();
        var tokens = sourceTags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var candidate = "attack." + token.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
            if (available.Exists(item => item.Id == candidate)) return candidate;
        }
        return string.Empty;
    }

    private static string ScopeName(EffectTargetScope scope) => scope switch
    {
        EffectTargetScope.Self => "Self",
        EffectTargetScope.SelectedAlly => "SelectedAlly",
        EffectTargetScope.AllAllies => "AllAllies",
        EffectTargetScope.AllEnemies => "AllEnemies",
        _ => "SelectedEnemy"
    };

    [MenuItem("Fighting Allstar/Content/Build WIP Phase Character Runtime Catalog")]
    public static void BuildRuntimeCatalog()
    {
        if (!File.Exists(DraftCatalogPath)) throw new FileNotFoundException("The preserved source catalog is missing.", DraftCatalogPath);
        EnsureFolder("Assets/Project/Content/Generated");
        var source = JsonUtility.FromJson<ContentCatalog>(File.ReadAllText(DraftCatalogPath));
        if (source == null || source.Characters == null) throw new InvalidOperationException("The source character catalog could not be read.");
        var published = new ContentCatalog
        {
            SchemaVersion = source.SchemaVersion,
            ContentVersion = "wip-phase-character-v2",
            Characters = new List<CharacterDefinition>(),
            StatusRecipes = source.StatusRecipes != null && source.StatusRecipes.Count > 0
                ? source.StatusRecipes : StandardEffectDatabase.CreateStatusRecipes(),
            AttackEffectRecipes = source.AttackEffectRecipes != null && source.AttackEffectRecipes.Count > 0
                ? source.AttackEffectRecipes : StandardEffectDatabase.CreateAttackEffects(),
            CardEffectRecipes = source.CardEffectRecipes
        };
        foreach (var id in PhaseEIds)
        {
            var character = source.Characters.Find(item => item != null && item.Id == id);
            if (character == null) throw new InvalidOperationException("Source character is missing: " + id);
            var playable = character.Clone();
            if (playable.Passive == null || string.IsNullOrWhiteSpace(playable.Passive.Id))
                playable.Passive = StandardCharacterPassives.Create(playable.Id);
            playable.RuntimeReady = true;
            foreach (var skill in playable.Skills)
            {
                skill.Category = CardRules.ResolveCategory(skill);
                skill.TargetScope = CardRules.ResolveTargetScope(skill);
                foreach (var rank in skill.Ranks)
                {
                    if (rank.Effect == null)
                    {
                        rank.Effect = new EffectDefinition
                        {
                            Family = DamageFamily.Normal,
                            Scaling = StatScaling.Attack,
                            CoefficientBp = Mathf.RoundToInt(SkillMultiplier(playable.Id, skill.Slot, rank.Rank) * 10000f),
                            KeywordId = skill.SourceEffectTags,
                            Target = string.IsNullOrWhiteSpace(skill.SourceTarget) ? "SelectedEnemy" : skill.SourceTarget,
                            Provenance = SourceProvenance.Proposal
                        };
                        rank.Provenance = SourceProvenance.Proposal;
                    }
                }
            }
            foreach (var tier in playable.UltimateTiers)
            {
                if (tier.Effect == null)
                {
                    tier.Effect = new EffectDefinition
                    {
                        Family = DamageFamily.Normal,
                        Scaling = StatScaling.Attack,
                        CoefficientBp = Mathf.RoundToInt(UltimateMultiplier(playable.Id, tier.Tier) * 10000f),
                        KeywordId = "ultimate",
                        Target = "SelectedEnemy",
                        Provenance = SourceProvenance.Proposal
                    };
                    tier.Provenance = SourceProvenance.Proposal;
                }
            }
            published.Characters.Add(playable);
        }

        var errors = ContentValidator.Validate(published);
        if (errors.Count > 0) throw new InvalidOperationException("The WIP Phase Character catalog is invalid: " + string.Join("; ", errors));
        published.ContentHash = string.Empty;
        var canonical = JsonUtility.ToJson(published);
        using (var sha = SHA256.Create())
            published.ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", string.Empty).ToLowerInvariant();
        File.WriteAllText(PublishedCatalogOutput, JsonUtility.ToJson(published, true));
        AssetDatabase.ImportAsset(PublishedCatalogOutput, ImportAssetOptions.ForceUpdate);
        var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(PublishedCatalogOutput);
        foreach (var sourceGuid in AssetDatabase.FindAssets("t:CombatContentSource"))
        {
            var sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuid);
            var sourceAsset = AssetDatabase.LoadAssetAtPath<CombatContentSource>(sourcePath);
            if (sourceAsset == null) continue;
            var serializedSource = new SerializedObject(sourceAsset);
            var catalogProperty = serializedSource.FindProperty("catalogJson");
            if (catalogProperty == null) continue;
            catalogProperty.objectReferenceValue = catalogAsset;
            serializedSource.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sourceAsset);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Published eight runtime-ready WIP Phase Character definitions to " + PublishedCatalogOutput + ".");
    }

    private static CharacterObject BuildCharacter(CharacterDefinition source, GameObject model)
    {
        var safeName = source.Id.Replace("fighter.", string.Empty);
        var path = CharacterOutput + "/" + safeName + ".asset";
        var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(path);
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<CharacterObject>();
            AssetDatabase.CreateAsset(character, path);
        }

        var serialized = new SerializedObject(character);
        Set(serialized, "definitionId", source.Id);
        Set(serialized, "runtimeReady", true);
        Set(serialized, "sourceId", ParseSourceId(source.SourceId));
        Set(serialized, "sourceRecord", source.SourceRecord);
        Set(serialized, "familyId", source.FamilyId);
        Set(serialized, "seriesId", source.SeriesId);
        Set(serialized, "role", source.Role);
        Set(serialized, "traitIds", source.TraitIds == null ? Array.Empty<string>() : source.TraitIds.ToArray());
        Set(serialized, "passiveSourceDescription", source.PassiveSource == null ? string.Empty : source.PassiveSource.Description);
        Set(serialized, "passiveSourceType", source.PassiveSource == null ? string.Empty : source.PassiveSource.SourceType);
        Set(serialized, "passiveRestriction", source.PassiveSource == null ? string.Empty : source.PassiveSource.Restriction);
        Set(serialized, "id", 1000 + ParseSourceId(source.SourceId));
        Set(serialized, "fighterName", source.DisplayName);
        Set(serialized, "fighterTag", source.FamilyId);
        Set(serialized, "FighterAttribute", ParseAttribute(source.AttributeId));
        Set(serialized, "FighterRarity", ParseRarity(source.RarityId));
        Set(serialized, "fighterLevel", 1);
        Set(serialized, "fighterAwakening", 0);
        Set(serialized, "fighterUltimateLevel", 0);
        Set(serialized, "Classpower", (float)source.BaseStats.CombatClass);
        Set(serialized, "attack", (float)source.BaseStats.Attack);
        Set(serialized, "defense", (float)source.BaseStats.Defense);
        Set(serialized, "health", (float)source.BaseStats.MaxHealth);
        Set(serialized, "fighter3DPrefab", model);
        Set(serialized, "fighterPic", LoadPortrait(source.Id));
        Set(serialized, "fighterIcon", LoadIcon(source.Id));
        SetSecondaryStats(serialized.FindProperty("SecondaryStats"), source.BaseStats);
        SetPassive(serialized.FindProperty("passiveEffect"), source);

        var skill1 = BuildSkill(source, 1);
        var skill2 = BuildSkill(source, 2);
        var ultimate = BuildUltimate(source);
        Set(serialized, "Skill1", skill1);
        Set(serialized, "Skill2", skill2);
        Set(serialized, "Ultimate", ultimate);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
        return character;
    }

    private static SkillCardSO BuildSkill(CharacterDefinition character, int slot)
    {
        var skill = character.Skills.FirstOrDefault(item => item != null && item.Slot == slot);
        if (skill == null || skill.Ranks == null || skill.Ranks.Count != 3)
            throw new InvalidOperationException(character.Id + " is missing a complete skill slot " + slot + ".");
        var path = CardOutput + "/" + character.Id.Replace("fighter.", string.Empty) + "_Skill" + slot + ".asset";
        var card = LoadOrCreate<SkillCardSO>(path);
        card.cardName = character.DisplayName + " · Skill " + slot;
        card.cardIcon = LoadIcon(character.Id);
        card.ranks = new List<CardRankData>();
        foreach (var rank in skill.Ranks.OrderBy(item => item.Rank))
        {
            var cardRank = new CardRankData
            {
                rankLevel = rank.Rank,
                description = rank.SourceDescription,
                sourceDescription = rank.SourceDescription,
                provenance = rank.Provenance.ToString(),
                targetType = ParseTarget(skill.SourceTarget),
                skillType = ParseSkillType(skill.SourceType),
                damageScalingType = DamageScalingType.ATK,
                damageMultiplier = SkillMultiplier(character.Id, slot, rank.Rank),
                damageKeyword = skill.SourceEffectTags
            };
            cardRank.effects = AuthorSkillEffects(character.Id, slot, rank.Rank);
            card.ranks.Add(cardRank);
        }
        EditorUtility.SetDirty(card);
        return card;
    }

    private static UltimateCardSO BuildUltimate(CharacterDefinition character)
    {
        if (character.UltimateTiers == null || character.UltimateTiers.Count != 7)
            throw new InvalidOperationException(character.Id + " is missing C0-C6 ultimate data.");
        var path = CardOutput + "/" + character.Id.Replace("fighter.", string.Empty) + "_Ultimate.asset";
        var card = LoadOrCreate<UltimateCardSO>(path);
        card.ultimateName = character.DisplayName + " · Ultimate";
        card.icon = LoadIcon(character.Id);
        card.levels = new List<UltimateLevelData>();
        foreach (var tier in character.UltimateTiers.OrderBy(item => item.Tier))
        {
            var data = new UltimateLevelData
            {
                level = tier.Tier,
                description = tier.SourceDescription,
                sourceDescription = tier.SourceDescription,
                provenance = tier.Provenance.ToString(),
                targetType = character.Id == "fighter.athena94" ? SkillTargetType.AllAllies : SkillTargetType.Single,
                damageScalingType = DamageScalingType.ATK,
                damageMultiplier = UltimateMultiplier(character.Id, tier.Tier)
            };
            data.effects = AuthorUltimateEffects(character.Id, tier.Tier);
            card.levels.Add(data);
        }
        EditorUtility.SetDirty(card);
        return card;
    }

    private static void SetSecondaryStats(SerializedProperty property, StatBlock stats)
    {
        Set(property, "PierceRate", FromBasisPoints(stats.PierceBp));
        Set(property, "Resistance", FromBasisPoints(stats.ResistanceBp));
        Set(property, "Regenerate", FromBasisPoints(stats.RegenerationBp));
        Set(property, "CritChance", FromBasisPoints(stats.CritChanceBp));
        Set(property, "CritDmg", FromBasisPoints(stats.CritDamageBp));
        Set(property, "CritResistance", FromBasisPoints(stats.CritResistanceBp));
        Set(property, "CritDefense", FromBasisPoints(stats.CritDefenseBp));
        Set(property, "RecoveryRate", FromBasisPoints(stats.RecoveryBp));
        Set(property, "BlockChance", FromBasisPoints(stats.BlockChanceBp));
        Set(property, "BlockPower", FromBasisPoints(stats.BlockPowerBp));
        Set(property, "LifeSteal", FromBasisPoints(stats.LifeStealBp));
        Set(property, "AvoidanceRate", FromBasisPoints(stats.AvoidanceBp));
        Set(property, "EvadeRate", FromBasisPoints(stats.EvadeBp));
        Set(property, "ControlRate", FromBasisPoints(stats.ControlBp));
        Set(property, "PerceptionRate", FromBasisPoints(stats.PerceptionBp));
    }

    private static void SetPassive(SerializedProperty property, CharacterDefinition character)
    {
        var effect = property.FindPropertyRelative("kind");
        var source = character.PassiveSource;
        if (source == null) return;
        if (character.Id == "fighter.kyo94")
        {
            SetEnum(effect, CharacterPassiveKind.AttackPerStatusStack);
            Set(property, "statusId", "Ignite"); Set(property, "statId", "Attack");
            Set(property, "magnitudePercent", 3f); Set(property, "maximumStacks", 10);
        }
        else if (character.Id == "fighter.mai94")
        {
            SetEnum(effect, CharacterPassiveKind.EnemyStatModifier);
            Set(property, "statId", "RecoveryRate"); Set(property, "magnitudePercent", -FromBasisPoints(character.BaseStats.RegenerationBp));
        }
        else if (character.Id == "fighter.king94")
        {
            SetEnum(effect, CharacterPassiveKind.EndTurnTeamStatStack);
            Set(property, "statId", "PierceRate"); Set(property, "magnitudePercent", 8f); Set(property, "maximumStacks", 5); Set(property, "turnsPerStack", 1);
        }
        else if (character.Id == "fighter.benimaru94")
        {
            SetEnum(effect, CharacterPassiveKind.TeamStatModifier);
            Set(property, "statId", "Attack"); Set(property, "attributeFilter", "Red"); Set(property, "magnitudePercent", 10f);
        }
        else if (character.Id == "fighter.athena94")
        {
            SetEnum(effect, CharacterPassiveKind.TeamStatModifier);
            Set(property, "statId", "Attack"); Set(property, "attributeFilter", "Women"); Set(property, "magnitudePercent", 15f);
        }
        else if (character.Id == "fighter.chin94")
        {
            SetEnum(effect, CharacterPassiveKind.TeamStatModifier);
            Set(property, "statId", "MaxHealth"); Set(property, "attributeFilter", "Green"); Set(property, "magnitudePercent", 20f);
        }
        else if (character.Id == "fighter.kensou94")
        {
            SetEnum(effect, CharacterPassiveKind.TeamStatModifier);
            Set(property, "statId", "RecoveryRate"); Set(property, "magnitudePercent", 40f);
        }
        else if (character.Id == "fighter.shingo97")
        {
            SetEnum(effect, CharacterPassiveKind.GaugeRestoreFromEnemyDrain);
            Set(property, "statId", "PowerGauge");
        }
    }

    private static List<CharacterCardEffect> AuthorSkillEffects(string id, int slot, int rank)
    {
        var duration = rank == 1 ? 1 : 2;
        var count = rank;
        var effects = new List<CharacterCardEffect>();
        if (id == "fighter.chin94" && slot == 1) effects.Add(Status("Ignite", count, rank));
        if (id == "fighter.chin94" && slot == 2) { effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.RemoveBuffs }); effects.Add(Status("Ignite", count, rank)); }
        if (id == "fighter.kensou94" && slot == 2) { effects.Add(HealMissing(rank == 1 ? 16.67f : rank == 2 ? 33.33f : 50f)); effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.CleanseDebuffs }); }
        if (id == "fighter.king94" && slot == 2) effects.Add(Status("Poison", 1, rank));
        if (id == "fighter.mai94" && slot == 2) effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.DisableCardType, disabledCardType = SkillType.Heal, durationTurns = duration });
        if (id == "fighter.shingo97" && slot == 1) effects.Add(Drain(rank));
        if (id == "fighter.shingo97" && slot == 2) effects.Add(Stat("Defense", -(rank * 20f), duration));
        if (id == "fighter.benimaru94" && slot == 1) effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.DisableCardType, disabledCardType = SkillType.Stance, durationTurns = duration });
        if (id == "fighter.benimaru94" && slot == 2) effects.Add(Drain(rank));
        if (id == "fighter.athena94" && slot == 1) effects.Add(Stat("RecoveryRate", -(rank == 1 ? 26.67f : rank == 2 ? 53.33f : 80f), duration));
        if (id == "fighter.athena94" && slot == 2)
        {
            effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.HealAttackMultiplier, magnitude = rank == 1 ? 1.0833f : rank == 2 ? 2.1667f : 3.25f, targetType = SkillTargetType.AllAllies });
            effects.Add(Status("Rejuvenation", rank, rank));
        }
        return effects;
    }

    private static List<CharacterCardEffect> AuthorUltimateEffects(string id, int tier)
    {
        var effects = new List<CharacterCardEffect>();
        if (id == "fighter.kyo94") effects.Add(Status("Ignite", 2 + tier, 3));
        if (id == "fighter.chin94") effects.Add(Status("Ignite", 0, 3));
        if (id == "fighter.kensou94") { effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.HealMaxHealthPercent, magnitude = 50f + tier * 2.5f }); effects.Add(Stat("AllBasicStats", 25f, 3)); effects.Add(Status("DebuffImmunity", 1, 3)); }
        if (id == "fighter.king94" || id == "fighter.shingo97") effects.Add(Drain(id == "fighter.king94" ? 3 : 5));
        if (id == "fighter.mai94") effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.IncreaseCardRank, targetType = SkillTargetType.Self });
        if (id == "fighter.shingo97") effects.Add(Stat("Resistance", -50f, 2));
        if (id == "fighter.benimaru94") { effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.RemoveBuffs }); effects.Add(Status("Paralyze", 1, 1)); }
        if (id == "fighter.athena94")
        {
            effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.HealAttackMultiplier, magnitude = 3f + tier * .15f, targetType = SkillTargetType.AllAllies });
            effects.Add(new CharacterCardEffect { kind = CharacterCardEffectKind.CleanseDebuffs, targetType = SkillTargetType.AllAllies });
        }
        return effects;
    }

    private static CharacterCardEffect Status(string id, int count, int duration) =>
        new CharacterCardEffect { kind = CharacterCardEffectKind.ApplyStatus, statusId = id, stackCount = count, durationTurns = duration };
    private static CharacterCardEffect Drain(int gauge) =>
        new CharacterCardEffect { kind = CharacterCardEffectKind.DrainPowerGauge, magnitude = gauge };
    private static CharacterCardEffect Stat(string id, float magnitude, int duration) =>
        new CharacterCardEffect { kind = CharacterCardEffectKind.ModifyStat, statId = id, magnitude = magnitude, durationTurns = duration };
    private static CharacterCardEffect HealMissing(float percent) =>
        new CharacterCardEffect { kind = CharacterCardEffectKind.HealMissingHealthPercent, magnitude = percent, targetType = SkillTargetType.AllAllies };

    private static float SkillMultiplier(string id, int slot, int rank)
    {
        float[] values;
        switch (id)
        {
            case "fighter.kyo94": values = slot == 1 ? new[] { 1f, 2f, 3f } : new[] { 1.5f, 3f, 4.5f }; break;
            case "fighter.chin94": values = slot == 1 ? new[] { 1.5f, 3f, 4.5f } : new[] { 1.2f, 2.4f, 3.6f }; break;
            case "fighter.kensou94": values = slot == 1 ? new[] { 1.5f, 3f, 4.5f } : new[] { 0f, 0f, 0f }; break;
            case "fighter.king94": values = slot == 1 ? new[] { 1.5f, 3f, 4.5f } : new[] { 1.25f, 2.5f, 3.75f }; break;
            case "fighter.mai94": values = slot == 1 ? new[] { 1.3333f, 2.6667f, 4f } : new[] { .9167f, 1.8333f, 2.75f }; break;
            case "fighter.shingo97": values = slot == 1 ? new[] { 1.5f, 3f, 4.5f } : new[] { 1.1667f, 2.3333f, 3.5f }; break;
            case "fighter.benimaru94": values = slot == 1 ? new[] { 1.2f, 2.4f, 3.6f } : new[] { 1.5f, 3f, 4.5f }; break;
            case "fighter.athena94": values = slot == 1 ? new[] { 1.3333f, 2.6667f, 4f } : new[] { 0f, 0f, 0f }; break;
            default: throw new InvalidOperationException("No authored rank values for " + id);
        }
        return values[rank - 1];
    }

    private static float UltimateMultiplier(string id, int tier)
    {
        switch (id)
        {
            case "fighter.kyo94": return 5f + tier * .25f;
            case "fighter.chin94": return 2.1f + tier * .105f;
            case "fighter.kensou94": return 0f;
            case "fighter.king94": return 6.3f + tier * .315f;
            case "fighter.mai94": return 3.75f + tier * .1875f;
            case "fighter.shingo97": return 5.5f + tier * .275f;
            case "fighter.benimaru94": return 5f + tier * .25f;
            case "fighter.athena94": return 3.75f + tier * .1875f;
            default: throw new InvalidOperationException("No authored constellation values for " + id);
        }
    }

    private static Sprite LoadIcon(string id)
    {
        var names = new Dictionary<string, string>
        {
            ["fighter.kyo94"] = "Icon_1_Kyo94", ["fighter.mai94"] = "Icon_17_Mai94",
            ["fighter.king94"] = "Icon_18_King94", ["fighter.benimaru94"] = "Icon_2_Benimaru94",
            ["fighter.shingo97"] = "Icon_4_Shingo97", ["fighter.athena94"] = "Icon_16_Athena94"
        };
        return names.TryGetValue(id, out var name) ? LoadSprite("Assets/Project/Material/Character/Icon/" + name + ".png") : null;
    }

    private static Sprite LoadPortrait(string id)
    {
        var names = new Dictionary<string, string>
        {
            ["fighter.kyo94"] = "Cut_1_Kyo94", ["fighter.mai94"] = "Cut_17_Mai94",
            ["fighter.king94"] = "Cut_18_King94", ["fighter.benimaru94"] = "Cut_2_Benimaru94",
            ["fighter.athena94"] = "Cut_16_Athena94"
        };
        return names.TryGetValue(id, out var name) ? LoadSprite("Assets/Project/Material/Character/Cut/" + name + ".png") : null;
    }

    private static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }
    private static FighterAttribute ParseAttribute(string id) => (FighterAttribute)Enum.Parse(typeof(FighterAttribute), id.Replace("attribute.", string.Empty), true);
    private static FighterRarity ParseRarity(string id) => (FighterRarity)Enum.Parse(typeof(FighterRarity), id.Replace("rarity.", string.Empty), true);
    private static int ParseSourceId(string id) => int.Parse(id.Substring(id.LastIndexOf('.') + 1));
    private static float FromBasisPoints(int value) => value / 100f;

    private static SkillTargetType ParseTarget(string value)
    {
        if (string.Equals(value, "AOE", StringComparison.OrdinalIgnoreCase)) return SkillTargetType.AOE;
        if (string.Equals(value, "Self", StringComparison.OrdinalIgnoreCase)) return SkillTargetType.Self;
        if (string.Equals(value, "AllAllies", StringComparison.OrdinalIgnoreCase)) return SkillTargetType.AllAllies;
        return SkillTargetType.Single;
    }

    private static SkillType ParseSkillType(string value)
    {
        if (Enum.TryParse(value, true, out SkillType type)) return type;
        if (string.Equals(value, "DebuffAtk", StringComparison.OrdinalIgnoreCase)) return SkillType.DebuffAtk;
        return SkillType.Attack;
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
        var current = string.Empty;
        foreach (var part in path.Split('/'))
        {
            if (string.IsNullOrEmpty(current)) current = part;
            else
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }

    private static void Set(SerializedObject obj, string name, string value) => Set(obj.FindProperty(name), value);
    private static void Set(SerializedObject obj, string name, int value) => Set(obj.FindProperty(name), value);
    private static void Set(SerializedObject obj, string name, float value) => Set(obj.FindProperty(name), value);
    private static void Set(SerializedObject obj, string name, bool value) => Set(obj.FindProperty(name), value);
    private static void Set(SerializedObject obj, string name, Enum value) => SetEnum(obj.FindProperty(name), value);
    private static void Set(SerializedObject obj, string name, UnityEngine.Object value) { var p = obj.FindProperty(name); if (p != null) p.objectReferenceValue = value; }
    private static void Set(SerializedObject obj, string name, string[] value)
    {
        var p = obj.FindProperty(name); if (p == null) return;
        p.arraySize = value.Length;
        for (var i = 0; i < value.Length; i++) p.GetArrayElementAtIndex(i).stringValue = value[i];
    }
    private static void Set(SerializedObject obj, string name, string value, bool ignored = false) => Set(obj.FindProperty(name), value);
    private static void Set(SerializedProperty parent, string relativeName, string value) => Set(parent.FindPropertyRelative(relativeName), value);
    private static void Set(SerializedProperty parent, string relativeName, int value) => Set(parent.FindPropertyRelative(relativeName), value);
    private static void Set(SerializedProperty parent, string relativeName, float value) => Set(parent.FindPropertyRelative(relativeName), value);
    private static void Set(SerializedProperty parent, string relativeName, bool value) => Set(parent.FindPropertyRelative(relativeName), value);
    private static void SetEnum(SerializedProperty property, Enum value) { if (property != null) property.enumValueIndex = Convert.ToInt32(value); }
    private static void Set(SerializedProperty property, string value) { if (property != null) property.stringValue = value ?? string.Empty; }
    private static void Set(SerializedProperty property, int value) { if (property != null) property.intValue = value; }
    private static void Set(SerializedProperty property, float value) { if (property != null) property.floatValue = value; }
    private static void Set(SerializedProperty property, bool value) { if (property != null) property.boolValue = value; }
}

