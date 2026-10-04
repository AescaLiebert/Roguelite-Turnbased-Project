using System;
using System.Collections.Generic;
using System.Numerics;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;

namespace FightingAllstar.Core.Combat
{
    public static class BattleEngine
    {
        public const int MaximumCompletedTurns = 40;
        private static readonly List<AttackEffectRecipeDefinition> StandardAttackEffects = StandardEffectDatabase.CreateAttackEffects();

        public static BattleState Create(string matchId, IReadOnlyList<CharacterDefinition> player,
            IReadOnlyList<int> playerTiers, IReadOnlyList<CharacterDefinition> opponent, IReadOnlyList<int> opponentTiers,
            ulong seed, TeamSide firstSide = TeamSide.Player, IReadOnlyList<RunBoonDefinition> runBoons = null,
            IReadOnlyList<int> playerHealth = null, IReadOnlyList<int> opponentHealth = null,
            BattleModeMask mode = BattleModeMask.PvE)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("A stable match id is required.", nameof(matchId));
            if (mode != BattleModeMask.PvE && mode != BattleModeMask.PvP) throw new ArgumentOutOfRangeException(nameof(mode));
            var state = new BattleState { MatchId = matchId, Revision = 1, TurnNumber = 1, ActingSide = firstSide, Phase = BattlePhase.Setup, Mode = mode };
            AddTeam(state.Player, player, playerTiers, playerHealth);
            AddTeam(state.Opponent, opponent, opponentTiers, opponentHealth);
            state.Player.HandCapacity = CardRules.GetHandCapacity(state.Player);
            state.Opponent.HandCapacity = CardRules.GetHandCapacity(state.Opponent);
            AddEvent(state, BattleEventKind.BattleStarted, null, null, null, 0, "Battle initialized.");
            var fullHealth = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fighter in state.Player.Fighters)
                if (playerHealth == null || fighter.TeamIndex >= playerHealth.Count) fullHealth.Add(fighter.Id);
            foreach (var fighter in state.Opponent.Fighters)
                if (opponentHealth == null || fighter.TeamIndex >= opponentHealth.Count) fullHealth.Add(fighter.Id);
            CharacterPassiveRuntime.Refresh(state, fullHealth);
            CharacterPassiveRuntime.Notify(state, PassiveEventKind.BattleStarted);
            if (runBoons != null)
                foreach (var boon in runBoons)
                {
                    state.RunBoons.Add(boon.Clone());
                    if (boon.EffectKind == RunBoonEffectKind.StartPowerGauge
                        && firstSide != TeamSide.Player && state.Player.LivingActive().Count > 0)
                    {
                        var first = state.Player.LivingActive()[0];
                        first.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, first.PowerGauge + Math.Max(0, boon.MagnitudePoints));
                    }
                }
            var rng = new DeterministicRandom(seed);
            DrawOpeningHand(state, state.Player, rng);
            DrawOpeningHand(state, state.Opponent, rng);
            BeginTurn(state, firstSide, rng, false);
            state.RngState = rng.State;
            state.RngDrawCount = rng.DrawCount;
            return state;
        }

        public static bool TryResolvePlan(BattleState current, TurnPlan plan, out BattleState next, out string error)
        {
            next = null;
            error = null;
            if (current == null || plan == null) { error = "Battle or plan is missing."; return false; }
            if (current.Phase != BattlePhase.Planning || current.Winner.HasValue || current.IsDraw) { error = "The battle is not accepting a plan."; return false; }
            if (plan.ExpectedRevision != current.Revision) { error = "Battle revision is stale."; return false; }
            if (string.IsNullOrWhiteSpace(plan.RequestId)) { error = "Plan requires an idempotency request id."; return false; }
            if (plan.Actions == null || plan.Actions.Count > current.ActionBudget) { error = "Plan exceeds the frozen action budget."; return false; }
            if (!ValidatePlan(current, plan, out error)) return false;

            var work = current.Clone();
            CharacterPassiveRuntime.Refresh(work);
            var team = work.Team(work.ActingSide);
            var rng = DeterministicRandom.Restore(work.RngState, work.RngDrawCount);
            work.Phase = BattlePhase.Resolving;
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                if (!ApplyAction(work, team, action, rng, out error)) return false;
                AdvanceActionClock(work);
                if (work.Winner.HasValue || work.IsDraw) break;
            }
            if (!work.Winner.HasValue && !work.IsDraw) EndTurn(work, rng);
            work.Revision++;
            work.RngState = rng.State;
            work.RngDrawCount = rng.DrawCount;
            next = work;
            return true;
        }

        private static bool ValidatePlan(BattleState state, TurnPlan plan, out string error)
        {
            error = null;
            var draft = new PlanDraft(state);
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                var valid = action.IsMove
                    ? draft.QueueMove(action.CardId, action.DestinationIndex, out error)
                    : draft.QueuePlay(action.CardId, action.TargetFighterId, out error);
                if (!valid) return false;
            }
            return true;
        }

        private static bool ApplyAction(BattleState state, BattleTeamState team, PlannedAction action, DeterministicRandom rng, out string error)
        {
            error = null;
            var index = team.Hand.FindIndex(c => c.Id == action.CardId);
            if (index < 0) { Fizzle(state, action, null, "The planned card is no longer available."); return true; }
            var card = team.Hand[index];
            var owner = team.FindFighter(card.OwnerFighterId);
            if (owner == null || !owner.IsAlive || owner.IsReserve)
            { Fizzle(state, action, card.OwnerFighterId, "The card owner is no longer active."); return true; }
            if (action.IsMove)
            {
                var timeline = new List<BattleEvent>();
                if (!CardRules.TryMove(team, index, action.DestinationIndex, true, out error, null, timeline))
                { Fizzle(state, action, owner.Id, error); error = null; return true; }
                AppendTimeline(state, timeline);
                return true;
            }

            if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost)
            { Fizzle(state, action, owner.Id, "Ultimate lost readiness before it resolved."); return true; }
            var targets = ResolveActionTargets(state, owner, card, action.TargetFighterId, rng);
            if (targets.Count == 0) { Fizzle(state, action, owner.Id, "No living active target remained."); return true; }
            var target = targets[0];
            EffectDefinition effect;
            var found = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            if (!found) { Fizzle(state, action, owner.Id, "The planned card has no runtime effect definition."); return true; }
            var category = card.Kind == CardKind.Ultimate ? CardCategory.Attack : card.Category;
            if (StatusSystem.IsCardUseBlocked(owner, category, card.Rank, card.Kind == CardKind.Ultimate,
                effect.Sequence != null && effect.Sequence.Count > 0))
            { Fizzle(state, action, owner.Id, "A status prevents this card category or effect from being used."); return true; }
            team.Hand.RemoveAt(index);
            if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
            else owner.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, owner.PowerGauge + 1);
            // Explicit pre-effects resolve before CardPlayed, which starts the skill animation in playback.
            ApplyCardEffectSteps(state, owner, target, effect, card.Id, CardEffectTiming.BeforeAction, card.Kind == CardKind.Ultimate);
            foreach (var actionTarget in targets)
                ApplyCardEffectSteps(state, owner, actionTarget, effect, card.Id, CardEffectTiming.BeforeDamage, card.Kind == CardKind.Ultimate);
            CharacterPassiveRuntime.Refresh(state);
            AddEvent(state, BattleEventKind.CardPlayed, owner.Id, target.Id, card.Id, card.Rank, "Card played.");
            state.Events[state.Events.Count - 1].Card = card.Clone();
            foreach (var actionTarget in targets) state.Events[state.Events.Count - 1].TargetIds.Add(actionTarget.Id);
            state.Events[state.Events.Count - 1].PowerGaugeAfter = owner.PowerGauge;
            var mergedAfterPlay = new List<BattleEvent>();
            CardRules.MergeAdjacent(team, null, mergedAfterPlay);
            AppendTimeline(state, mergedAfterPlay);
            if (effect.Kind != EffectKind.Damage)
            {
                ApplyCardUtilityEffect(state, owner, target, effect, card.Id, card.Kind == CardKind.Ultimate);
                ApplyCardEffectSteps(state, owner, target, effect, card.Id, CardEffectTiming.AfterAction, card.Kind == CardKind.Ultimate);
                CheckOutcome(state);
                return true;
            }
            foreach (var actionTarget in targets)
                ResolveCardDamage(state, owner, actionTarget, card, effect, rng);
            CharacterPassiveRuntime.Refresh(state);
            ApplyCardEffectSteps(state, owner, target, effect, card.Id, CardEffectTiming.AfterAction, card.Kind == CardKind.Ultimate);
            CheckOutcome(state);
            return true;
        }

        private static List<FighterState> ResolveActionTargets(BattleState state, FighterState owner,
            CardState card, string requestedTargetId, DeterministicRandom rng)
        {
            var result = new List<FighterState>();
            var friendly = state.Team(owner.Side);
            var enemy = state.OtherTeam(owner.Side);
            switch (card.TargetScope)
            {
                case EffectTargetScope.Self:
                    if (owner.IsAlive && !owner.IsReserve) result.Add(owner);
                    break;
                case EffectTargetScope.SelectedAlly:
                    var ally = friendly.FindFighter(requestedTargetId);
                    if (ally != null && ally.IsAlive && !ally.IsReserve) result.Add(ally);
                    break;
                case EffectTargetScope.AllAllies:
                    result.AddRange(friendly.LivingActive());
                    break;
                case EffectTargetScope.AllEnemies:
                    result.AddRange(enemy.LivingActive());
                    break;
                default:
                    var target = enemy.FindFighter(requestedTargetId);
                    if (target == null || !target.IsAlive || target.IsReserve) target = RandomValidTarget(enemy, rng);
                    if (target != null) result.Add(target);
                    break;
            }
            return result;
        }

        private static void ResolveCardDamage(BattleState state, FighterState owner,
            FighterState target, CardState card, EffectDefinition effect, DeterministicRandom rng)
        {
            if (target == null || !target.IsAlive || target.Health <= 0) return;
            var attackEffect = FindAttackEffect(effect.KeywordId);
            var calculation = AttackEffectSystem.Prepare(attackEffect, state, owner, target, effect.Family);
            if (calculation.RemoveTargetBuffsBeforeDamage)
                EmitStatusRemoval(state, owner, target, StatusPolarity.Buff, true, card.Id);
            var attacker = calculation.Attacker;
            var defender = calculation.Defender;
            var baseAmount = effect.Scaling switch
            {
                StatScaling.Attack => attacker.Attack,
                StatScaling.Defense => attacker.Defense,
                StatScaling.MaxHealth => attacker.MaxHealth,
                StatScaling.SpecificStat => attacker.Get(effect.ScalingStat),
                _ => effect.Magnitude
            };
            if (effect.Scaling == StatScaling.Fixed) baseAmount = effect.Magnitude;
            var keywordFactor = (int)Math.Min(int.MaxValue,
                (long)Math.Max(0, effect.KeywordFactorBp) * Math.Max(0, calculation.KeywordFactorBp) / 10000);
            var packet = new DamagePacket { BaseAmount = baseAmount, CoefficientBp = effect.CoefficientBp,
                KeywordFactorBp = keywordFactor, Policy = calculation.Policy };
            var critRoll = -1;
            var blockRoll = -1;
            if (effect.Family != DamageFamily.Destructive)
            {
                critRoll = rng.NextBasisPoints();
                var critChance = Math.Max(0, Math.Min(10000, attacker.CritChanceBp - defender.CritResistanceBp));
                if (critRoll >= critChance) blockRoll = rng.NextBasisPoints();
            }
            var damage = DamageResolver.Resolve(packet, attacker, defender, target.Health, target.Shield, critRoll, blockRoll);
            target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            AddEvent(state, BattleEventKind.DamageApplied, owner.Id, target.Id, card.Id, damage.HealthLost,
                (damage.WasCritical ? "Critical. " : string.Empty) + (damage.WasBlocked ? "Blocked. " : string.Empty));
            state.Events[state.Events.Count - 1].HealthAfter = target.Health;
            state.Events[state.Events.Count - 1].ShieldAfter = target.Shield;
            state.Events[state.Events.Count - 1].WasCritical = damage.WasCritical;
            state.Events[state.Events.Count - 1].WasBlocked = damage.WasBlocked;
            state.Events[state.Events.Count - 1].WasEndured = damage.WasEndured;
            state.Events[state.Events.Count - 1].ShieldLost = damage.ShieldLost;
            ApplyLifesteal(state, owner, attacker, damage.HealthLost);
            ApplyCardEffectSteps(state, owner, target, effect, card.Id, CardEffectTiming.AfterDamage, card.Kind == CardKind.Ultimate);
            var hasSequenceGaugeDrain = effect?.Sequence != null && effect.Sequence.Exists(s => s?.Effect?.Kind == EffectKind.ChangePowerGauge);
            if (!hasSequenceGaugeDrain && target.IsAlive && target.Health > 0)
            {
                var drain = 0;
                if (effect.PowerGaugeAmount < 0) drain = -effect.PowerGaugeAmount;
                else if (string.Equals(effect.KeywordId, "Depletes", StringComparison.OrdinalIgnoreCase))
                {
                    drain = card.Kind == CardKind.Ultimate
                        ? (owner.Definition?.Id == "fighter.king94" ? 3 : 5)
                        : card.Rank;
                }
                if (drain > 0)
                {
                    CharacterPassiveRuntime.ChangePowerGauge(state, owner, target, -drain, card.Id, cardOrigin: true, isUltimate: card.Kind == CardKind.Ultimate);
                }
            }
            if (damage.Executed || target.Health <= 0)
            {
                target.Health = 0;
                target.IsAlive = false;
                target.PowerGauge = 0;
                CardRules.RemoveDeadOwnerCards(state.Team(target.Side), target.Id);
                AddEvent(state, BattleEventKind.FighterDefeated, target.Id, null, card.Id, 0, "Fighter defeated.");
                var mergedAfterDeath = new List<BattleEvent>();
                CardRules.MergeAdjacent(state.Team(target.Side), null, mergedAfterDeath);
                AppendTimeline(state, mergedAfterDeath);
            }
        }

        private static AttackEffectRecipeDefinition FindAttackEffect(string keywordId)
        {
            if (string.IsNullOrWhiteSpace(keywordId)) return null;
            var normalized = keywordId.Trim().ToLowerInvariant().Replace(" ", "-").Replace("_", "-");
            if (!normalized.StartsWith("attack.", StringComparison.Ordinal)) normalized = "attack." + normalized;
            return StandardAttackEffects.Find(item => item != null && string.Equals(item.Id, normalized, StringComparison.Ordinal));
        }

        private static void AdvanceActionClock(BattleState state)
        {
            foreach (var fighter in state.Player.Fighters) fighter?.Statuses?.Advance(StatusDurationClock.ActionEnd);
            foreach (var fighter in state.Opponent.Fighters) fighter?.Statuses?.Advance(StatusDurationClock.ActionEnd);
            CharacterPassiveRuntime.Refresh(state);
        }

        private static void ApplyCardEffectSteps(BattleState state, FighterState owner, FighterState target,
            EffectDefinition rootEffect, string cardId, CardEffectTiming timing, bool isUltimate = false)
        {
            if (rootEffect?.Sequence == null) return;
            foreach (var step in rootEffect.Sequence)
            {
                if (step?.Effect == null || step.Timing != timing || step.Effect.Kind == EffectKind.Damage) continue;
                ApplyCardUtilityEffect(state, owner, target, step.Effect, cardId, isUltimate);
            }
        }

        private static void ApplyCardUtilityEffect(BattleState state, FighterState owner, FighterState target,
            EffectDefinition effect, string cardId, bool isUltimate = false)
        {
            var targets = ResolveUtilityTargets(state, owner, target, effect.Target);
            foreach (var effectTarget in targets)
                ApplyCardUtilityEffectSingle(state, owner, effectTarget, effect, cardId, isUltimate);
        }

        private static List<FighterState> ResolveUtilityTargets(BattleState state, FighterState owner,
            FighterState selectedTarget, string targetScope)
        {
            var result = new List<FighterState>();
            if (string.Equals(targetScope, "Self", StringComparison.OrdinalIgnoreCase))
            {
                if (owner != null && owner.IsAlive && owner.Health > 0) result.Add(owner);
            }
            else if (string.Equals(targetScope, "SelectedAlly", StringComparison.OrdinalIgnoreCase))
            {
                if (selectedTarget != null && selectedTarget.Side == owner?.Side && selectedTarget.IsAlive && selectedTarget.Health > 0)
                    result.Add(selectedTarget);
                else if (owner != null && owner.IsAlive && owner.Health > 0) result.Add(owner);
            }
            else if (string.Equals(targetScope, "AllAllies", StringComparison.OrdinalIgnoreCase))
            {
                if (owner != null) result.AddRange(state.Team(owner.Side).LivingActive());
            }
            else if (string.Equals(targetScope, "AllEnemies", StringComparison.OrdinalIgnoreCase))
            {
                if (owner != null) result.AddRange(state.OtherTeam(owner.Side).LivingActive());
            }
            else if (selectedTarget != null && selectedTarget.IsAlive && selectedTarget.Health > 0)
                result.Add(selectedTarget);
            return result;
        }

        private static void ApplyCardUtilityEffectSingle(BattleState state, FighterState owner, FighterState effectTarget,
            EffectDefinition effect, string cardId, bool isUltimate)
        {
            if (effectTarget == null || !effectTarget.IsAlive || effectTarget.Health <= 0) return;
            switch (effect.Kind)
            {
                case EffectKind.ApplyStatus:
                    var statusInstanceId = state.MatchId + ":status:" + (state.Events.Count + 1);
                    var appliedStatus = effect.StatusRecipe != null
                        ? StatusSystem.Apply(effectTarget, owner.Id, owner.Side, effect.StatusRecipe, statusInstanceId,
                            cardId, state.Events.Count + 1, stackCount: Math.Max(1, effect.StatusStackCount),
                            duration: effect.StatusDurationOverride).Accepted
                        : effect.Status != null && StatusSystem.Apply(effectTarget, owner.Id, effect.Status,
                            statusInstanceId, state.Events.Count + 1,
                            effectTarget.OwnerTurnsCompleted + (effectTarget.Side == state.ActingSide ? 1 : 0), owner.Side);
                    if (appliedStatus)
                        AddEvent(state, BattleEventKind.StatusApplied, owner.Id, effectTarget.Id, cardId, 1,
                            effect.StatusRecipe?.Id ?? effect.Status?.Id);
                    break;
                case EffectKind.Cleanse:
                case EffectKind.RemoveDebuffs:
                    EmitStatusRemoval(state, owner, effectTarget, StatusPolarity.Debuff, true, cardId);
                    break;
                case EffectKind.RemoveBuffs:
                    EmitStatusRemoval(state, owner, effectTarget, StatusPolarity.Buff, true, cardId);
                    break;
                case EffectKind.Heal:
                    var targetStats = StatusSystem.GetEffectiveStats(effectTarget);
                    var requested = 0;
                    if (effect.HealValue != null)
                    {
                        var healContext = new CardEffectContext { Battle = state, EffectOwner = owner, Actor = owner,
                            SelectedTarget = effectTarget };
                        var raw = CardEffectSystem.ResolveValue(effect.HealValue, healContext, effectTarget);
                        requested = (int)BigInteger.Min(int.MaxValue,
                            BigInteger.Divide(new BigInteger(Math.Max(0, raw)) * Math.Max(0, targetStats.RecoveryBp), 10000));
                    }
                    else
                    {
                        var sourceValue = StatusSystem.GetEffectiveStats(owner).Get(effect.HealScalingStat);
                        requested = (int)BigInteger.Min(int.MaxValue,
                            BigInteger.Divide(new BigInteger(Math.Max(0, sourceValue)) * Math.Max(0, effect.HealCoefficientBp) *
                                Math.Max(0, targetStats.RecoveryBp), 100000000));
                    }
                    if (requested > 0)
                    {
                        effectTarget.Health = Math.Min(targetStats.MaxHealth, effectTarget.Health + requested);
                        AddEvent(state, BattleEventKind.HealApplied, owner.Id, effectTarget.Id, cardId, requested, "Card effect.");
                        state.Events[state.Events.Count - 1].HealthAfter = effectTarget.Health;
                    }
                    break;
                case EffectKind.ChangePowerGauge:
                    CharacterPassiveRuntime.ChangePowerGauge(state, owner, effectTarget, effect.PowerGaugeAmount,
                        cardId, cardOrigin: true, isUltimate: isUltimate);
                    break;
                case EffectKind.ModifyCardRank:
                    var rankTeam = state.Team(effectTarget.Side);
                    foreach (var handCard in rankTeam.Hand)
                    {
                        if (handCard == null || handCard.OwnerFighterId != effectTarget.Id || handCard.Kind != CardKind.Skill)
                            continue;
                        var updatedRank = Math.Max(1, Math.Min(3, handCard.Rank + Math.Max(1, effect.Magnitude)));
                        if (updatedRank == handCard.Rank) continue;
                        handCard.Rank = updatedRank;
                        AddEvent(state, BattleEventKind.CardRankChanged, owner.Id, effectTarget.Id, handCard.Id,
                            updatedRank, "Card rank increased.");
                        state.Events[state.Events.Count - 1].Card = handCard.Clone();
                    }
                    break;
            }
            CharacterPassiveRuntime.Refresh(state);
        }

        private static void EmitStatusRemoval(BattleState state, FighterState owner, FighterState target,
            StatusPolarity polarity, bool removeAll, string cardId)
        {
            var removed = StatusSystem.Remove(target, polarity, removeAll);
            if (removed > 0) AddEvent(state, BattleEventKind.StatusRemoved, owner.Id, target.Id, cardId, removed,
                polarity == StatusPolarity.Buff ? "Buffs removed." : "Debuffs cleansed.");
        }

        private static void ApplyLifesteal(BattleState state, FighterState owner, StatBlock stats, int actualHealthLost)
        {
            if (owner == null || stats == null || !owner.IsAlive || owner.Health <= 0 || actualHealthLost <= 0 ||
                stats.LifeStealBp <= 0 || stats.RecoveryBp <= 0) return;

            var numerator = new BigInteger(actualHealthLost) * stats.LifeStealBp * stats.RecoveryBp;
            var requestedHeal = (int)BigInteger.Min(int.MaxValue, BigInteger.Divide(numerator, 100000000));
            if (requestedHeal <= 0) return;

            owner.Health = Math.Min(stats.MaxHealth, owner.Health + requestedHeal);
            AddEvent(state, BattleEventKind.HealApplied, owner.Id, owner.Id, null, requestedHeal, "Lifesteal.");
            state.Events[state.Events.Count - 1].HealthAfter = owner.Health;
        }

        private static void Fizzle(BattleState state, PlannedAction action, string sourceId, string reason) =>
            AddEvent(state, BattleEventKind.ActionFizzled, sourceId, action?.TargetFighterId, action?.CardId, 0, reason);

        private static FighterState RandomValidTarget(BattleTeamState team, DeterministicRandom rng)
        {
            var targets = team.LivingActive();
            return targets.Count == 0 ? null : targets[rng.Next(targets.Count)];
        }

        private static void DrawOpeningHand(BattleState state, BattleTeamState team, DeterministicRandom rng)
        {
            var timeline = new List<BattleEvent>();
            CardRules.DrawOpeningHand(team, rng, timeline: timeline);
            AppendTimeline(state, timeline);
        }

        private static void AppendTimeline(BattleState state, List<BattleEvent> timeline)
        {
            foreach (var item in timeline)
            {
                item.Id = state.Events.Count + 1;
                state.Events.Add(item);
            }
        }

        private static void AddTeam(BattleTeamState team, IReadOnlyList<CharacterDefinition> characters,
            IReadOnlyList<int> tiers, IReadOnlyList<int> health)
        {
            if (characters == null || characters.Count < 1 || characters.Count > 4) throw new ArgumentException("A team must contain one to four fighters.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < characters.Count; i++)
            {
                var definition = characters[i];
                if (definition == null || !ids.Add(definition.Id)) throw new ArgumentException("Team has a missing or duplicate fighter definition.");
                var tier = tiers != null && i < tiers.Count ? tiers[i] : 0;
                if (tier < 0 || tier > 6) throw new ArgumentOutOfRangeException(nameof(tiers), "Constellation tier must be 0-6.");
                var initialHealth = health != null && i < health.Count ? health[i] : definition.BaseStats.MaxHealth;
                var frozen = definition.Clone();
                // Compatibility for older exported starter catalogs and serialization defaults; explicitly authored bundles always win.
                if (frozen.Passive == null || string.IsNullOrWhiteSpace(frozen.Passive.Id))
                    frozen.Passive = StandardCharacterPassives.Create(frozen.Id);
                var passiveErrors = PassiveRuleValidator.Validate(frozen.Passive);
                if (passiveErrors.Count > 0) throw new ArgumentException(string.Join("; ", passiveErrors), nameof(characters));
                team.Fighters.Add(new FighterState { Id = team.Side + ":" + i + ":" + definition.Id, Side = team.Side, Definition = frozen,
                    TeamIndex = i, FormationSlot = Math.Min(i, 2), IsReserve = i >= 3,
                    Health = Math.Max(0, initialHealth), IsAlive = initialHealth > 0, ConstellationTier = tier });
            }
        }

        private static void BeginTurn(BattleState state, TeamSide side, DeterministicRandom rng, bool drawTurnCards = true)
        {
            var team = state.Team(side);
            var activeCount = team.LivingActive().Count;
            BattleEvent reserveEntry = null;
            if (activeCount < 3)
            {
                var reserve = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
                if (reserve != null)
                {
                    var openSlot = FindOpenFormationSlot(team);
                    reserveEntry = DeployReserve(state, side, openSlot);
                    activeCount++;
                }
            }
            if (activeCount == 0) { CheckOutcome(state); return; }
            state.ActingSide = side;
            state.Phase = BattlePhase.TurnStart;
            state.ActionBudget = activeCount > 2 ? 3 : 2;
            AddEvent(state, BattleEventKind.TurnStarted, side.ToString(), null, null, state.ActionBudget, "Turn started.");
            if (reserveEntry != null) AppendTimeline(state, new List<BattleEvent> { reserveEntry });
            CharacterPassiveRuntime.Refresh(state);
            CharacterPassiveRuntime.Notify(state, PassiveEventKind.TeamTurnStarted);
            ApplyStatusTicks(state, team, StatusTickTiming.TargetTurnStart);
            foreach (var fighter in team.Fighters) fighter?.Statuses?.Advance(StatusDurationClock.TargetTurnStart);
            CharacterPassiveRuntime.Refresh(state);
            if (team.LivingActive().Count < 3)
            {
                var replacement = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
                if (replacement != null)
                {
                    var entry = DeployReserve(state, side, FindOpenFormationSlot(team));
                    if (entry != null) AppendTimeline(state, new List<BattleEvent> { entry });
                }
            }
            CharacterPassiveRuntime.Refresh(state);
            CheckOutcome(state);
            if (state.Winner.HasValue || state.IsDraw) return;
            ApplyTurnStartRecovery(state, team);
            if (drawTurnCards)
            {
                var timeline = new List<BattleEvent>();
                CardRules.StartTurn(team, rng, timeline: timeline);
                AppendTimeline(state, timeline);
            }
            // Three active fighters expose three ordered action slots. A reduced formation
            // keeps two slots so a surviving fighter can still form a meaningful turn.
            state.ActionBudget = team.LivingActive().Count > 2 ? 3 : 2;
            state.Phase = BattlePhase.Planning;
        }

        private static void ApplyTurnStartRecovery(BattleState state, BattleTeamState team)
        {
            foreach (var fighter in team.Fighters)
            {
                var stats = StatusSystem.GetEffectiveStats(fighter);
                if (fighter == null || !fighter.IsAlive || fighter.Health <= 0 || stats == null ||
                    stats.RegenerationBp <= 0 || stats.RecoveryBp <= 0) continue;

                var missingHealth = Math.Max(0, stats.MaxHealth - fighter.Health);
                if (missingHealth <= 0) continue;
                var numerator = new BigInteger(missingHealth) * stats.RegenerationBp * stats.RecoveryBp;
                var requestedHeal = (int)BigInteger.Min(int.MaxValue, BigInteger.Divide(numerator, 100000000));
                if (requestedHeal <= 0) continue;

                fighter.Health = Math.Min(stats.MaxHealth, fighter.Health + requestedHeal);
                AddEvent(state, BattleEventKind.HealApplied, fighter.Id, fighter.Id, null, requestedHeal, "Turn-start Recovery.");
                state.Events[state.Events.Count - 1].HealthAfter = fighter.Health;
            }
        }

        private static void EndTurn(BattleState state, DeterministicRandom rng)
        {
            state.Phase = BattlePhase.TurnEnd;
            var actingTeam = state.Team(state.ActingSide);
            ApplyStatusTicks(state, actingTeam, StatusTickTiming.TargetTurnEnd);
            CheckOutcome(state);
            if (state.Winner.HasValue || state.IsDraw) return;
            AddEvent(state, BattleEventKind.TurnEnded, state.ActingSide.ToString(), null, null, 0, "Turn ended.");
            CharacterPassiveRuntime.Notify(state, PassiveEventKind.TeamTurnEnded);
            foreach (var fighter in actingTeam.Fighters)
                if (fighter != null && fighter.IsAlive) StatusSystem.AdvanceOwnerTurn(fighter);
            foreach (var source in actingTeam.Fighters)
            {
                if (source == null) continue;
                foreach (var target in state.Player.Fighters) target?.Statuses?.Advance(StatusDurationClock.SourceTurnEnd, source.Id);
                foreach (var target in state.Opponent.Fighters) target?.Statuses?.Advance(StatusDurationClock.SourceTurnEnd, source.Id);
            }
            CharacterPassiveRuntime.Refresh(state);
            state.CompletedTurnCount++;
            if (state.CompletedTurnCount >= MaximumCompletedTurns)
            {
                state.Phase = BattlePhase.Complete;
                state.IsDraw = true;
                state.ActionBudget = 0;
                AddEvent(state, BattleEventKind.BattleCompleted, null, null, null, 0, "Battle reached the 40-turn limit and ended in a draw.");
                return;
            }
            if (state.ActingSide == TeamSide.Player)
            {
                var timeline = new List<BattleEvent>();
                CardRules.StartTurn(state.Player, rng, timeline: timeline);
                AppendTimeline(state, timeline);
            }
            var next = state.ActingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
            if (state.ActingSide == TeamSide.Player) state.TurnNumber++;
            BeginTurn(state, next, rng, drawTurnCards: next == TeamSide.Opponent);
        }

        private static void ApplyStatusTicks(BattleState state, BattleTeamState team, StatusTickTiming timing)
        {
            if (team?.Fighters == null) return;
            foreach (var target in team.Fighters)
            {
                if (target == null || !target.IsAlive || target.Statuses == null) continue;
                var ticks = target.Statuses.CollectTicks(timing);
                foreach (var tick in ticks)
                {
                    if (!target.IsAlive) break;
                    var source = FindFighter(state, tick.SourceFighterId);
                    var attacker = source == null ? new StatBlock() : StatusSystem.GetEffectiveStats(source);
                    var defender = StatusSystem.GetEffectiveStats(target);
                    var policy = StatusSystem.BuildDamagePolicy(source, target, tick.Family);
                    policy.CannotCrit = true;
                    policy.CannotBlock = true;
                    var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = tick.Amount, Policy = policy },
                        attacker, defender, target.Health, target.Shield, -1, -1);
                    target.Health = damage.RemainingHealth;
                    target.Shield = damage.RemainingShield;
                    AddEvent(state, BattleEventKind.DamageApplied, tick.SourceFighterId, target.Id, null,
                        damage.HealthLost, "Status tick: " + tick.RecipeId);
                    var battleEvent = state.Events[state.Events.Count - 1];
                    battleEvent.StatusInstanceId = tick.StatusInstanceId;
                    battleEvent.StatusRecipeId = tick.RecipeId;
                    battleEvent.HealthAfter = target.Health;
                    battleEvent.ShieldAfter = target.Shield;
                    battleEvent.ShieldLost = damage.ShieldLost;
                    battleEvent.WasEndured = damage.WasEndured;
                    if (tick.ConsumeOnTrigger) target.Statuses.RemoveInstance(tick.StatusInstanceId);
                    if (target.Health <= 0) DefeatFromStatus(state, target, tick);
                }
            }
        }

        private static void DefeatFromStatus(BattleState state, FighterState target, StatusTick tick)
        {
            target.Health = 0;
            target.IsAlive = false;
            target.PowerGauge = 0;
            CardRules.RemoveDeadOwnerCards(state.Team(target.Side), target.Id);
            AddEvent(state, BattleEventKind.FighterDefeated, target.Id, null, null, 0, "Fighter defeated by status.");
            state.Events[state.Events.Count - 1].StatusInstanceId = tick.StatusInstanceId;
            state.Events[state.Events.Count - 1].StatusRecipeId = tick.RecipeId;
            var merged = new List<BattleEvent>();
            CardRules.MergeAdjacent(state.Team(target.Side), null, merged);
            AppendTimeline(state, merged);
            CharacterPassiveRuntime.Refresh(state);
        }

        private static FighterState FindFighter(BattleState state, string fighterId)
        {
            if (string.IsNullOrEmpty(fighterId)) return null;
            return state.Player.FindFighter(fighterId) ?? state.Opponent.FindFighter(fighterId);
        }

        private static int FindOpenFormationSlot(BattleTeamState team)
        {
            for (var slot = 0; slot < 3; slot++)
                if (!team.Fighters.Exists(f => f.IsAlive && !f.IsReserve && f.FormationSlot == slot)) return slot;
            return 0;
        }

        private static BattleEvent DeployReserve(BattleState state, TeamSide side, int openSlot)
        {
            var team = state.Team(side);
            var reserve = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
            if (reserve == null) return null;
            reserve.IsReserve = false;
            reserve.FormationSlot = openSlot;
            return new BattleEvent { Kind = BattleEventKind.ReserveEntered, SourceId = reserve.Id,
                DestinationIndex = openSlot, Message = "Reserve entered the open formation slot." };
        }

        private static void CheckOutcome(BattleState state)
        {
            var playerAlive = state.Player.Fighters.Exists(f => f.IsAlive);
            var opponentAlive = state.Opponent.Fighters.Exists(f => f.IsAlive);
            if (playerAlive && opponentAlive) return;
            state.Phase = BattlePhase.Complete;
            if (!playerAlive && !opponentAlive) state.IsDraw = true;
            else state.Winner = playerAlive ? TeamSide.Player : TeamSide.Opponent;
            AddEvent(state, BattleEventKind.BattleCompleted, null, null, null, 0,
                state.IsDraw ? "Battle ended in a draw." : "Battle completed.");
        }

        private static void AddEvent(BattleState state, BattleEventKind kind, string source, string target, string card, int amount, string message)
        {
            state.Events.Add(new BattleEvent { Id = state.Events.Count + 1, Kind = kind, SourceId = source,
                TargetId = target, CardId = card, Amount = amount, Message = message });
        }
    }
}
