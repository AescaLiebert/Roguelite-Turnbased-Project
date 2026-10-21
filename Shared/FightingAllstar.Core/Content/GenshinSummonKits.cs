using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    /// <summary>Source-backed upgrades for the three pre-release summon kits; safe to apply repeatedly.</summary>
    public static class GenshinSummonKits
    {
        public const string GuobaId = "summon.guoba";
        public const string OzId = "summon.oz";
        public const string JadeScreenId = "geo-construct.jade-screen";
        public const string PyronadoId = "summon.pyronado";
        public const string StarJadeRecipeId = "fighter.pre-release.ningguang.status.star-jade";
        public const string SourcePath = "ArtDirection/Character Database/Pre-Release-Character.csv";

        public static bool IsSupported(string id) => id == "fighter.pre-release.fischl" ||
            id == "fighter.pre-release.xiangling" || id == "fighter.pre-release.ningguang";

        public static void Upgrade(CharacterDefinition character)
        {
            if (character == null || !IsSupported(character.Id)) return;
            if (character.Id.EndsWith(".fischl", StringComparison.Ordinal)) UpgradeFischl(character);
            else if (character.Id.EndsWith(".xiangling", StringComparison.Ordinal)) UpgradeXiangling(character);
            else UpgradeNingguang(character);
            character.PassiveId = character.Passive.Id;
            character.ReviewNotes ??= new List<string>();
            character.ReviewNotes.RemoveAll(note => note != null &&
                (note.StartsWith("Oz is represented", StringComparison.Ordinal) ||
                 note.StartsWith("Jade Screen is represented", StringComparison.Ordinal) ||
                 note.StartsWith("Guoba and Pyronado use", StringComparison.Ordinal) ||
                 note.StartsWith("Summon kit migration:", StringComparison.Ordinal)));
            character.ReviewNotes.Add("Summon kit migration: source-backed entities and elemental riders replace the legacy marker/self-barrier approximations. Owner turn means owner-side turn end; maximum-level Ultimate values use C5. DOT-scaled clauses multiply the explicit DOT Dealt bonus, without adding a base 100%. Ningguang Skill 2 snapshots layers after BeforeAction, then consumes all Star Jade after the action, including layers gained by its Geo hit.");
            if (character.Id.EndsWith(".fischl", StringComparison.Ordinal))
                character.ReviewNotes.Add("Summon kit migration: Fischl Skill 1's unqualified Max HP clause retains the prior source-owner Max HP interpretation, now as Additional damage; that extra damage has no specified element. Oz retains the approved plan's all-enemy periodic target; the source does not explicitly specify its target scope.");
        }

        public static BattleEntityDefinition CreateOz() => new BattleEntityDefinition
        {
            Id = OzId, NameKey = "Oz", PresentationKey = OzId, Duration = 2,
            MarkerRecipe = Marker(OzId, "Oz", "summon.oz"),
            ActionTarget = EffectTargetScope.AllEnemies,
            TurnEndAttack = Attack(ElementId.Electro, 25000),
            ReactionFollowUp = Attack(ElementId.Electro, 8000)
        };

        public static BattleEntityDefinition CreateGuoba()
        {
            var entity = new BattleEntityDefinition
            {
                Id = GuobaId, NameKey = "Guoba", PresentationKey = GuobaId, Duration = 2,
                MarkerRecipe = Marker(GuobaId, "Guoba", "summon.guoba"),
                TurnEndAttack = new TriggeredElementalAttackDefinition { Element = ElementId.Pyro,
                    AttackCoefficientBp = 30000, StatusRecipeId = "status.debuff.ignite", StatusDuration = 3, StatusStackCount = 2 },
                ActionTarget = EffectTargetScope.AllEnemies,
                AttackCoefficientPerDotDealtBp = 8000
            };
            entity.Auras.Add(Aura(GuobaId + ".owner-dot-and-protection", PassiveRelation.Self,
                Stat(StatId.DamageOverTimeDealt, ModifierOperation.PercentagePoints, 2000),
                new StatModifierDefinition { Target = ModifierTarget.AnyDamageReceived,
                    Operation = ModifierOperation.PercentagePoints, Amount = -2000 }));
            entity.OnHitStatuses.Add(ElementalStatus("fighter.pre-release.xiangling.status.guoba-pyro-resistance",
                "Guoba: Pyro Resistance Down", ElementId.Pyro, 0, -2000, 2, StatusPolarity.Debuff));
            return entity;
        }

        public static BattleEntityDefinition CreateJadeScreen() => new BattleEntityDefinition
        {
            Id = JadeScreenId, NameKey = "Jade Screen", PresentationKey = JadeScreenId,
            Category = BattleEntityCategory.GeoConstruct, Duration = 2,
            MarkerRecipe = Marker(JadeScreenId, "Jade Screen", "geo-construct.jade-screen"),
            CapacityCoefficientBp = 2000, CapacitySourceStat = StatId.MaxHealth,
            DepletedStatus = ElementalStatus("fighter.pre-release.ningguang.status.jade-screen-geo-damage",
                "Jade Screen: Geo Damage", ElementId.Geo, 1200, 0, 2, StatusPolarity.Buff),
            DepletedTargets = new PassiveTargetFilter { Relation = PassiveRelation.Allies,
                IncludeOwner = true, IncludeReserve = false, TraitId = "trait.genshin-geo" }
        };

        public static BattleEntityDefinition CreatePyronado(bool maximumLevel) => new BattleEntityDefinition
        {
            Id = PyronadoId, NameKey = "Pyronado", PresentationKey = PyronadoId,
            Duration = maximumLevel ? 3 : 2,
            MarkerRecipe = Marker(PyronadoId, "Pyronado", "summon.pyronado"),
            TurnEndAttack = Attack(ElementId.Pyro, maximumLevel ? 25000 : 15000),
            ActionTarget = EffectTargetScope.AllEnemies, ActOnBothSidesTurnEnd = true
        };

        public static StatusRecipeDefinition CreateStarJade()
        {
            var status = ElementalStatus(StarJadeRecipeId, "Star Jade", ElementId.Geo, 300, 0, 1, StatusPolarity.Buff);
            status.DurationClock = StatusDurationClock.Permanent;
            status.Stacking = StatusStackingPolicy.AddStacks;
            status.MaxStacks = 10;
            status.Color = StatusColor.Grey;
            status.Tags.Add("resource.star-jade");
            return status;
        }

        private static void UpgradeFischl(CharacterDefinition character)
        {
            var passive = NewPassive(character);
            var mondstadt = Aura(character.Id + ".passive.mondstadt-attack", PassiveRelation.Allies,
                Stat(StatId.Attack, ModifierOperation.PercentOfBase, 1200));
            mondstadt.Gate.Presence = PassivePresence.LivingRoster;
            mondstadt.Targets.IncludeReserve = true;
            mondstadt.Targets.TraitId = "trait.region.mondstadt";
            passive.Auras.Add(mondstadt);
            foreach (var skill in character.Skills)
            foreach (var rank in skill.Ranks)
            {
                if (skill.Slot == 1)
                {
                    rank.Effect = Damage(character.Id + ".skill1.r" + rank.Rank, ElementId.Electro, 45000);
                    AddStep(rank.Effect, new EffectDefinition { Id = rank.Effect.Id + ".max-health",
                        Kind = EffectKind.Damage, Family = DamageFamily.Additional, Scaling = StatScaling.MaxHealth,
                        CoefficientBp = 800,
                        Target = EffectTargetScope.SelectedEnemy }, CardEffectTiming.AfterDamage);
                }
                else
                {
                    rank.Effect = Summon(character.Id + ".skill2.r" + rank.Rank, CreateOz());
                    var evade = new StatusRecipeDefinition { Id = character.Id + ".status.skill2-evasion",
                        NameKey = "Evasion", Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stance,
                        DefaultDuration = 1, DurationClock = StatusDurationClock.TargetTurnEnd,
                        SkipApplyingTurnEnd = true, Stacking = StatusStackingPolicy.RefreshDuration, EvadeAttacks = true };
                    AddStep(rank.Effect, Status(character.Id + ".skill2.evade", evade, EffectTargetScope.Self), CardEffectTiming.AfterAction);
                }
                ApplySkillScope(skill, rank);
            }
            foreach (var tier in character.UltimateTiers)
            {
                tier.Effect = Damage(character.Id + ".ultimate.c" + tier.Tier, ElementId.Electro, 40000);
                AddStep(tier.Effect, Summon(tier.Effect.Id + ".oz", CreateOz()), CardEffectTiming.AfterDamage);
            }
            character.HasUltimateCardSettings = true;
            character.UltimateCategory = CardCategory.Attack;
            character.UltimateTargetScope = EffectTargetScope.SelectedEnemy;
        }

        private static void UpgradeXiangling(CharacterDefinition character)
        {
            var passive = NewPassive(character);
            var pyro = ElementalStatus(character.Id + ".status.passive-pyro", "Pyro Damage",
                ElementId.Pyro, 2000, 0, 1, StatusPolarity.Buff);
            pyro.Color = StatusColor.Grey;
            pyro.DurationClock = StatusDurationClock.Permanent;
            passive.Reactions.Add(OpeningStatus(character, pyro));
            foreach (var skill in character.Skills)
            foreach (var rank in skill.Ranks)
            {
                rank.Effect = skill.Slot == 1
                    ? Damage(character.Id + ".skill1.r" + rank.Rank, ElementId.None, 50000, "attack.clash")
                    : Summon(character.Id + ".skill2.r" + rank.Rank, CreateGuoba());
                if (skill.Slot == 2)
                {
                    var teamAttack = new StatusRecipeDefinition { Id = character.Id + ".status.skill2-team-attack",
                        NameKey = "Attack Up", Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stat,
                        DefaultDuration = 2, DurationClock = StatusDurationClock.TargetTurnEnd,
                        Stacking = StatusStackingPolicy.RefreshDuration,
                        Modifiers = new List<StatModifierDefinition> { Stat(StatId.Attack, ModifierOperation.PercentOfBase, 1200) } };
                    AddStep(rank.Effect, Status(rank.Effect.Id + ".team-attack", teamAttack, EffectTargetScope.AllAllies), CardEffectTiming.AfterAction);
                }
                ApplySkillScope(skill, rank);
            }
            foreach (var tier in character.UltimateTiers)
            {
                tier.Effect = Damage(character.Id + ".ultimate.c" + tier.Tier, ElementId.Pyro, 20000);
                AddStep(tier.Effect, Summon(tier.Effect.Id + ".pyronado", CreatePyronado(tier.Tier == 5)), CardEffectTiming.AfterDamage);
            }
            character.HasUltimateCardSettings = true;
            character.UltimateCategory = CardCategory.Attack;
            character.UltimateTargetScope = EffectTargetScope.SelectedEnemy;
        }

        private static void UpgradeNingguang(CharacterDefinition character)
        {
            var passive = NewPassive(character);
            var liyue = Aura(character.Id + ".passive.liyue-basic-stats", PassiveRelation.Allies,
                new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.BasicStats,
                    Operation = ModifierOperation.PercentOfBase, Amount = 2000 });
            liyue.Gate.Presence = PassivePresence.LivingRoster;
            liyue.Targets.IncludeReserve = true;
            liyue.Targets.TraitId = "trait.region.liyue";
            passive.Auras.Add(liyue);
            passive.Reactions.Add(new PassiveReactionDefinition
            {
                Id = character.Id + ".passive.jade-screen", ExecutionVersion = 1, Trigger = PassiveEventKind.BattleStarted,
                Gate = new PassiveGate { Presence = PassivePresence.LivingActive },
                ActorRelation = PassiveRelation.Any, TargetRelation = PassiveRelation.Any,
                Commands = new List<PassiveCommandDefinition> { Execute(new CardEffectOperationDefinition {
                    Id = character.Id + ".passive.jade-screen.summon", Kind = CardEffectOperationKind.Summon,
                    Target = EffectTargetScope.Self, Summon = CreateJadeScreen() }) }
            });
            passive.Reactions.Add(new PassiveReactionDefinition
            {
                Id = character.Id + ".passive.star-jade", ExecutionVersion = 1, Trigger = PassiveEventKind.DamageResolved,
                Gate = new PassiveGate { Presence = PassivePresence.LivingActive },
                ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
                RequiredDamageElement = ElementId.Geo, AllowReactionOrigin = true,
                LimitScope = PassiveLimitScope.Event,
                Commands = new List<PassiveCommandDefinition> { Execute(StatusOperation(character.Id + ".passive.star-jade.gain", CreateStarJade())) }
            });
            foreach (var skill in character.Skills)
            foreach (var rank in skill.Ranks)
            {
                rank.Effect = Damage(character.Id + ".skill" + skill.Slot + ".r" + rank.Rank,
                    ElementId.Geo, skill.Slot == 1 ? 45000 : 35000, skill.Slot == 1 ? "attack.sever" : null);
                if (skill.Slot == 2)
                {
                    rank.Effect.SourceStackRecipeId = StarJadeRecipeId;
                    rank.Effect.SourceStackDamageBp = 3000;
                    rank.Effect.SourceStackDotScaleBp = 10000;
                    rank.Effect.ConsumeSourceStacks = true;
                }
                ApplySkillScope(skill, rank);
            }
            foreach (var tier in character.UltimateTiers)
            {
                tier.Effect = Damage(character.Id + ".ultimate.c" + tier.Tier, ElementId.Geo, 50000);
                tier.Effect.SourceStackRecipeId = StarJadeRecipeId;
                tier.Effect.SourceStackDamageBp = 5000;
                tier.Effect.SourceStackDotScaleBp = 10000;
                var gain = Status(tier.Effect.Id + ".star-jade", CreateStarJade(), EffectTargetScope.Self);
                gain.StatusStackCount = tier.Tier == 5 ? 10 : 1;
                AddStep(tier.Effect, gain, CardEffectTiming.BeforeAction);
            }
            character.HasUltimateCardSettings = true;
            character.UltimateCategory = CardCategory.Attack;
            character.UltimateTargetScope = EffectTargetScope.SelectedEnemy;
        }

        private static PassiveDefinition NewPassive(CharacterDefinition character)
        {
            var passive = new PassiveDefinition { Id = character.Id + ".passive" };
            character.Passive = passive;
            return passive;
        }

        private static PassiveReactionDefinition OpeningStatus(CharacterDefinition character, StatusRecipeDefinition recipe) =>
            new PassiveReactionDefinition { Id = recipe.Id + ".opening", ExecutionVersion = 1,
                Trigger = PassiveEventKind.BattleStarted, ActorRelation = PassiveRelation.Any,
                TargetRelation = PassiveRelation.Any, Gate = new PassiveGate { Presence = PassivePresence.LivingRoster },
                Commands = new List<PassiveCommandDefinition> { Execute(StatusOperation(recipe.Id + ".apply", recipe)) } };

        private static CardEffectOperationDefinition StatusOperation(string id, StatusRecipeDefinition recipe) =>
            new CardEffectOperationDefinition { Id = id, Kind = CardEffectOperationKind.ApplyStatus,
                Target = EffectTargetScope.Self, Status = new StatusApplicationRecipe { StatusRecipeId = recipe.Id,
                    InlineRecipe = recipe, DurationOverride = recipe.DefaultDuration } };

        private static PassiveCommandDefinition Execute(CardEffectOperationDefinition operation) =>
            new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect, Effect = operation };

        private static PassiveAuraDefinition Aura(string id, PassiveRelation relation, params StatModifierDefinition[] modifiers) =>
            new PassiveAuraDefinition { Id = id, Gate = new PassiveGate { Presence = PassivePresence.LivingActive },
                Targets = new PassiveTargetFilter { Relation = relation, IncludeOwner = true, IncludeReserve = false },
                Modifiers = new List<StatModifierDefinition>(modifiers) };

        private static StatModifierDefinition Stat(StatId stat, ModifierOperation operation, int amount) =>
            new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = stat, Operation = operation, Amount = amount };

        private static TriggeredElementalAttackDefinition Attack(ElementId element, int coefficient) =>
            new TriggeredElementalAttackDefinition { Element = element, AttackCoefficientBp = coefficient };

        private static StatusRecipeDefinition Marker(string id, string name, string tag) =>
            new StatusRecipeDefinition { Id = id + ".marker", NameKey = name, Polarity = StatusPolarity.Buff,
                Color = StatusColor.Grey, Behavior = StatusBehavior.Triggered, DefaultDuration = 2,
                Stacking = StatusStackingPolicy.ReplaceAlways, Tags = new List<string> { tag } };

        private static StatusRecipeDefinition ElementalStatus(string id, string name, ElementId element,
            int damage, int resistance, int duration, StatusPolarity polarity) => new StatusRecipeDefinition
            {
                Id = id, NameKey = name, Polarity = polarity, Behavior = StatusBehavior.Stat,
                DefaultDuration = duration, DurationClock = StatusDurationClock.TargetTurnEnd,
                Stacking = StatusStackingPolicy.RefreshDuration,
                ElementalModifiers = new List<ElementalModifierDefinition> { new ElementalModifierDefinition {
                    Element = element, DamageDealtBp = damage, ResistanceBp = resistance } }
            };

        private static EffectDefinition Damage(string id, ElementId element, int coefficient, string keyword = null) =>
            new EffectDefinition { Id = id, Kind = EffectKind.Damage, Family = DamageFamily.Normal,
                Scaling = StatScaling.Attack, CoefficientBp = coefficient, DamageElement = element,
                Target = EffectTargetScope.SelectedEnemy, KeywordId = keyword };

        private static EffectDefinition Summon(string id, BattleEntityDefinition entity) =>
            new EffectDefinition { Id = id, Kind = EffectKind.Summon, Target = EffectTargetScope.Self, Summon = entity };

        private static EffectDefinition Status(string id, StatusRecipeDefinition recipe, EffectTargetScope target) =>
            new EffectDefinition { Id = id, Kind = EffectKind.ApplyStatus, Target = target,
                StatusRecipe = recipe, StatusDurationOverride = recipe.DefaultDuration };

        private static void AddStep(EffectDefinition root, EffectDefinition step, CardEffectTiming timing) =>
            root.Sequence.Add(new CardEffectStep { Timing = timing, Effect = EffectStepDefinition.From(step) });

        private static void ApplySkillScope(SkillDefinition skill, SkillRankDefinition rank)
        {
            rank.Category = rank.Effect.Kind == EffectKind.Summon ? CardCategory.Buff : CardCategory.Attack;
            rank.TargetScope = rank.Effect.Target;
            skill.Category = rank.Category;
            skill.TargetScope = rank.TargetScope;
            skill.SourceTarget = rank.TargetScope.ToString();
        }
    }
}
