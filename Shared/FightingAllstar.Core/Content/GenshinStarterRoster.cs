using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    /// <summary>Source kits plus explicitly generated level-one stats and lower-rank starting values.</summary>
    public static class GenshinStarterRoster
    {
        public static List<CharacterDefinition> Create()
        {
            var amber = Fighter("amber", "Amber", "red", "r", 280, 150, 3600, 2700, 2000, 1000, 2000, 500, 1500, 500, 2000);
            amber.Passive.Support = new CombatSupportDefinition { ExplosionMultiplierBp = 20000 };
            AddSkill(amber, 1, CardCategory.AttackDebuff, r => {
                var effect = Damage(Scale(25000, r), EffectTargetScope.AllEnemies);
                var bunny = Recipe("status.debuff.baron-bunny");
                bunny.NaturalExpiryAttack.AttackCoefficientBp = Scale(15000, r);
                bunny.NaturalExpiryAttack.StatusDuration = Turns(4, r);
                Status(effect, bunny, 1);
                return effect;
            });
            AddSkill(amber, 2, CardCategory.Attack, r => Damage(Scale(30000, r), keyword: "attack.weakpoint"));
            AddUlt(amber, CardCategory.Attack, c => Damage(Ult(30000, c), EffectTargetScope.AllEnemies, ElementId.Pyro));

            var kaeya = Fighter("kaeya", "Kaeya", "blue", "sr", 350, 260, 4800, 3150, 2500, 1500, 2500, 1000, 2000, 1000, 2500);
            kaeya.Passive.Support = new CombatSupportDefinition { OpeningTeamGauge = 1,
                BonusAgainstRecipeId = "status.debuff.frost-bite", BonusCritChanceBp = 1500, BonusDamageBp = 1000,
                AttackerGaugeAgainstRecipeId = "status.debuff.frost-bite", AttackerGaugeGainBp = 5000 };
            AddSkill(kaeya, 1, CardCategory.Attack, r => {
                var effect = Damage(Scale(45000, r));
                effect.PowerGaugeAmount = -Turns(3, r);
                return effect;
            });
            AddSkill(kaeya, 2, CardCategory.AttackDebuff, r => {
                var effect = Damage(Scale(36000, r));
                Fuel(effect, ElementId.Cryo, "status.debuff.frost-bite", Turns(3, r));
                return effect;
            });
            AddUlt(kaeya, CardCategory.Buff, c => new EffectDefinition { Kind = EffectKind.ApplyStatus,
                Target = c == 5 ? EffectTargetScope.AllAllies : EffectTargetScope.Self,
                StatusRecipe = Recipe("status.buff.glacial-waltz") });

            var lisa = Fighter("lisa", "Lisa", "purple", "sr", 390, 180, 4200, 3100, 3000, 1000, 3000, 500, 1500, 500, 2000);
            lisa.Passive.Support = new CombatSupportDefinition { AfterAttackStatusRecipeId = "status.debuff.conductive", RequiresElementalTeam = true };
            Aura(lisa, "purple-team", new PassiveTargetFilter { AttributeId = "attribute.purple", IncludeReserve = false },
                new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.AttackRelated, Amount = 1000 },
                new StatModifierDefinition { Target = ModifierTarget.AnyDamageDealt, Operation = ModifierOperation.PercentagePoints, Amount = 1000 });
            AddSkill(lisa, 1, CardCategory.AttackDebuff, r => {
                var effect = Damage(Scale(21000, r), EffectTargetScope.AllEnemies);
                Fuel(effect, ElementId.Electro, "status.debuff.shock", Turns(4, r));
                return effect;
            });
            AddSkill(lisa, 2, CardCategory.Attack, r => {
                var effect = Damage(Scale(25000, r), EffectTargetScope.AllEnemies, keyword: "attack.pierce");
                effect.ConsumeTargetRecipeId = "status.debuff.conductive";
                effect.DamagePerConsumedStackBp = 2000;
                Fuel(effect, ElementId.Electro, "status.debuff.shock", Turns(4, r), critical: true);
                return effect;
            });
            AddUlt(lisa, CardCategory.AttackDebuff, c => {
                var effect = Damage(Ult(40000, c), EffectTargetScope.AllEnemies, ElementId.Electro);
                Status(effect, Recipe("status.debuff.stat.defense-related"), 3, CardEffectTiming.BeforeAction, potency: 6000);
                Fuel(effect, ElementId.Electro, "status.debuff.shock", 4);
                return effect;
            });

            var barbara = Fighter("barbara", "Barbara", "blue", "sr", 230, 240, 5400, 3200, 1000, 2000, 1000, 1500, 2500, 1000, 2500);
            barbara.Passive.Support = new CombatSupportDefinition { DamageHealLowestAllyBp = 3000 };
            Aura(barbara, "mondstadt-hp", new PassiveTargetFilter { TraitId = "trait.region.mondstadt", IncludeReserve = false },
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.MaxHealth, Operation = ModifierOperation.PercentOfBase, Amount = 3000 });
            AddSkill(barbara, 1, CardCategory.AttackDebuff, r => {
                var effect = Damage(Scale(21000, r), EffectTargetScope.AllEnemies, ElementId.Hydro);
                Status(effect, new StatusRecipeDefinition { Id = "status.debuff.dissolve", NameKey = "Dissolve", Polarity = StatusPolarity.Debuff,
                    Behavior = StatusBehavior.Triggered, Stacking = StatusStackingPolicy.RefreshStronger,
                    EndTurnPowerGaugeLoss = r == 1 ? 1 : 2 }, r == 3 ? 2 : 1);
                return effect;
            });
            AddSkill(barbara, 2, CardCategory.Recovery, r => {
                var effect = Heal(Scale(4000, r));
                Status(effect, Recipe("status.buff.melody-loop"), 2, CardEffectTiming.AfterAction);
                return effect;
            });
            AddUlt(barbara, CardCategory.Recovery, c => {
                var effect = Heal(Ult(5000, c));
                effect.Sequence.Add(Step(new EffectDefinition { Kind = EffectKind.Cleanse, Target = EffectTargetScope.AllAllies }, CardEffectTiming.BeforeAction));
                return effect;
            });

            var lumine = Fighter("lumine-anemo", "Anemo Lumine", "green", "ssr", 420, 300, 5600, 3500, 2000, 1500, 2500, 1000, 2000, 1000, 2500);
            lumine.TraitIds.Remove("trait.region.mondstadt");
            // Reserve aura and active attack cadence have independent availability.
            lumine.Passive.Support = new CombatSupportDefinition { SwirlStrengthBp = 4000,
                AttackCadence = 3, CadenceDamageBp = 2000, CadenceDotBp = 2000 };
            AddSkill(lumine, 1, CardCategory.Attack, r => {
                var effect = Damage(Scale(45000, r));
                effect.Sequence.Add(Step(new EffectDefinition { Kind = EffectKind.ChangePowerGauge,
                    Target = EffectTargetScope.Self, PowerGaugeAmount = Turns(2, r) }, CardEffectTiming.AfterAction));
                return effect;
            });
            AddSkill(lumine, 2, CardCategory.AttackDebuff, r => {
                var effect = Damage(Scale(25000, r), EffectTargetScope.AllEnemies, ElementId.Anemo);
                effect.OnKillSelfHealMaxHealthBp = Scale(800, r);
                Status(effect, new StatusRecipeDefinition { Id = "status.debuff.anemo-resistance", NameKey = "Anemo Resistance Down",
                    Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.Stat,
                    ElementalModifiers = new List<ElementalModifierDefinition> { new ElementalModifierDefinition {
                        Element = ElementId.Anemo, ResistanceBp = -Scale(2500, r) } } }, Turns(3, r), CardEffectTiming.BeforeAction);
                return effect;
            });
            AddUlt(lumine, CardCategory.Attack, c => {
                var effect = Damage(Ult(30000, c), EffectTargetScope.AllEnemies, ElementId.Anemo, "attack.shatter");
                effect.Sequence.Add(Step(new EffectDefinition { Kind = EffectKind.RemoveBuffs, Target = EffectTargetScope.AllEnemies }, CardEffectTiming.BeforeAction));
                effect.Sequence.Add(Step(new EffectDefinition { Kind = EffectKind.RemoveStance, Target = EffectTargetScope.AllEnemies }, CardEffectTiming.BeforeAction));
                return effect;
            });
            return new List<CharacterDefinition> { amber, kaeya, lisa, barbara, lumine };
        }

        private static CharacterDefinition Fighter(string id, string name, string attribute, string rarity, int atk, int def, int hp, int cc,
            int pierce, int resistance, int crit, int critRes, int critDef, int block, int blockPower) => new CharacterDefinition {
                Id = "fighter." + id, SourceId = "fighter." + id, DisplayName = "[Genshin Impact] " + name, RuntimeReady = true, FamilyId = "family." + id,
                SeriesId = "series.genshin-impact", AttributeId = "attribute." + attribute, RarityId = "rarity." + rarity,
                TraitIds = new List<string> { ElementalRuleSet.GenshinTrait, "trait.region.mondstadt" },
                StatsProvenance = SourceProvenance.GeneratedStartingValue,
                StatGrowth = new CharacterStatGrowth { AttackBp = 2135, DefenseBp = 2135, HealthBp = 2135 },
                BaseStats = new StatBlock { Attack = atk, Defense = def, MaxHealth = hp, CombatClass = cc, PierceBp = pierce,
                    ResistanceBp = resistance, RegenerationBp = 500, CritChanceBp = crit, CritDamageBp = 15000,
                    CritResistanceBp = critRes, CritDefenseBp = critDef, RecoveryBp = 11000, BlockChanceBp = block, BlockPowerBp = blockPower },
                PassiveId = "fighter." + id + ".passive.source", Passive = new PassiveDefinition { Id = "fighter." + id + ".passive.source" } };

        private static int Scale(int value, int rank) => (value * rank + 1) / 3;
        private static int Turns(int turns, int rank) => (turns * rank + 2) / 3;
        private static int Ult(int value, int tier) => value + value * tier * 5 / 100;
        private static StatusRecipeDefinition Recipe(string id) => StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == id).Clone();
        private static EffectDefinition Damage(int coefficient, EffectTargetScope scope = EffectTargetScope.SelectedEnemy,
            ElementId element = ElementId.None, string keyword = null) => new EffectDefinition { Kind = EffectKind.Damage,
                Scaling = StatScaling.Attack, CoefficientBp = coefficient, Target = scope, DamageElement = element, KeywordId = keyword,
                Attack = new DamageAttackDefinition { Range = scope == EffectTargetScope.AllEnemies ? AttackRange.Long : AttackRange.Close } };
        private static EffectDefinition Heal(int coefficient) => new EffectDefinition { Kind = EffectKind.Heal,
            Target = EffectTargetScope.AllAllies, HealScalingStat = StatId.MaxHealth, HealCoefficientBp = coefficient };
        private static CardEffectStep Step(EffectDefinition effect, CardEffectTiming timing) => new CardEffectStep { Timing = timing, Effect = effect };
        private static void Status(EffectDefinition effect, StatusRecipeDefinition recipe, int turns,
            CardEffectTiming timing = CardEffectTiming.AfterDamage, int potency = 0)
        {
            effect.Sequence.Add(Step(new EffectDefinition { Id = recipe.Id + ".delivery", Kind = EffectKind.ApplyStatus,
                Target = effect.Target, StatusRecipe = recipe, StatusDurationOverride = turns, StatusPotencyBp = potency }, timing));
        }
        private static void Fuel(EffectDefinition effect, ElementId element, string id, int turns, bool critical = false)
        {
            Status(effect, Recipe(id), turns);
            effect.ElementalApplication = new ElementalApplicationDefinition { Element = element, StatusEffectId = id + ".delivery", RequiresCritical = critical };
        }
        private static void AddSkill(CharacterDefinition fighter, int slot, CardCategory category, Func<int, EffectDefinition> factory)
        {
            var skill = new SkillDefinition { Id = fighter.Id + ".skill." + slot, Slot = slot, Category = category };
            for (var rank = 1; rank <= 3; rank++)
            {
                var effect = factory(rank); effect.Id = skill.Id + ".rank." + rank;
                skill.TargetScope = effect.Target;
                skill.Ranks.Add(new SkillRankDefinition { Rank = rank, HasCardKind = true, Category = category, TargetScope = effect.Target,
                    Effect = effect, Provenance = rank == 3 ? SourceProvenance.Supplied : SourceProvenance.GeneratedStartingValue });
            }
            fighter.Skills.Add(skill);
        }
        private static void AddUlt(CharacterDefinition fighter, CardCategory category, Func<int, EffectDefinition> factory)
        {
            for (var tier = 0; tier <= 5; tier++) fighter.UltimateTiers.Add(new UltimateTierDefinition {
                Tier = tier, Category = category, Effect = factory(tier), Provenance = tier == 0 ? SourceProvenance.Supplied : SourceProvenance.GeneratedStartingValue });
        }
        private static void Aura(CharacterDefinition fighter, string id, PassiveTargetFilter targets, params StatModifierDefinition[] modifiers)
        {
            var aura = new PassiveAuraDefinition { Id = id, Gate = new PassiveGate { Presence = PassivePresence.LivingActive }, Targets = targets };
            aura.Modifiers.AddRange(modifiers);
            fighter.Passive.Auras.Add(aura);
        }
    }
}
