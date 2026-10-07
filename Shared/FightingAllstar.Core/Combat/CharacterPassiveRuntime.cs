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
    internal sealed class PassiveFact
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
        public int DamageAmount;
        public DamageFamily DamageFamily;
        public CardCategory CardCategory;
        public int CardRank;
        public bool WasCritical;
        public bool WasBlocked;
    }

    internal sealed class ReactionBinding
    {
        public PassiveReactionDefinition Rule;
        public string SourceId;
        public bool StatusOwned;
    }

    public static class CharacterPassiveRuntime
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
            foreach (var owner in Fighters(state))
            {
                foreach (var binding in ReactionBindings(owner))
                {
                    var rule = binding.Rule;
                    if (rule == null || rule.Trigger != PassiveEventKind.BeforeDamage ||
                        !ReactionAvailable(state, owner, binding) ||
                        rule.OwnTeamTurnOnly && state.ActingSide != owner.Side ||
                        rule.CardOriginOnly && !cardOrigin || rule.ExcludeUltimate && isUltimate ||
                        rule.RequireCritical || rule.RequireBlocked ||
                        rule.FilterDamageFamily && rule.DamageFamily != family ||
                        rule.FilterCategory && (card == null ||
                            !MatchesCategory(rule.Category, CardRules.GetEffectCategory(card), isUltimate) ||
                            card.Rank < rule.MinimumRank) ||
                        !Related(owner, actor, rule.ActorRelation) || !Related(owner, target, rule.TargetRelation)) continue;

                    var context = new CardEffectContext
                    {
                        Battle = state,
                        EffectOwner = owner,
                        Actor = actor,
                        SelectedTarget = target,
                        CardCategory = CardRules.GetEffectCategory(card),
                        CardRank = card?.Rank ?? 0,
                        IsUltimate = isUltimate,
                        DamageFamily = family
                    };
                    if (!CardEffectSystem.ConditionsPass(rule.Conditions, context, target)) continue;

                    var applied = false;
                    if (rule.Commands == null) continue;
                    foreach (var command in rule.Commands)
                    {
                        if (command == null || command.Amount <= 0 || command.ValueSource != PassiveValueSource.Fixed) continue;
                        switch (command.Kind)
                        {
                            case PassiveCommandKind.IncreaseCurrentAttackPercent:
                                calculation.Attacker.Attack = (int)Math.Min(int.MaxValue,
                                    (long)calculation.Attacker.Attack * (10000L + command.Amount) / 10000L);
                                applied = true;
                                break;
                            case PassiveCommandKind.IncreaseCurrentDamageDealtPercent:
                                calculation.Policy.OutgoingIncreaseBp = (int)Math.Min(int.MaxValue,
                                    (long)calculation.Policy.OutgoingIncreaseBp + command.Amount);
                                applied = true;
                                break;
                        }
                    }
                    if (applied)
                        calculation.TriggeredPassiveReactionIds.Add(binding.SourceId + ":" + rule.Id);
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
            var lowerLayer = new Dictionary<string, StatBlock>(StringComparer.Ordinal);
            foreach (var fighter in fighters) lowerLayer.Add(fighter.Id, StatusSystem.GetEffectiveStats(fighter));
            foreach (var owner in fighters) AddAuras(state, fighters, owner, true, lowerLayer[owner.Id]);

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
        }

        private static void AddAuras(BattleState state, List<FighterState> fighters, FighterState owner,
            bool statLayer, StatBlock lowerStats)
        {
            var definitions = owner.Definition?.Passive?.Auras;
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
                            OwnerId = owner.Id, RuleId = owner.Definition.Passive.Id + ":" + rule.Id, Modifier = modifier });
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
            Dispatch(state, new PassiveFact { Kind = PassiveEventKind.FighterDefeated,
                TurnSide = state.ActingSide, ActorId = source?.Id, TargetId = defeated.Id,
                RootActionId = rootActionId, CardOrigin = cardOrigin, IsUltimate = isUltimate,
                CardCategory = category, CardRank = cardRank });
        }

        public static void NotifyDamageResolved(BattleState state, FighterState source, FighterState target, int damageAmount,
            string rootActionId = null, bool cardOrigin = false, bool isUltimate = false,
            DamageFamily family = DamageFamily.Normal, CardCategory category = CardCategory.Attack, int cardRank = 0,
            bool wasCritical = false, bool wasBlocked = false)
        {
            if (state == null || target == null || damageAmount <= 0) return;
            Dispatch(state, new PassiveFact { Kind = PassiveEventKind.DamageResolved, TurnSide = state.ActingSide,
                ActorId = source?.Id, TargetId = target.Id, RootActionId = rootActionId, DamageAmount = damageAmount,
                CardOrigin = cardOrigin, IsUltimate = isUltimate, DamageFamily = family, CardCategory = category,
                CardRank = cardRank, WasCritical = wasCritical, WasBlocked = wasBlocked });
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
            string rootActionId, bool cardOrigin, bool ultimate)
        {
            if (state == null || target == null || !target.IsAlive || target.Health <= 0 || target.PowerGaugeDisabled) return null;
            var before = target.PowerGauge;
            target.PowerGauge = (int)Math.Max(0, Math.Min(CardRules.UltimateGaugeCost, (long)before + delta));
            var actual = target.PowerGauge - before;
            if (actual == 0) return null;
            var item = Emit(state, BattleEventKind.PowerGaugeChanged, actor?.Id, target.Id, rootActionId, actual, "Power gauge changed.");
            item.PowerGaugeAfter = target.PowerGauge;
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
                RootActionId = rootActionId, TurnSide = state.ActingSide, CardOrigin = cardOrigin, IsUltimate = ultimate, GaugeDelta = actual };
        }

        private static void Dispatch(BattleState state, PassiveFact first)
        {
            var queue = new Queue<PassiveFact>();
            queue.Enqueue(first);
            var processed = 0;
            while (queue.Count > 0)
            {
                if (++processed > 256) throw new InvalidOperationException("Passive reaction chain exceeds the deterministic event budget.");
                var fact = queue.Dequeue();
                fact.Sequence = ++state.PassiveEventSequence;
                foreach (var owner in Fighters(state))
                {
                    var bindings = ReactionBindings(owner);
                    bindings.Sort((a, b) =>
                    {
                        var source = StringComparer.Ordinal.Compare(a.SourceId, b.SourceId);
                        return source != 0 ? source : StringComparer.Ordinal.Compare(a.Rule?.Id, b.Rule?.Id);
                    });
                    foreach (var binding in bindings)
                    {
                        var rule = binding.Rule;
                        var factActor = Find(state, fact.ActorId);
                        var factTarget = Find(state, fact.TargetId);
                        if (rule == null || rule.Trigger != fact.Kind || !ReactionAvailable(state, owner, binding) ||
                            rule.OwnTeamTurnOnly && fact.TurnSide != owner.Side || rule.CardOriginOnly && !fact.CardOrigin ||
                            rule.ExcludeUltimate && fact.IsUltimate || rule.RequireGaugeLoss && fact.GaugeDelta >= 0 ||
                            rule.RequireCritical && !fact.WasCritical || rule.RequireBlocked && !fact.WasBlocked ||
                            rule.FilterDamageFamily && rule.DamageFamily != fact.DamageFamily ||
                            rule.FilterCategory && (!MatchesCategory(rule.Category, fact.CardCategory, fact.IsUltimate) ||
                                fact.CardRank < rule.MinimumRank) ||
                            !Related(owner, factActor, rule.ActorRelation) ||
                            !Related(owner, factTarget, rule.TargetRelation)) continue;
                        var context = new CardEffectContext { Battle = state, EffectOwner = owner,
                            Actor = factActor ?? owner, SelectedTarget = factTarget ?? owner,
                            RootActionId = fact.RootActionId,
                            CardCategory = fact.CardCategory, CardRank = fact.CardRank, IsUltimate = fact.IsUltimate,
                            DamageFamily = fact.DamageFamily, WasCritical = fact.WasCritical, WasBlocked = fact.WasBlocked };
                        var passed = CardEffectSystem.ConditionsPass(rule.Conditions, context, context.SelectedTarget);
                        var commands = passed ? rule.Commands : rule.ElseCommands;
                        if (commands == null) continue;
                        foreach (var command in commands)
                        {
                            if (command == null || command.Kind == PassiveCommandKind.IncreaseCurrentAttackPercent ||
                                command.Kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent) continue;
                            var basis = command.ValueSource == PassiveValueSource.ActualGaugeLost ? Math.Max(0, -fact.GaugeDelta) : 1;
                            var amount = (int)Math.Min(int.MaxValue, (long)basis * command.Amount);
                            if (command.Kind == PassiveCommandKind.IncrementCounter)
                            {
                                if (amount <= 0) continue;
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
                                BattleEngine.ExecutePassiveOperation(state, owner, context, command.Effect,
                                    fact.RootActionId, binding.SourceId + ":" + rule.Id);
                            }
                            else
                            {
                                if (amount <= 0) continue;
                                var emitted = CommitGauge(state, owner, owner, amount, fact.RootActionId, false, false);
                                if (emitted != null)
                                {
                                    Emit(state, BattleEventKind.PassiveTriggered, owner.Id, owner.Id, fact.RootActionId,
                                        emitted.GaugeDelta, binding.SourceId + ":" + rule.Id);
                                    queue.Enqueue(emitted);
                                }
                            }
                        }
                    }
                }
                Refresh(state);
            }
        }

        private static FighterState Find(BattleState state, string id) => string.IsNullOrEmpty(id) ? null :
            state.Player.FindFighter(id) ?? state.Opponent.FindFighter(id);

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
                            SourceId = status.RecipeId ?? status.Recipe?.Id ?? "status", StatusOwned = true });
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
