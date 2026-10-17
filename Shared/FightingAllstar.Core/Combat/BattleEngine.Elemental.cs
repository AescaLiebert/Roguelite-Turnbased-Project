using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private sealed class ElementalActionContext
        {
            public readonly Dictionary<string, int> AmplificationByTarget = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> ConsumedStackCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly HashSet<string> BonusTargets = new HashSet<string>(StringComparer.Ordinal);
            public int CadenceDamageBp;
            public int CadenceDotBp;
            public readonly HashSet<string> Applications = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> OverloadRecipients = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ReactionReceipts = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ReactionDamageRecipients = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<ElementalContactCommit> DeferredFreezes = new List<ElementalContactCommit>();
            public readonly List<ElementalContactCommit> PendingShatters = new List<ElementalContactCommit>();
            public readonly List<ElementalContactCommit> Pending = new List<ElementalContactCommit>();
        }

        private sealed class ElementalContactCommit
        {
            public FighterState Target;
            public ElementId IncomingElement;
            public ElementalReactionDecision Decision;
            public int SourceAttack;
            public int HealthDamage;
            public int ShieldDamage;
            public int HitIndex;
            public int HitCount;
            public long ParentEventId;
        }

        private static void InitializeElementalRules(BattleState state, ElementalRuleSet rules)
        {
            rules = rules.Clone();
            rules.UpgradeLegacyDefaults();
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
            FighterState target, EffectDefinition effect, ActionResolutionContext resolution,
            DamagePacket packet, StatBlock attacker, int hitIndex)
        {
            if (state.ElementalSchemaVersion != ElementalRuleSet.CurrentSchemaVersion || state.ElementalRules == null ||
                resolution == null || effect.Family != DamageFamily.Normal) return null;
            if (resolution.Elemental.AmplificationByTarget.TryGetValue(target.Id, out var multiplier))
                packet.Policy.AmplificationBp = multiplier;
            var element = effect.DamageElement != ElementId.None ? effect.DamageElement : effect.ElementalApplication?.Element ?? ElementId.None;
            ElementalReactionRules.ApplyElementPolicy(packet.Policy, element, source, target, state.ElementalRules);
            if (element == ElementId.None || target.Health <= 0 ||
                !resolution.Elemental.Applications.Add(target.Id)) return null;
            var contact = new ElementalContactCommit { Target = target, IncomingElement = element,
                SourceAttack = attacker.Attack, HitIndex = hitIndex, HitCount = packet.HitCount };
            if (target.Side != source.Side && state.Team(source.Side).ElementalReactionsEnabled)
                contact.Decision = ElementalReactionRules.Evaluate(state.ElementalRules, target, element);
            packet.Policy.AmplificationBp = contact.Decision?.Reaction.AmplificationBp ?? 10000;
            resolution.Elemental.AmplificationByTarget[target.Id] = packet.Policy.AmplificationBp;
            return contact;
        }

        private static void CommitElementalHitBatch(BattleState state, FighterState source, CardState card,
            ActionResolutionContext resolution, DeterministicRandom rng, List<FighterState> pendingDefeats)
        {
            if (resolution.Elemental.Pending.Count == 0 && resolution.Elemental.PendingShatters.Count == 0) return;
            // All root recipients have resolved against pre-consumption views before any child damage.
            foreach (var contact in resolution.Elemental.Pending)
            {
                var target = contact.Target;
                if (contact.Decision?.Reaction.Id == ElementalReactionId.Swirl) continue;
                if (contact.Decision != null)
                {
                    CommitReactionFacts(state, source, card, contact, resolution.Elemental);
                }
                if ((contact.Decision == null || contact.Decision.Reaction.PreserveFuel) && target.Health > 0)
                    ApplyElementStatus(state, source, target, contact.IncomingElement, card.Id, contact.ParentEventId);

            }
            CharacterPassiveRuntime.Refresh(state);
            ResolvePendingShatters(state, source, card, resolution.Elemental, pendingDefeats);
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
            ResolvePendingShatters(state, source, card, resolution.Elemental, pendingDefeats);
            resolution.Elemental.Pending.Clear();
        }

        // A successful attack breaks the shell once; the extra packet resolves after the root hit batch.
        private static void QueueShatter(BattleState state, FighterState target, BattleEvent hit, ElementalActionContext context)
        {
            if (state.ElementalRules == null || context == null) return;
            var reaction = (state.ElementalRules.Reactions.Find(r => r.Id == ElementalReactionId.Shatter) ??
                ElementalRuleSet.CreateDefault().Reactions.Find(r => r.Id == ElementalReactionId.Shatter)).Clone();
            reaction.AttackCoefficientBp = ElementalRuleSet.ShatterCoefficientBp;
            context.PendingShatters.Add(new ElementalContactCommit { Target = target,
                ParentEventId = hit.Id, HitIndex = hit.HitIndex, HitCount = hit.HitCount,
                Decision = new ElementalReactionDecision { Reaction = reaction } });
        }

        private static void ResolvePendingShatters(BattleState state, FighterState source, CardState card,
            ElementalActionContext context, List<FighterState> pendingDefeats)
        {
            if (context.PendingShatters.Count == 0) return;
            var pending = new List<ElementalContactCommit>(context.PendingShatters);
            context.PendingShatters.Clear();
            foreach (var contact in pending)
            {
                CommitReactionFacts(state, source, card, contact, context);
                ResolveReactionOutput(state, source, card, contact, context, pendingDefeats);
            }
        }

        private static void CommitReactionFacts(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context)
        {
            var decision = contact.Decision;
            context.ReactionReceipts.Add(contact.Target.Id + ":" + decision.Reaction.Id);
            foreach (var id in decision.ConsumedInstanceIds) contact.Target.ElementStatuses.RemoveInstance(id);
            if (decision.ConsumedInstanceIds.Count > 0)
            {
                AddEvent(state, BattleEventKind.ElementStatusesChanged, source.Id, contact.Target.Id, card.Id, 0, "Elemental aura consumed.");
                var removal = state.Events[state.Events.Count - 1];
                SetElementalFacts(removal, contact, card.Id);
                removal.ElementStatusesAfter = contact.Target.ElementStatuses.Instances.ConvertAll(s => s.Clone());
                removal.ConsumedStatusInstanceIds.AddRange(decision.ConsumedInstanceIds);
            }
            AddEvent(state, BattleEventKind.ElementalReactionTriggered, source.Id, contact.Target.Id, card.Id,
                decision.Reaction.AmplificationBp, decision.Reaction.Id.ToString());
            var reaction = state.Events[state.Events.Count - 1];
            SetElementalFacts(reaction, contact, card.Id);
            reaction.DamageElement = decision.Reaction.Id == ElementalReactionId.Shatter ? ElementId.Cryo : decision.Reaction.ExistingElement;
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
                    ElementalReactionRules.ConfigureReactionDamagePolicy(policy, ElementalReactionId.Overload);
                    policy.ExplosionFactorBp = TeamExplosionFactor(state, source);
                    policy.CannotCrit = policy.CannotBlock = true;
                    policy.DamageCap = state.ElementalRules.ReactionDamageCap;
                    ElementalReactionRules.ApplyElementPolicy(policy, ElementId.Pyro, source, recipient, state.ElementalRules);
                    ElementalReactionRules.ApplyOwnReactionBonus(policy, source, context.CadenceDotBp);
                    policy.ShieldEfficiencyBp = 8000;
                    var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = ElementalReactionRules.LevelDamage(source, state.ElementalRules),
                        CoefficientBp = contact.Decision.Reaction.AttackCoefficientBp, Policy = policy },
                        StatusSystem.GetEffectiveStats(source), StatusSystem.GetEffectiveStats(recipient), recipient.Health, recipient.Shield, -1, -1);
                    if (!recipient.CannotDie) recipient.Health = damage.RemainingHealth;
                    recipient.Shield = damage.RemainingShield;
                    ConsumeBarrierShield(recipient, damage.ShieldLost);
                    var freezeBroken = StatusSystem.BreakFreezeOnAttack(recipient);
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
                    if (freezeBroken) QueueShatter(state, recipient, evt, context);
                    if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
                    // Terminal origin: deliberately no normal keywords, lifesteal, reflect or passive damage callbacks.
                }
        }

        private static void ResolveSwirl(BattleState state, FighterState source, CardState card,
            ElementalContactCommit contact, ElementalActionContext context, DeterministicRandom rng,
            List<FighterState> pendingDefeats)
        {
            var decision = contact.Decision;
            if (!contact.Target.ElementStatuses.Instances.Exists(s => decision.SourceInstanceIds.Contains(s.InstanceId)) ||
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
                var freezeBroken = StatusSystem.BreakFreezeOnAttack(recipient);
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
                if (freezeBroken) QueueShatter(state, recipient, evt, context);
                child.ParentEventId = evt.Id;
                if (childDecision?.Reaction.PreserveFuel == true)
                    ApplyElementStatus(state, source, recipient, decision.Reaction.OutputElement, card.Id, evt.Id);
                if (childDecision != null)
                {
                    CommitReactionFacts(state, source, card, child, context);
                    ResolveReactionOutput(state, source, card, child, context, pendingDefeats);
                }
                else if (!alreadyReacted && recipient.Health > 0)
                {
                    ApplyElementStatus(state, source, recipient, decision.Reaction.OutputElement, card.Id, evt.Id);

                }
                CharacterPassiveRuntime.Refresh(state);
                if (recipient.Health <= 0 && !recipient.CannotDie && !pendingDefeats.Contains(recipient)) pendingDefeats.Add(recipient);
            }
        }

        private static bool ApplyElementStatus(BattleState state, FighterState source, FighterState target,
            ElementId element, string actionId, long parentId = 0)
        {
            if (!state.Team(source.Side).ElementalReactionsEnabled || target.Health <= 0 || !target.ElementStatuses.Apply(element, source.Id, "element:" + (state.Events.Count + 1))) return false;
            AddEvent(state, BattleEventKind.ElementStatusApplied, source.Id, target.Id, actionId, 1, element.ToString());
            var evt = state.Events[state.Events.Count - 1];
            evt.DamageElement = element;
            evt.RootActionId = actionId;
            evt.ParentEventId = parentId;
            evt.ElementStatusesAfter = target.ElementStatuses.Instances.ConvertAll(s => s.Clone());
            return true;
        }

        private static void AdvanceElementStatuses(BattleState state, FighterState target)
        {
            if (!target.ElementStatuses.AdvanceTargetTurnEnd()) return;
            AddEvent(state, BattleEventKind.ElementStatusesChanged, target.Id, target.Id, null, 0, "Elemental aura duration.");
            state.Events[state.Events.Count - 1].ElementStatusesAfter = target.ElementStatuses.Instances.ConvertAll(s => s.Clone());
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
