using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    /// <summary>Stable recipe IDs and source-backed defaults from the combat database.</summary>
    public static class StandardEffectDatabase
    {
        public static EffectDatabaseDefinition Create() => new EffectDatabaseDefinition {
            ContentVersion = "7dsgc-sheet-foundation-v1", StatusRecipes = CreateStatusRecipes(),
            AttackEffectRecipes = CreateAttackEffects(), CardEffectRecipes = new List<CardEffectRecipeDefinition>() };

        public static List<AttackEffectRecipeDefinition> CreateAttackEffects() => new List<AttackEffectRecipeDefinition>
        {
            Attack("attack.charge", AttackEffectKind.Charge),
            Attack("attack.shatter", AttackEffectKind.Shatter),
            Attack("attack.rupture", AttackEffectKind.Rupture, 20000),
            Attack("attack.detonate", AttackEffectKind.Detonate, 2000),
            Attack("attack.blaze", AttackEffectKind.Blaze, 2500),
            Attack("attack.depletes", AttackEffectKind.Depletes),
            Attack("attack.fills", AttackEffectKind.Fills),
            Attack("attack.clash", AttackEffectKind.Clash, 2000, 4),
            Attack("attack.co-destruction", AttackEffectKind.CoDestruction, 2000),
            Attack("attack.sever", AttackEffectKind.Sever, 30000),
            Attack("attack.spike", AttackEffectKind.Spike, 20000),
            Attack("attack.breakthrough", AttackEffectKind.Breakthrough),
            Attack("attack.despair", AttackEffectKind.Despair),
            Attack("attack.pierce", AttackEffectKind.Pierce, 30000),
            Attack("attack.weakpoint", AttackEffectKind.Weakpoint, 30000),
            Attack("attack.flood", AttackEffectKind.Flood, 8000),
            Attack("attack.power-strike", AttackEffectKind.PowerStrike, 10000),
            Attack("attack.cleave", AttackEffectKind.Cleave, 7500),
            Attack("attack.quell", AttackEffectKind.Quell, 5000),
            Attack("attack.amplify", AttackEffectKind.Amplify, 3000),
            Attack("attack.secret-technique", AttackEffectKind.SecretTechnique, 2000),
            Attack("attack.remove-buff", AttackEffectKind.RemoveBuff, window: CardEffectWindow.Damaging),
            Attack("attack.cancel-stance", AttackEffectKind.CancelStance, window: CardEffectWindow.Damaging),
            Attack("attack.wave", AttackEffectKind.Wave, ready: false),
            Attack("attack.absorb-energy", AttackEffectKind.AbsorbEnergy, 3000, 3, DamageFamily.Additional,
                CardEffectWindow.AfterDamage),
            Attack("attack.dot-burst", AttackEffectKind.DotBurst, family: DamageFamily.Additional,
                window: CardEffectWindow.AfterDamage, ready: false),
            Attack("attack.dot-explosion", AttackEffectKind.DotExplosion, family: DamageFamily.Additional,
                window: CardEffectWindow.AfterDamage, ready: false),
            Attack("attack.abyss", AttackEffectKind.Abyss, family: DamageFamily.DamageOverTime,
                window: CardEffectWindow.AfterDamage, ready: false)
        };

        public static List<StatusRecipeDefinition> CreateStatusRecipes()
        {
            var recipes = new List<StatusRecipeDefinition>
            {
                new StatusRecipeDefinition { Id = "status.debuff.ignite", Polarity = StatusPolarity.Debuff,
                    Behavior = StatusBehavior.Stat, Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 10,
                    Tags = new List<string> { CombatTags.Ignite, "status.dot" },
                    Modifiers = new List<StatModifierDefinition> { new StatModifierDefinition {
                        Target = ModifierTarget.AnyDamageReceived, Operation = ModifierOperation.PercentagePoints, Amount = 1000 } } },
                Dot("status.debuff.bleed", CombatTags.Bleed, 3300),
                Dot("status.debuff.shock", CombatTags.Shock, 3600),
                Dot("status.debuff.poison", CombatTags.Poison, 4500),
                Disable("status.debuff.seal", CombatTags.Seal, CardCategoryMask.AllCards),
                Disable("status.debuff.paralyze", CombatTags.Paralyze, CardCategoryMask.AllCards),
                Disable("status.debuff.stun", CombatTags.Stun, CardCategoryMask.AllCards),
                Disable("status.debuff.disable-attack", "disable.attack", CardCategoryMask.Attack),
                Disable("status.debuff.disable-debuff", "disable.debuff", CardCategoryMask.Debuff),
                Disable("status.debuff.disable-buff", "disable.buff", CardCategoryMask.Buff),
                PreventRecovery("status.debuff.disable-recovery", "disable.recovery"),
                Disable("status.debuff.disable-healing-card", "disable.healing-card", CardCategoryMask.Recovery),
                Disable("status.debuff.disable-stance", "disable.stance", CardCategoryMask.Stance | CardCategoryMask.ReceiveStances),
                Disable("status.debuff.disable-ultimate", "disable.ultimate", CardCategoryMask.Ultimate),
                Disable("status.debuff.disable-rank-2-3", "disable.rank", CardCategoryMask.RankTwoOrThree),
                Disable("status.debuff.disable-card-effect", "disable.card-effect", CardCategoryMask.CardEffects)
            };
            AddStatBundlePair(recipes, "basic", StatBundleKind.BasicStats);
            AddStatGroupPair(recipes, "defense", StatId.Defense);
            AddStatBundlePair(recipes, "attack-related", StatBundleKind.AttackRelated);
            AddStatBundlePair(recipes, "defense-related", StatBundleKind.DefenseRelated);
            AddStatBundlePair(recipes, "hp-related", StatBundleKind.HpRelated);
            AddStatGroupPair(recipes, "special", StatId.BlockChance, StatId.BlockPower, StatId.Evade,
                StatId.Perception, StatId.Control, StatId.Avoidance);
            AddStatBundlePair(recipes, "all", StatBundleKind.AllStats);
            return recipes;
        }

        private static AttackEffectRecipeDefinition Attack(string id, AttackEffectKind kind, int primary = 0,
            int secondary = 0, DamageFamily family = DamageFamily.Normal,
            CardEffectWindow window = CardEffectWindow.Damaging, bool ready = true) =>
            new AttackEffectRecipeDefinition { Id = id, Kind = kind, PrimaryValueBp = primary,
                SecondaryValue = secondary, OutputFamily = family, Window = window, RuntimeReady = ready };

        private static StatusRecipeDefinition Dot(string id, string tag, int coefficientBp) =>
            new StatusRecipeDefinition { Id = id, Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.DamageOverTime, Stacking = StatusStackingPolicy.IndependentStacks,
                Identity = StatusIdentityScope.RecipeAndSource, MaxStacks = 10,
                Tags = new List<string> { tag, "status.dot" }, PeriodicDamage = new PeriodicDamageDefinition {
                    Family = DamageFamily.DamageOverTime, Timing = StatusTickTiming.TargetTurnEnd,
                    Scaling = StatusSnapshotScaling.TriggeringHealthDamage, CoefficientBp = coefficientBp } };

        private static StatusRecipeDefinition Disable(string id, string tag, CardCategoryMask mask) =>
            new StatusRecipeDefinition { Id = id, Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.Disable, DisableMask = mask, Tags = new List<string> { tag, "status.disable" } };

        private static StatusRecipeDefinition PreventRecovery(string id, string tag) =>
            new StatusRecipeDefinition { Id = id, Polarity = StatusPolarity.Debuff,
                Behavior = StatusBehavior.PreventsRecovery, Tags = new List<string> { tag, "status.prevents-recovery" } };

        private static void AddStatGroupPair(List<StatusRecipeDefinition> recipes, string id, params StatId[] stats)
        {
            recipes.Add(StatGroup("status.buff.stat." + id, StatusPolarity.Buff, 10000, stats));
            recipes.Add(StatGroup("status.debuff.stat." + id, StatusPolarity.Debuff, -10000, stats));
        }

        private static void AddStatBundlePair(List<StatusRecipeDefinition> recipes, string id, StatBundleKind bundle)
        {
            recipes.Add(StatBundle("status.buff.stat." + id, StatusPolarity.Buff, 10000, bundle));
            recipes.Add(StatBundle("status.debuff.stat." + id, StatusPolarity.Debuff, -10000, bundle));
        }

        private static StatusRecipeDefinition StatBundle(string id, StatusPolarity polarity,
            int potencyCoefficientBp, StatBundleKind bundle) =>
            new StatusRecipeDefinition { Id = id, Polarity = polarity,
                Behavior = StatusBehavior.Stat, Stacking = StatusStackingPolicy.RefreshStronger,
                Tags = new List<string> { "status.stat" }, Modifiers = new List<StatModifierDefinition> {
                    new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = bundle,
                        ScaleByStatusPotency = true, PotencyCoefficientBp = potencyCoefficientBp }
                } };

        private static StatusRecipeDefinition StatGroup(string id, StatusPolarity polarity,
            int potencyCoefficientBp, StatId[] stats)
        {
            var recipe = new StatusRecipeDefinition { Id = id, Polarity = polarity,
                Behavior = StatusBehavior.Stat, Stacking = StatusStackingPolicy.RefreshStronger,
                Tags = new List<string> { "status.stat" } };
            foreach (var stat in stats) recipe.Modifiers.Add(new StatModifierDefinition { Target = ModifierTarget.Stat,
                Stat = stat, Operation = ModifierOperation.PercentOfBase, ScaleByStatusPotency = true,
                PotencyCoefficientBp = potencyCoefficientBp });
            return recipe;
        }
    }
}
