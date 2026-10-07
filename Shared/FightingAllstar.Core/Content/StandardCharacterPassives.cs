using System;

namespace FightingAllstar.Core.Content
{
    /// <summary>Source-authored starter passives. Content mapping only; the resolver has no fighter-id branches.</summary>
    public static class StandardCharacterPassives
    {
        public static void AttachMissing(ContentCatalog catalog)
        {
            if (catalog?.Characters == null) return;
            foreach (var character in catalog.Characters)
                if (character != null && (character.Passive == null || string.IsNullOrWhiteSpace(character.Passive.Id)))
                    character.Passive = Create(character.Id);
        }

        public static PassiveDefinition Create(string characterId)
        {
            var passive = new PassiveDefinition { Id = characterId + ".passive.source" };
            PassiveAuraDefinition aura;
            switch (characterId)
            {
                case "fighter.kyo94":
                    aura = Aura(passive, "ignite-attack", PassivePresence.LivingActive, PassiveRelation.Self);
                    aura.Scaling = PassiveScaling.FieldStatusStacks; aura.ScalingKey = CombatTags.Ignite; aura.MaximumUnits = 10;
                    Add(aura, StatId.Attack, 300);
                    break;
                case "fighter.chin94":
                    aura = Aura(passive, "green-hp", PassivePresence.LivingRoster);
                    aura.Targets.AttributeId = "attribute.green";
                    Add(aura, StatId.MaxHealth, 2000); Add(aura, StatId.Recovery, 2000);
                    Add(aura, StatId.Regeneration, 2000); Add(aura, StatId.LifeSteal, 2000);
                    break;
                case "fighter.kensou94":
                    aura = Aura(passive, "team-recovery", PassivePresence.LivingRoster);
                    Add(aura, StatId.Recovery, 4000);
                    break;
                case "fighter.king94":
                    aura = Aura(passive, "team-pierce", PassivePresence.LivingRoster);
                    aura.Scaling = PassiveScaling.OwnerCounter; aura.ScalingKey = "turn-stacks"; aura.MaximumUnits = 5;
                    Add(aura, StatId.Pierce, 800);
                    passive.Reactions.Add(new PassiveReactionDefinition { Id = "turn-stack", Trigger = PassiveEventKind.TeamTurnEnded,
                        OwnTeamTurnOnly = true, Commands = { new PassiveCommandDefinition {
                            Kind = PassiveCommandKind.IncrementCounter, CounterKey = "turn-stacks", CounterCap = 5 } } });
                    break;
                case "fighter.mai94":
                    aura = Aura(passive, "enemy-recovery", PassivePresence.LivingActive, PassiveRelation.Enemies);
                    aura.Scaling = PassiveScaling.OwnerStat; aura.SourceStat = StatId.Regeneration;
                    Add(aura, StatId.Recovery, -10000);
                    break;
                case "fighter.ryo94":
                    aura = Aura(passive, "reflect-damage", PassivePresence.LivingActive, PassiveRelation.Self);
                    aura.Modifiers.Add(new StatModifierDefinition { Target = ModifierTarget.Stat,
                        Stat = StatId.ReflectDamage, Operation = ModifierOperation.PercentagePoints, Amount = 1500 });
                    break;
                case "fighter.shingo97":
                    passive.Reactions.Add(new PassiveReactionDefinition { Id = "drain-refund",
                        Gate = new PassiveGate { Presence = PassivePresence.LivingActive }, Trigger = PassiveEventKind.GaugeChanged,
                        ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
                        CardOriginOnly = true, ExcludeUltimate = true, RequireGaugeLoss = true,
                        Commands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.ChangePowerGauge,
                            ValueSource = PassiveValueSource.ActualGaugeLost } } });
                    break;
                case "fighter.benimaru94":
                    aura = Aura(passive, "red-attack", PassivePresence.LivingRoster);
                    aura.Targets.AttributeId = "attribute.red"; AttackRelated(aura, 1000);
                    break;
                case "fighter.athena94":
                    aura = Aura(passive, "women-attack", PassivePresence.LivingRoster);
                    aura.Targets.TraitId = "trait.women"; AttackRelated(aura, 1500);
                    break;
                default: return null;
            }
            return passive;
        }

        private static PassiveAuraDefinition Aura(PassiveDefinition passive, string id, PassivePresence presence,
            PassiveRelation relation = PassiveRelation.Allies)
        {
            var aura = new PassiveAuraDefinition { Id = id, Gate = new PassiveGate { Presence = presence },
                Targets = new PassiveTargetFilter { Relation = relation } };
            passive.Auras.Add(aura);
            return aura;
        }

        private static void AttackRelated(PassiveAuraDefinition aura, int amount)
        {
            Add(aura, StatId.Attack, amount); Add(aura, StatId.Pierce, amount);
            Add(aura, StatId.CritChance, amount); Add(aura, StatId.CritDamage, amount);
        }

        private static void Add(PassiveAuraDefinition aura, StatId stat, int amount) =>
            aura.Modifiers.Add(new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
                Operation = stat == StatId.Attack || stat == StatId.Defense || stat == StatId.MaxHealth || stat == StatId.CombatClass
                    ? ModifierOperation.PercentOfBase : ModifierOperation.PercentagePoints });
    }
}
