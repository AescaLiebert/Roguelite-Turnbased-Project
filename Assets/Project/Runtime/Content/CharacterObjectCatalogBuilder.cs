using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEngine;

namespace FightingAllstar.Presentation.Content
{
    /// <summary>Builds combat definitions from CharacterObject and its card/passive assets.</summary>
    public static class CharacterObjectCatalogBuilder
    {
        public static ContentCatalog Load(TextAsset sharedContent)
        {
            var catalog = sharedContent == null ? new ContentCatalog {
                ContentVersion = "scriptable-object-cards-v1",
                ElementalRules = ElementalRuleSet.CreateDefault(),
                StatusRecipes = StandardEffectDatabase.CreateStatusRecipes(),
                AttackEffectRecipes = StandardEffectDatabase.CreateAttackEffects()
            } : SnapshotJson.Deserialize<ContentCatalog>(sharedContent.text);
            if (catalog == null) throw new InvalidOperationException("Shared combat recipe catalog could not be parsed.");
            return Build(catalog, CharacterObjectRegistrySO.LoadAll());
        }

        public static ContentCatalog Build(ContentCatalog sharedContent, IReadOnlyList<CharacterObject> characterAssets)
        {
            if (sharedContent == null) throw new ArgumentNullException(nameof(sharedContent));
            if (characterAssets == null) throw new ArgumentNullException(nameof(characterAssets));

            if (sharedContent.ElementalRules == null || sharedContent.ElementalRules.SchemaVersion < ElementalRuleSet.CurrentSchemaVersion)
                sharedContent.ElementalRules = ElementalRuleSet.CreateDefault();
            sharedContent.ElementalRules.UpgradeLegacyDefaults();
            sharedContent.Characters = new List<CharacterDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var character in characterAssets)
            {
                if (character == null || !character.RuntimeReady) continue;
                var definition = CreateDefinition(character);
                if (!ids.Add(definition.Id)) throw new InvalidOperationException("Duplicate CharacterObject definition id: " + definition.Id);
                sharedContent.Characters.Add(definition);
            }
            if (sharedContent.Characters.Count == 0)
                throw new InvalidOperationException("No runtime-ready CharacterObject assets were found under a Resources folder.");

            sharedContent.ContentHash = string.Empty;
            var canonical = SnapshotJson.Serialize(sharedContent);
            using (var sha = SHA256.Create())
                sharedContent.ContentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            return sharedContent;
        }

        public static CharacterDefinition CreateDefinition(CharacterObject character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (string.IsNullOrWhiteSpace(character.DefinitionId))
                throw new InvalidOperationException("CharacterObject has no stable Definition Id: " + character.name);
            if (character.Skill1 == null || character.Skill2 == null || character.Ultimate == null)
                throw new InvalidOperationException(character.DefinitionId + " must reference Skill 1, Skill 2, and Ultimate ScriptableObjects.");

            var stats = new StatBlock
            {
                Attack = Mathf.RoundToInt(character.attack),
                Defense = Mathf.RoundToInt(character.defense),
                MaxHealth = Mathf.RoundToInt(character.health),
                CombatClass = Mathf.RoundToInt(character.Classpower)
            };
            CopySecondaryStats(character.SecondaryStats, stats);

            var passive = character.GetPassiveDefinition();
            if (passive == null || string.IsNullOrWhiteSpace(passive.Id))
                throw new InvalidOperationException(character.DefinitionId + " has no PassiveDefinitionSO assigned.");

            var definition = new CharacterDefinition
            {
                RuntimeReady = true,
                Id = character.DefinitionId,
                CategoryId = character.ID,
                SourceId = character.DefinitionId,
                SourceRecord = 0,
                DisplayName = character.FighterName,
                FamilyId = character.FamilyId,
                Role = character.Role,
                SeriesId = character.SeriesId,
                AttributeId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant(),
                RarityId = "rarity." + character.FighterRarity.ToString().ToLowerInvariant(),
                BaseStats = stats,
                StatGrowth = character.LevelGrowth?.Clone() ?? new CharacterStatGrowth(),
                PassiveId = passive.Id,
                PassiveSource = new PassiveSourceDefinition
                {
                    Description = PassiveDescriptionText.Format(character.PassiveSourceDescription, passive),
                    SourceType = CharacterPassiveMetadata.SourceTypeLabel(character.PassiveSourceType),
                    Restriction = CharacterPassiveMetadata.RestrictionLabel(character.PassiveRestriction),
                    Provenance = "CharacterObject"
                },
                Passive = passive,
                Skills = new List<SkillDefinition>(),
                UltimateTiers = new List<UltimateTierDefinition>()
            };
            var studio = character.CreateStudioMetadata();
            if (studio != null)
            {
                if (studio.Id != character.DefinitionId) throw new InvalidOperationException("Studio character identity differs from its native asset.");
                definition.SourceId = studio.SourceId; definition.SourceRecord = studio.SourceRecord;
                definition.BaseStats = studio.BaseStats.Clone(); definition.StatGrowth = studio.StatGrowth?.Clone();
                definition.StatsProvenance = studio.StatsProvenance; definition.HolyRelicSource = studio.HolyRelicSource;
                definition.ArtifactLoadout = studio.ArtifactLoadout; definition.ReviewNotes = studio.ReviewNotes;
                definition.SourceTraits = studio.SourceTraits; definition.SourceStats = studio.SourceStats;
                if (studio.PassiveSource != null) definition.PassiveSource = studio.PassiveSource.Clone();
            }
            if (character.TraitIds != null) definition.TraitIds.AddRange(character.TraitIds);
            if (character.PassiveDefinition != null && character.PassiveDefinition.statusVisuals != null)
            {
                foreach (var visual in character.PassiveDefinition.statusVisuals)
                    FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual);
            }
            definition.Skills.Add(CreateSkill(character, character.Skill1, 1));
            definition.Skills.Add(CreateSkill(character, character.Skill2, 2));
            CreateUltimate(character, character.Ultimate, definition);
            return definition;
        }

        private static SkillDefinition CreateSkill(CharacterObject character, SkillCardSO card, int slot)
        {
            if (card.StudioOwned)
            {
                var authored = card.CreateStudioDefinition();
                if (authored.Slot != slot || authored.Id != character.DefinitionId + ".skill." + slot)
                    throw new InvalidOperationException("Studio skill identity/slot does not match its character: " + authored.Id);
                if (authored.DamageAnimation == null) authored.DamageAnimation = FirstDamageAnimation(authored.Ranks);
                CardRules.ApplySharedSkillSettings(authored);
                return authored;
            }
            if (card.statusVisuals != null)
            {
                foreach (var visual in card.statusVisuals)
                    FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual);
            }
            if (card.ranks == null || card.ranks.Count != 3)
                throw new InvalidOperationException(character.DefinitionId + " Skill " + slot + " must define ranks 1-3.");
            if (card.ranks[0] == null)
                throw new InvalidOperationException(character.DefinitionId + " Skill " + slot + " has no rank 1 data.");
            var skill = new SkillDefinition
            {
                Id = character.DefinitionId + ".skill." + slot,
                Slot = slot,
                Category = Category(card.ranks[0].skillType),
                TargetScope = Target(card.ranks[0].runtimeEffect),
                DamageAnimation = FirstDamageAnimation(card.ranks),
                SourceTarget = card.ranks[0].runtimeEffect?.Target.ToString(),
                SourceType = card.ranks[0].skillType.ToString()
            };
            for (var i = 0; i < card.ranks.Count; i++)
            {
                var rank = card.ranks[i];
                if (rank == null)
                    throw new InvalidOperationException(character.DefinitionId + " Skill " + slot + " has invalid rank data at rank " + (i + 1) + ".");
                var effect = BuildRuntimeEffect(rank.runtimeEffect,
                    character.DefinitionId + " Skill " + slot + " rank " + (i + 1));
                skill.Ranks.Add(new SkillRankDefinition
                {
                    Rank = i + 1,
                    HasCardKind = true, Category = Category(rank.skillType), TargetScope = Target(effect),
                    Effect = effect,
                    Description = CardDescriptionText.Format(rank.description, effect, rank.Wording),
                    Wording = rank.Wording?.Clone(),
                    SourceDescription = rank.description,
                    Provenance = SourceProvenance.Supplied
                });
            }
            CardRules.ApplySharedSkillSettings(skill);
            return skill;
        }

        private static void CreateUltimate(CharacterObject character, UltimateCardSO card, CharacterDefinition definition)
        {
            var tiers = definition.UltimateTiers;
            if (card.StudioOwned) tiers.AddRange(card.CreateStudioDefinition());
            else
            {
                if (card.statusVisuals != null)
                {
                    foreach (var visual in card.statusVisuals)
                        FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual);
                }
                if (card.levels == null || card.levels.Count != 6)
                    throw new InvalidOperationException(card.ultimateName + " must define C0-C5.");
                for (var i = 0; i < card.levels.Count; i++)
                {
                    var level = card.levels[i];
                    if (level == null || level.level != i)
                        throw new InvalidOperationException(card.ultimateName + " has invalid constellation tier data at tier " + i + ".");
                    var effect = BuildRuntimeEffect(level.runtimeEffect,
                        character.DefinitionId + " Ultimate tier " + level.level);
                    tiers.Add(new UltimateTierDefinition
                    {
                        Tier = level.level, Category = Category(level.skillType),
                        SourceLabel = card.ultimateName,
                        Description = CardDescriptionText.Format(level.description, effect, level.Wording, level.level),
                        Wording = level.Wording?.Clone(),
                        SourceDescription = level.description,
                        Effect = effect,
                        Provenance = SourceProvenance.Supplied
                    });
                }
            }

            if (card.HasStudioCardSettings)
            {
                definition.HasUltimateCardSettings = true;
                definition.UltimateCategory = card.StudioCategory;
                definition.UltimateTargetScope = card.StudioTargetScope;
                definition.UltimateDamageAnimation = card.StudioDamageAnimation?.Clone();
            }
            else
            {
                var baseTier = tiers.Find(t => t?.Tier == 0) ?? tiers.Find(t => t != null);
                if (baseTier == null) return;
                definition.UltimateCategory = baseTier.Category;
                definition.UltimateTargetScope = baseTier.Effect?.Target ?? EffectTargetScope.SelectedEnemy;
                definition.UltimateDamageAnimation = FirstDamageAnimation(baseTier.Effect);
            }
        }

        private static DamageAttackDefinition FirstDamageAnimation(List<CardRankData> ranks)
        {
            if (ranks == null) return null;
            foreach (var rank in ranks)
            {
                var attack = FirstDamageAnimation(rank?.runtimeEffect);
                if (attack != null) return attack;
            }
            return null;
        }

        private static DamageAttackDefinition FirstDamageAnimation(List<SkillRankDefinition> ranks)
        {
            if (ranks == null) return null;
            foreach (var rank in ranks)
            {
                var attack = FirstDamageAnimation(rank?.Effect);
                if (attack != null) return attack;
            }
            return null;
        }

        private static DamageAttackDefinition FirstDamageAnimation(EffectDefinition root)
        {
            if (root == null) return null;
            if (root.Kind == EffectKind.Damage && root.Attack != null) return root.Attack.Clone();
            if (root.Sequence != null)
                foreach (var step in root.Sequence)
                    if (step?.Effect?.Kind == EffectKind.Damage && step.Effect.Attack != null)
                        return step.Effect.Attack.Clone();
            return null;
        }

        private static void CopySecondaryStats(CharacterSecondaryStats source, StatBlock target)
        {
            if (source == null) return;
            target.PierceBp = Percent(source.PierceRate);
            target.ResistanceBp = Percent(source.Resistance);
            target.RegenerationBp = Percent(source.Regenerate);
            target.CritChanceBp = Percent(source.CritChance);
            target.CritDamageBp = Percent(source.CritDmg);
            target.CritResistanceBp = Percent(source.CritResistance);
            target.CritDefenseBp = Percent(source.CritDefense);
            target.RecoveryBp = Percent(source.RecoveryRate);
            target.BlockChanceBp = Percent(source.BlockChance);
            target.BlockPowerBp = Percent(source.BlockPower);
            target.LifeStealBp = Percent(source.LifeSteal);
            target.AvoidanceBp = Percent(source.AvoidanceRate);
            target.EvadeBp = Percent(source.EvadeRate);
            target.ControlBp = Percent(source.ControlRate);
            target.PerceptionBp = Percent(source.PerceptionRate);
        }

        private static int Percent(float value) => Mathf.RoundToInt(value * 100f);

        private static EffectDefinition BuildRuntimeEffect(EffectDefinition source, string identity)
        {
            if (source == null)
                throw new InvalidOperationException(identity + " is missing its Runtime Effect.");
            var effect = source.Clone();
            SanitizeEffect(effect);


            return effect;
        }

        private static void SanitizeEffect(EffectOperationDefinition effect)
        {
            if (effect == null) return;
            SanitizeRecipe(effect.StatusRecipe);
            if (effect is EffectDefinition root && root.Sequence != null)
            {
                foreach (var step in root.Sequence)
                {
                    if (step?.Effect != null)
                        SanitizeEffect(step.Effect);
                }
            }
        }

        private static void SanitizeRecipe(StatusRecipeDefinition recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id)) return;
            recipe.NormalizePolarity();
            // Authored polarity is authoritative. Increasing damage taken is a
            // Debuff; an "increase" prefix must never turn it into a Buff.
        }

        private static CardCategory Category(SkillType value) => value switch
        {
            SkillType.Buff => CardCategory.Buff,
            SkillType.Debuff => CardCategory.Debuff,
            SkillType.Stance => CardCategory.Stance,
            SkillType.Heal => CardCategory.Recovery,
            SkillType.DebuffAtk => CardCategory.AttackDebuff,
            _ => CardCategory.Attack
        };

        private static EffectTargetScope Target(EffectDefinition effect)
        {
            if (effect == null)
                throw new InvalidOperationException("A Runtime Effect is missing.");
            return effect.Target;
        }

    }
}
