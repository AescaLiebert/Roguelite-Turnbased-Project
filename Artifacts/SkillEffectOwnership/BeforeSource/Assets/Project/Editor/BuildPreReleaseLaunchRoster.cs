#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.EditorTools.CharacterCreatorStudio;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    /// <summary>Builds pre-release roadmap fighters as reviewed native Studio packages.</summary>
    public static class BuildPreReleaseLaunchRoster
    {
        private const string MenuPath = "Fighting Allstar/Content/Build Pre-Release Launch Roster (7 Fighters)";
        private const string BeginMiddleMenuPath = "Fighting Allstar/Content/Build Pre-Release Begin & Middle (13 Fighters)";
        private const string LateEndMenuPath = "Fighting Allstar/Content/Build Pre-Release Late & End (10 Fighters)";
        private const string SourcePath = "ArtDirection/Character Database/Pre-Release-Character.csv";
        private const string IconRoot = "Assets/Project/Art/Character/Icon/pr-icon-char/";
        private const string FullRoot = "Assets/Project/Art/Character/Full-pic/pr-full/";
        private const string PreReleaseRoot = "Assets/Project/Data/Character/Pre-Release_Phase/";
        private const string StudioRoot = "Assets/Project/Data/Character/Studio/";

        private sealed class SourceFighter
        {
            public int CsvRow;
            public string Name, Attribute, Tier, Traits, Passive, Skills, Ultimate, Slug, Family, TemplateId, Roadmap, SeriesId;
            public string IconFile, FullFile;
            public bool IconInFullRoot;
            public int CategoryId;
            public FighterAttribute NativeAttribute;
            public FighterRarity NativeRarity;
            public StatBlock Stats;
            public List<string> TraitIds;
            public List<SourceStatField> SourceStats;
            public string SkillOneText, SkillTwoText, UltimateText;
            public string DefinitionId => "fighter.pre-release." + Slug;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildRoadmaps(new[] { "Pre-Lunch", "Pre-Launch" }, 7, "Pre-Lunch/Pre-Launch");
        }

        [MenuItem(BeginMiddleMenuPath)]
        public static void BuildBeginAndMiddle()
        {
            BuildRoadmaps(new[] { "Pre-Begin", "Pre-Middle" }, 13, "Pre-Begin + Pre-Middle");
        }

        [MenuItem(LateEndMenuPath)]
        public static void BuildLateAndEnd()
        {
            BuildRoadmaps(new[] { "Pre-Late", "Pre-End" }, 10, "Pre-Late + Pre-End");
        }

        private static void BuildRoadmaps(string[] selectedRoadmaps, int expectedCount, string launchLabel)
        {
            var absoluteCsvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourcePath));
            if (!File.Exists(absoluteCsvPath)) throw new FileNotFoundException("Pre-release fighter database CSV was not found.", absoluteCsvPath);

            var records = ReadCsv(File.ReadAllText(absoluteCsvPath, Encoding.UTF8));
            if (records.Count < 2) throw new InvalidOperationException("The pre-release fighter database is empty.");
            var headers = records[0].Select((value, index) => new { value = value.Trim(), index })
                .ToDictionary(item => item.value, item => item.index, StringComparer.OrdinalIgnoreCase);
            var launches = new List<SourceFighter>();
            for (var i = 1; i < records.Count; i++)
            {
                var row = records[i];
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                var roadmap = Field(row, headers, "Release Roadmap").Trim();
                if (!selectedRoadmaps.Any(selected => roadmap.Equals(selected, StringComparison.OrdinalIgnoreCase))) continue;
                launches.Add(ParseFighter(row, headers, i + 1));
            }
            if (launches.Count != expectedCount)
                throw new InvalidOperationException("Expected exactly " + expectedCount + " " + launchLabel + " rows; found " + launches.Count + ". No assets were written.");

            var existing = CharacterObjectRegistrySO.LoadAll().Where(character => character != null).ToArray();
            var usedIds = new HashSet<int>(existing.Select(character => character.ID));
            var existingByDefinition = existing.ToDictionary(character => character.DefinitionId, StringComparer.Ordinal);
            var nextCategoryId = 9101;
            foreach (var fighter in launches)
            {
                if (existingByDefinition.TryGetValue(fighter.DefinitionId, out var published))
                {
                    if (!published.StudioOwned)
                        throw new InvalidOperationException("Launch fighter exists outside a Character Creator Studio package: " + fighter.DefinitionId + ". No assets were written.");
                    fighter.CategoryId = published.ID;
                }
                else
                {
                    while (usedIds.Contains(nextCategoryId)) nextCategoryId++;
                    fighter.CategoryId = nextCategoryId;
                    usedIds.Add(nextCategoryId++);
                }
            }

            // Review every exact package revision before the first asset is created.
            var pending = new List<(SourceFighter source, StudioDraftAsset draft, StudioPublishPlan plan)>();
            try
            {
                foreach (var source in launches)
                {
                    var definition = BuildDefinition(source);
                    var document = StudioDocumentService.ImportCharacter(definition);
                    document.Id = "studio.pre-release." + source.Slug;
                    document.Name = source.Name;
                    document.SourceCharacterId = definition.Id;
                    document.Description = DescriptionDocument.FromSource(definition.PassiveSource.Description);
                    document.Description.OwnerId = document.Id;
                    document.Presentation = new StudioPresentationProfile { Id = "presentation.default" };

                    var draft = ScriptableObject.CreateInstance<StudioDraftAsset>();
                    draft.name = source.Name + " · launch import";
                    draft.DocumentJson = SnapshotJson.Serialize(document);
                    draft.LibraryJson = SnapshotJson.Serialize(DescriptionLibrary.CreateOpeningGaugeLibrary());
                    var published = existingByDefinition.TryGetValue(definition.Id, out var target) ? target : null;
                    if (published != null)
                    {
                        draft.PublishedCharacter = published;
                        draft.PublishedFilesJson = PublishedFilesReceipt(published);
                    }
                    pending.Add((source, draft, StudioProductionBridge.Review(draft, definition.Id, source.CategoryId,
                        source.Name, published != null, StudioPublishWording.Preserve)));
                }

                foreach (var item in pending)
                {
                    var character = StudioProductionBridge.Publish(item.draft, item.plan);
                    ApplyVisuals(character, item.source);
                }
                AlignAllPreReleasePackages();
                CharacterRegistrySync.Sync();
                AssetDatabase.SaveAssets();

                Debug.Log("Built and registered " + launches.Count + " " + launchLabel + " Pre-Release fighters from " + SourcePath + ": " +
                    string.Join(", ", launches.Select(fighter => fighter.Name)) + ".");
            }
            finally
            {
                foreach (var item in pending)
                    if (item.draft != null) UnityEngine.Object.DestroyImmediate(item.draft);
            }
        }

        private static CharacterDefinition BuildDefinition(SourceFighter source)
        {
            var passive = BuildPassive(source);
            var skillOneEffect = BuildSkillOne(source);
            var skillTwoEffect = BuildSkillTwo(source);
            var ultimateEffect = BuildUltimate(source);
            var skillOneCategory = SkillCategory(source, 1);
            var skillTwoCategory = SkillCategory(source, 2);
            var ultimateCategory = UltimateCategory(source);
            var skills = new List<SkillDefinition>
            {
                BuildSkill(source, 1, source.SkillOneText, skillOneCategory, skillOneEffect),
                BuildSkill(source, 2, source.SkillTwoText, skillTwoCategory, skillTwoEffect)
            };
            var ultimateTiers = new List<UltimateTierDefinition>();
            for (var tier = 0; tier <= 5; tier++)
                ultimateTiers.Add(new UltimateTierDefinition
                {
                    Tier = tier,
                    Category = ultimateCategory,
                    SourceLabel = source.Name + " · Ultimate",
                    Description = source.UltimateText,
                    SourceDescription = source.UltimateText,
                    Effect = CloneWithSuffix(ultimateEffect, ".c" + tier),
                    Provenance = SourceProvenance.Supplied
                });

            var definition = new CharacterDefinition
            {
                CategoryId = source.CategoryId,
                RuntimeReady = true,
                Id = source.DefinitionId,
                SourceId = "pre-release-character.csv#row-" + source.CsvRow,
                SourceRecord = source.CsvRow,
                DisplayName = source.Name,
                FamilyId = source.Family,
                Role = string.Empty,
                SeriesId = source.SeriesId,
                AttributeId = "attribute." + source.NativeAttribute.ToString().ToLowerInvariant(),
                RarityId = "rarity." + source.NativeRarity.ToString().ToLowerInvariant(),
                TraitIds = source.TraitIds,
                BaseStats = source.Stats,
                StatGrowth = new CharacterStatGrowth(),
                Skills = skills,
                PassiveId = passive.Id,
                PassiveSource = new PassiveSourceDefinition
                {
                    SourceType = string.Empty,
                    Restriction = string.Empty,
                    Description = source.Passive,
                    Provenance = SourcePath
                },
                Passive = passive,
                UltimateTiers = ultimateTiers,
                StatsProvenance = SourceProvenance.Supplied,
                SourceTraits = source.Traits.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToList(),
                SourceStats = source.SourceStats,
                ReviewNotes = BuildReviewNotes(source)
            };
            GenshinSummonKits.Upgrade(definition);
            return definition;
        }

        private static SkillDefinition BuildSkill(SourceFighter source, int slot, string description,
            CardCategory category, EffectDefinition effect)
        {
            var skill = new SkillDefinition
            {
                Id = source.DefinitionId + ".skill." + slot,
                Slot = slot,
                Category = category,
                TargetScope = effect.Target,
                SourceTarget = effect.Target.ToString(),
                SourceType = category.ToString()
            };
            for (var rank = 1; rank <= 3; rank++)
                skill.Ranks.Add(new SkillRankDefinition
                {
                    Rank = rank,
                    HasCardKind = true,
                    Category = category,
                    TargetScope = effect.Target,
                    Effect = CloneWithSuffix(effect, ".r" + rank),
                    Description = description,
                    SourceDescription = description,
                    Provenance = SourceProvenance.Supplied
                });
            return skill;
        }

        private static EffectDefinition BuildSkillOne(SourceFighter source)
        {
            switch (source.Slug)
            {
                case "andy96":
                {
                    var effect = Attack(source, "skill1", 300, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill1-pierce", "Pierce", EffectTargetScope.Self,
                        StatRecipe(source, "skill1-pierce", "Pierce", StatusPolarity.Buff, 2,
                            StatModifier(StatId.Pierce, ModifierOperation.PercentagePoints, 8000))), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "goro95":
                {
                    var effect = Attack(source, "skill1", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill1-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill1-stun", "Stun", EffectTargetScope.SelectedEnemy,
                        StunRecipe(), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "saisyu95":
                    return Attack(source, "skill1", 300, EffectTargetScope.SelectedEnemy);
                case "kyo95":
                {
                    var effect = Attack(source, "skill1", 350, EffectTargetScope.SelectedEnemy);
                    var remove = Utility(source, "skill1-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy);
                    remove.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasRecipe, RecipeId = "status.reaction.burning" });
                    AddStep(effect, remove, CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill1-burning", "Burning", EffectTargetScope.SelectedEnemy,
                        BurningRecipe(), 3, 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ryo95":
                {
                    var effect = Attack(source, "skill1", 450, EffectTargetScope.SelectedEnemy);
                    var reflect = StatRecipe(source, "skill1-reflect", "Reflection", StatusPolarity.Buff, 2,
                        StatModifier(StatId.ReflectDamage, ModifierOperation.PercentagePoints, 12000));
                    reflect.Tags.Add("status.ryo.reflect");
                    AddStep(effect, ApplyStatus(source, "skill1-reflect", "Reflection", EffectTargetScope.Self, reflect, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kim94":
                    return Attack(source, "skill1", 450, EffectTargetScope.SelectedEnemy);
                case "kensou98":
                {
                    var effect = Attack(source, "skill1", 450, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill1-attack", "Attack Up", EffectTargetScope.Self,
                        StatRecipe(source, "skill1-attack", "Attack Up", StatusPolarity.Buff, 2,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 3000))), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "rugal94": return KeywordAttack(source, "skill1", 400, "attack.shatter");
                case "chizuru96":
                {
                    var effect = Attack(source, "skill1", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, GaugeChange(source, "skill1-drain", EffectTargetScope.SelectedEnemy, -2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "king97":
                {
                    var effect = Attack(source, "skill1", 450, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill1-team-resistance", "Resistance Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "skill1-team-resistance", "Resistance Up", StatusPolarity.Buff, 3,
                            StatModifier(StatId.Resistance, ModifierOperation.PercentagePoints, 6000)), 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "clark98":
                {
                    var effect = Attack(source, "skill1", 550, EffectTargetScope.SelectedEnemy);
                    AddAttributeBonus(effect, source, "skill1-blue-bonus", 30, "attribute.blue");
                    return effect;
                }
                case "ralf98":
                {
                    var effect = Attack(source, "skill1", 360, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill1-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStandardStatus(source, "skill1-disable-stance", "status.debuff.disable-stance", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "mature96":
                {
                    var effect = Attack(source, "skill1", 375, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStandardStatus(source, "skill1-bleed", "status.debuff.bleed", EffectTargetScope.SelectedEnemy, 3, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "vice96":
                {
                    var effect = Attack(source, "skill1", 400, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, new EffectDefinition { Id = source.DefinitionId + ".skill1.damage-heal", Kind = EffectKind.Heal,
                        Target = EffectTargetScope.Self, HealValue = new EffectValueDefinition { Source = EffectValueSource.ActualHealthDamage, CoefficientBp = 10000 } }, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kasumi96":
                    return ApplyStandardStatus(source, "skill1-disable-attack", "status.debuff.disable-attack", EffectTargetScope.SelectedEnemy, 2);
                case "mary97":
                {
                    var effect = Attack(source, "skill1", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill1-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, Utility(source, "skill1-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "heidern95":
                {
                    var effect = Attack(source, "skill1", 325, EffectTargetScope.SelectedEnemy);
                    AddAttributeBonus(effect, source, "skill1-yellow-bonus", 30, "attribute.yellow");
                    return effect;
                }
                case "billy97":
                {
                    var effect = Attack(source, "skill1", 325, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill1-team-crit", "Critical Rate Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "skill1-team-crit", "Critical Rate Up", StatusPolarity.Buff, 2,
                            StatModifier(StatId.CritChance, ModifierOperation.PercentagePoints, 3500)), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "armorking":
                {
                    var effect = Attack(source, "skill1", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill1-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill1-stun", "Stun", EffectTargetScope.SelectedEnemy, StunRecipe(), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "jin":
                {
                    var effect = Attack(source, "skill1", 450, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, GaugeChange(source, "skill1-drain", EffectTargetScope.SelectedEnemy, -3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "razor": return ElementalAttack(source, "skill1", 400, ElementId.Electro, "attack.breakthrough", applyShock: true);
                case "noelle": return Attack(source, "skill1", 375, EffectTargetScope.SelectedEnemy);
                case "yamazaki97": return Attack(source, "skill1", 600, EffectTargetScope.SelectedEnemy);
                case "ling-xiaoyu": return KeywordAttack(source, "skill1", 450, "attack.pierce");
                case "kazuya-mishima": return ElementalAttack(source, "skill1", 375, ElementId.None, applyShock: true);
                case "paul-phoenix": return KeywordAttack(source, "skill1", 400, "attack.shatter");
                case "heihachi-mishima": return KeywordAttack(source, "skill1", 300, "attack.weakpoint");
                case "fischl":
                {
                    var effect = ElementalAttack(source, "skill1", 450, ElementId.Electro, applyShock: true);
                    AddStep(effect, new EffectDefinition { Id = source.DefinitionId + ".skill1.max-health-damage", Kind = EffectKind.Damage,
                        Family = DamageFamily.Normal, Scaling = StatScaling.MaxHealth, CoefficientBp = 800, KeywordFactorBp = 10000,
                        Target = EffectTargetScope.SelectedEnemy }, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ningguang": return ElementalAttack(source, "skill1", 450, ElementId.Geo, "attack.sever");
                case "xiangling": return KeywordAttack(source, "skill1", 500, "attack.clash");
                default: throw new InvalidOperationException("No Skill 1 mapping exists for " + source.Name);
            }
        }

        private static EffectDefinition BuildSkillTwo(SourceFighter source)
        {
            switch (source.Slug)
            {
                case "andy96":
                    return ApplyStatus(source, "skill2-team-attack", "Attack Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "skill2-team-attack", "Attack Up", StatusPolarity.Buff, 3,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 6000)), 3);
                case "goro95":
                    return Attack(source, "skill2", 250, EffectTargetScope.SelectedEnemy);
                case "saisyu95":
                {
                    var effect = Attack(source, "skill2", 350, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill2-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill2-burning", "Burning", EffectTargetScope.SelectedEnemy,
                        BurningRecipe(), 3, 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kyo95":
                    return Attack(source, "skill2", 400, EffectTargetScope.SelectedEnemy);
                case "ryo95":
                    return ApplyStatus(source, "skill2-taunt", "Taunting Guard", EffectTargetScope.Self,
                        new StatusRecipeDefinition
                        {
                            Id = source.DefinitionId + ".skill2.taunt",
                            NameKey = "Taunting Guard",
                            Polarity = StatusPolarity.Neutral,
                            Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
                            Taunt = true,
                            DefaultDuration = 2,
                            DurationClock = StatusDurationClock.TargetTurnEnd,
                            Stacking = StatusStackingPolicy.RefreshDuration,
                            Tags = new List<string> { CombatTags.Stance },
                            RecoverDamageTakenBp = 7500
                        }, 2);
                case "kim94":
                {
                    var effect = Attack(source, "skill2", 250, EffectTargetScope.SelectedEnemy);
                    var heal = new EffectDefinition
                    {
                        Id = source.DefinitionId + ".skill2.damage-heal",
                        Kind = EffectKind.Heal,
                        Target = EffectTargetScope.Self,
                        HealValue = new EffectValueDefinition { Source = EffectValueSource.ActualHealthDamage, CoefficientBp = 4000 }
                    };
                    AddStep(effect, heal, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kensou98":
                {
                    var effect = Attack(source, "skill2", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill2-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, Utility(source, "skill2-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "rugal94": return KeywordAttack(source, "skill2", 450, "attack.pierce");
                case "chizuru96": return TauntGuard(source, "skill2", 7500, 2);
                case "king97":
                {
                    var effect = Attack(source, "skill2", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill2-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill2-stun", "Stun", EffectTargetScope.SelectedEnemy, StunRecipe(), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "clark98":
                {
                    var effect = Attack(source, "skill2", 450, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill2-team-crit-damage", "Critical Damage Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "skill2-team-crit-damage", "Critical Damage Up", StatusPolarity.Buff, 2,
                            StatModifier(StatId.CritDamage, ModifierOperation.PercentagePoints, 4500)), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ralf98":
                    return TauntGuard(source, "skill2", 0, 2, ModifierTarget.AnyDamageDealt, 7500);
                case "mature96":
                {
                    var effect = new EffectDefinition { Id = source.DefinitionId + ".skill2.team-heal", Kind = EffectKind.Heal,
                        Target = EffectTargetScope.AllAllies, HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 5000 } };
                    AddStep(effect, Utility(source, "skill2-cleanse", EffectKind.Cleanse, EffectTargetScope.AllAllies), CardEffectTiming.BeforeAction);
                    return effect;
                }
                case "vice96":
                {
                    var effect = Attack(source, "skill2", 210, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStandardStatus(source, "skill2-poison", "status.debuff.poison", EffectTargetScope.SelectedEnemy, 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kasumi96": return AttackWithGaugeDrain(source, "skill2", 450, -3);
                case "mary97": return AttackWithGaugeDrain(source, "skill2", 450, -3);
                case "heidern95":
                {
                    var effect = Attack(source, "skill2", 375, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStandardStatus(source, "skill2-shock", "status.debuff.shock", EffectTargetScope.SelectedEnemy, 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "billy97": return TauntWithCounterBurn(source, "skill2");
                case "armorking": return AttackWithStatDebuff(source, "skill2", 300, StatId.Attack, -4000, 2);
                case "jin": return AttackWithStatDebuff(source, "skill2", 300, StatId.Attack, -4000, 2);
                case "razor": return Attack(source, "skill2", 600, EffectTargetScope.SelectedEnemy);
                case "noelle":
                {
                    var effect = TauntGuard(source, "skill2", 0, 2);
                    AddStep(effect, ApplyStatus(source, "skill2-team-barrier", "Noelle's Shield", EffectTargetScope.AllAllies,
                        BarrierRecipe(source, "skill2-team-barrier", BarrierSourceStat.Defense, 60000), 2), CardEffectTiming.AfterAction);
                    return effect;
                }
                case "yamazaki97":
                {
                    var effect = Attack(source, "skill2", 250, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "skill2-steal-target", "Attack and Defense Down", EffectTargetScope.SelectedEnemy,
                        StatRecipe(source, "skill2-steal-target", "Attack and Defense Down", StatusPolarity.Debuff, 2,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, -5000),
                            StatModifier(StatId.Defense, ModifierOperation.PercentOfBase, -5000)), 2), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "skill2-steal-self", "Attack and Defense Up", EffectTargetScope.Self,
                        StatRecipe(source, "skill2-steal-self", "Attack and Defense Up", StatusPolarity.Buff, 2,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 5000),
                            StatModifier(StatId.Defense, ModifierOperation.PercentOfBase, 5000)), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ling-xiaoyu":
                {
                    var effect = Attack(source, "skill2", 180, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStandardStatus(source, "skill2-disable-buff-cards", "status.debuff.disable-buff", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStandardStatus(source, "skill2-disable-debuff-cards", "status.debuff.disable-debuff", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kazuya-mishima": return Attack(source, "skill2", 600, EffectTargetScope.SelectedEnemy);
                case "paul-phoenix":
                {
                    var effect = Attack(source, "skill2", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "skill2-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, Utility(source, "skill2-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "heihachi-mishima":
                {
                    var effect = ElementalAttack(source, "skill2", 375, ElementId.None, applyShock: true);
                    AddStep(effect, ApplyStandardStatus(source, "skill2-shock", "status.debuff.shock", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "fischl": return SummonEffect(source, "skill2-oz", "Oz", ElementId.Electro, 2, true);
                case "ningguang": return ElementalAttack(source, "skill2", 350, ElementId.Geo);
                case "xiangling":
                {
                    var effect = SummonEffect(source, "skill2-guoba", "Guoba", ElementId.Pyro, 2, false);
                    AddStep(effect, ApplyStatus(source, "skill2-team-attack", "Attack Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "skill2-team-attack", "Attack Up", StatusPolarity.Buff, 2,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 1200)), 2), CardEffectTiming.AfterAction);
                    return effect;
                }
                default: throw new InvalidOperationException("No Skill 2 mapping exists for " + source.Name);
            }
        }

        private static EffectDefinition BuildUltimate(SourceFighter source)
        {
            switch (source.Slug)
            {
                case "andy96":
                {
                    var effect = Attack(source, "ultimate", 225, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-stun", "Stun", EffectTargetScope.SelectedEnemy,
                        StunRecipe(), 1, 1, 4000), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "goro95":
                    return Attack(source, "ultimate", 600, EffectTargetScope.SelectedEnemy);
                case "saisyu95":
                {
                    var effect = Attack(source, "ultimate", 400, EffectTargetScope.AllEnemies);
                    AddStep(effect, Utility(source, "ultimate-rank-down", EffectKind.ModifyCardRank,
                        EffectTargetScope.AllEnemies, magnitude: -1), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kyo95":
                {
                    var effect = Attack(source, "ultimate", 500, EffectTargetScope.AllEnemies);
                    AddStep(effect, ApplyStatus(source, "ultimate-burning", "Burning", EffectTargetScope.AllEnemies,
                        BurningRecipe(), 3, 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ryo95":
                {
                    var effect = Attack(source, "ultimate", 300, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-attack-down", "Attack Down", EffectTargetScope.SelectedEnemy,
                        StatRecipe(source, "ultimate-attack-down", "Attack Down", StatusPolarity.Debuff, 2,
                            StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, -4000)), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kim94":
                {
                    var effect = Attack(source, "ultimate", 550, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-block", "Block Up", EffectTargetScope.AllAllies,
                        StatRecipe(source, "ultimate-block", "Block Up", StatusPolarity.Buff, 3,
                            StatModifier(StatId.BlockChance, ModifierOperation.PercentagePoints, 4000)), 3), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kensou98":
                {
                    var effect = new EffectDefinition
                    {
                        Id = source.DefinitionId + ".ultimate.heal",
                        Kind = EffectKind.Heal,
                        Target = EffectTargetScope.SelectedAlly,
                        HealValue = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 5000 }
                    };
                    var basicStats = new StatusRecipeDefinition
                    {
                        Id = source.DefinitionId + ".ultimate.basic-stats",
                        NameKey = "Basic Stats Up",
                        Polarity = StatusPolarity.Buff,
                        Behavior = StatusBehavior.Stat,
                        DefaultDuration = 3,
                        DurationClock = StatusDurationClock.TargetTurnEnd,
                        Stacking = StatusStackingPolicy.RefreshDuration,
                        Modifiers = new List<StatModifierDefinition>
                        {
                            new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.BasicStats,
                                Operation = ModifierOperation.PercentOfBase, Amount = 2500 }
                        }
                    };
                    basicStats.Modifiers[0].SerializedTarget = (int)ModifierTarget.StatBundle;
                    basicStats.Modifiers[0].SerializedBundle = (int)StatBundleKind.BasicStats;
                    basicStats.Modifiers[0].SerializedOperation = (int)ModifierOperation.PercentOfBase;
                    AddStep(effect, ApplyStatus(source, "ultimate-basic-stats", "Basic Stats Up", EffectTargetScope.SelectedAlly, basicStats, 3), CardEffectTiming.AfterAction);
                    var protection = new StatusRecipeDefinition
                    {
                        Id = source.DefinitionId + ".ultimate.debuff-immunity",
                        NameKey = "Debuff Immunity",
                        Polarity = StatusPolarity.Buff,
                        Behavior = StatusBehavior.Stat,
                        DefaultDuration = 3,
                        DurationClock = StatusDurationClock.TargetTurnEnd,
                        Stacking = StatusStackingPolicy.RefreshDuration,
                        DebuffImmunity = true
                    };
                    AddStep(effect, ApplyStatus(source, "ultimate-debuff-immunity", "Debuff Immunity", EffectTargetScope.SelectedAlly, protection, 3), CardEffectTiming.AfterAction);
                    return effect;
                }
                case "rugal94": return KeywordAttack(source, "ultimate", 600, "attack.clash");
                case "chizuru96":
                {
                    var effect = Attack(source, "ultimate", 275, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStandardStatus(source, "ultimate-disable-ultimate", "status.debuff.disable-ultimate", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "king97": return AttackWithGaugeDrain(source, "ultimate", 630, -3);
                case "clark98":
                {
                    var effect = KeywordAttack(source, "ultimate", 500, "attack.breakthrough");
                    AddAttributeBonus(effect, source, "ultimate-blue-bonus", 60, "attribute.blue");
                    AddStep(effect, ApplyStatus(source, "ultimate-damage-up", "Damage Up", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-damage-up", "Damage Up", StatusPolarity.Buff, 2,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 2000 }), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ralf98":
                {
                    var effect = KeywordAttack(source, "ultimate", 800, "attack.breakthrough");
                    AddAttributeBonus(effect, source, "ultimate-red-bonus", 140, "attribute.red");
                    var redDamageUp = ApplyStatus(source, "ultimate-red-damage-up", "Damage Up vs Red", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-red-damage-up", "Damage Up", StatusPolarity.Buff, 2,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 6000 }), 2);
                    redDamageUp.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetAttributeIs,
                        Subject = ConditionSubject.EventTarget, StringValue = "attribute.red" });
                    AddStep(effect, redDamageUp, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "mature96":
                {
                    var effect = KeywordAttack(source, "ultimate", 600, "attack.pierce");
                    AddAttributeBonus(effect, source, "ultimate-blue-bonus", 300, "attribute.blue");
                    return effect;
                }
                case "vice96":
                {
                    var effect = KeywordAttack(source, "ultimate", 550, "attack.sever");
                    return effect;
                }
                case "kasumi96": return AttackWithGaugeDrain(source, "ultimate", 350, -5);
                case "mary97":
                {
                    var effect = Attack(source, "ultimate", 250, EffectTargetScope.SelectedEnemy);
                    var stance = new StatusRecipeDefinition
                    {
                        Id = source.DefinitionId + ".status.ultimate-stance", NameKey = "Blue Mary Guard",
                        Polarity = StatusPolarity.Neutral, Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
                        Taunt = true, DefaultDuration = 5, DurationClock = StatusDurationClock.TargetTurnEnd,
                        Stacking = StatusStackingPolicy.RefreshDuration, Tags = new List<string> { CombatTags.Stance },
                        RecoverDamageTakenBp = 5000,
                        Modifiers = new List<StatModifierDefinition> { StatModifier(StatId.Defense, ModifierOperation.PercentOfBase, 3500) }
                    };
                    AddStep(effect, ApplyStatus(source, "ultimate-stance", "Blue Mary Guard", EffectTargetScope.Self, stance, 5), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "heidern95":
                {
                    var effect = Attack(source, "ultimate", 475, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-basic-stat-down", "Basic Stats Down", EffectTargetScope.SelectedEnemy,
                            StatRecipe(source, "ultimate-basic-stat-down", "Basic Stats Down", StatusPolarity.Debuff, 2,
                            BundleModifier(StatBundleKind.BasicStats, ModifierOperation.PercentOfBase, -2500)), 2), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStandardStatus(source, "ultimate-shock", "status.debuff.shock", EffectTargetScope.SelectedEnemy, 4), CardEffectTiming.AfterDamage);
                    AddStep(effect, new EffectDefinition { Id = source.DefinitionId + ".ultimate-team-heal", Kind = EffectKind.Heal,
                        Target = EffectTargetScope.AllAllies, HealValue = new EffectValueDefinition { Source = EffectValueSource.ActualHealthDamage, CoefficientBp = 3000 } }, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "billy97":
                {
                    var effect = Attack(source, "ultimate", 375, EffectTargetScope.SelectedEnemy);
                    var critDown = ApplyStatus(source, "ultimate-crit-resistance-down", "Critical Resistance Down", EffectTargetScope.SelectedEnemy,
                        StatRecipe(source, "ultimate-crit-resistance-down", "Critical Resistance Down", StatusPolarity.Debuff, 2,
                            StatModifier(StatId.CritResistance, ModifierOperation.PercentagePoints, -3000)), 2);
                    critDown.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.WasCritical });
                    AddStep(effect, critDown, CardEffectTiming.AfterDamage);
                    var stun = ApplyStatus(source, "ultimate-stun", "Stun", EffectTargetScope.SelectedEnemy, StunRecipe(), 1, 1, 4000);
                    stun.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.WasCritical });
                    AddStep(effect, stun, CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "armorking":
                {
                    var effect = Attack(source, "ultimate", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "ultimate-remove-buffs", EffectKind.RemoveBuffs, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, Utility(source, "ultimate-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "jin": return KeywordAttack(source, "ultimate", 550, "attack.breakthrough");
                case "razor":
                {
                    var effect = ApplyStatus(source, "ultimate-wolf-within", "Wolf Within", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-wolf-within", "Wolf Within", StatusPolarity.Buff, 4,
                            BundleModifier(StatBundleKind.BasicStats, ModifierOperation.PercentOfBase, 2500),
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 3000 }), 4);
                    return effect;
                }
                case "noelle":
                {
                    var effect = ElementalAttack(source, "ultimate", 300, ElementId.Geo);
                    effect.Scaling = StatScaling.Defense;
                    AddStep(effect, ApplyStatus(source, "ultimate-execution-girl", "Execution Girl", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-execution-girl", "Execution Girl", StatusPolarity.Buff, 3,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 2000 })), CardEffectTiming.AfterAction);
                    return effect;
                }
                case "yamazaki97":
                {
                    var effect = Attack(source, "ultimate", 550, EffectTargetScope.SelectedEnemy);
                    AddAttributeBonus(effect, source, "ultimate-blue-bonus", 30, "attribute.blue");
                    AddStep(effect, ApplyStatus(source, "ultimate-lifesteal", "Lifesteal Up", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-lifesteal", "Lifesteal Up", StatusPolarity.Buff, 1,
                            StatModifier(StatId.LifeSteal, ModifierOperation.PercentagePoints, 1500)), 1), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ling-xiaoyu":
                {
                    var effect = Attack(source, "ultimate", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-stance", "Agile Guard", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-stance", "Agile Guard", StatusPolarity.Buff, 3,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 5000 })), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "kazuya-mishima": return Attack(source, "ultimate", 450, EffectTargetScope.SelectedEnemy);
                case "paul-phoenix":
                {
                    var effect = Attack(source, "ultimate", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, Utility(source, "ultimate-remove-stance", EffectKind.RemoveStance, EffectTargetScope.SelectedEnemy), CardEffectTiming.AfterDamage);
                    AddStep(effect, ApplyStatus(source, "ultimate-stun", "Stun", EffectTargetScope.SelectedEnemy, StunRecipe(), 1), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "heihachi-mishima":
                {
                    var effect = Attack(source, "ultimate", 500, EffectTargetScope.SelectedEnemy);
                    AddStep(effect, ApplyStatus(source, "ultimate-vulnerability", "Damage Taken Up", EffectTargetScope.SelectedEnemy,
                        StatRecipe(source, "ultimate-vulnerability", "Damage Taken Up", StatusPolarity.Debuff, 2,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageReceived, Operation = ModifierOperation.PercentagePoints, Amount = 4000 })), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "fischl":
                {
                    var effect = ElementalAttack(source, "ultimate", 400, ElementId.Electro);
                    AddStep(effect, ApplyStatus(source, "ultimate-oz", "Oz", EffectTargetScope.Self, SummonRecipe(source, "ultimate-oz", "Oz", ElementId.Electro, 2), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                case "ningguang":
                {
                    var effect = ElementalAttack(source, "ultimate", 500, ElementId.Geo);
                    AddStep(effect, ApplyStatus(source, "ultimate-star-jade", "Star Jade", EffectTargetScope.Self,
                        StatRecipe(source, "ultimate-star-jade", "Star Jade", StatusPolarity.Buff, 99,
                            new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 300 })), CardEffectTiming.AfterAction);
                    return effect;
                }
                case "xiangling":
                {
                    var effect = ElementalAttack(source, "ultimate", 200, ElementId.Pyro);
                    AddStep(effect, ApplyStatus(source, "ultimate-pyronado", "Pyronado", EffectTargetScope.Self, SummonRecipe(source, "ultimate-pyronado", "Pyronado", ElementId.Pyro, 2), 2), CardEffectTiming.AfterDamage);
                    return effect;
                }
                default: throw new InvalidOperationException("No Ultimate mapping exists for " + source.Name);
            }
        }

        private static PassiveDefinition BuildPassive(SourceFighter source)
        {
            var passive = new PassiveDefinition { Id = source.DefinitionId + ".passive" };
            switch (source.Slug)
            {
                case "andy96":
                    passive.Auras.Add(Aura(source, "pvp-team-defense", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Defense,
                            Operation = ModifierOperation.PercentOfBase, Amount = 2000 }, BattleModeMask.PvP));
                    break;
                case "goro95":
                {
                    var aura = Aura(source, "japan-team-reduction", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        new StatModifierDefinition { Target = ModifierTarget.FinalDamageReduction,
                            Operation = ModifierOperation.PercentagePoints, Amount = 2000 });
                    aura.Targets.TraitId = "trait.japan";
                    passive.Auras.Add(aura);
                    break;
                }
                case "saisyu95":
                {
                    var threshold = new EffectConditionDefinition { Kind = EffectConditionKind.SourceHealthAtMost,
                        Threshold = 5000, Subject = ConditionSubject.Owner };
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-burn",
                        Trigger = PassiveEventKind.TeamTurnStarted,
                        OwnTeamTurnOnly = true,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { threshold.Clone() },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-burning", "Burning", EffectTargetScope.AllEnemies,
                                BurningRecipe(), 3, 2)))
                        }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-damage",
                        Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Enemies,
                        CardOriginOnly = true,
                        Conditions = new List<EffectConditionDefinition> { threshold },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 2000 }
                        }
                    });
                    break;
                }
                case "kyo95":
                {
                    passive.Auras.Add(Aura(source, "burn-damage", PassivePresence.LivingActive, PassiveRelation.Self,
                        new StatModifierDefinition { Target = ModifierTarget.FamilyDamageDealt,
                            Family = DamageFamily.ElementalReaction, Operation = ModifierOperation.PercentagePoints, Amount = 5000 }));
                    foreach (var category in new[] { CardCategory.Attack, CardCategory.AttackDebuff })
                    {
                        var additionalDot = new CardEffectOperationDefinition
                        {
                            Id = source.DefinitionId + ".passive.additional-dot." + category,
                            Kind = CardEffectOperationKind.Damage,
                            Target = EffectTargetScope.TriggerTarget,
                            Damage = new DamageEffectRecipe { Family = DamageFamily.DamageOverTime,
                                Scaling = StatScaling.Attack, CoefficientBp = 7000 }
                        };
                        passive.Reactions.Add(new PassiveReactionDefinition
                        {
                            Id = source.DefinitionId + ".attack-dot." + category,
                            Trigger = PassiveEventKind.AfterAction,
                            ActorRelation = PassiveRelation.Self,
                            TargetRelation = PassiveRelation.Enemies,
                            CardOriginOnly = true,
                            FilterCategory = true,
                            Category = category,
                            Commands = new List<PassiveCommandDefinition> { Execute(additionalDot) }
                        });
                    }
                    var ioriRoster = new EffectConditionDefinition
                    {
                        Kind = EffectConditionKind.RosterCountAtLeast,
                        Threshold = 1,
                        Subject = ConditionSubject.Owner,
                        RosterFilter = new PassiveTargetFilter { Relation = PassiveRelation.Enemies,
                            IncludeOwner = false, IncludeReserve = true, TraitId = "trait.iori" }
                    };
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".iori-counter-buff",
                        Trigger = PassiveEventKind.BattleStarted,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { ioriRoster },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-iori-attack", "Attack Up", EffectTargetScope.Self,
                                StatRecipe(source, "passive-iori-attack", "Attack Up", StatusPolarity.Buff, 99,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 2000)), 99)))
                        }
                    });
                    break;
                }
                case "ryo95":
                {
                    const string counter = "reflection-count";
                    passive.Auras.Add(Aura(source, "reflection-resistance", PassivePresence.LivingActive, PassiveRelation.Self,
                        new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Resistance,
                            Operation = ModifierOperation.PercentagePoints, Amount = 1200 }, scaling: PassiveScaling.OwnerCounter,
                        scalingKey: counter, maximumUnits: 5));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".reflection-counter",
                        Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Enemies,
                        CardOriginOnly = true,
                        ExcludeUltimate = true,
                        FilterCategory = true,
                        Category = CardCategory.Attack,
                        Conditions = new List<EffectConditionDefinition>
                        {
                            new EffectConditionDefinition { Kind = EffectConditionKind.SourceHasStatusTag, Tag = "status.ryo.reflect" }
                        },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter,
                                CounterKey = counter, CounterCap = 5 }
                        }
                    });
                    break;
                }
                case "kim94":
                {
                    const string counter = "critical-stacks";
                    passive.Auras.Add(Aura(source, "critical-recovery", PassivePresence.LivingRoster, PassiveRelation.Self,
                        new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Recovery,
                            Operation = ModifierOperation.PercentagePoints, Amount = 800 }, scaling: PassiveScaling.OwnerCounter,
                        scalingKey: counter, maximumUnits: 7));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".critical-heal-and-stack",
                        Trigger = PassiveEventKind.DamageResolved,
                        ExecutionVersion = 1,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Enemies,
                        CardOriginOnly = true,
                        RequireCritical = true,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter,
                                CounterKey = counter, CounterCap = 7 },
                            Execute(new CardEffectOperationDefinition
                            {
                                Id = source.DefinitionId + ".passive.critical-heal",
                                Kind = CardEffectOperationKind.Heal,
                                Target = EffectTargetScope.Self,
                                Value = new EffectValueDefinition { Source = EffectValueSource.Fixed }
                            }, PassiveValueSource.EventDamage, 10000, PassiveEffectBindingField.HealFixedAmount)
                        }
                    });
                    passive.Reactions.Add(KimUltimateSplash(source));
                    break;
                }
                case "kensou98":
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".ultimate-team-reduction",
                        Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self,
                        TargetRelation = PassiveRelation.Any,
                        CardOriginOnly = true,
                        FilterCategory = true,
                        Category = CardCategory.Ultimate,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-ultimate-reduction", "Damage Reduction", EffectTargetScope.AllAllies,
                                StatRecipe(source, "passive-ultimate-reduction", "Damage Reduction", StatusPolarity.Buff, 3,
                                    new StatModifierDefinition { Target = ModifierTarget.FinalDamageReduction,
                                         Operation = ModifierOperation.PercentagePoints, Amount = 3500 }), 3)))
                        }
                    });
                    break;
                case "rugal94":
                    passive.Auras.Add(Aura(source, "team-evade", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        StatModifier(StatId.Evade, ModifierOperation.PercentagePoints, 3000)));
                    break;
                case "chizuru96":
                    passive.Auras.Add(Aura(source, "self-block", PassivePresence.LivingRoster, PassiveRelation.Self,
                        StatModifier(StatId.BlockChance, ModifierOperation.PercentagePoints, 2000)));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".blocked-damage-reduction", Trigger = PassiveEventKind.DamageResolved,
                        ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self, RequireBlocked = true,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-block-reduction", "Damage Reduction", EffectTargetScope.Self,
                                StatRecipe(source, "passive-block-reduction", "Damage Reduction", StatusPolarity.Buff, 1,
                                    new StatModifierDefinition { Target = ModifierTarget.FinalDamageReduction,
                                        Operation = ModifierOperation.PercentagePoints, Amount = 4500 }), 1)))
                        }
                    });
                    break;
                case "king97":
                    passive.Auras.Add(Aura(source, "team-pierce", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        StatModifier(StatId.Pierce, ModifierOperation.PercentagePoints, 5000)));
                    break;
                case "clark98":
                {
                    var pairedTeam = RosterTraitAtLeast("trait.ikari-team", 2);
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".ralf-team-turn-buff", Trigger = PassiveEventKind.TeamTurnStarted,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { pairedTeam },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-turn-heal",
                                Kind = CardEffectOperationKind.Heal, Target = EffectTargetScope.Self,
                                Value = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 800 } }),
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-turn-attack", "Attack Up", EffectTargetScope.Self,
                                StatRecipe(source, "passive-turn-attack", "Attack Up", StatusPolarity.Buff, 1,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 3000)), 1)))
                        }
                    });
                    break;
                }
                case "ralf98":
                {
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".clark-team-ultimate", Trigger = PassiveEventKind.BattleStarted,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { RosterTraitAtLeast("trait.ikari-team", 2) },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-team-ultimate-damage", "Ultimate Damage Up", EffectTargetScope.AllAllies,
                                StatRecipe(source, "passive-team-ultimate-damage", "Ultimate Damage Up", StatusPolarity.Buff, 99,
                                    new StatModifierDefinition { Target = ModifierTarget.UltimateDamageDealt,
                                        Operation = ModifierOperation.PercentagePoints, Amount = 2000 }), 99)))
                        }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".ultimate-heal-drain", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Ultimate,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-ultimate-heal",
                                Kind = CardEffectOperationKind.Heal, Target = EffectTargetScope.Self,
                                Value = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 800 } }),
                            Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-ultimate-gauge",
                                Kind = CardEffectOperationKind.ChangePowerGauge, Target = EffectTargetScope.TriggerTarget, Magnitude = -1 })
                        }
                    });
                    break;
                }
                case "mature96":
                {
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".vice-team-lifesteal", Trigger = PassiveEventKind.BattleStarted,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { RosterTraitAtLeast("trait.rival-team", 2) },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-team-lifesteal", "Lifesteal Up", EffectTargetScope.Self,
                                StatRecipe(source, "passive-team-lifesteal", "Lifesteal Up", StatusPolarity.Buff, 99,
                                    StatModifier(StatId.LifeSteal, ModifierOperation.PercentagePoints, 2000)), 99)))
                        }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".ultimate-guaranteed-crit", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Ultimate,
                        Conditions = new List<EffectConditionDefinition> { RosterTraitAtLeast("trait.rival-team", 2) },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                        { Kind = PassiveCommandKind.IncreaseCurrentCritChancePercent, Amount = 10000 } }
                    });
                    break;
                }
                case "vice96":
                {
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-team-bonus", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        Conditions = new List<EffectConditionDefinition>
                        {
                            RosterTraitAtLeast("trait.rival-team", 2),
                            new EffectConditionDefinition { Kind = EffectConditionKind.TargetHealthAtMost, Threshold = 5000 }
                        },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                        { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 3000 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".ultimate-poison-bonus", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Ultimate,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.TargetHasRecipe, RecipeId = "status.debuff.poison" } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                            { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 3000 } }
                    });
                    break;
                }
                case "kasumi96":
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".enemy-gauge-attack-down", Trigger = PassiveEventKind.GaugeChanged,
                        ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Enemies,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-gauge-attack-down", "Attack Down", EffectTargetScope.TriggerActor,
                                StatRecipe(source, "passive-gauge-attack-down", "Attack Down", StatusPolarity.Debuff, 2,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, -4000)), 2)))
                        }
                    });
                    break;
                case "mary97":
                    passive.Auras.Add(Aura(source, "resistance", PassivePresence.LivingRoster, PassiveRelation.Self,
                        StatModifier(StatId.Resistance, ModifierOperation.PercentagePoints, 2000)));
                    break;
                case "heidern95":
                {
                    var blueHealth = Aura(source, "blue-team-health", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        StatModifier(StatId.MaxHealth, ModifierOperation.PercentOfBase, 2000));
                    blueHealth.Targets.AttributeId = "attribute.blue";
                    passive.Auras.Add(blueHealth);
                    break;
                }
                case "billy97":
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-crit", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.SourceHealthAtMost, Subject = ConditionSubject.Owner, Threshold = 5000 } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                        { Kind = PassiveCommandKind.IncreaseCurrentCritChancePercent, Amount = 5000 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".guard-burn", Trigger = PassiveEventKind.DamageResolved,
                        ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStandardStatus(source, "passive-guard-burn", "status.debuff.ignite",
                                EffectTargetScope.TriggerActor, 2)))
                        }
                    });
                    break;
                case "armorking":
                {
                    var threshold = new EffectConditionDefinition { Kind = EffectConditionKind.SourceHealthAtMost,
                        Subject = ConditionSubject.Owner, Threshold = 4000 };
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".turn-start-growth", Trigger = PassiveEventKind.TeamTurnStarted,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-turn-stats", "Attack and Defense Up", EffectTargetScope.Self,
                                StatRecipe(source, "passive-turn-stats", "Attack and Defense Up", StatusPolarity.Buff, 5,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 1500),
                                    StatModifier(StatId.Defense, ModifierOperation.PercentOfBase, 1500)), 5)))
                        }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".rage-art", Trigger = PassiveEventKind.TeamTurnStarted,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { threshold },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-rage-art", "Rage Art", EffectTargetScope.Self,
                                StatRecipe(source, "passive-rage-art", "Rage Art", StatusPolarity.Buff, 1,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 3000)), 1)))
                        }
                    });
                    break;
                }
                case "jin":
                {
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".opening-gauge", Trigger = PassiveEventKind.BattleStarted,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                            { Kind = PassiveCommandKind.ChangePowerGauge, Amount = 1 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".rage-art", Trigger = PassiveEventKind.TeamTurnStarted,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.SourceHealthAtMost, Subject = ConditionSubject.Owner, Threshold = 4000 } },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-rage-art", "Rage Art", EffectTargetScope.Self,
                                StatRecipe(source, "passive-rage-art", "Rage Art", StatusPolarity.Buff, 1,
                                    StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 3000)), 1)))
                        }
                    });
                    break;
                }
                case "razor":
                {
                    const string stacks = "electro-sigils";
                    passive.Auras.Add(Aura(source, "electro-sigil-attack", PassivePresence.LivingActive, PassiveRelation.Self,
                        StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 1000), scaling: PassiveScaling.OwnerCounter,
                        scalingKey: stacks, maximumUnits: 10));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".electro-sigil-stack", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Shock } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = stacks, CounterCap = 10 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".skill-heal", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Attack,
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition
                        {
                            Id = source.DefinitionId + ".passive.skill-heal", Kind = CardEffectOperationKind.Heal,
                            Target = EffectTargetScope.Self, Value = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 1500 }
                        }) }
                    });
                    break;
                }
                case "noelle":
                {
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-team-barrier", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
                        LimitScope = PassiveLimitScope.Battle, MaximumActivations = 1,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.SourceHealthAtMost, Subject = ConditionSubject.Owner, Threshold = 4000 } },
                        Commands = new List<PassiveCommandDefinition> { Execute(ToPassiveStatusOperation(ApplyStatus(source,
                            "passive-team-barrier", "Guardian Shield", EffectTargetScope.AllAllies,
                            BarrierRecipe(source, "passive-team-barrier", BarrierSourceStat.Defense, 30000), 2))) }
                    });
                    break;
                }
                case "yamazaki97":
                    passive.Auras.Add(Aura(source, "base-lifesteal", PassivePresence.LivingActive, PassiveRelation.Self,
                        StatModifier(StatId.LifeSteal, ModifierOperation.PercentagePoints, 500)));
                    break;
                case "ling-xiaoyu":
                {
                    const string stacks = "tekken-pierce-stacks";
                    var pierce = Aura(source, "tekken-pierce", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        StatModifier(StatId.Pierce, ModifierOperation.PercentagePoints, 1000), scaling: PassiveScaling.OwnerCounter,
                        scalingKey: stacks, maximumUnits: 4);
                    pierce.Targets.TraitId = "trait.tekken";
                    passive.Auras.Add(pierce);
                    passive.Reactions.Add(CounterOnOwnTurn(source, "pierce-stack", stacks, 4));
                    AddRageArt(passive, source);
                    break;
                }
                case "kazuya-mishima":
                {
                    var teamStats = Aura(source, "tekken-basic-stats", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        BundleModifier(StatBundleKind.BasicStats, ModifierOperation.PercentOfBase, 2000));
                    teamStats.Targets.TraitId = "trait.tekken";
                    passive.Auras.Add(teamStats);
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-protection", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
                        LimitScope = PassiveLimitScope.Battle, MaximumActivations = 1,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.SourceHealthAtMost, Subject = ConditionSubject.Owner, Threshold = 3000 } },
                        Commands = new List<PassiveCommandDefinition> { Execute(ToPassiveStatusOperation(ApplyStatus(source,
                            "passive-invulnerability", "Devil's Protection", EffectTargetScope.Self,
                            StatRecipe(source, "passive-invulnerability", "Devil's Protection", StatusPolarity.Buff, 1,
                                new StatModifierDefinition { Target = ModifierTarget.FinalDamageReduction, Operation = ModifierOperation.PercentagePoints, Amount = 10000 }), 1))) }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".low-health-recovery", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Attack, LimitScope = PassiveLimitScope.Battle, MaximumActivations = 1,
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition
                        {
                            Id = source.DefinitionId + ".passive-recovery", Kind = CardEffectOperationKind.Heal,
                            Target = EffectTargetScope.Self, Value = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 3000 }
                        }) }
                    });
                    AddRageArt(passive, source);
                    break;
                }
                case "paul-phoenix":
                {
                    var repeatAttack = new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".repeat-target-bonus", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Attack, LimitScope = PassiveLimitScope.OwnerTurn, MaximumActivations = 1,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.OwnerCardsPlayedThisTurnAtLeast, Subject = ConditionSubject.Owner, Threshold = 2 } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                            { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 5000 } }
                    };
                    passive.Reactions.Add(repeatAttack);
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".repeat-target-extra", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        FilterCategory = true, Category = CardCategory.Attack, LimitScope = PassiveLimitScope.OwnerTurn, MaximumActivations = 1,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                            { Kind = EffectConditionKind.OwnerCardsPlayedThisTurnAtLeast, Subject = ConditionSubject.Owner, Threshold = 2 } },
                        Commands = new List<PassiveCommandDefinition>
                        {
                            Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-repeat-max-health",
                                Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.TriggerTarget,
                                Damage = new DamageEffectRecipe { Family = DamageFamily.True, Scaling = StatScaling.MaxHealth,
                                    StatSource = DamageStatSource.Target, CoefficientBp = 1600, CannotCrit = true, CannotBlock = true } }),
                            Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-repeat-heal",
                                Kind = CardEffectOperationKind.Heal, Target = EffectTargetScope.Self,
                                Value = new EffectValueDefinition { Source = EffectValueSource.TargetMaxHealth, CoefficientBp = 1600 } }),
                            Execute(ToPassiveStatusOperation(ApplyStatus(source, "passive-team-defense", "Defense Up", EffectTargetScope.AllAllies,
                                StatRecipe(source, "passive-team-defense", "Defense Up", StatusPolarity.Buff, 2,
                                    StatModifier(StatId.Defense, ModifierOperation.PercentOfBase, 1000)), 2)))
                        }
                    });
                    AddRageArt(passive, source);
                    break;
                }
                case "heihachi-mishima":
                {
                    const string stacks = "critical-stacks";
                    var criticalGrowth = Aura(source, "critical-growth", PassivePresence.LivingRoster, PassiveRelation.Self,
                        StatModifier(StatId.CritChance, ModifierOperation.PercentagePoints, 700),
                        scaling: PassiveScaling.OwnerCounter, scalingKey: stacks, maximumUnits: 8);
                    criticalGrowth.Modifiers.Add(StatModifier(StatId.CritDamage, ModifierOperation.PercentagePoints, 700));
                    passive.Auras.Add(criticalGrowth);
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".shock-crit-stack", Trigger = PassiveEventKind.DamageResolved,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true, RequireCritical = true,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Shock } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = stacks, CounterCap = 8 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".shock-crit-bonus", Trigger = PassiveEventKind.BeforeDamage,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true, RequireCritical = true,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Shock } },
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 5000 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".shock-crit-cleanse", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true, RequireCritical = true,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Shock } },
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition
                            { Id = source.DefinitionId + ".passive-remove-shock", Kind = CardEffectOperationKind.Cleanse, Target = EffectTargetScope.TriggerTarget }) }
                    });
                    AddRageArt(passive, source);
                    break;
                }
                case "fischl":
                {
                    var attack = Aura(source, "mondstadt-attack", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 1200));
                    attack.Targets.TraitId = "trait.region.mondstadt";
                    passive.Auras.Add(attack);
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".oz-turn-attack", Trigger = PassiveEventKind.TeamTurnEnded,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.SourceHasStatusTag, Tag = "summon.oz" } },
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-oz-damage",
                            Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.AllEnemies,
                            Damage = new DamageEffectRecipe { Family = DamageFamily.Normal, Scaling = StatScaling.Attack, CoefficientBp = 25000 } }) }
                    });
                    break;
                }
                case "ningguang":
                {
                    var liyue = Aura(source, "liyue-basic-stats", PassivePresence.LivingRoster, PassiveRelation.Allies,
                        BundleModifier(StatBundleKind.BasicStats, ModifierOperation.PercentOfBase, 2000));
                    liyue.Targets.TraitId = "trait.region.liyue";
                    passive.Auras.Add(liyue);
                    const string stacks = "star-jade";
                    passive.Auras.Add(Aura(source, "star-jade-damage", PassivePresence.LivingActive, PassiveRelation.Self,
                        new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 300 },
                        scaling: PassiveScaling.OwnerCounter, scalingKey: stacks, maximumUnits: 10));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".star-jade-stack", Trigger = PassiveEventKind.AfterAction,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
                        Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = stacks, CounterCap = 10 } }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".jade-screen", Trigger = PassiveEventKind.BattleStarted,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Any,
                        Commands = new List<PassiveCommandDefinition> { Execute(ToPassiveStatusOperation(ApplyStatus(source,
                            "passive-jade-screen", "Jade Screen", EffectTargetScope.Self,
                            BarrierRecipe(source, "passive-jade-screen", BarrierSourceStat.MaxHealth, 2000), 2))) }
                    });
                    break;
                }
                case "xiangling":
                {
                    passive.Auras.Add(Aura(source, "pyro-damage", PassivePresence.LivingActive, PassiveRelation.Self,
                        new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 2000 }));
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".guoba-turn-attack", Trigger = PassiveEventKind.TeamTurnEnded,
                        OwnTeamTurnOnly = true, ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.SourceHasStatusTag, Tag = "summon.guoba" } },
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-guoba-damage",
                            Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.AllEnemies,
                            Damage = new DamageEffectRecipe { Family = DamageFamily.Normal, Scaling = StatScaling.Attack, CoefficientBp = 30000 } }) }
                    });
                    passive.Reactions.Add(new PassiveReactionDefinition
                    {
                        Id = source.DefinitionId + ".pyronado-turn-attack", Trigger = PassiveEventKind.TeamTurnEnded,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
                        Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition { Kind = EffectConditionKind.SourceHasStatusTag, Tag = "summon.pyronado" } },
                        Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition { Id = source.DefinitionId + ".passive-pyronado-damage",
                            Kind = CardEffectOperationKind.Damage, Target = EffectTargetScope.AllEnemies,
                            Damage = new DamageEffectRecipe { Family = DamageFamily.Normal, Scaling = StatScaling.Attack, CoefficientBp = 15000 } }) }
                    });
                    break;
                }
            }
            return passive;
        }

        private static PassiveReactionDefinition KimUltimateSplash(SourceFighter source)
        {
            var splash = new CardEffectOperationDefinition
            {
                Id = source.DefinitionId + ".passive.ultimate-splash",
                Kind = CardEffectOperationKind.Damage,
                Target = EffectTargetScope.AllEnemies,
                TargetQuery = new TargetQueryDefinition
                {
                    BaseScope = EffectTargetScope.AllEnemies,
                    Anchor = TargetQueryAnchor.EventTarget,
                    Filter = new PassiveTargetFilter { Relation = PassiveRelation.Enemies, IncludeOwner = false,
                        IncludeReserve = false },
                    ExcludeEventTarget = true,
                    AdjacentToEventTarget = true,
                    Limit = 3
                },
                Damage = new DamageEffectRecipe { Family = DamageFamily.Normal, Scaling = StatScaling.Fixed,
                    CoefficientBp = 10000, KeywordFactorBp = 10000 }
            };
            return new PassiveReactionDefinition
            {
                Id = source.DefinitionId + ".ultimate-splash",
                Trigger = PassiveEventKind.DamageResolved,
                ExecutionVersion = 1,
                ActorRelation = PassiveRelation.Self,
                TargetRelation = PassiveRelation.Enemies,
                CardOriginOnly = true,
                FilterCategory = true,
                Category = CardCategory.Attack,
                Commands = new List<PassiveCommandDefinition>
                {
                    Execute(splash, PassiveValueSource.EventDamage, 3000, PassiveEffectBindingField.DamageFixedAmount)
                }
            };
        }

        private static CardCategory SkillCategory(SourceFighter source, int slot) => source.Slug switch
        {
            "andy96" when slot == 2 => CardCategory.Buff,
            "ryo95" when slot == 2 => CardCategory.Stance,
            "kim94" when slot == 2 => CardCategory.Attack,
            "goro95" when slot == 1 => CardCategory.AttackDebuff,
            "saisyu95" when slot == 2 => CardCategory.AttackDebuff,
            "kyo95" when slot == 1 => CardCategory.AttackDebuff,
            "kensou98" when slot == 2 => CardCategory.AttackDebuff,
            "chizuru96" when slot == 2 => CardCategory.Stance,
            "king97" when slot == 2 => CardCategory.AttackDebuff,
            "ralf98" when slot == 1 => CardCategory.AttackDebuff,
            "ralf98" when slot == 2 => CardCategory.Stance,
            "mature96" when slot == 1 => CardCategory.AttackDebuff,
            "mature96" when slot == 2 => CardCategory.Recovery,
            "vice96" when slot == 2 => CardCategory.AttackDebuff,
            "kasumi96" when slot == 1 => CardCategory.Debuff,
            "mary97" when slot == 1 => CardCategory.AttackDebuff,
            "heidern95" when slot == 2 => CardCategory.AttackDebuff,
            "billy97" when slot == 2 => CardCategory.Stance,
            "armorking" when slot == 1 || slot == 2 => CardCategory.AttackDebuff,
            "jin" when slot == 2 => CardCategory.AttackDebuff,
            "noelle" when slot == 2 => CardCategory.Stance,
            "yamazaki97" when slot == 2 => CardCategory.AttackDebuff,
            "ling-xiaoyu" when slot == 2 => CardCategory.AttackDebuff,
            "heihachi-mishima" when slot == 2 => CardCategory.AttackDebuff,
            "fischl" when slot == 2 => CardCategory.Buff,
            "xiangling" when slot == 2 => CardCategory.Buff,
            _ => CardCategory.Attack
        };

        private static CardCategory UltimateCategory(SourceFighter source) => source.Slug switch
        {
            "andy96" or "saisyu95" or "kyo95" or "ryo95" => CardCategory.AttackDebuff,
            "kensou98" => CardCategory.Recovery,
            "chizuru96" or "vice96" or "kasumi96" or "heidern95" or "billy97" or "armorking" => CardCategory.AttackDebuff,
            "mary97" => CardCategory.Stance,
            "razor" => CardCategory.Buff,
            _ => CardCategory.Attack
        };

        private static PassiveAuraDefinition Aura(SourceFighter source, string name, PassivePresence presence,
            PassiveRelation relation, StatModifierDefinition modifier, BattleModeMask modes = BattleModeMask.All,
            PassiveScaling scaling = PassiveScaling.Constant, string scalingKey = "", int maximumUnits = int.MaxValue)
        {
            var aura = new PassiveAuraDefinition
            {
                Id = source.DefinitionId + ".passive." + name,
                Gate = new PassiveGate { Presence = presence, Modes = modes },
                Targets = new PassiveTargetFilter { Relation = relation, IncludeOwner = true, IncludeReserve = true },
                Scaling = scaling,
                ScalingKey = scalingKey,
                MaximumUnits = maximumUnits
            };
            aura.Modifiers.Add(modifier);
            return aura;
        }

        private static PassiveCommandDefinition Execute(CardEffectOperationDefinition effect,
            PassiveValueSource valueSource = PassiveValueSource.Fixed, int amount = 1,
            PassiveEffectBindingField binding = PassiveEffectBindingField.None) => new PassiveCommandDefinition
        {
            Kind = PassiveCommandKind.ExecuteEffect,
            ValueSource = valueSource,
            Amount = amount,
            ScalingCoefficientBp = 10000,
            EffectBinding = binding,
            Effect = effect
        };

        private static EffectConditionDefinition RosterTraitAtLeast(string traitId, int count) => new EffectConditionDefinition
        {
            Kind = EffectConditionKind.RosterCountAtLeast,
            Threshold = count,
            Subject = ConditionSubject.Owner,
            RosterFilter = new PassiveTargetFilter { Relation = PassiveRelation.Allies, IncludeOwner = true,
                IncludeReserve = true, TraitId = traitId }
        };

        private static StatModifierDefinition BundleModifier(StatBundleKind bundle, ModifierOperation operation, int amount) => new StatModifierDefinition
        {
            Target = ModifierTarget.StatBundle,
            Bundle = bundle,
            Operation = operation,
            Amount = amount,
            SerializedTarget = (int)ModifierTarget.StatBundle,
            SerializedBundle = (int)bundle,
            SerializedOperation = (int)operation
        };

        private static EffectDefinition KeywordAttack(SourceFighter source, string key, int percent, string keyword)
        {
            var effect = Attack(source, key, percent, EffectTargetScope.SelectedEnemy);
            effect.KeywordId = keyword;
            return effect;
        }

        private static EffectDefinition ElementalAttack(SourceFighter source, string key, int percent,
            ElementId element, string keyword = null, bool applyShock = false)
        {
            var effect = Attack(source, key, percent, EffectTargetScope.SelectedEnemy);
            effect.DamageElement = element;
            effect.KeywordId = keyword;
            if (applyShock)
                AddStep(effect, ApplyStandardStatus(source, key + "-shock", "status.debuff.shock", EffectTargetScope.SelectedEnemy, 2), CardEffectTiming.AfterDamage);
            return effect;
        }

        private static EffectDefinition SummonEffect(SourceFighter source, string key, string name,
            ElementId element, int duration, bool grantEvade)
        {
            var effect = ApplyStatus(source, key, name, EffectTargetScope.Self, SummonRecipe(source, key, name, element, duration), duration);
            if (grantEvade)
            {
                var evade = new StatusRecipeDefinition
                {
                    Id = source.DefinitionId + ".status." + key + ".evade",
                    NameKey = "Evasion",
                    Polarity = StatusPolarity.Buff,
                    Behavior = StatusBehavior.Stance,
                    DefaultDuration = 1,
                    DurationClock = StatusDurationClock.TargetTurnEnd,
                    Stacking = StatusStackingPolicy.RefreshDuration,
                    EvadeAttacks = true
                };
                AddStep(effect, ApplyStatus(source, key + "-evade", "Evasion", EffectTargetScope.Self, evade, 1), CardEffectTiming.AfterAction);
            }
            return effect;
        }

        private static StatusRecipeDefinition SummonRecipe(SourceFighter source, string key, string name, ElementId element, int duration)
        {
            var slug = Slug(name);
            return new StatusRecipeDefinition
            {
                Id = source.DefinitionId + ".status." + key,
                NameKey = name,
                Polarity = StatusPolarity.Neutral,
                Behavior = StatusBehavior.Triggered,
                DefaultDuration = duration,
                DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.RefreshDuration,
                Tags = new List<string> { "summon." + slug }
            };
        }

        private static StatusRecipeDefinition BarrierRecipe(SourceFighter source, string key,
            BarrierSourceStat stat, int coefficientBp)
        {
            return new StatusRecipeDefinition
            {
                Id = source.DefinitionId + ".status." + key,
                NameKey = "Shield",
                Polarity = StatusPolarity.Buff,
                Behavior = StatusBehavior.Barrier,
                DefaultDuration = 2,
                DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.RefreshDuration,
                BarrierSourceStat = stat,
                BarrierCoefficientBp = coefficientBp,
                BarrierDurationPolicy = BarrierDurationPolicy.Refresh,
                BarrierAmountPolicy = BarrierAmountPolicy.Replace,
                Tags = new List<string> { "status.barrier" }
            };
        }

        private static PassiveReactionDefinition CounterOnOwnTurn(SourceFighter source, string key, string counter, int cap) =>
            new PassiveReactionDefinition
            {
                Id = source.DefinitionId + "." + key,
                Trigger = PassiveEventKind.TeamTurnStarted,
                OwnTeamTurnOnly = true,
                ActorRelation = PassiveRelation.Self,
                TargetRelation = PassiveRelation.Any,
                Commands = new List<PassiveCommandDefinition> { new PassiveCommandDefinition
                    { Kind = PassiveCommandKind.IncrementCounter, CounterKey = counter, CounterCap = cap } }
            };

        private static void AddRageArt(PassiveDefinition passive, SourceFighter source)
        {
            passive.Reactions.Add(new PassiveReactionDefinition
            {
                Id = source.DefinitionId + ".rage-art",
                Trigger = PassiveEventKind.TeamTurnStarted,
                OwnTeamTurnOnly = true,
                ActorRelation = PassiveRelation.Self,
                TargetRelation = PassiveRelation.Any,
                Conditions = new List<EffectConditionDefinition> { new EffectConditionDefinition
                    { Kind = EffectConditionKind.SourceHealthAtMost, Subject = ConditionSubject.Owner, Threshold = 4000 } },
                Commands = new List<PassiveCommandDefinition> { Execute(ToPassiveStatusOperation(ApplyStatus(source,
                    "passive-rage-art", "Rage Art", EffectTargetScope.Self,
                    StatRecipe(source, "passive-rage-art", "Rage Art", StatusPolarity.Buff, 1,
                        StatModifier(StatId.Attack, ModifierOperation.PercentOfBase, 3000)), 1))) }
            });
        }

        private static void AddAttributeBonus(EffectDefinition root, SourceFighter source, string key, int percent, string attributeId)
        {
            var bonus = Attack(source, key, percent, EffectTargetScope.SelectedEnemy);
            bonus.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetAttributeIs, StringValue = attributeId });
            AddStep(root, bonus, CardEffectTiming.AfterDamage);
        }

        private static EffectDefinition GaugeChange(SourceFighter source, string key, EffectTargetScope target, int amount) => new EffectDefinition
        {
            Id = source.DefinitionId + "." + key,
            Kind = EffectKind.ChangePowerGauge,
            Target = target,
            PowerGaugeAmount = amount
        };

        private static EffectDefinition AttackWithGaugeDrain(SourceFighter source, string key, int percent, int gaugeAmount)
        {
            var effect = Attack(source, key, percent, EffectTargetScope.SelectedEnemy);
            AddStep(effect, GaugeChange(source, key + "-drain", EffectTargetScope.SelectedEnemy, gaugeAmount), CardEffectTiming.AfterDamage);
            return effect;
        }

        private static EffectDefinition AttackWithStatDebuff(SourceFighter source, string key, int percent,
            StatId stat, int amount, int duration)
        {
            var effect = Attack(source, key, percent, EffectTargetScope.SelectedEnemy);
            AddStep(effect, ApplyStatus(source, key + "-debuff", "Stat Down", EffectTargetScope.SelectedEnemy,
                StatRecipe(source, key + "-debuff", "Stat Down", StatusPolarity.Debuff, duration,
                    StatModifier(stat, ModifierOperation.PercentOfBase, amount)), duration), CardEffectTiming.AfterDamage);
            return effect;
        }

        private static EffectDefinition ApplyStandardStatus(SourceFighter source, string key, string recipeId,
            EffectTargetScope target, int duration, int stacks = 1, int chanceBp = 10000)
        {
            var recipe = StandardEffectDatabase.CreateStatusRecipes().FirstOrDefault(item => item.Id == recipeId);
            if (recipe == null) throw new InvalidOperationException("No standard status recipe exists for " + recipeId + ".");
            return ApplyStatus(source, key, recipe.NameKey, target, recipe.Clone(), duration, stacks, chanceBp);
        }

        private static EffectDefinition TauntGuard(SourceFighter source, string key, int damageReductionBp, int duration,
            ModifierTarget bonusTarget = ModifierTarget.AnyDamageDealt, int bonusBp = 0)
        {
            var recipe = new StatusRecipeDefinition
            {
                Id = source.DefinitionId + ".status." + key + "-taunt",
                NameKey = "Taunting Guard",
                Polarity = StatusPolarity.Neutral,
                Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
                Taunt = true,
                DefaultDuration = duration,
                DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.RefreshDuration,
                Tags = new List<string> { CombatTags.Stance },
                RecoverDamageTakenBp = damageReductionBp
            };
            if (bonusBp > 0)
                recipe.Modifiers.Add(new StatModifierDefinition { Target = bonusTarget,
                    Operation = ModifierOperation.PercentagePoints, Amount = bonusBp });
            return ApplyStatus(source, key + "-taunt", "Taunting Guard", EffectTargetScope.Self, recipe, duration);
        }

        private static EffectDefinition TauntWithCounterBurn(SourceFighter source, string key) =>
            ApplyStatus(source, key + "-stance", "Burning Guard", EffectTargetScope.Self,
                new StatusRecipeDefinition
                {
                    Id = source.DefinitionId + ".status." + key + "-stance",
                    NameKey = "Burning Guard",
                    Polarity = StatusPolarity.Neutral,
                    Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
                    Taunt = true,
                    DefaultDuration = 2,
                    DurationClock = StatusDurationClock.TargetTurnEnd,
                    Stacking = StatusStackingPolicy.RefreshDuration,
                    Tags = new List<string> { CombatTags.Stance }
                }, 2);

        private static CardEffectOperationDefinition ToPassiveStatusOperation(EffectDefinition effect)
        {
            if (effect == null || effect.Kind != EffectKind.ApplyStatus || effect.StatusRecipe == null)
                throw new InvalidOperationException("Passive status commands require a configured ApplyStatus effect.");
            return new CardEffectOperationDefinition
            {
                Id = effect.Id,
                Window = CardEffectWindow.AfterAction,
                Kind = CardEffectOperationKind.ApplyStatus,
                Target = effect.Target,
                Status = new StatusApplicationRecipe
                {
                    StatusRecipeId = effect.StatusRecipe.Id,
                    InlineRecipe = effect.StatusRecipe.Clone(),
                    DurationOverride = effect.StatusDurationOverride,
                    StackCount = effect.StatusStackCount,
                    ProcChanceBp = effect.StatusApplyChanceBp
                }
            };
        }

        private static EffectDefinition Attack(SourceFighter source, string key, int percent, EffectTargetScope target) => new EffectDefinition
        {
            Id = source.DefinitionId + "." + key + ".damage",
            Kind = EffectKind.Damage,
            Family = DamageFamily.Normal,
            Scaling = StatScaling.Attack,
            CoefficientBp = percent * 100,
            KeywordFactorBp = 10000,
            Target = target
        };

        private static EffectDefinition ApplyStatus(SourceFighter source, string key, string name,
            EffectTargetScope target, StatusRecipeDefinition recipe, int duration = 0,
            int stacks = 1, int chanceBp = 10000) => new EffectDefinition
        {
            Id = source.DefinitionId + "." + key,
            Kind = EffectKind.ApplyStatus,
            Target = target,
            StatusRecipe = recipe,
            StatusDurationOverride = duration,
            StatusStackCount = stacks,
            StatusApplyChanceBp = chanceBp
        };

        private static EffectDefinition Utility(SourceFighter source, string key, EffectKind kind,
            EffectTargetScope target, int magnitude = 0) => new EffectDefinition
        {
            Id = source.DefinitionId + "." + key,
            Kind = kind,
            Target = target,
            Magnitude = magnitude
        };

        private static StatusRecipeDefinition StatRecipe(SourceFighter source, string key, string name,
            StatusPolarity polarity, int duration, params StatModifierDefinition[] modifiers) => new StatusRecipeDefinition
        {
            Id = source.DefinitionId + ".status." + key,
            NameKey = name,
            Polarity = polarity,
            Behavior = StatusBehavior.Stat,
            DefaultDuration = duration,
            DurationClock = duration == 99 ? StatusDurationClock.Permanent : StatusDurationClock.TargetTurnEnd,
            Stacking = StatusStackingPolicy.RefreshDuration,
            Modifiers = modifiers.ToList()
        };

        private static StatusRecipeDefinition BurningRecipe()
        {
            return new StatusRecipeDefinition
            {
                Id = "status.reaction.burning",
                NameKey = "Burning",
                Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.DamageOverTime,
                DefaultDuration = 3,
                DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.IndependentStacks,
                Identity = StatusIdentityScope.Recipe,
                MaxStacks = 3,
                Tags = new List<string> { CombatTags.Ignite, "status.dot" },
                PeriodicDamage = new PeriodicDamageDefinition
                {
                    Family = DamageFamily.ElementalReaction,
                    Element = ElementId.Pyro,
                    Timing = StatusTickTiming.TargetTurnEnd,
                    Scaling = StatusSnapshotScaling.SourceAttack,
                    CoefficientBp = 30000
                }
            };
        }

        private static StatusRecipeDefinition StunRecipe()
        {
            return StandardEffectDatabase.CreateStatusRecipes().First(recipe => recipe.Id == "status.debuff.stun").Clone();
        }

        private static StatModifierDefinition StatModifier(StatId stat, ModifierOperation operation, int amount) =>
            new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = stat, Operation = operation, Amount = amount };

        private static void AddStep(EffectDefinition root, EffectOperationDefinition step, CardEffectTiming timing)
        {
            root.Sequence.Add(new CardEffectStep { Timing = timing, Effect = EffectStepDefinition.From(step) });
        }

        private static EffectDefinition CloneWithSuffix(EffectDefinition original, string suffix)
        {
            var copy = original.Clone();
            copy.Id += suffix;
            foreach (var step in copy.Sequence)
                if (step?.Effect != null) step.Effect.Id += suffix;
            return copy;
        }

        private static SourceFighter ParseFighter(List<string> row, Dictionary<string, int> headers, int csvRow)
        {
            var name = Field(row, headers, "Name").Trim();
            var slug = name switch
            {
                "Andy Bogard96" => "andy96",
                "Goro Daimon95" => "goro95",
                "Saisyu95" => "saisyu95",
                "Kyo95" => "kyo95",
                "Ryo95" => "ryo95",
                "Kim Kaphwan94" => "kim94",
                "Sie Kensou98" => "kensou98",
                "Rugal94" => "rugal94",
                "Chizuru96" => "chizuru96",
                "King97" => "king97",
                "Clark Still98" => "clark98",
                "Ralf Jone98" => "ralf98",
                "Mature96" => "mature96",
                "Vice96" => "vice96",
                "Kasumi Todoh96" => "kasumi96",
                "Blue Mary97" => "mary97",
                "Heidern95" => "heidern95",
                "Billy Kane97" => "billy97",
                "Armor King" => "armorking",
                "Jin Kazama" => "jin",
                "Razor" => "razor",
                "Noelle" => "noelle",
                "Ryuji Yamazaki97" => "yamazaki97",
                "Ling Xiaoyu" => "ling-xiaoyu",
                "Kazuya Mishima" => "kazuya-mishima",
                "Paul Phoenix" => "paul-phoenix",
                "Heihachi Mishima" => "heihachi-mishima",
                "Fischl" => "fischl",
                "Nigguang" => "ningguang",
                "Xiangling" => "xiangling",
                _ => throw new InvalidOperationException("Unexpected pre-release fighter row: " + name)
            };
            var attribute = Field(row, headers, "Attribute").Trim();
            var fighterAttribute = attribute switch
            {
                "🔴" => FighterAttribute.Red,
                "🟢" => FighterAttribute.Green,
                "🔵" => FighterAttribute.Blue,
                "🟡" => FighterAttribute.Yellow,
                "🟣" => FighterAttribute.Purple,
                _ => throw new InvalidOperationException("Unsupported pre-release attribute " + attribute + " for " + name)
            };
            var tier = Field(row, headers, "Tier").Trim();
            var rarity = tier.IndexOf("SSR", StringComparison.OrdinalIgnoreCase) >= 0 ? FighterRarity.SSR :
                tier.IndexOf("SR", StringComparison.OrdinalIgnoreCase) >= 0 ? FighterRarity.SR :
                tier.IndexOf("R", StringComparison.OrdinalIgnoreCase) >= 0 ? FighterRarity.R :
                throw new InvalidOperationException("Unsupported launch tier " + tier + " for " + name);

            var stats = new StatBlock
            {
                Attack = Integer(row, headers, "ATK"),
                Defense = Integer(row, headers, "DEF"),
                MaxHealth = Integer(row, headers, "HP"),
                CombatClass = Integer(row, headers, "Class Power"),
                PierceBp = BasisPoints(row, headers, "Pierce Rate"),
                ResistanceBp = BasisPoints(row, headers, "Resistence"),
                RegenerationBp = BasisPoints(row, headers, "Regenerate"),
                CritChanceBp = BasisPoints(row, headers, "Crit Chance"),
                CritDamageBp = BasisPoints(row, headers, "Crit Damage"),
                CritResistanceBp = BasisPoints(row, headers, "Crit Resistence"),
                CritDefenseBp = BasisPoints(row, headers, "Crit Defense"),
                RecoveryBp = BasisPoints(row, headers, "Recovery Rate"),
                BlockChanceBp = BasisPoints(row, headers, "Block Chance"),
                BlockPowerBp = BasisPoints(row, headers, "Block Power"),
                LifeStealBp = BasisPoints(row, headers, "Lifesteal"),
                PerceptionBp = 10000,
                ControlBp = 10000,
                AvoidanceBp = 10000
            };
            var traitSource = Field(row, headers, "Trait");
            var traitIds = traitSource.Split(',').Select(value => TraitId(value.Trim())).Distinct(StringComparer.Ordinal).ToList();
            var fighter = new SourceFighter
            {
                CsvRow = csvRow,
                Name = name == "Nigguang" ? "Ningguang" : name,
                Roadmap = Field(row, headers, "Release Roadmap").Trim(),
                Attribute = attribute,
                Tier = tier,
                Traits = traitSource,
                Passive = StripBeforeDash(Field(row, headers, "Passive-Text")),
                Skills = Field(row, headers, "Skill-Text"),
                Ultimate = Field(row, headers, "Ultimate-Text"),
                Slug = slug,
                Family = "family." + slug,
                SeriesId = SeriesFor(slug),
                NativeAttribute = fighterAttribute,
                NativeRarity = rarity,
                Stats = stats,
                TraitIds = traitIds,
                SkillOneText = SkillLine(Field(row, headers, "Skill-Text"), 0),
                SkillTwoText = SkillLine(Field(row, headers, "Skill-Text"), 1),
                UltimateText = StripBeforeDash(Field(row, headers, "Ultimate-Text")),
                SourceStats = BuildSourceStats(row, headers)
            };

            switch (slug)
            {
                case "andy96": fighter.IconFile = "icon-andy96.png"; fighter.FullFile = "full-andy96.png"; fighter.TemplateId = "fighter.andy94"; break;
                case "goro95": fighter.IconFile = "icon-goro95.png"; fighter.FullFile = "full-goro95.png"; fighter.TemplateId = "fighter.goro94"; break;
                case "saisyu95": fighter.IconFile = "icon-saisyu95.png"; fighter.FullFile = "full-saisyu95.png"; break;
                case "kyo95": fighter.IconFile = "icon-kyo95.png"; fighter.FullFile = "full-kyo95.png"; fighter.TemplateId = "fighter.kyo98"; break;
                case "ryo95": fighter.IconFile = "icon-ryo95.png"; fighter.FullFile = "full_ryo95.png"; fighter.TemplateId = "fighter.ryo94"; break;
                case "kim94": fighter.IconFile = "icon-kim94.png"; fighter.FullFile = "full_kim94.png"; break;
                case "kensou98": fighter.IconFile = "icon-kensou98.png"; fighter.FullFile = "full-kensou98.png"; fighter.TemplateId = "fighter.kensou94"; break;
                case "rugal94": fighter.IconFile = "icon-rugal94.png"; fighter.FullFile = "full-rugal94.png"; break;
                case "chizuru96": fighter.IconFile = "icon-chizuru96.png"; fighter.FullFile = "full-chizuru96.png"; break;
                case "king97": fighter.IconFile = "icon-king97.png"; fighter.FullFile = "full-king97.png"; break;
                case "clark98": fighter.IconFile = "icon-clark98.png"; fighter.FullFile = "full-clark98.png"; fighter.TemplateId = "fighter.clark94"; break;
                case "ralf98": fighter.IconFile = "icon-ralf98.png"; fighter.FullFile = "full-ralf98.png"; fighter.TemplateId = "fighter.ralf94"; break;
                case "mature96": fighter.IconFile = "icon-mature96.png"; fighter.FullFile = "full-mature96.png"; break;
                case "vice96": fighter.IconFile = "icon-vice96.png"; fighter.FullFile = "full-vice96.png"; break;
                case "kasumi96": fighter.IconFile = "icon-kasumi96.png"; fighter.FullFile = "full-kasumi96.png"; break;
                case "mary97": fighter.IconFile = "icon-mary97.png"; fighter.FullFile = "full-mary97.png"; break;
                case "heidern95": fighter.IconFile = "icon-heidern95.png"; fighter.FullFile = "full-heidern95.png"; break;
                case "billy97": fighter.IconFile = "icon-billy97.png"; fighter.FullFile = "full_billy97.png"; break;
                case "armorking": fighter.IconFile = "icon-tk-armorking.png"; fighter.FullFile = "full-tk-armorking.png"; break;
                case "jin": fighter.IconFile = "icon-tk-jin.png"; fighter.FullFile = "full-tk-jin.png"; break;
                case "razor": fighter.IconFile = "icon_gi_razor.png"; fighter.FullFile = "Character_Razor_Full_Wish.png"; break;
                case "noelle": fighter.IconFile = "icon-gi-noelle.png"; fighter.FullFile = "full-gi-Noelle.png"; break;
                case "yamazaki97": fighter.IconFile = "full-ryuji97.png"; fighter.FullFile = "full-ryuji97.png"; fighter.IconInFullRoot = true; break;
                case "ling-xiaoyu": fighter.IconFile = "full-tk-xiaoyu.png"; fighter.FullFile = "full-tk-xiaoyu.png"; fighter.IconInFullRoot = true; break;
                case "kazuya-mishima": fighter.IconFile = "icon-tk-kazuya.png"; fighter.FullFile = "full-tk-kazuya.png"; break;
                case "paul-phoenix": fighter.IconFile = "full-tk-paul.png"; fighter.FullFile = "full-tk-paul.png"; fighter.IconInFullRoot = true; break;
                case "heihachi-mishima": fighter.IconFile = "full-tk-heihachi.png"; fighter.FullFile = "full-tk-heihachi.png"; fighter.IconInFullRoot = true; break;
                case "fischl": fighter.IconFile = "icon-gi-fischl.png"; fighter.FullFile = "Character_Fischl_Full_Wish.png"; break;
                case "ningguang": fighter.IconFile = "icon-gi-ningguang.png"; fighter.FullFile = "Character_Ningguang_Full_Wish.png"; break;
                case "xiangling": fighter.IconFile = "icon-gi-xiangling.png"; fighter.FullFile = "Character_Xiangling_Full_Wish.png"; break;
            }
            return fighter;
        }

        private static string SeriesFor(string slug) => slug switch
        {
            "armorking" or "jin" or "ling-xiaoyu" or "kazuya-mishima" or "paul-phoenix" or "heihachi-mishima" => "series.tekken",
            "razor" or "noelle" or "fischl" or "ningguang" or "xiangling" => "series.genshin-impact",
            _ => "series.kof"
        };

        private static List<SourceStatField> BuildSourceStats(List<string> row, Dictionary<string, int> headers)
        {
            var source = new List<(string stat, string field, float value, string unit, float normalized)>
            {
                ("Attack", "ATK", Number(row, headers, "ATK"), "stat", Number(row, headers, "ATK")),
                ("Defense", "DEF", Number(row, headers, "DEF"), "stat", Number(row, headers, "DEF")),
                ("MaxHealth", "HP", Number(row, headers, "HP"), "stat", Number(row, headers, "HP")),
                ("CombatClass", "Class Power", Number(row, headers, "Class Power"), "stat", Number(row, headers, "Class Power")),
                ("Pierce", "Pierce Rate", Percent(row, headers, "Pierce Rate"), "percent", BasisPoints(row, headers, "Pierce Rate")),
                ("Resistance", "Resistence", Percent(row, headers, "Resistence"), "percent", BasisPoints(row, headers, "Resistence")),
                ("Regeneration", "Regenerate", Percent(row, headers, "Regenerate"), "percent", BasisPoints(row, headers, "Regenerate")),
                ("CritChance", "Crit Chance", Percent(row, headers, "Crit Chance"), "percent", BasisPoints(row, headers, "Crit Chance")),
                ("CritDamage", "Crit Damage", Percent(row, headers, "Crit Damage"), "percent", BasisPoints(row, headers, "Crit Damage")),
                ("CritResistance", "Crit Resistence", Percent(row, headers, "Crit Resistence"), "percent", BasisPoints(row, headers, "Crit Resistence")),
                ("CritDefense", "Crit Defense", Percent(row, headers, "Crit Defense"), "percent", BasisPoints(row, headers, "Crit Defense")),
                ("Recovery", "Recovery Rate", Percent(row, headers, "Recovery Rate"), "percent", BasisPoints(row, headers, "Recovery Rate")),
                ("BlockChance", "Block Chance", Percent(row, headers, "Block Chance"), "percent", BasisPoints(row, headers, "Block Chance")),
                ("BlockPower", "Block Power", Percent(row, headers, "Block Power"), "percent", BasisPoints(row, headers, "Block Power")),
                ("LifeSteal", "Lifesteal", Percent(row, headers, "Lifesteal"), "percent", BasisPoints(row, headers, "Lifesteal")),
                ("Evade", "Not supplied", 0, "basis-points", 0),
                ("Perception", "Not supplied", 100, "percent", 10000),
                ("Control", "Not supplied", 100, "percent", 10000),
                ("Avoidance", "Not supplied", 100, "percent", 10000)
            };
            return source.Select(item => new SourceStatField
            {
                StatId = item.stat,
                SourceField = item.field,
                SourceValue = item.value,
                Unit = item.unit,
                Provenance = item.field == "Not supplied" ? "Project neutral default; absent from CSV" : SourcePath,
                Value = item.normalized
            }).ToList();
        }

        private static List<string> BuildReviewNotes(SourceFighter source)
        {
            var notes = new List<string>
            {
                "Source: " + SourcePath + ", CSV row " + source.CsvRow + "; release label: " + source.Roadmap + ".",
                "The source provides one value per Skill and Ultimate. That value is preserved across ranks 1-3 and Ultimate tiers C0-C5 until progression data is supplied.",
                "The source omits Evade, Perception, Control, Avoidance, and level growth. The four secondary fields use the project neutral defaults; growth uses CharacterStatGrowth defaults."
            };
            if (source.Tier.IndexOf("R-SR", StringComparison.OrdinalIgnoreCase) >= 0)
                notes.Add("Source tier R-SR is represented as SR because the runtime CharacterObject supports one rarity per fighter.");
            if (source.Slug == "king97")
                notes.Add("The source describes team Pierce as half the owner's Pierce on entry. The current passive aura model stores the 50% as a flat Pierce percentage-point bonus for the team.");
            if (source.Slug == "mary97")
                notes.Add("The source describes Resistance scaling with lost HP. The current passive aura model stores a flat 20% Resistance bonus; health-scaled progression is not represented by this runtime passive format. The ultimate stance uses its listed 35% Defense value; the 80% max-level value is preserved in the source description.");
            if (source.Slug == "billy97")
                notes.Add("The source describes Crit scaling continuously with lost HP. The current passive mapping applies a 50% Crit bonus below half HP. The ultimate stun uses the listed 40% proc chance; its additional Crit-based chance is preserved in the source description.");
            if (source.Slug == "chizuru96")
                notes.Add("The source applies 45% damage reduction when a block succeeds. The current reaction grants a one-turn reduction status after a blocked hit, so it affects subsequent damage.");
            if (source.Slug == "clark98" || source.Slug == "ralf98")
                notes.Add("The source names the Clark/Ralf pair. The current roster condition checks for two Ikari Team trait members rather than the exact named partner.");
            if (source.Slug == "vice96")
                notes.Add("The source says Ultimate ignores Taunt. The current target-selection rules do not expose an ability-level Taunt bypass; damage and poisoned-target bonus are implemented, with the Taunt clause retained in the source description.");
            if (source.Slug == "kasumi96")
                notes.Add("The source specifies an enemy gaining five gauge. The current event model exposes gauge-change events without a gain threshold binding, so the attack reduction triggers on enemy gauge changes.");
            if (source.Slug == "jin")
                notes.Add("The source describes gaining gauge from the fighter with the highest gauge. The current passive mapping grants one gauge at battle start.");
            if (source.Slug == "ningguang")
                notes.Add("The CSV spells the fighter name Nigguang; the display name and asset slug use the canonical spelling Ningguang/ningguang.");
            if (source.Slug == "razor")
                notes.Add("The wolf form uses a four-turn Basic Stats and damage buff. Per-skill healing is represented on Attack-category skills; its damage protection, card lockout, and Electro follow-up damage are not exposed by the current status model. Electro Sigils stack from attacks against Shocked targets.");
            if (source.Slug == "noelle")
                notes.Add("The runtime supports taunt and Defense-scaled barriers. Shield amplification, shield-triggered team healing, and the Geo conversion of her transformed basic attacks are retained in the source wording but not fully represented by the current status model.");
            if (source.Slug == "yamazaki97")
                notes.Add("The source's missing-HP-based Lifesteal and max-level missing-HP Attack scaling are not dynamic passive inputs in the current runtime model. Skill 2 applies matching Attack/Defense buffs and debuffs as a stat-transfer approximation.");
            if (source.Slug == "ling-xiaoyu")
                notes.Add("Her Tekken Pierce aura stacks by 10% per own turn to a 40% cap. The source does not state the amount of her Ultimate damage reduction, so the current kit applies its listed damage bonus and preserves the reduction clause in the source description.");
            if (source.Slug == "kazuya-mishima")
                notes.Add("The low-health protection and recovery each trigger once per battle. The listed max-HP damage clause on the Ultimate remains in the source description because the current card effect cannot scale damage from the target's max HP.");
            if (source.Slug == "paul-phoenix")
                notes.Add("The current passive approximates the repeat-target condition as a once-per-turn bonus after the second Attack-category card; the runtime does not track whether both cards selected the same target.");
            if (source.Slug == "heihachi-mishima")
                notes.Add("Critical hits against Shocked targets grant the 50% damage bonus, remove debuffs, and add one of up to eight Crit Chance/Crit Damage stacks. Rage Art is modeled below 40% HP.");
            if (source.Slug == "fischl")
                notes.Add("Oz is represented by a two-turn summon marker and a 250% Attack end-of-own-turn hit. The extra Electro hit on elemental reactions is approximated by Oz's turn-end hit because the passive event model does not expose a reaction-target trigger.");
            if (source.Slug == "ningguang")
                notes.Add("Jade Screen is represented by a self barrier at battle start; the current passive counter tracks card actions rather than Geo damage only. Star Jade's DOT-scaled Skill 2 bonus and Geo-team shield-break reward remain in the source descriptions.");
            if (source.Slug == "xiangling")
                notes.Add("Guoba and Pyronado use summon markers with turn-end area damage. The current status model does not bind their periodic hits to Pyro damage, RES reduction, Ignite stacks, or both sides' turn-end events; the listed source wording is retained.");
            return notes;
        }

        private static void AlignAllPreReleasePackages()
        {
            var fighters = CharacterObjectRegistrySO.LoadAll()
                .Where(character => character != null && character.StudioOwned &&
                    character.DefinitionId.StartsWith("fighter.pre-release.", StringComparison.Ordinal))
                .OrderBy(character => character.DefinitionId, StringComparer.Ordinal).ToArray();
            var moves = new List<(UnityEngine.Object asset, string from, string to, string assetName)>();
            foreach (var character in fighters)
            {
                var slug = character.DefinitionId.Substring("fighter.pre-release.".Length);
                var destinationFolder = PreReleaseRoot + SeriesFolderFor(slug);
                if (!AssetDatabase.IsValidFolder(destinationFolder))
                    throw new InvalidOperationException("Missing Pre-Release series folder " + destinationFolder + ". No files were moved.");
                var assets = new UnityEngine.Object[] { character, character.Skill1, character.Skill2, character.Ultimate, character.PassiveDefinition };
                if (assets.Any(asset => asset == null))
                    throw new InvalidOperationException("Incomplete five-asset Studio package for " + character.DefinitionId + ". No files were moved.");
                var filenames = new[] { slug + ".asset", slug + "_skilldata1.asset", slug + "_skilldata2.asset", slug + "_ultdata1.asset", slug + "_Passive.asset" };
                for (var i = 0; i < assets.Length; i++)
                {
                    var from = AssetDatabase.GetAssetPath(assets[i]).Replace('\\', '/');
                    var to = destinationFolder + "/" + filenames[i];
                    if (string.IsNullOrWhiteSpace(from) || !from.EndsWith(".asset", StringComparison.Ordinal))
                        throw new InvalidOperationException("Unsaved or unexpected asset in " + character.DefinitionId + ". No files were moved.");
                    moves.Add((assets[i], from, to, Path.GetFileNameWithoutExtension(filenames[i])));
                }
            }

            var destinations = moves.Select(move => move.to).ToArray();
            if (destinations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != destinations.Length)
                throw new InvalidOperationException("Pre-Release filename collision detected. No files were moved.");
            foreach (var move in moves)
            {
                if (string.Equals(move.from, move.to, StringComparison.Ordinal)) continue;
                var existing = AssetDatabase.LoadMainAssetAtPath(move.to);
                if (existing != null || File.Exists(move.to))
                    throw new InvalidOperationException("Pre-Release destination already exists: " + move.to + ". No files were moved.");
            }

            var moved = new List<(string from, string to)>();
            try
            {
                foreach (var move in moves)
                {
                    if (string.Equals(move.from, move.to, StringComparison.Ordinal)) continue;
                    var error = AssetDatabase.MoveAsset(move.from, move.to);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not align " + move.from + ": " + error);
                    moved.Add((move.from, move.to));
                }
                foreach (var move in moves)
                {
                    move.asset.name = move.assetName;
                    EditorUtility.SetDirty(move.asset);
                }
                AssetDatabase.SaveAssets();

                var studioPackageFolders = fighters.Select(character => StudioRoot + "/pre-release." +
                    character.DefinitionId.Substring("fighter.pre-release.".Length));
                foreach (var folder in moves.Select(move => Path.GetDirectoryName(move.from).Replace('\\', '/'))
                    .Concat(studioPackageFolders).Where(folder => folder.StartsWith(StudioRoot + "/pre-release.", StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal))
                {
                    var absoluteFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var absoluteStudioRoot = Path.GetFullPath(StudioRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!absoluteFolder.StartsWith(absoluteStudioRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Refusing to clean an empty package folder outside the Studio root: " + folder);
                    if (AssetDatabase.IsValidFolder(folder) && Directory.GetFileSystemEntries(absoluteFolder).Length == 0)
                        AssetDatabase.DeleteAsset(folder);
                    if (Directory.Exists(absoluteFolder) && Directory.GetFileSystemEntries(absoluteFolder).Length == 0)
                        Directory.Delete(absoluteFolder);
                    var folderMeta = absoluteFolder + ".meta";
                    if (File.Exists(folderMeta) && !Directory.Exists(absoluteFolder)) File.Delete(folderMeta);
                }
                AssetDatabase.Refresh();
            }
            catch
            {
                foreach (var move in moved.AsEnumerable().Reverse())
                {
                    if (!File.Exists(move.to) || File.Exists(move.from)) continue;
                    var rollbackError = AssetDatabase.MoveAsset(move.to, move.from);
                    if (!string.IsNullOrEmpty(rollbackError)) Debug.LogError("Pre-Release asset move rollback failed for " + move.to + ": " + rollbackError);
                }
                AssetDatabase.Refresh();
                throw;
            }
        }

        private static string SeriesFolderFor(string slug) => SeriesFor(slug) switch
        {
            "series.kof" => "PR_KOF",
            "series.tekken" => "PR_Tekken",
            "series.genshin-impact" => "PR_Genshin-Impact",
            _ => throw new InvalidOperationException("No Pre-Release series folder mapping exists for " + slug)
        };

        private static string PublishedFilesReceipt(CharacterObject character)
        {
            var objects = new UnityEngine.Object[]
            {
                character, character.Skill1, character.Skill2, character.Ultimate, character.PassiveDefinition
            };
            var paths = objects.Select(AssetDatabase.GetAssetPath).ToArray();
            if (paths.Length != 5 || paths.Any(string.IsNullOrEmpty) || paths.Distinct(StringComparer.Ordinal).Count() != 5)
                throw new InvalidOperationException("Existing Studio package does not contain five distinct saved assets: " + character.DefinitionId);
            var hashes = paths.ToDictionary(path => path, path =>
            {
                using (var sha = SHA256.Create())
                    return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
            }, StringComparer.Ordinal);
            return SnapshotJson.Serialize(hashes);
        }

        private static void ApplyVisuals(CharacterObject character, SourceFighter source)
        {
            var serialized = new SerializedObject(character);
            var iconPath = (source.IconInFullRoot ? FullRoot : IconRoot) + source.IconFile;
            var fullPath = FullRoot + source.FullFile;
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            var full = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
            if (icon == null || full == null)
                throw new InvalidOperationException("Launch art is missing or is not imported as a Sprite for " + source.Name + ".");
            serialized.FindProperty("fighterIcon").objectReferenceValue = icon;
            serialized.FindProperty("fighterPic").objectReferenceValue = full;
            serialized.FindProperty("fighterFull").objectReferenceValue = full;

            if (!string.IsNullOrWhiteSpace(source.TemplateId))
            {
                var template = CharacterObjectRegistrySO.LoadAll().FirstOrDefault(candidate => candidate != null && candidate.DefinitionId == source.TemplateId);
                if (template != null)
                {
                    var sourceObject = new SerializedObject(template);
                    foreach (var propertyName in new[] { "fighter3DPrefab", "fighter3DMesh", "fighter3DMaterial" })
                        serialized.FindProperty(propertyName).objectReferenceValue = sourceObject.FindProperty(propertyName).objectReferenceValue;
                    serialized.FindProperty("fighterModelYawOffset").floatValue = template.FighterModelYawOffset;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
        }

        private static List<List<string>> ReadCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else if (ch == '"') quoted = false;
                    else field.Append(ch);
                    continue;
                }
                if (ch == '"') quoted = true;
                else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r' || ch == '\n')
                {
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row.ToArray()); row.Clear();
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else field.Append(ch);
            }
            if (quoted) throw new InvalidOperationException("The pre-release CSV ends inside a quoted field.");
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }
            return rows.Select(values => values.ToList()).ToList();
        }

        private static string Field(List<string> row, Dictionary<string, int> headers, string name)
        {
            if (!headers.TryGetValue(name, out var index) || index >= row.Count)
                throw new InvalidOperationException("CSV is missing required field " + name + ".");
            return row[index] ?? string.Empty;
        }

        private static int Integer(List<string> row, Dictionary<string, int> headers, string field) =>
            Mathf.RoundToInt(Number(row, headers, field));

        private static int BasisPoints(List<string> row, Dictionary<string, int> headers, string field) =>
            Mathf.RoundToInt(Percent(row, headers, field) * 100f);

        private static float Percent(List<string> row, Dictionary<string, int> headers, string field) =>
            Number(row, headers, field);

        private static float Number(List<string> row, Dictionary<string, int> headers, string field)
        {
            var text = Field(row, headers, field).Trim().Replace(",", string.Empty).Replace("%", string.Empty);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException("CSV value for " + field + " is not numeric: " + text);
            return value;
        }

        private static string SkillLine(string source, int index)
        {
            var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
                .Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
            if (index >= lines.Length) throw new InvalidOperationException("A launch fighter is missing Skill " + (index + 1) + " text.");
            return StripBeforeDash(lines[index]);
        }

        private static string StripBeforeDash(string source)
        {
            var dash = source.IndexOf('-');
            return (dash >= 0 ? source.Substring(dash + 1) : source).Trim();
        }

        private static string TraitId(string source)
        {
            switch (source.Trim())
            {
                case "Fatal Fury": return "trait.fatal-fury";
                case "Hero Team": return "trait.hero-team";
                case "Japan": return "trait.japan";
                case "KOF": return "trait.series-kof";
                case "Male": return "trait.male";
                case "Female": return "trait.women";
                case "Fire Element": return "trait.fire-elements";
                case "Three Sacred Treasures": return "trait.sacred-treasure";
                case "Art of Fighting": return "trait.art-of-fighting";
                case "Korea": return "trait.korea-justice";
                case "China": return "trait.chinese";
                case "Psycho Soldier": return "trait.psycho-soldier";
                case "Women Fighter Team": return "trait.women-fighter-team";
                case "Ikari Team": return "trait.ikari-team";
                case "Rival Team": return "trait.rival-team";
                case "Anti-Hero Team": return "trait.anti-hero-team";
                case "Boss Syndrome": return "trait.boss-syndrome";
                case "Orochi Blood": return "trait.orochi-blood";
                case "Secret Agent": return "trait.secret-agent";
                case "South Town": return "trait.south-town";
                case "Tekken": return "trait.tekken";
                case "Genshin-Monstadt": return "trait.region.mondstadt";
                case "Genshin-Liyue": return "trait.region.liyue";
                case "Genshin Impact": return "trait.genshin-impact";
                default: return "trait." + Slug(source);
            }
        }

        private static string Slug(string value)
        {
            var result = new StringBuilder();
            var needsDash = false;
            foreach (var ch in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (needsDash && result.Length > 0) result.Append('-');
                    result.Append(ch);
                    needsDash = false;
                }
                else needsDash = true;
            }
            return result.ToString();
        }
    }
}
#endif
