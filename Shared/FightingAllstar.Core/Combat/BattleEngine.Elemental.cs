using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private sealed class ElementalActionContext
        {
            public readonly Dictionary<string, int> ConsumedStackCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly HashSet<string> BonusTargets = new HashSet<string>(StringComparer.Ordinal);
            public int CadenceDamageBp;
            public int CadenceDotBp;
            public readonly HashSet<string> Applications = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> OverloadRecipients = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ReactionReceipts = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ReactionDamageRecipients = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<ElementalContactCommit> Pending = new List<ElementalContactCommit>();
        }

        private sealed class ElementalContactCommit
        {
            public FighterState Target;
            public ElementalReactionDecision Decision;
            public EffectDefinition Rider;
            public bool RiderAccepted;
            public StatusApplyOutcome RiderOutcome;
            public string RejectionReason;
            public int SourceAttack;
            public int HealthDamage;
            public int ShieldDamage;
            public int HitIndex;
            public int HitCount;
            public long ParentEventId;
        }

        private static void InitializeElementalRules(BattleState state, ElementalRuleSet rules)
        {
            var errors = ElementalContentValidator.Validate(rules);
            foreach (var team in new[] { state.Player, state.Opponent })
                foreach (var fighter in team.Fighters)
                    errors.AddRange(ElementalContentValidator.Validate(fighter.Definition, rules));
            if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors), nameof(rules));
            state.ElementalSchemaVersion = ElementalRuleSet.CurrentSchemaVersion;
            state.ElementalRules = rules.Clone();
            state.Player.ElementalReactionsEnabled = ElementalReactionRules.Qualifies(state.Player);
            state.Opponent.ElementalReactionsEnabled = ElementalReactionRules.Qualifies(state.Opponent);
        }

        private static ElementalContactCommit PrepareElementalContact(BattleState state, FighterState source,
            FighterState target, CardState card, EffectDefinition effect, ActionResolutionContext resolution,
            DamagePacket packet, StatBlock attacker, StatBlock defender, DeterministicRandom rng,
            int critRoll, int blockRoll, int hitIndex, DamageResult preliminary)
        {
            if (state.ElementalSchemaVersion != ElementalRuleSet.CurrentSchemaVersion || state.ElementalRules == null ||
                resolution == null || effect.Family != DamageFamily.Normal) return null;
            ElementalReactionRules.ApplyElementPolicy(packet.Policy, effect.DamageElement, source, target, state.ElementalRules);
            if (effect.DamageElement != ElementId.None && resolution.Elemental.CadenceDotBp > 0)
                packet.Policy.DotBridgeFactorBp += resolution.Elemental.CadenceDotBp;
            var application = effect.ElementalApplication;
            var element = application?.Element ?? effect.DamageElement;
            if (element == ElementId.None || hitIndex != (application?.HitIndex ?? 1) || target.Health <= 0 ||
                !resolution.Elemental.Applications.Add(target.Id)) return null;

            var contact = new ElementalContactCommit { Target = target, SourceAttack = attacker.Attack,
                HitIndex = hitIndex, HitCount = packet.HitCount };
            var eligible = target.Side != source.Side && state.Team(source.Side).ElementalReactionsEnabled &&
                (effect.DamageElement != ElementId.None || application?.RequiresCritical != true || preliminary.WasCritical);
            if (!string.IsNullOrWhiteSpace(application?.StatusEffectId))
                contact.Rider = effect.Sequence.Find(s => s?.Effect?.Id == application.StatusEffectId).Effect.ToEffectDefinition();

            if (eligible) contact.Decision = ElementalReactionRules.Evaluate(state.ElementalRules, target, element);
            packet.Policy.AmplificationBp = contact.Decision?.Reaction.AmplificationBp ?? 10000;
            var predicted = DamageResolver.Resolve(packet, attacker, defender, target.Health, target.Shield, critRoll, blockRoll);
            if (contact.Rider != null && (application.RequiresCritical == false || preliminary.WasCritical))
            {
                // Probe a clone with the exact predicted HP basis. Acceptance/roll is owned by this delivery once.
                var trial = StatusSystem.Apply(target.Clone(), source.Id, source.Side, contact.Rider.StatusRecipe.Clone(),
                    "elemental-probe", card.Id, 0,
                    snapshot: new StatusSnapshot { SourceAttack = attacker.Attack, SourceDefense = attacker.Defense,
                        SourceMaxHealth = attacker.MaxHealth, TargetMaxHealth = defender.MaxHealth,
                        TriggeringHealthDamage = target.CannotDie ? 0 : predicted.HealthLost, FixedAmount = contact.Rider.Magnitude },
                    potencyBp: contact.Rider.StatusPotencyBp, stackCount: contact.Rider.StatusStackCount,
                    duration: contact.Rider.StatusDurationOverride);
                contact.RiderOutcome = trial.Outcome;
                contact.RejectionReason = trial.Reason;
                var chance = ResolveStatusApplyChanceBp(contact.Rider.StatusApplyChanceBp,
                    attacker.ControlBp, defender.AvoidanceBp, true);
                contact.RiderAccepted = trial.Accepted && (chance >= 10000 || chance > 0 && rng.NextBasisPoints() < chance);
            }
            if (effect.DamageElement == ElementId.None && !contact.RiderAccepted)
            {
                contact.Decision = null;
                packet.Policy.AmplificationBp = 10000;
            }
            return contact;
        }

        private static void CommitElementalHitBatch(BattleState state, FighterState source, CardState card,
            ActionResolutionContext resolution, DeterministicRandom rng, List<FighterState> pendingDefeats)
        {
            if (resolution.Elemental.Pending.Count == 0) return;
            // All root recipients have resolved against pre-consumption views before any child damage.
            foreach (var contact in resolution.Elemental.Pending)
            {
                var target = contact.Target;
                if (contact.Decision?.Reaction.Id == ElementalReactionId.Swirl) continue;
                if (contact.Decision != null)
                {
                    CommitReactionFacts(state, source, card, contact, resolution.Elemental);
                }
                else if (contact.RiderAccepted && target.Health > 0)
                    ApplyCardUtilityEffectSingle(state, rng, source, target, contact.Rider, card.Id,
                        card.Kind == CardKind.Ultimate, contact.HealthDamage, contact.HealthDamage,
                        skipApplyChance: true, contactSnapshotDamage: contact.HealthDamage);
                else if (contact.Rider != null && contact.RejectionReason == "Debuff immunity.")
                    AddEvent(state, BattleEventKind.StatusImmuned, source.Id, target.Id, card.Id, 0, "Immune");
                else if (contact.Rider != null && contact.RiderOutcome == StatusApplyOutcome.IgnoredWeaker)
                    AddEvent(state, BattleEventKind.StatusWeaker, source.Id, target.Id, card.Id, 0, contact.Rider.StatusRecipe.Id);
            }
            CharacterPassiveRuntime.Refresh(state);
            foreach (var contact in resolution.Elemental.Pending)
            {
                ResolveReactionOutput(state, source, card, contact, resolution.Elemental, pendingDefeats);
            }
            // Pending contacts are captured before any root consumption or child delivery.
            // Stable formation order is independent of target enumeration and never compresses empty slots.
            var swirls = resolution.Elemental.Pending.FindAll(c => c.Decision?.Reaction.Id == ElementalReactionId.Swirl);
            swirls.Sort((a, b) => a.Target.FormationSlot.CompareTo(b.Target.FormationSlot));
            foreach (var contact in swirls)
                ResolveSwirl(state, source, card, contact, resolution.Elemental, rng, pendingDefeats);
            resolution.Elemental.Pending.Clear();
        }

        private static void CommitReactionFacts(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context)
        {
            var decision = contact.Decision;
            context.ReactionReceipts.Add(contact.Target.Id + ":" + decision.Reaction.Id);
            foreach (var id in decision.ConsumedInstanceIds) contact.Target.Statuses.RemoveInstance(id);
            if (decision.ConsumedInstanceIds.Count > 0)
            {
                AddEvent(state, BattleEventKind.StatusesChanged, source.Id, contact.Target.Id, card.Id, 0, "Elemental fuel consumed.");
                var removal = state.Events[state.Events.Count - 1];
                SetElementalFacts(removal, contact, card.Id);
                removal.StatusesAfter = contact.Target.Statuses.Instances.ConvertAll(s => s.Clone());
                removal.ConsumedStatusInstanceIds.AddRange(decision.ConsumedInstanceIds);
            }
            AddEvent(state, BattleEventKind.ElementalReactionTriggered, source.Id, contact.Target.Id, card.Id,
                decision.Reaction.AmplificationBp, decision.Reaction.Id.ToString());
            var reaction = state.Events[state.Events.Count - 1];
            SetElementalFacts(reaction, contact, card.Id);
            reaction.DamageElement = decision.Reaction.ExistingElement;
            reaction.TriggerElement = decision.IncomingElement;
            reaction.ConsumedStatusInstanceIds.AddRange(decision.ConsumedInstanceIds);
        }

        private static void ResolveOverload(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context, List<FighterState> pendingDefeats)
        {
                foreach (var recipient in state.Team(contact.Target.Side).LivingActive())
                {
                    if (recipient.Health <= 0 || Math.Abs(recipient.FormationSlot - contact.Target.FormationSlot) > 1 ||
                        !context.OverloadRecipients.Add(recipient.Id)) continue;
                    var policy = StatusSystem.BuildDamagePolicy(source, recipient, DamageFamily.ElementalReaction);
                    policy.ExplosionFactorBp = TeamExplosionFactor(state, source);
                    policy.CannotCrit = policy.CannotBlock = true;
                    policy.DamageCap = state.ElementalRules.ReactionDamageCap;
                    ElementalReactionRules.ApplyElementPolicy(policy, ElementId.Pyro, source, recipient, state.ElementalRules);
                    var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = contact.SourceAttack,
                        CoefficientBp = contact.Decision.Reaction.AttackCoefficientBp, Policy = policy },
                        StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(recipient), recipient.Health, recipient.Shield, -1, -1);
                    if (!recipient.CannotDie) recipient.Health = damage.RemainingHealth;
                    recipient.Shield = damage.RemainingShield;
                    ConsumeBarrierShield(recipient, damage.ShieldLost);
                    StatusSystem.BreakFreezeOnAttack(recipient);
                    AddEvent(state, BattleEventKind.DamageApplied, source.Id, recipient.Id, card.Id, damage.CalculatedDamage, "Overload");
                    var evt = state.Events[state.Events.Count - 1];
                    SetElementalFacts(evt, contact, card.Id);
                    evt.DamageOrigin = ElementalDamageOrigin.Reaction;
                    evt.DamageElement = ElementId.Pyro;
                    evt.HealthAfter = recipient.Health;
                    evt.ShieldAfter = recipient.Shield;
                    evt.HealthLost = recipient.CannotDie ? 0 : damage.HealthLost;
                    evt.ShieldLost = damage.ShieldLost;
                    evt.StatusesAfter = recipient.Statuses.Instances.ConvertAll(s => s.Clone());
                    if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
                    // Terminal origin: deliberately no normal keywords, lifesteal, reflect or passive damage callbacks.
                }
        }

        private static void ResolveSwirl(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context, DeterministicRandom rng,
            List<FighterState> pendingDefeats)
        {
            var decision = contact.Decision;
            if (!contact.Target.Statuses.Instances.Exists(s => decision.SourceInstanceIds.Contains(s.InstanceId)) ||
                context.ReactionReceipts.Contains(contact.Target.Id + ":" + ElementalReactionId.Swirl)) return;
            CommitReactionFacts(state, source, card, contact, context);
            var parentId = state.Events[state.Events.Count - 1].Id;
            foreach (var recipient in state.Team(contact.Target.Side).LivingActive())
            {
                if (recipient.Health <= 0 || recipient.Id == contact.Target.Id ||
                    Math.Abs(recipient.FormationSlot - contact.Target.FormationSlot) != 1) continue;
                var childDecision = ElementalReactionRules.Evaluate(state.ElementalRules, recipient, decision.Reaction.OutputElement);
                var alreadyReacted = childDecision != null && context.ReactionReceipts.Contains(recipient.Id + ":" + childDecision.Reaction.Id);
                if (alreadyReacted) childDecision = null;
                var policy = StatusSystem.BuildDamagePolicy(source, recipient, DamageFamily.ElementalReaction);
                policy.CannotCrit = policy.CannotBlock = true;
                policy.DamageCap = state.ElementalRules.ReactionDamageCap;
                policy.AmplificationBp = childDecision?.Reaction.AmplificationBp ?? 10000;
                policy.ExplosionFactorBp = TeamSwirlFactor(state, source);
                ElementalReactionRules.ApplyElementPolicy(policy, decision.Reaction.OutputElement, source, recipient, state.ElementalRules);
                var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = contact.SourceAttack,
                    CoefficientBp = decision.Reaction.AttackCoefficientBp, Policy = policy },
                    StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(recipient),
                    recipient.Health, recipient.Shield, -1, -1);
                if (!recipient.CannotDie) recipient.Health = damage.RemainingHealth;
                recipient.Shield = damage.RemainingShield;
                ConsumeBarrierShield(recipient, damage.ShieldLost);
                StatusSystem.BreakFreezeOnAttack(recipient);
                var child = new ElementalContactCommit { Target = recipient, Decision = childDecision,
                    SourceAttack = contact.SourceAttack, HealthDamage = recipient.CannotDie ? 0 : damage.HealthLost,
                    HitIndex = contact.HitIndex, HitCount = contact.HitCount, ParentEventId = parentId };
                AddEvent(state, BattleEventKind.DamageApplied, source.Id, recipient.Id, card.Id, damage.CalculatedDamage, "Swirl");
                var evt = state.Events[state.Events.Count - 1];
                SetElementalFacts(evt, child, card.Id);
                evt.DamageOrigin = ElementalDamageOrigin.Reaction;
                evt.DamageElement = decision.Reaction.OutputElement;
                evt.ElementalReaction = childDecision?.Reaction.Id ?? ElementalReactionId.Swirl;
                evt.HealthAfter = recipient.Health;
                evt.ShieldAfter = recipient.Shield;
                evt.HealthLost = child.HealthDamage;
                evt.ShieldLost = damage.ShieldLost;
                evt.StatusesAfter = recipient.Statuses.Instances.ConvertAll(s => s.Clone());
                child.ParentEventId = evt.Id;
                if (childDecision != null)
                {
                    CommitReactionFacts(state, source, card, child, context);
                    ResolveReactionOutput(state, source, card, child, context, pendingDefeats);
                }
                else if (!alreadyReacted && recipient.Health > 0)
                {
                    // A new delivery owns a new damage snapshot and one default-duration stack.
                    // It never copies the source status's duration, strength or stack count.
                    var mapping = state.ElementalRules.Fuel.Find(f => f.Element == decision.Reaction.OutputElement);
                    var recipe = StandardEffectDatabase.CreateStatusRecipes().Find(s => s.Id == mapping?.RecipeId);
                    if (recipe != null)
                        ApplyCardUtilityEffectSingle(state, rng, source, recipient, new EffectDefinition {
                            Kind = EffectKind.ApplyStatus, StatusRecipe = recipe.Clone(),
                            Target = EffectTargetScope.SelectedEnemy }, card.Id, card.Kind == CardKind.Ultimate,
                            child.HealthDamage, child.HealthDamage, contactSnapshotDamage: child.HealthDamage);
                }
                CharacterPassiveRuntime.Refresh(state);
                if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
            }
        }

        private static void SetElementalFacts(BattleEvent evt, ElementalContactCommit contact, string rootActionId)
        {
            evt.RootActionId = rootActionId;
            evt.ParentEventId = contact.ParentEventId;
            evt.HitIndex = contact.HitIndex;
            evt.HitCount = contact.HitCount;
            evt.ElementalReaction = contact.Decision?.Reaction.Id ?? ElementalReactionId.None;
        }
    }
}
