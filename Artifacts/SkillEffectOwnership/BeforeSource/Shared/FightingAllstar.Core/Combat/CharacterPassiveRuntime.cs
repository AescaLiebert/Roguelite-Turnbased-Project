using System;
using System.Collections.Generic;
using System.Numerics;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class PassiveCounterState
    {
        public string Key;
        public int Value;
        public PassiveCounterState Clone() => (PassiveCounterState)MemberwiseClone();
    }

    [Serializable]
    public sealed class PassiveStatContribution
    {
        public string OwnerId;
        public string RuleId;
        public StatModifierDefinition Modifier;
        public PassiveStatContribution Clone() => new PassiveStatContribution {
            OwnerId = OwnerId, RuleId = RuleId, Modifier = Modifier?.Clone() };
    }

    /// <summary>Committed facts, not presentation/animation callbacks. Only supported facts are exposed.</summary>
    [Serializable]
    public sealed class PassiveFact
    {
        public long Sequence;
        public PassiveEventKind Kind;
        public TeamSide TurnSide;
        public string ActorId;
        public string TargetId;
        public string RootActionId;
        public bool CardOrigin;
        public bool IsUltimate;
        public int GaugeDelta;
        public int GaugeDeltaBp;
        public int DamageAmount;
        public DamageFamily DamageFamily;
        public ElementId DamageElement;
        public CardCategory CardCategory;
        public int CardRank;
        public bool WasCritical;
        public bool WasBlocked;
        public int HitIndex = 1;
        public int HitCount = 1;
        public bool ReactionOrigin;
        // New production lifecycle hooks are opt-in; dormant shipped version-zero declarations stay dormant.
        public bool VersionedLifecycleOnly;
        public string ResourceKey;
        public int PreviousResourceValue;
        public int ResourceValue;
        public int HealingAmount;
        public StatusRemovalSnapshot RemovedStatus;
        public StatusInstance AppliedStatus;
        public PassiveFact Clone()
        {
            var copy = (PassiveFact)MemberwiseClone();
            copy.RemovedStatus = RemovedStatus?.Clone(); copy.AppliedStatus = AppliedStatus?.Clone();
            return copy;
        }
    }

    internal sealed class ReactionBinding
    {
        public PassiveReactionDefinition Rule;
        public string SourceId;
        public bool StatusOwned;
    }

    public static partial class CharacterPassiveRuntime
    {
        public static bool IsAvailable(BattleState state, FighterState owner, PassiveGate gate) =>
            state != null && owner != null && owner.IsAlive && owner.Health > 0 && gate != null &&
            owner.ConstellationTier >= gate.MinimumTier && owner.ConstellationTier <= gate.MaximumTier &&
            (gate.Modes & state.Mode) != 0 &&
            (gate.Presence == PassivePresence.LivingRoster ||
             gate.Presence == PassivePresence.LivingActive && !owner.IsReserve ||
             gate.Presence == PassivePresence.LivingReserve && owner.IsReserve);

        public static int Counter(FighterState owner, string key) =>
            owner?.PassiveCounters?.Find(item => item.Key == key)?.Value ?? 0;

        /// <summary>Applies Before Damage reaction commands to the local hit calculation.</summary>
        public static void ApplyBeforeDamageReactions(BattleState state, FighterState actor, FighterState target,
            DamageFamily family, CardState card, AttackEffectCalculation calculation)
        {
            if (state == null || actor == null || target == null || calculation == null) return;
            var cardOrigin = card != null;
            var isUltimate = card?.Kind == CardKind.Ultimate;
            var fact = new PassiveFact { Kind = PassiveEventKind.BeforeDamage, ActorId = actor.Id, TargetId = target.Id,
                RootActionId = state.PassiveSession?.RootId, TurnSide = state.ActingSide, CardOrigin = cardOrigin,
                IsUltimate = isUltimate, DamageFamily = family, CardCategory = CardRules.GetEffectCategory(card),
                CardRank = card?.Rank ?? 0, ReactionOrigin = state.PassiveSession?.ReactionDepth > 0 };
            var scheduled = ScheduledBindings(state, fact);
            var hasVersionOne = scheduled.Exists(s => s.Binding.Rule.ExecutionVersion == 1 &&
                s.Binding.Rule.Trigger == PassiveEventKind.BeforeDamage);
            if (!hasVersionOne)
            {
                // Shipped version-zero hit modifiers retain their existing composition/rounding order.
                scheduled.Clear();
                foreach (var existingOwner in Fighters(state))
                    foreach (var existingBinding in ReactionBindings(existingOwner))
                        scheduled.Add(new ScheduledReaction { Owner = existingOwner, Binding = existingBinding });
            }
            using (BeginRootResolution(state, fact.RootActionId))
            {
                if (hasVersionOne) { state.PassiveSession.Fact(); fact.Sequence = ++state.PassiveEventSequence; }
                foreach (var entry in scheduled)
                {
                    var owner = entry.Owner;
                    var binding = entry.Binding;
                    var rule = binding.Rule;
                    var eventStart = state.Events.Count;
                    if (rule == null || rule.Trigger != PassiveEventKind.BeforeDamage ||
                        !ReactionAvailable(state, owner, binding) ||
                        rule.OwnTeamTurnOnly && state.ActingSide != owner.Side ||
                        rule.CardOriginOnly && !cardOrigin || rule.ExcludeUltimate && isUltimate ||
                        rule.RequireCritical || rule.RequireBlocked ||
                        rule.FilterDamageFamily && rule.DamageFamily != family ||
                        rule.FilterCategory && (card == null ||
                            !MatchesCategory(rule.Category, CardRules.GetEffectCategory(card), isUltimate) ||
                            card.Rank < rule.MinimumRank) ||
                        !Related(owner, actor, rule.ActorRelation) || !Related(owner, target, rule.TargetRelation))
                    {
                        if (rule?.ExecutionVersion == 1 && rule.Trigger == PassiveEventKind.BeforeDamage)
                            Trace(state, fact, owner, rule, false, false, "Eligibility gate or event filter did not match.", eventStart);
                        continue;
                    }
                    if (!CanActivate(state, owner, binding, fact, out var blockedReason))
                    { Trace(state, fact, owner, rule, false, false, blockedReason, eventStart); continue; }

                    var context = new CardEffectContext
                    {
                        Battle = state,
                        EffectOwner = owner,
                        Actor = actor,
                        SelectedTarget = target,
                        CardCategory = CardRules.GetEffectCategory(card),
                        CardRank = card?.Rank ?? 0,
                        IsUltimate = isUltimate,
                        DamageFamily = family, EventFact = fact, RootActionId = fact.RootActionId,
                        ReactionOrigin = fact.ReactionOrigin
                    };
                    if (state.CapturePassiveTrace) context.ConditionTrace = new List<PassiveConditionTrace>();
                    if (!CardEffectSystem.ConditionsPass(rule.Conditions, context, target))
                    {
                        if (rule.ExecutionVersion == 1) Trace(state, fact, owner, rule, false, false,
                            "Nested eligibility condition failed.", eventStart, context.ConditionTrace);
                        continue;
                    }

                    var applied = false;
                    if (rule.Commands == null) continue;
                    RecordActivation(state, owner, binding, fact);
                    foreach (var command in rule.Commands)
                    {
                        if (rule.ExecutionVersion == 1) state.PassiveSession.Command();
                        if (command == null || command.Amount <= 0 || command.ValueSource != PassiveValueSource.Fixed) continue;
                        switch (command.Kind)
                        {
                            case PassiveCommandKind.IncreaseCurrentAttackPercent:
                                calculation.Attacker.Attack = (int)Math.Min(int.MaxValue,
                                    (long)calculation.Attacker.Attack * (10000L + command.Amount) / 10000L);
                                applied = true;
                                break;
                            case PassiveCommandKind.IncreaseCurrentCritChancePercent:
                                calculation.Attacker.CritChanceBp = (int)Math.Min(int.MaxValue, (long)calculation.Attacker.CritChanceBp + command.Amount);
                                applied = true;
                                break;
                            case PassiveCommandKind.IncreaseCurrentDamageDealtPercent:
                                calculation.Policy.OutgoingIncreaseBp = (int)Math.Min(int.MaxValue,
                                    (long)calculation.Policy.OutgoingIncreaseBp + command.Amount);
                                applied = true;
                                break;
                            case PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent:
                                var baseStats = actor.Definition?.BaseStats;
                                var scalingCoefficientBp = Math.Max(0, command.ScalingCoefficientBp);
                                var scalingValue = Math.Max(0, baseStats?.Get(command.ScalingStat) ?? 0);
                                calculation.Policy.NonCriticalOutgoingIncreaseBp = (int)Math.Min(int.MaxValue,
                                    (long)calculation.Policy.NonCriticalOutgoingIncreaseBp +
                                    (long)scalingValue * scalingCoefficientBp / 10000L);
                                applied = true;
                                break;
                            case PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent:
                                calculation.Defender.CritDefenseBp = (int)Math.Max(0L,
                                    (long)calculation.Defender.CritDefenseBp * (10000L - Math.Min(10000, command.Amount)) / 10000L);
                                applied = true;
                                break;
                        }
                    }
                    if (applied)
                        calculation.TriggeredPassiveReactionIds.Add(binding.SourceId + ":" + rule.Id);
                    if (rule.ExecutionVersion == 1) Trace(state, fact, owner, rule, true, applied,
                        applied ? "Current-hit modifiers applied." : "No supported current-hit modifiers.", eventStart, context.ConditionTrace);
                }
            }
        }

        /// <summary>Rebuild derived contributions after authoritative mutations. Never adds/removes real statuses.</summary>
        public static void Refresh(BattleState state, ISet<string> initializeFullHealth = null,
            IReadOnlyDictionary<string, int> previousMaxHealthOverrides = null)
        {
            if (state == null) return;
            var fighters = Fighters(state);
            var previous = new Dictionary<string, List<PassiveStatContribution>>(StringComparer.Ordinal);
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var fighter in fighters)
            {
                previous.Add(fighter.Id, fighter.PassiveContributions);
                var maximum = previousMaxHealthOverrides != null &&
                    previousMaxHealthOverrides.TryGetValue(fighter.Id, out var previousMaximum)
                    ? previousMaximum
                    : StatusSystem.GetEffectiveStats(fighter).MaxHealth;
                previousMaxHealth.Add(fighter.Id, Math.Max(1, maximum));
                fighter.PassiveContributions = new List<PassiveStatContribution>();
            }
            // Fixed, field-count and counter auras form the lower dependency layer.
            foreach (var owner in fighters) AddAuras(state, fighters, owner, false, null);
            AddEntityAuras(state, fighters, false, null);
            var lowerLayer = new Dictionary<string, StatBlock>(StringComparer.Ordinal);
            foreach (var fighter in fighters) lowerLayer.Add(fighter.Id, StatusSystem.GetEffectiveStats(fighter));
            foreach (var owner in fighters) AddAuras(state, fighters, owner, true, lowerLayer[owner.Id]);
            AddEntityAuras(state, fighters, true, lowerLayer);

            var currentAuras = new HashSet<string>(StringComparer.Ordinal);
            var auraOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var fighter in fighters)
            {
                if (fighter.PassiveContributions == null) continue;
                foreach (var c in fighter.PassiveContributions)
                {
                    if (c == null || string.IsNullOrEmpty(c.OwnerId) || string.IsNullOrEmpty(c.RuleId)) continue;
                    var owner = Find(state, c.OwnerId);
                    var auraDef = owner?.Definition?.Passive?.Auras?.Find(a => (owner.Definition.Passive.Id + ":" + a.Id) == c.RuleId);
                    if (auraDef != null && auraDef.Scaling == PassiveScaling.OwnerCounter) continue;

                    var key = c.OwnerId + "::" + c.RuleId;
                    currentAuras.Add(key);
                    auraOwners[key] = c.OwnerId;
                }
            }

            var newlyActive = new List<string>();
            foreach (var key in currentAuras)
            {
                if (state.ActivePassiveAuras.Contains(key)) continue;
                state.ActivePassiveAuras.Add(key);
                newlyActive.Add(key);
            }
            state.ActivePassiveAuras.RemoveAll(k => !currentAuras.Contains(k));

            foreach (var key in newlyActive)
            {
                var ownerId = auraOwners[key];
                Emit(state, BattleEventKind.PassiveTriggered, ownerId, ownerId, null, 0, "Passive Trigger: " + key);
            }
            foreach (var fighter in fighters)
            {
                var oldHealth = fighter.Health;
                if (fighter.IsAlive && fighter.Health > 0)
                {
                    var maximum = Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                    if (initializeFullHealth?.Contains(fighter.Id) == true)
                    {
                        fighter.Health = maximum;
                    }
                    else
                    {
                        var oldMaximum = previousMaxHealth[fighter.Id];
                        fighter.Health = oldMaximum == maximum
                            ? Math.Min(maximum, fighter.Health)
                            : (int)Math.Min(maximum, (long)fighter.Health * maximum / oldMaximum);
                    }
                }
                if (oldHealth == fighter.Health && Same(previous[fighter.Id], fighter.PassiveContributions)) continue;
                var item = Emit(state, BattleEventKind.PassiveStatsChanged, fighter.Id, fighter.Id, null, 0, "Passive stat contributions changed.");
                item.HealthAfter = fighter.Health;
                item.EffectiveMaxHealth = Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                foreach (var contribution in fighter.PassiveContributions) item.PassiveContributions.Add(contribution.Clone());
            }
            AbilityVariantSystem.RefreshCards(state);
        }

        private static void AddAuras(BattleState state, List<FighterState> fighters, FighterState owner,
            bool statLayer, StatBlock lowerStats)
        {
            var definitions = owner.Definition?.Passive?.Auras;
            if (definitions == null) return;
            AddAuraDefinitions(state, fighters, owner, definitions, owner.Definition.Passive.Id, statLayer, lowerStats);
        }

        private static void AddEntityAuras(BattleState state, List<FighterState> fighters, bool statLayer,
            Dictionary<string, StatBlock> lowerLayer)
        {
            if (state.Entities == null) return;
            foreach (var entity in state.Entities)
            {
                if (!BattleEntitySystem.IsAvailable(state, entity)) continue;
                var owner = BattleEntitySystem.FindOwner(state, entity);
                AddAuraDefinitions(state, fighters, owner, entity.Definition.Auras, entity.Id, statLayer,
                    statLayer ? lowerLayer[owner.Id] : null);
            }
        }

        private static void AddAuraDefinitions(BattleState state, List<FighterState> fighters, FighterState owner,
            List<PassiveAuraDefinition> definitions, string prefix, bool statLayer, StatBlock lowerStats)
        {
            if (definitions == null) return;
            var rules = new List<PassiveAuraDefinition>(definitions);
            rules.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            foreach (var rule in rules)
            {
                if (!IsAvailable(state, owner, rule.Gate) || (rule.Scaling == PassiveScaling.OwnerStat) != statLayer) continue;
                long units = 1;
                if (rule.Scaling == PassiveScaling.OwnerCounter) units = Counter(owner, rule.ScalingKey);
                else if (rule.Scaling == PassiveScaling.FieldStatusStacks)
                {
                    units = 0;
                    foreach (var subject in fighters)
                        if (subject.IsAlive && subject.Health > 0 && !subject.IsReserve && Related(owner, subject, rule.ScalingRelation))
                            units += StatusSystem.Count(subject, requiredTag: rule.ScalingKey);
                }
                else if (statLayer) units = lowerStats.Get(rule.SourceStat);
                // Constant auras have exactly one contribution. MaximumUnits is a hidden,
                // irrelevant field for this scaling mode and may be zero in serialized assets.
                if (rule.Scaling != PassiveScaling.Constant)
                    units = Math.Min(rule.MaximumUnits, Math.Max(0, units));
                foreach (var target in fighters)
                {
                    if (!Matches(owner, target, rule.Targets)) continue;
                    foreach (var definition in rule.Modifiers)
                    {
                        var modifier = definition.Clone();
                        var amount = new BigInteger(modifier.Amount) * units;
                        if (statLayer) amount /= 10000;
                        modifier.Amount = (int)BigInteger.Max(int.MinValue, BigInteger.Min(int.MaxValue, amount));
                        if (modifier.Amount != 0) target.PassiveContributions.Add(new PassiveStatContribution {
                            OwnerId = owner.Id, RuleId = prefix + ":" + rule.Id, Modifier = modifier });
                    }
                }
            }
        }

        private static bool Matches(FighterState owner, FighterState target, PassiveTargetFilter filter) =>
            target.IsAlive && target.Health > 0 && (filter.IncludeReserve || !target.IsReserve) &&
            (filter.IncludeOwner || owner.Id != target.Id) && Related(owner, target, filter.Relation) &&
            (string.IsNullOrEmpty(filter.AttributeId) || target.Definition.AttributeId == filter.AttributeId) &&
            (string.IsNullOrEmpty(filter.SeriesId) || target.Definition.SeriesId == filter.SeriesId) &&
            (string.IsNullOrEmpty(filter.TraitId) || target.Definition.TraitIds?.Contains(filter.TraitId) == true);

        private static bool Related(FighterState owner, FighterState subject, PassiveRelation relation) =>
            relation == PassiveRelation.Any || subject != null && (relation == PassiveRelation.Self ? subject.Id == owner.Id :
                relation == PassiveRelation.Allies ? subject.Side == owner.Side : subject.Side != owner.Side);

        internal static void Notify(BattleState state, PassiveEventKind kind)
        {
            Dispatch(state, new PassiveFact { Kind = kind, TurnSide = state.ActingSide });
        }

        internal static void NotifyFighterDefeated(BattleState state, FighterState defeated,
            FighterState source = null, string rootActionId = null, bool cardOrigin = false,
            bool isUltimate = false, CardCategory category = CardCategory.Attack, int cardRank = 0)
        {
            if (state == null || defeated == null) return;
            using (BeginRootResolution(state, rootActionId))
            {
            BattleEntitySystem.RemoveOwned(state, defeated.Id, rootActionId);
            foreach (var target in Fighters(state))
            {
                if (target?.Statuses?.Instances == null) continue;
                var previous = target.Statuses.Instances.ConvertAll(status => status?.Clone());
                long shieldBefore = 0;
                foreach (var status in previous) shieldBefore += Math.Max(0, status?.ShieldRemaining ?? 0);
                foreach (var status in previous)
                    if (status?.Recipe?.BreakRule == StatusBreakRule.OnSourceDefeated &&
                        status.SourceFighterId == defeated.Id) target.Statuses.RemoveInstance(status.InstanceId);
                if (previous.Count == target.Statuses.Instances.Count) continue;
                long shieldAfter = 0;
                foreach (var status in target.Statuses.Instances) shieldAfter += Math.Max(0, status?.ShieldRemaining ?? 0);
                target.Shield = (int)Math.Max(0L, (long)target.Shield - Math.Max(0L, shieldBefore - shieldAfter));
                var removal = Emit(state, BattleEventKind.StatusRemoved, defeated.Id, target.Id, rootActionId,
                    previous.Count - target.Statuses.Instances.Count, "Source defeated; dependent statuses removed.");
                removal.ShieldAfter = target.Shield;
                removal.StatusesAfter = target.Statuses.Instances.ConvertAll(status => status?.Clone());
                RecordStatusRemovals(state, defeated, target, previous, StatusRemovalReason.OwnerDefeated,
                    rootActionId, cardOrigin);
            }
            Dispatch(state, new PassiveFact { Kind = PassiveEventKind.FighterDefeated,
                TurnSide = state.ActingSide, ActorId = source?.Id, TargetId = defeated.Id,
                RootActionId = rootActionId, CardOrigin = cardOrigin, IsUltimate = isUltimate,
                CardCategory = category, CardRank = cardRank });
            }
        }

        public static void NotifyDamageResolved(BattleState state, FighterState source, FighterState target, int damageAmount,
            string rootActionId = null, bool cardOrigin = false, bool isUltimate = false,
            DamageFamily family = DamageFamily.Normal, CardCategory category = CardCategory.Attack, int cardRank = 0,
            bool wasCritical = false, bool wasBlocked = false, int hitIndex = 1, int hitCount = 1,
            bool deferUntilActionResolved = false)
        {
            if (state == null || target == null || damageAmount <= 0) return;
            var damageElement = ElementId.None;
            for (var eventIndex = state.Events.Count - 1; eventIndex >= 0; eventIndex--)
            {
                var contact = state.Events[eventIndex];
                if (contact.Kind != BattleEventKind.DamageApplied || contact.SourceId != source?.Id ||
                    contact.TargetId != target.Id || contact.RootActionId != rootActionId && contact.CardId != rootActionId) continue;
                damageElement = contact.DamageElement;
                break;
            }
            var fact = new PassiveFact { Kind = PassiveEventKind.DamageResolved, TurnSide = state.ActingSide,
                ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId, DamageAmount = damageAmount,
                CardOrigin = cardOrigin, IsUltimate = isUltimate, DamageFamily = family, DamageElement = damageElement, CardCategory = category,
                CardRank = cardRank, WasCritical = wasCritical, WasBlocked = wasBlocked,
                HitIndex = hitIndex, HitCount = Math.Max(1, hitCount),
                ReactionOrigin = state.PassiveSession?.ReactionDepth > 0 };
            if (deferUntilActionResolved && !string.IsNullOrEmpty(rootActionId))
            {
                var previous = state.PendingPassiveDamageFacts.Find(f => f.RootActionId == rootActionId &&
                    f.ActorId == fact.ActorId && f.TargetId == fact.TargetId && f.DamageFamily == family &&
                    f.ReactionOrigin == fact.ReactionOrigin && f.CardOrigin == fact.CardOrigin && f.DamageElement == damageElement);
                if (previous == null) state.PendingPassiveDamageFacts.Add(fact);
                else
                {
                    previous.DamageAmount = (int)Math.Min(int.MaxValue, (long)previous.DamageAmount + damageAmount);
                    previous.WasCritical |= wasCritical; previous.WasBlocked |= wasBlocked;
                    previous.HitCount = Math.Max(previous.HitCount, fact.HitCount);
                    previous.HitIndex = Math.Max(previous.HitIndex, hitIndex);
                }
            }
            else Dispatch(state, fact);
        }

        /// <summary>Evaluate attack facts against the statuses left by this action, with any-hit crit/block facts.</summary>
        public static void ResolveActionDamageReactions(BattleState state, string rootActionId)
        {
            var facts = state.PendingPassiveDamageFacts.FindAll(f => f.RootActionId == rootActionId);
            state.PendingPassiveDamageFacts.RemoveAll(f => f.RootActionId == rootActionId);
            foreach (var fact in facts)
            {
                fact.HitIndex = fact.HitCount;
                Dispatch(state, fact);
            }
        }

        /// <summary>Resolve final-hit reactions while the attack is still in its damage presentation window.</summary>
        public static void ResolveFinalHitDamageReactions(BattleState state, string rootActionId)
        {
            if (state?.PendingPassiveDamageFacts == null || string.IsNullOrEmpty(rootActionId)) return;
            var facts = state.PendingPassiveDamageFacts.FindAll(f => f.RootActionId == rootActionId);
            foreach (var fact in facts)
            {
                fact.HitIndex = fact.HitCount;
                Dispatch(state, fact, PassiveDamageTiming.FinalHit);
            }
        }

        /// <summary>Single gateway for effect-driven gauge changes. Positive passive refunds cannot be mistaken for card drains.</summary>
        public static int ChangePowerGauge(BattleState state, FighterState actor, FighterState target, int requestedDelta,
            string rootActionId = null, bool cardOrigin = false, bool isUltimate = false)
        {
            var fact = CommitGauge(state, actor, target, requestedDelta, rootActionId, cardOrigin, isUltimate);
            if (fact == null) return 0;
            Dispatch(state, fact);
            return fact.GaugeDelta;
        }

        private static PassiveFact CommitGauge(BattleState state, FighterState actor, FighterState target, int delta,
            string rootActionId, bool cardOrigin, bool ultimate, int? exactDeltaBp = null)
        {
            if (state == null || target == null || !target.IsAlive || target.Health <= 0 || target.PowerGaugeDisabled) return null;
            if (CombatPolicySystem.BlocksGaugeLoss(state, target, exactDeltaBp ?? delta, rootActionId)) return null;
            var before = target.PowerGauge;
            var beforeBp = (long)before * 10000 + Math.Max(0, Math.Min(9999, target.PowerGaugeRemainderBp));
            var afterBp = Math.Max(0, Math.Min(CardRules.UltimateGaugeCost * 10000L,
                beforeBp + (exactDeltaBp.HasValue ? exactDeltaBp.Value : (long)delta * 10000)));
            target.PowerGauge = (int)(afterBp / 10000);
            target.PowerGaugeRemainderBp = (int)(afterBp % 10000);
            var actual = target.PowerGauge - before;
            var actualBp = (int)(afterBp - beforeBp);
            if (actualBp == 0) return null;
            var item = Emit(state, BattleEventKind.PowerGaugeChanged, actor?.Id, target.Id, rootActionId, actual, "Power gauge changed.");
            item.PowerGaugeAfter = target.PowerGauge;
            item.PowerGaugeRemainderAfter = target.PowerGaugeRemainderBp;
            item.GaugeDeltaBp = actualBp;
            if (target.PowerGauge < CardRules.UltimateGaugeCost)
            {
                var hand = state.Team(target.Side).Hand;
                var removedCard = false;
                for (var i = hand.Count - 1; i >= 0; i--)
                {
                    var card = hand[i];
                    if (card.OwnerFighterId != target.Id || card.Kind != CardKind.Ultimate) continue;
                    hand.RemoveAt(i);
                    removedCard = true;
                    Emit(state, BattleEventKind.CardRemoved, target.Id, target.Id, rootActionId, 0, "Ultimate lost readiness.").CardId = card.Id;
                }
                if (removedCard)
                {
                    var merges = new List<BattleEvent>();
                    CardRules.MergeAdjacent(state.Team(target.Side), null, merges);
                    foreach (var merge in merges)
                    {
                        merge.Id = state.Events.Count + 1;
                        merge.RootActionId = rootActionId;
                        state.Events.Add(merge);
                    }
                }
            }
            return new PassiveFact { Kind = PassiveEventKind.GaugeChanged, ActorId = actor?.Id, TargetId = target.Id,
                RootActionId = rootActionId, TurnSide = state.ActingSide, CardOrigin = cardOrigin, IsUltimate = ultimate,
                GaugeDelta = actual, GaugeDeltaBp = actualBp };
        }

        private static void Dispatch(BattleState state, PassiveFact first, PassiveDamageTiming? timingFilter = null)
        {
            using (BeginRootResolution(state, first.RootActionId))
            {
                var session = state.PassiveSession;
                first.ReactionOrigin |= session.ReactionDepth > 0;
                session.Queue.Enqueue(new PassiveDispatchEntry { Fact = first, Timing = timingFilter });
                if (session.IsDraining) return;
                session.IsDraining = true;
                try
                {
            while (session.Queue.Count > 0)
            {
                var entry = session.Queue.Dequeue();
                ProcessFact(state, entry.Fact, entry.Timing);
            }
                }
                finally { session.IsDraining = false; }
            }
        }

        private static void ProcessFact(BattleState state, PassiveFact fact, PassiveDamageTiming? timingFilter = null)
        {
            var session = state.PassiveSession;
            session.Fact();
                fact.Sequence = ++state.PassiveEventSequence;
                foreach (var scheduled in ScheduledBindings(state, fact))
                {
                        var owner = scheduled.Owner;
                        var binding = scheduled.Binding;
                        var rule = binding.Rule;
                        var factActor = Find(state, fact.ActorId);
                        var factTarget = Find(state, fact.TargetId);
                        if (rule == null || rule.Trigger != fact.Kind) continue;
                        var eventStart = state.Events.Count;
                        if (
                            timingFilter.HasValue && rule.DamageTiming != timingFilter.Value ||
                            !(fact.Kind == PassiveEventKind.BeforeDefeat && owner.Id == fact.TargetId && owner.IsAlive
                                ? binding.StatusOwned || GateAvailableAtPendingDefeat(state, owner, rule.Gate)
                                : ReactionAvailable(state, owner, binding)) ||
                            fact.Kind == PassiveEventKind.DamageResolved &&
                                rule.DamageTiming == PassiveDamageTiming.FinalHit && fact.HitIndex < fact.HitCount ||
                            rule.OwnTeamTurnOnly && fact.TurnSide != owner.Side || rule.CardOriginOnly && !fact.CardOrigin ||
                            rule.ExcludeUltimate && fact.IsUltimate || rule.RequireGaugeLoss && GaugeDeltaInBp(fact) >= 0 ||
                            rule.RequireCritical && !fact.WasCritical || rule.RequireBlocked && !fact.WasBlocked ||
                            rule.RequiredDamageElement != ElementId.None && rule.RequiredDamageElement != fact.DamageElement ||
                            rule.FilterDamageFamily && rule.DamageFamily != fact.DamageFamily ||
                            rule.FilterCategory && (!MatchesCategory(rule.Category, fact.CardCategory, fact.IsUltimate) ||
                                fact.CardRank < rule.MinimumRank) ||
                            !Related(owner, factActor, rule.ActorRelation) ||
                            !Related(owner, factTarget, rule.TargetRelation))
                        {
                            Trace(state, fact, owner, rule, false, false, "Eligibility gate or event filter did not match.", eventStart);
                            continue;
                        }
                        if (!CanActivate(state, owner, binding, fact, out var blockedReason))
                        {
                            Trace(state, fact, owner, rule, false, false, blockedReason, eventStart);
                            continue;
                        }
                        var context = new CardEffectContext { Battle = state, EffectOwner = owner,
                            Actor = factActor ?? owner, SelectedTarget = factTarget ?? owner,
                            RootActionId = fact.RootActionId,
                            CardCategory = fact.CardCategory, CardRank = fact.CardRank, IsUltimate = fact.IsUltimate,
                            DamageFamily = fact.DamageFamily, WasCritical = fact.WasCritical, WasBlocked = fact.WasBlocked,
                            HitIndex = fact.HitIndex, HitCount = fact.HitCount,
                            IsFinalHit = fact.Kind == PassiveEventKind.DamageResolved && timingFilter == PassiveDamageTiming.FinalHit,
                            EventFact = fact.Clone(), ReactionOrigin = true, ExecutionVersion = rule.ExecutionVersion,
                            ResourceKey = fact.ResourceKey, PreviousResourceValue = fact.PreviousResourceValue,
                            ResourceValue = fact.ResourceValue, RemovedStatus = fact.RemovedStatus?.Clone(),
                            HealingAmount = fact.HealingAmount, ExecutingRuleId = binding.SourceId + ":" + rule.Id };
                        if (state.CapturePassiveTrace) context.ConditionTrace = new List<PassiveConditionTrace>();
                        var passed = CardEffectSystem.ConditionsPass(rule.Conditions, context, context.SelectedTarget);
                        var commands = passed ? rule.Commands : rule.ElseCommands;
                        if ((commands == null || commands.Count == 0) && (!passed || rule.Actions == null || rule.Actions.Count == 0))
                        {
                            Trace(state, fact, owner, rule, passed, false,
                                passed ? "No commands in selected branch." : "Nested eligibility condition failed; no else commands.", eventStart, context.ConditionTrace);
                            continue;
                        }
                        RecordActivation(state, owner, binding, fact);
                        var executed = false;
                        session.ReactionDepth++;
                        try
                        {
                        if (passed && rule.Actions != null && rule.Actions.Count > 0)
                            executed |= ExecuteActions(state, owner, context, rule.Actions);
                        foreach (var command in commands ?? new List<PassiveCommandDefinition>())
                        {
                            session.Command();
                            if (command == null || command.Kind == PassiveCommandKind.IncreaseCurrentAttackPercent ||
                                command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent ||
                                command.Kind == PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent ||
                                command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent || command.Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent) continue;
                            // On-attack additional damage is one unsplit packet on the final hit,
                            // regardless of a legacy reaction's per-hit timing setting.
                            if (fact.Kind == PassiveEventKind.DamageResolved && fact.CardOrigin &&
                                fact.HitIndex < fact.HitCount && command.Kind == PassiveCommandKind.ExecuteEffect &&
                                command.Effect?.Kind == CardEffectOperationKind.Damage &&
                                command.Effect.Damage?.Family == DamageFamily.Additional) continue;
                            // One activation per command/owner/action, even across hits and area targets.
                            // Status tick notifications with no root action remain independent events.
                            if (rule.ExecutionVersion == 0 && fact.Kind == PassiveEventKind.DamageResolved && !string.IsNullOrEmpty(fact.RootActionId))
                            {
                                var receipt = fact.RootActionId + "\0" + owner.Id + "\0" + binding.SourceId + "\0" +
                                    rule.Id + "\0" + passed + "\0" + commands.IndexOf(command);
                                if (rule.LimitScope == PassiveLimitScope.RootActionTarget)
                                    receipt += "\0" + fact.TargetId;
                                if (state.PassiveDamageReactionReceipts.Contains(receipt)) continue;
                                state.PassiveDamageReactionReceipts.Add(receipt);
                            }
                            var exactRefund = command.Kind == PassiveCommandKind.ChangePowerGauge && command.ValueSource == PassiveValueSource.ActualGaugeLost;
                            var basis = command.ValueSource == PassiveValueSource.ActualGaugeLost
                                ? Math.Max(0, exactRefund ? -GaugeDeltaInBp(fact) : -fact.GaugeDelta) :
                                command.ValueSource == PassiveValueSource.RemovedStatusLayers ? fact.RemovedStatus?.RemovedLayers ?? 0 :
                                command.ValueSource == PassiveValueSource.EventDamage ? fact.DamageAmount :
                                command.ValueSource == PassiveValueSource.EventHealing ? fact.HealingAmount : 1;
                            var amount = rule.ExecutionVersion == 1 ? ResolveCommandAmount(command, context) :
                                (int)Math.Min(int.MaxValue, (long)basis * command.Amount);
                            if (command.Kind == PassiveCommandKind.IncrementCounter)
                            {
                                if (amount <= 0) continue;
                                if (rule.ExecutionVersion == 1)
                                {
                                    ChangeResource(state, owner, command.CounterKey, amount, command.CounterCap, fact.RootActionId);
                                    continue;
                                }
                                var counter = owner.PassiveCounters.Find(item => item.Key == command.CounterKey);
                                if (counter == null) { counter = new PassiveCounterState { Key = command.CounterKey }; owner.PassiveCounters.Add(counter); }
                                var previous = counter.Value;
                                counter.Value = (int)Math.Min(command.CounterCap, (long)counter.Value + amount);
                                if (counter.Value != previous)
                                    Emit(state, BattleEventKind.PassiveTriggered, owner.Id, owner.Id, fact.RootActionId,
                                        counter.Value - previous, binding.SourceId + ":" + rule.Id);
                            }
                            else if (command.Kind == PassiveCommandKind.SetCounter)
                            {
                                if (rule.ExecutionVersion == 1)
                                {
                                    ChangeResource(state, owner, command.CounterKey, amount - Counter(owner, command.CounterKey), command.CounterCap, fact.RootActionId);
                                    continue;
                                }
                                var counter = owner.PassiveCounters.Find(item => item.Key == command.CounterKey);
                                if (counter == null) { counter = new PassiveCounterState { Key = command.CounterKey }; owner.PassiveCounters.Add(counter); }
                                var previous = counter.Value;
                                counter.Value = Math.Min(command.CounterCap, amount);
                                if (counter.Value != previous)
                                    Emit(state, BattleEventKind.PassiveTriggered, owner.Id, owner.Id, fact.RootActionId,
                                        counter.Value - previous, binding.SourceId + ":" + rule.Id);
                            }
                            else if (command.Kind == PassiveCommandKind.ExecuteEffect)
                            {
                                if (command.Effect == null) continue;
                                executed = true;
                                BattleEngine.ExecutePassiveOperation(state, owner, context,
                                    rule.ExecutionVersion == 1 ? BindEffect(command, amount) : command.Effect,
                                    fact.RootActionId, binding.SourceId + ":" + rule.Id);
                            }
                            else if (command.Kind == PassiveCommandKind.SetAbilityVariant)
                            {
                                AbilityVariantSystem.SetUnlocked(state, owner, command.SkillId, command.VariantId, command.VariantUnlocked);
                                executed = true;
                            }
                            else if (command.Kind == PassiveCommandKind.ChangePowerGauge)
                            {
                                if (amount <= 0) continue;
                                var emitted = CommitGauge(state, owner, owner, exactRefund ? 0 : amount, fact.RootActionId,
                                    false, false, exactRefund ? amount : (int?)null);
                                if (emitted != null)
                                {
                                    Emit(state, BattleEventKind.PassiveTriggered, owner.Id, owner.Id, fact.RootActionId,
                                        emitted.GaugeDelta, binding.SourceId + ":" + rule.Id);
                                    emitted.ReactionOrigin = true;
                                    session.Queue.Enqueue(new PassiveDispatchEntry { Fact = emitted });
                                }
                            }
                        }
                        }
                        finally { session.ReactionDepth--; }
                        Trace(state, fact, owner, rule, passed, executed || state.Events.Count != eventStart,
                            passed ? "Eligibility and nested conditions passed; selected commands dispatched." :
                                "Nested eligibility condition failed; else branch selected.", eventStart, context.ConditionTrace);
                }
                Refresh(state);
        }

        /// <summary>Interception is synchronous; facts emitted by its commands retain the root queue and budget.</summary>
        public static void EmitInterceptFact(BattleState state, PassiveFact fact)
        {
            if (state == null || fact == null) return;
            if (fact.Kind != PassiveEventKind.BeforeDefeat)
                throw new ArgumentOutOfRangeException(nameof(fact), "Only BeforeDefeat is an interception fact.");
            using (BeginRootResolution(state, fact.RootActionId))
            {
                var frozen = fact.Clone();
                frozen.ReactionOrigin |= state.PassiveSession.ReactionDepth > 0;
                if (!state.PassiveSession.IsDraining) { Dispatch(state, frozen); return; }
                ProcessFact(state, frozen);
            }
        }

        private static FighterState Find(BattleState state, string id) => string.IsNullOrEmpty(id) ? null :
            state.Player.FindFighter(id) ?? state.Opponent.FindFighter(id);

        private static void Trace(BattleState state, PassiveFact fact, FighterState owner,
            PassiveReactionDefinition rule, bool passed, bool fired, string reason, int firstEvent,
            List<PassiveConditionTrace> conditions = null)
        {
            if (!state.CapturePassiveTrace) return;
            state.PassiveTrace.Add(new PassiveRuleTrace { FactSequence = fact.Sequence, Trigger = fact.Kind.ToString(),
                OwnerId = owner.Id, RuleId = rule.Id, RootActionId = fact.RootActionId, ConditionsPassed = passed,
                Fired = fired, Reason = reason, FirstEventIndex = firstEvent, EventCount = state.Events.Count - firstEvent,
                ActorId = fact.ActorId, EventTargetId = fact.TargetId, OperationTargetId = fact.TargetId,
                Outcome = fired ? "Applied" : "Skipped", Branch = passed ? "Eligible" : "Ineligible",
                Conditions = conditions?.ConvertAll(c => c.Clone()) ?? new List<PassiveConditionTrace>() });
        }

        private static int GaugeDeltaInBp(PassiveFact fact) => fact.GaugeDeltaBp != 0 ? fact.GaugeDeltaBp : fact.GaugeDelta * 10000;

        private static bool MatchesCategory(CardCategory requested, CardCategory actual, bool isUltimate) =>
            requested == CardCategory.Ultimate ? isUltimate : requested == actual;

        private static bool ReactionAvailable(BattleState state, FighterState owner, ReactionBinding binding)
        {
            if (binding == null || binding.Rule == null || owner == null || !owner.IsAlive || owner.Health <= 0)
                return false;
            // The presence of the live status instance is the gate for a status-owned reaction.
            // Passive gates remain applicable to reactions authored on the character passive.
            return binding.StatusOwned || IsAvailable(state, owner, binding.Rule.Gate);
        }

        private static List<ReactionBinding> ReactionBindings(FighterState owner)
        {
            var result = new List<ReactionBinding>();
            var passive = owner?.Definition?.Passive;
            if (passive?.Reactions != null)
                foreach (var rule in passive.Reactions)
                    if (rule != null) result.Add(new ReactionBinding { Rule = rule,
                        SourceId = passive.Id ?? "passive", StatusOwned = false });

            if (owner?.Statuses?.Instances != null)
                foreach (var status in owner.Statuses.Instances)
                {
                    var reactions = status?.Recipe?.Reactions;
                    if (reactions == null) continue;
                    foreach (var rule in reactions)
                        if (rule != null) result.Add(new ReactionBinding { Rule = rule,
                            SourceId = StatusBindingSource(status, rule), StatusOwned = true });
                }
            return result;
        }

        private static List<FighterState> Fighters(BattleState state)
        {
            var result = new List<FighterState>();
            result.AddRange(state.Player.Fighters); result.AddRange(state.Opponent.Fighters);
            result.Sort((a, b) => a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.TeamIndex.CompareTo(b.TeamIndex));
            return result;
        }

        private static bool Same(List<PassiveStatContribution> left, List<PassiveStatContribution> right)
        {
            if (left == null || left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
                if (left[i].OwnerId != right[i].OwnerId || left[i].RuleId != right[i].RuleId ||
                    left[i].Modifier.ResolvedStat != right[i].Modifier.ResolvedStat || left[i].Modifier.ResolvedOperation != right[i].Modifier.ResolvedOperation ||
                    left[i].Modifier.Amount != right[i].Modifier.Amount) return false;
            return true;
        }

        private static BattleEvent Emit(BattleState state, BattleEventKind kind, string source, string target,
            string root, int amount, string message)
        {
            var item = new BattleEvent { Id = state.Events.Count + 1, Kind = kind, SourceId = source, TargetId = target,
                RootActionId = root, Amount = amount, Message = message };
            state.Events.Add(item);
            return item;
        }
    }
}
