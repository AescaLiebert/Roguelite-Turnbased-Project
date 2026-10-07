using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;
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
                SourceId = "source." + character.SourceId,
                SourceRecord = character.SourceRecord,
                DisplayName = character.FighterName,
                FamilyId = character.FamilyId,
                Role = character.Role,
                SeriesId = character.SeriesId,
                AttributeId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant(),
                RarityId = "rarity." + character.FighterRarity.ToString().ToLowerInvariant(),
                BaseStats = stats,
                PassiveId = passive.Id,
                PassiveSource = new PassiveSourceDefinition
                {
                    Description = character.PassiveSourceDescription,
                    SourceType = character.PassiveSourceType,
                    Restriction = character.PassiveRestriction,
                    Provenance = "CharacterObject"
                },
                Passive = passive,
                Skills = new List<SkillDefinition>(),
                UltimateTiers = new List<UltimateTierDefinition>()
            };
            if (character.TraitIds != null) definition.TraitIds.AddRange(character.TraitIds);
            if (character.PassiveDefinition != null && character.PassiveDefinition.statusVisuals != null)
            {
                foreach (var visual in character.PassiveDefinition.statusVisuals)
                    FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual);
            }
            definition.Skills.Add(CreateSkill(character, character.Skill1, 1));
            definition.Skills.Add(CreateSkill(character, character.Skill2, 2));
            CreateUltimate(character, character.Ultimate, definition.UltimateTiers);
            return definition;
        }

        private static SkillDefinition CreateSkill(CharacterObject character, SkillCardSO card, int slot)
        {
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
                SourceTarget = card.ranks[0].runtimeEffect?.Target.ToString(),
                SourceType = card.ranks[0].skillType.ToString()
            };
            for (var i = 0; i < card.ranks.Count; i++)
            {
                var rank = card.ranks[i];
                if (rank == null || rank.rankLevel != i + 1)
                    throw new InvalidOperationException(character.DefinitionId + " Skill " + slot + " has invalid rank data at rank " + (i + 1) + ".");
                var effect = BuildRuntimeEffect(rank.runtimeEffect,
                    character.DefinitionId + " Skill " + slot + " rank " + rank.rankLevel);
                skill.Ranks.Add(new SkillRankDefinition
                {
                    Rank = rank.rankLevel,
                    HasCardKind = true, Category = Category(rank.skillType), TargetScope = Target(effect),
                    Effect = effect,
                    Description = rank.description,
                    SourceDescription = rank.description,
                    Provenance = effect.Provenance
                });
            }
            return skill;
        }

        private static void CreateUltimate(CharacterObject character, UltimateCardSO card, List<UltimateTierDefinition> tiers)
        {
            if (card.statusVisuals != null)
            {
                foreach (var visual in card.statusVisuals)
                    FightingAllstar.Presentation.Combat.StatusVisualData.Register(visual);
            }
            if (card.levels == null || card.levels.Count != 7)
                throw new InvalidOperationException(card.ultimateName + " must define C0-C6.");
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
                    Description = level.description,
                    SourceDescription = level.description,
                    Effect = effect,
                    Provenance = effect.Provenance
                });
            }
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
            var id = recipe.Id.ToLowerInvariant();
            if (id.StartsWith("status.debuff.") || id.StartsWith("status.disable.") || id.StartsWith("status.decrease."))
            {
                recipe.Polarity = StatusPolarity.Debuff;
            }
            else if (id.StartsWith("status.buff.") || id.StartsWith("status.increase."))
            {
                recipe.Polarity = StatusPolarity.Buff;
            }
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
