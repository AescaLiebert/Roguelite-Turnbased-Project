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

        private sealed class ActionResolutionContext
        {
            public readonly Dictionary<string, long> HealthDamageBySource = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly Dictionary<string, long> ReflectNumeratorByOwner = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> ReflectSourceByOwner = new Dictionary<string, string>(StringComparer.Ordinal);

            public void Record(FighterState source, FighterState target, int healthLost, int incomingDamage, int reflectDamageBp)
            {
                if (healthLost > 0 && source != null)
                    HealthDamageBySource[source.Id] = Math.Min(long.MaxValue, HealthDamageBySource.GetValueOrDefault(source.Id) + healthLost);
                if (incomingDamage > 0 && target != null && reflectDamageBp > 0)
                {
                    var addition = (long)incomingDamage * reflectDamageBp;
                    ReflectNumeratorByOwner[target.Id] = Math.Min(long.MaxValue,
                        ReflectNumeratorByOwner.GetValueOrDefault(target.Id) + addition);
                    ReflectSourceByOwner[target.Id] = source?.Id;
                }
            }
        }

        public static BattleState Create(string matchId, IReadOnlyList<CharacterDefinition> player,
            IReadOnlyList<int> playerTiers, IReadOnlyList<CharacterDefinition> opponent, IReadOnlyList<int> opponentTiers,
            ulong seed, TeamSide firstSide = TeamSide.Player, IReadOnlyList<RunBoonDefinition> runBoons = null,
            IReadOnlyList<int> playerHealth = null, IReadOnlyList<int> opponentHealth = null,
            BattleModeMask mode = BattleModeMask.PvE, ulong? cardDrawSeed = null, bool training = false)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("A stable match id is required.", nameof(matchId));
            if (mode != BattleModeMask.PvE && mode != BattleModeMask.PvP) throw new ArgumentOutOfRangeException(nameof(mode));
            var state = new BattleState { MatchId = matchId, Revision = 1, TurnNumber = 1, ActingSide = firstSide, Phase = BattlePhase.Setup, Mode = mode };
            AddTeam(state.Player, player, playerTiers, playerHealth);
            AddTeam(state.Opponent, opponent, opponentTiers, opponentHealth);
            if (training)
            {
                if (player.Count != 1 || opponent.Count != 1) throw new ArgumentException("Training requires one fighter on each side.");
                state.IsTraining = true;
                state.Player.TrainingDeck = true;
                state.Player.Fighters[0].CannotDie = true;
                state.Opponent.Fighters[0].CannotDie = true;
                state.Opponent.Fighters[0].PowerGaugeDisabled = true;
            }
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
            var secondSide = firstSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
            var secondTeam = state.Team(secondSide);
            foreach (var fighter in secondTeam.Fighters)
            {
                if (fighter.IsAlive && !fighter.PowerGaugeDisabled)
                {
                    fighter.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, fighter.PowerGauge + 1);
                    AddEvent(state, BattleEventKind.PowerGaugeChanged, null, fighter.Id, null, 1, "Going second starting gauge.");
                    state.Events[state.Events.Count - 1].PowerGaugeAfter = fighter.PowerGauge;
                }
            }
            if (runBoons != null)
                foreach (var boon in runBoons)
                {
                    state.RunBoons.Add(boon.Clone());
                    if (boon.EffectKind == RunBoonEffectKind.StartPowerGauge
                        && firstSide != TeamSide.Player && state.Player.LivingActive().Count > 0)
                    {
                        var first = state.Player.LivingActive()[0];
                        first.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, first.PowerGauge + Math.Max(0, boon.MagnitudePoints));
                        AddEvent(state, BattleEventKind.PowerGaugeChanged, null, first.Id, null, Math.Max(0, boon.MagnitudePoints), "Opening Plan boon.");
                        state.Events[state.Events.Count - 1].PowerGaugeAfter = first.PowerGauge;
                    }
                }
            var rng = new DeterministicRandom(seed);
            var cardRng = new DeterministicRandom(cardDrawSeed ?? seed);
            DrawOpeningHand(state, state.Player, cardRng);
            DrawOpeningHand(state, state.Opponent, cardRng);
            BeginTurn(state, firstSide, cardRng, false);
            state.RngState = rng.State;
            state.RngDrawCount = rng.DrawCount;
            state.CardRngState = cardRng.State;
            state.CardRngDrawCount = cardRng.DrawCount;
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
            var plannedOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var card in team.Hand)
                if (card != null && !string.IsNullOrEmpty(card.Id)) plannedOwners[card.Id] = card.OwnerFighterId;
            var rng = DeterministicRandom.Restore(work.RngState, work.RngDrawCount);
            var cardRng = work.CardRngState == 0 && work.CardRngDrawCount == 0
                ? new DeterministicRandom(BattleEntropy.CreateSeed())
                : DeterministicRandom.Restore(work.CardRngState, work.CardRngDrawCount);
            work.Phase = BattlePhase.Resolving;
            foreach (var action in plan.Actions)
            {
                if (action == null) { error = "Plan contains an empty action."; return false; }
                if (!ApplyAction(work, team, action, rng, plannedOwners, out error)) return false;
                AdvanceActionClock(work);
                if (work.Winner.HasValue || work.IsDraw) break;
            }
            if (!work.Winner.HasValue && !work.IsDraw) EndTurn(work, cardRng);
            work.Revision++;
            work.RngState = rng.State;
            work.RngDrawCount = rng.DrawCount;
            work.CardRngState = cardRng.State;
            work.CardRngDrawCount = cardRng.DrawCount;
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

        private static bool ApplyAction(BattleState state, BattleTeamState team, PlannedAction action, DeterministicRandom rng,
            Dictionary<string, string> plannedOwners, out string error)
        {
            error = null;
            var index = team.Hand.FindIndex(c => c.Id == action.CardId);
            if (index < 0)
            {
                var plannedOwnerId = plannedOwners != null && plannedOwners.TryGetValue(action.CardId ?? string.Empty, out var ownerId)
                    ? ownerId : null;
                var plannedOwner = FindFighter(state, plannedOwnerId);
                var reason = plannedOwner != null && !plannedOwner.IsAlive
                    ? "The card owner was defeated before this action."
                    : "The planned card is no longer available.";
                Fizzle(state, action, plannedOwnerId, reason);
                return true;
            }
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

            EffectDefinition effect;
            var found = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            if (!found) { Fizzle(state, action, owner.Id, "The planned card has no runtime effect definition."); return true; }
            var category = CardRules.GetEffectCategory(card);
            var disabled = StatusSystem.IsCardUseBlocked(owner, category, card.Rank, card.Kind == CardKind.Ultimate,
                effect.Sequence != null && effect.Sequence.Count > 0);
            if (disabled)
            {
                Fizzle(state, action, owner.Id, "Disabled card discarded; gained 1 PG.");
                team.Hand.RemoveAt(index);
                AddEvent(state, BattleEventKind.CardRemoved, owner.Id, owner.Id, card.Id, 0,
                    "Disabled card discarded.");
                state.Events[state.Events.Count - 1].Card = card.Clone();
                if (!owner.PowerGaugeDisabled) owner.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, owner.PowerGauge + 1);
                AddEvent(state, BattleEventKind.PowerGaugeChanged, owner.Id, owner.Id, null, 1,
                    "Discarding a disabled card.");
                state.Events[state.Events.Count - 1].PowerGaugeAfter = owner.PowerGauge;
                var mergedAfterDiscard = new List<BattleEvent>();
                CardRules.MergeAdjacent(team, null, mergedAfterDiscard);
                AppendTimeline(state, mergedAfterDiscard);
                return true;
            }
            if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !state.Team(owner.Side).TrainingDeck)
            { Fizzle(state, action, owner.Id, "Ultimate lost readiness before it resolved."); return true; }
            var targets = ResolveActionTargets(state, owner, card, action.TargetFighterId, rng);
            if (targets.Count == 0) { Fizzle(state, action, owner.Id, "No living active target remained."); return true; }
            var target = targets[0];
            team.Hand.RemoveAt(index);
            if (card.Kind == CardKind.Ultimate) owner.PowerGauge = 0;
            else if (!owner.PowerGaugeDisabled) owner.PowerGauge = Math.Min(CardRules.UltimateGaugeCost, owner.PowerGauge + 1);
            EmitActionTiming(state, owner, card, CardEffectTiming.BeforeAction);
            ApplyCardEffectSteps(state, rng, owner, target, effect, card.Id, CardEffectTiming.BeforeAction, card.Kind == CardKind.Ultimate);
            CharacterPassiveRuntime.Refresh(state);
            AddEvent(state, BattleEventKind.CardPlayed, owner.Id, target.Id, card.Id, card.Rank, "Card played.");
            state.Events[state.Events.Count - 1].Card = card.Clone();
            foreach (var actionTarget in targets) state.Events[state.Events.Count - 1].TargetIds.Add(actionTarget.Id);
            state.Events[state.Events.Count - 1].PowerGaugeAfter = owner.PowerGauge;
            SetAttackFacts(state.Events[state.Events.Count - 1], effect, IsAttack(category));
            EmitActionTiming(state, owner, card, CardEffectTiming.Damaging);
            foreach (var actionTarget in targets)
                ApplyCardEffectSteps(state, rng, owner, actionTarget, effect, card.Id, CardEffectTiming.Damaging,
                    card.Kind == CardKind.Ultimate, includeGroupEffects: actionTarget == targets[0],
                    suppressDebuffStatusEffects: false);
            CharacterPassiveRuntime.Refresh(state);
            var mergedAfterPlay = new List<BattleEvent>();
            CardRules.MergeAdjacent(team, null, mergedAfterPlay);
            AppendTimeline(state, mergedAfterPlay);
            if (effect.Kind != EffectKind.Damage || !IsAttack(category))
            {
                foreach (var utilityTarget in targets)
                    ApplyCardUtilityEffectSingle(state, rng, owner, utilityTarget, effect, card.Id, card.Kind == CardKind.Ultimate);
                EmitActionTiming(state, owner, card, CardEffectTiming.AfterDamage);
                // Legacy support assets authored their utility in AfterDamage. Execute it without a damage packet.
                foreach (var supportTarget in targets)
                {
                    ApplyCardEffectSteps(state, rng, owner, supportTarget, effect, card.Id, CardEffectTiming.AfterDamage,
                        card.Kind == CardKind.Ultimate, includeGroupEffects: supportTarget == targets[0]);
                }
                EmitActionTiming(state, owner, card, CardEffectTiming.AfterAction);
                foreach (var supportTarget in targets)
                    ApplyCardEffectSteps(state, rng, owner, supportTarget, effect, card.Id, CardEffectTiming.AfterAction,
                        card.Kind == CardKind.Ultimate, includeGroupEffects: supportTarget == targets[0]);
                ResolvePendingPassiveEffects(state, card.Id);
                AddEvent(state, BattleEventKind.ActionCompleted, owner.Id, null, card.Id, 0, "Action complete.");
                CheckOutcome(state);
                return true;
            }
            var resolution = new ActionResolutionContext();
            var pendingDefeats = ResolveDamageHits(state, owner, targets, card, effect, rng, resolution);
            EmitActionTiming(state, owner, card, CardEffectTiming.AfterDamage);
            foreach (var actionTarget in targets)
            {
                ApplyCardEffectSteps(state, rng, owner, actionTarget, effect, card.Id, CardEffectTiming.AfterDamage,
                    card.Kind == CardKind.Ultimate, CardDamageTotal(state, card.Id, actionTarget.Id), actionTarget == targets[0],
                    (int)Math.Min(int.MaxValue, resolution.HealthDamageBySource.GetValueOrDefault(owner.Id)),
                    suppressDebuffStatusEffects: true);
            }
            ApplySuccessfulHitDebuffStepsForTargets(state, rng, owner, targets, effect, card,
                CardEffectTiming.AfterDamage, resolution);
            CharacterPassiveRuntime.Refresh(state);
            EmitActionTiming(state, owner, card, CardEffectTiming.AfterAction);
            foreach (var actionTarget in targets)
            {
                ApplyCardEffectSteps(state, rng, owner, actionTarget, effect, card.Id, CardEffectTiming.AfterAction,
                    card.Kind == CardKind.Ultimate, CardDamageTotal(state, card.Id, actionTarget.Id), includeGroupEffects: actionTarget == targets[0],
                    actualHealthDamage: (int)Math.Min(int.MaxValue, resolution.HealthDamageBySource.GetValueOrDefault(owner.Id)),
                    suppressDebuffStatusEffects: true);
            }
            ApplySuccessfulHitDebuffStepsForTargets(state, rng, owner, targets, effect, card,
                CardEffectTiming.AfterAction, resolution);
            ResolvePendingPassiveEffects(state, card.Id);
            AddEvent(state, BattleEventKind.ActionCompleted, owner.Id, null, card.Id, 0, "Action complete.");
            ApplyPendingDefeats(state, pendingDefeats, card.Id, owner, card);
            ResolveActionReflect(state, resolution, card.Id);
            ResolveStanceCounters(state, owner, targets, card, rng);
            ResolveActionLifesteal(state, resolution, card.Id);
            CheckOutcome(state);
            return true;
        }

        private static void EmitActionTiming(BattleState state, FighterState owner, CardState card, CardEffectTiming timing)
        {
            AddEvent(state, BattleEventKind.ActionTiming, owner.Id, null, card.Id, 0, timing.ToString());
            state.Events[state.Events.Count - 1].Timing = timing;
        }

        private static bool IsAttack(CardCategory category) => category == CardCategory.Attack || category == CardCategory.AttackDebuff;

        private static void ResolveStanceCounters(BattleState state, FighterState attacker,
            List<FighterState> defenders, CardState attackingCard, DeterministicRandom rng)
        {
            foreach (var defender in defenders)
            {
                if (!attacker.IsAlive || !defender.IsAlive) continue;
                var counters = new List<StatusInstance>(defender.Statuses.Instances);
                foreach (var stance in counters)
                {
                    if (!attacker.IsAlive || !defender.IsAlive || stance.Recipe?.CounterEnabled != true || stance.Recipe.CounterEffect == null ||
                        !defender.Statuses.Instances.Contains(stance)) continue;
                    var recipe = stance.Recipe;
                    var counterContext = new CardEffectContext
                    {
                        Battle = state,
                        EffectOwner = defender,
                        Actor = attacker,
                        SelectedTarget = defender,
                        RootActionId = attackingCard.Id,
                        CardCategory = CardRules.GetEffectCategory(attackingCard),
                        CardRank = attackingCard.Rank,
                        IsUltimate = attackingCard.Kind == CardKind.Ultimate
                    };
                    if (!CardEffectSystem.ConditionsPass(recipe.CounterConditions, counterContext, attacker)) continue;
                    var counter = new CardState { Id = attackingCard.Id + ":counter:" + stance.InstanceId,
                        OwnerFighterId = defender.Id, Category = recipe.CounterCategory,
                        TargetScope = recipe.CounterTarget, Rank = 1 };
                    var targets = counter.TargetScope == EffectTargetScope.SelectedEnemy
                        ? new List<FighterState> { attacker } : ResolveActionTargets(state, defender, counter, attacker.Id, rng);
                    if (targets.Count == 0) continue;
                    var effect = recipe.CounterEffect.ToRuntimeEffect();
                    var resolution = new ActionResolutionContext();
                    EmitActionTiming(state, defender, counter, CardEffectTiming.BeforeAction);
                    ApplyCardEffectSteps(state, rng, defender, targets[0], effect, counter.Id, CardEffectTiming.BeforeAction);
                    AddEvent(state, BattleEventKind.CounterStarted, defender.Id, targets[0].Id, counter.Id, 0, "COUNTER");
                    var started = state.Events[state.Events.Count - 1];
                    started.Card = counter.Clone();
                    started.TargetIds = targets.ConvertAll(f => f.Id);
                    var counterDealsDamage = IsAttack(counter.Category) && effect.Kind == EffectKind.Damage;
                    SetAttackFacts(started, effect, counterDealsDamage);
                    EmitActionTiming(state, defender, counter, CardEffectTiming.Damaging);
                    foreach (var target in targets)
                        ApplyCardEffectSteps(state, rng, defender, target, effect, counter.Id, CardEffectTiming.Damaging,
                            includeGroupEffects: target == targets[0], suppressDebuffStatusEffects: false);
                    if (counterDealsDamage)
                    {
                        ResolveDamageHits(state, defender, targets, counter, effect, rng, resolution);
                    }
                    else
                    {
                        foreach (var target in targets)
                            ApplyCardUtilityEffectSingle(state, rng, defender, target, effect, counter.Id, false);
                    }
                    EmitActionTiming(state, defender, counter, CardEffectTiming.AfterDamage);
                    foreach (var target in targets)
                    {
                        ApplyCardEffectSteps(state, rng, defender, target, effect, counter.Id, CardEffectTiming.AfterDamage,
                            counter.Kind == CardKind.Ultimate, CardDamageTotal(state, counter.Id, target.Id), includeGroupEffects: target == targets[0],
                            suppressDebuffStatusEffects: counterDealsDamage);
                    }
                    if (counterDealsDamage)
                        ApplySuccessfulHitDebuffStepsForTargets(state, rng, defender, targets, effect, counter,
                            CardEffectTiming.AfterDamage, resolution);
                    EmitActionTiming(state, defender, counter, CardEffectTiming.AfterAction);
                    foreach (var target in targets)
                    {
                        ApplyCardEffectSteps(state, rng, defender, target, effect, counter.Id, CardEffectTiming.AfterAction,
                            counter.Kind == CardKind.Ultimate, CardDamageTotal(state, counter.Id, target.Id), includeGroupEffects: target == targets[0],
                            suppressDebuffStatusEffects: counterDealsDamage);
                    }
                    if (counterDealsDamage)
                        ApplySuccessfulHitDebuffStepsForTargets(state, rng, defender, targets, effect, counter,
                            CardEffectTiming.AfterAction, resolution);
                    ResolvePendingPassiveEffects(state, counter.Id);
                    AddEvent(state, BattleEventKind.CounterEnded, defender.Id, attacker.Id, counter.Id, 0, "Counter complete.");
                    if (IsAttack(counter.Category) && effect.Kind == EffectKind.Damage)
                    {
                        ResolveActionReflect(state, resolution, counter.Id);
                        ResolveActionLifesteal(state, resolution, counter.Id);
                    }
                }
            }
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
                    var taunters = enemy.LivingActive().FindAll(f => f.Statuses.Instances.Exists(s => s.Recipe?.HasTaunt == true));
                    if (taunters.Count > 0 && !taunters.Contains(target)) target = taunters[0];
                    if (target == null || !target.IsAlive || target.IsReserve) target = RandomValidTarget(enemy, rng);
                    if (target != null) result.Add(target);
                    break;
            }
            return result;
        }

        private static void SetAttackFacts(BattleEvent item, EffectDefinition effect, bool isAttack)
        {
            if (!isAttack || effect.Kind != EffectKind.Damage) return;
            item.HitCount = effect.DamageHitCount;
            item.AttackRange = effect.Attack?.Range ?? AttackRange.Close;
        }

        private static List<FighterState> ResolveDamageHits(BattleState state, FighterState owner,
            List<FighterState> targets, CardState card, EffectDefinition effect, DeterministicRandom rng,
            ActionResolutionContext resolution)
        {
            var hitCount = effect.DamageHitCount;
            var pendingDefeats = new List<FighterState>();
            // Hit-major ordering lets every AOE recipient react together, then advances the combo.
            for (var hitIndex = 1; hitIndex <= hitCount; hitIndex++)
            {
                // Keep emitting hit markers after all targets are defeated so presentation can
                // finish the attack animation. The per-target guard below prevents overkill.
                if (!owner.IsAlive) break;
                AddEvent(state, BattleEventKind.HitStarted, owner.Id, targets[0].Id, card.Id, 0, "Hit " + hitIndex + "/" + hitCount);
                var marker = state.Events[state.Events.Count - 1];
                SetAttackFacts(marker, effect, true);
                marker.HitIndex = hitIndex;
                foreach (var target in targets)
                {
                    // A fighter reduced to zero HP remains in the action until its final hit.
                    // Keep it in each hit marker so playback can show the usual FCT and hurt beat.
                    if (!target.IsAlive) continue;
                    marker.TargetIds.Add(target.Id);
                    if (ResolveCardDamage(state, owner, target, card, effect, rng,
                        target == targets[0], resolution, hitIndex, hitCount) && !pendingDefeats.Contains(target))
                        pendingDefeats.Add(target);
                }
            }
            return pendingDefeats;
        }

        private static bool ResolveCardDamage(BattleState state, FighterState owner,
            FighterState target, CardState card, EffectDefinition effect, DeterministicRandom rng,
            bool includeGroupEffects = true, ActionResolutionContext resolution = null, int hitIndex = 1, int hitCount = 1)
        {
            if (target == null || !target.IsAlive) return false;
            if (!EffectConditionsPass(state, owner, target, effect, card?.Id, card?.Kind == CardKind.Ultimate,
                card == null ? CardCategory.Attack : CardRules.GetEffectCategory(card), card?.Rank ?? 0)) return false;
            var attackEffect = FindAttackEffect(effect.KeywordId);
            var calculation = AttackEffectSystem.Prepare(attackEffect, state, owner, target, effect.Family, card);
            if (calculation.RemoveTargetBuffsBeforeDamage)
                EmitStatusRemoval(state, owner, target, StatusPolarity.Buff, true, card.Id);
            if (calculation.RemoveTargetStancesBeforeDamage)
                RemoveStances(state, owner, target, card.Id);
            // Recompute after dispels; removed stance defenses must not survive in cached stats.
            calculation = AttackEffectSystem.Prepare(attackEffect, state, owner, target, effect.Family, card);
            if (target.Statuses.Instances.Exists(s => s.Recipe?.EvadeAttacks == true))
            {
                AddEvent(state, BattleEventKind.AttackEvaded, owner.Id, target.Id, card.Id, 0, "Evade");
                state.Events[state.Events.Count - 1].HitIndex = hitIndex;
                state.Events[state.Events.Count - 1].HitCount = hitCount;
                state.Events[state.Events.Count - 1].AttackRange = effect.Attack?.Range ?? AttackRange.Close;
                return false;
            }
            foreach (var passiveId in calculation.TriggeredPassiveReactionIds)
                AddEvent(state, BattleEventKind.PassiveTriggered, owner.Id, target.Id, card.Id, 0,
                    "Passive Trigger: " + passiveId);
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
                KeywordFactorBp = keywordFactor, Policy = calculation.Policy, HitIndex = hitIndex, HitCount = hitCount };
            var critRoll = -1;
            var blockRoll = -1;
            if (effect.Family != DamageFamily.Destructive)
            {
                critRoll = rng.NextBasisPoints();
                var critChance = Math.Max(0, Math.Min(10000, attacker.CritChanceBp - defender.CritResistanceBp));
                if (calculation.Policy.CannotCrit || critRoll >= critChance) blockRoll = rng.NextBasisPoints();
            }
            var damage = DamageResolver.Resolve(packet, attacker, defender, target.Health, target.Shield, critRoll, blockRoll);
            if (!target.CannotDie) target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            var barrierBroken = ConsumeBarrierShield(target, damage.ShieldLost);
            AddEvent(state, BattleEventKind.DamageApplied, owner.Id, target.Id, card.Id, damage.CalculatedDamage,
                (damage.WasCritical ? "Critical. " : string.Empty) + (damage.WasBlocked ? "Blocked. " : string.Empty));
            state.Events[state.Events.Count - 1].HealthAfter = target.Health;
            state.Events[state.Events.Count - 1].ShieldAfter = target.Shield;
            state.Events[state.Events.Count - 1].WasCritical = damage.WasCritical;
            state.Events[state.Events.Count - 1].WasBlocked = damage.WasBlocked;
            state.Events[state.Events.Count - 1].WasEndured = damage.WasEndured;
            state.Events[state.Events.Count - 1].Affinity = AttributeRules.GetAffinity(owner.Definition?.AttributeId, target.Definition?.AttributeId);
            state.Events[state.Events.Count - 1].ShieldLost = damage.ShieldLost;
            if (barrierBroken)
                state.Events[state.Events.Count - 1].StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
            state.Events[state.Events.Count - 1].HitIndex = hitIndex;
            state.Events[state.Events.Count - 1].HitCount = hitCount;
            state.Events[state.Events.Count - 1].AttackRange = effect.Attack?.Range ?? AttackRange.Close;
            foreach (var status in target.Statuses.Instances)
                if (status.Recipe?.RecoverDamageTakenBp > 0)
                    status.DamageTaken = (int)Math.Min(int.MaxValue, (long)status.DamageTaken + damage.HealthLost);
            resolution?.Record(owner, target, damage.HealthLost, damage.CalculatedDamage,
                Math.Max(0, Math.Min(10000, StatusSystem.GetEffectiveStats(target).ReflectDamageBp)));
            CharacterPassiveRuntime.NotifyDamageResolved(state, owner, target, damage.CalculatedDamage, card.Id,
                cardOrigin: true, isUltimate: card.Kind == CardKind.Ultimate, family: effect.Family,
                category: CardRules.GetEffectCategory(card), cardRank: card.Rank, wasCritical: damage.WasCritical, wasBlocked: damage.WasBlocked);

            var hasSequenceGaugeDrain = effect?.Sequence != null && effect.Sequence.Exists(s => s?.Effect?.Kind == EffectKind.ChangePowerGauge);
            if (hitIndex == hitCount && !hasSequenceGaugeDrain && target.IsAlive && target.Health > 0)
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
            var isDefeated = !target.CannotDie && (damage.Executed || target.Health <= 0);
            if (isDefeated)
            {
                target.Health = 0;
            }
            return isDefeated;
        }

        private static void ApplyPendingDefeats(BattleState state, List<FighterState> pendingDefeats,
            string cardId, FighterState source = null, CardState card = null)
        {
            if (pendingDefeats == null) return;
            var defeatedAny = false;
            foreach (var target in pendingDefeats)
            {
                if (target == null || !target.IsAlive || target.Health > 0 || target.CannotDie) continue;
                target.Health = 0;
                target.IsAlive = false;
                target.PowerGauge = 0;
                ApplyFighterDefeat(state, target, cardId, source, card);
                defeatedAny = true;
            }
            if (defeatedAny) CharacterPassiveRuntime.Refresh(state);
        }

        private static void ApplyFighterDefeat(BattleState state, FighterState target, string cardId,
            FighterState source = null, CardState sourceCard = null)
        {
            var team = state.Team(target.Side);
            var removedCards = team.Hand.FindAll(card => card.OwnerFighterId == target.Id);
            foreach (var card in removedCards) team.Hand.Remove(card);
            AddEvent(state, BattleEventKind.FighterDefeated, target.Id, null, cardId, 0, "Fighter defeated.");
            CharacterPassiveRuntime.NotifyFighterDefeated(state, target, source, cardId,
                cardOrigin: sourceCard != null, isUltimate: sourceCard?.Kind == CardKind.Ultimate,
                category: sourceCard == null ? CardCategory.Attack : CardRules.GetEffectCategory(sourceCard),
                cardRank: sourceCard?.Rank ?? 0);
            foreach (var card in removedCards)
            {
                AddEvent(state, BattleEventKind.CardRemoved, target.Id, target.Id, cardId, 0, "Owner defeated.");
                state.Events[state.Events.Count - 1].CardId = card.Id;
                state.Events[state.Events.Count - 1].Card = card.Clone();
            }
            var mergedAfterDeath = new List<BattleEvent>();
            CardRules.MergeAdjacent(team, null, mergedAfterDeath);
            AppendTimeline(state, mergedAfterDeath);
        }

        private static AttackEffectRecipeDefinition FindAttackEffect(string keywordId)
        {
            if (string.IsNullOrWhiteSpace(keywordId)) return null;
            var raw = keywordId.Trim().ToLowerInvariant();
            if (raw == "remove-stance" || raw == "cancelstance" || raw == "removestance") raw = "attack.cancel-stance";
            var stripped = raw.Replace("-", "").Replace("_", "").Replace(".", "").Replace(" ", "");
            if (stripped.StartsWith("attack")) stripped = stripped.Substring("attack".Length);
            return StandardAttackEffects.Find(item =>
            {
                if (item == null) return false;
                var itemStripped = item.Id.Replace("-", "").Replace("_", "").Replace(".", "").Replace(" ", "").ToLowerInvariant();
                if (itemStripped.StartsWith("attack")) itemStripped = itemStripped.Substring("attack".Length);
                return string.Equals(stripped, itemStripped, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static void AdvanceActionClock(BattleState state)
        {
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var fighter in state.Player.Fighters)
                AdvanceStatuses(state, fighter, StatusDurationClock.ActionEnd, null, previousMaxHealth);
            foreach (var fighter in state.Opponent.Fighters)
                AdvanceStatuses(state, fighter, StatusDurationClock.ActionEnd, null, previousMaxHealth);
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
        }

        private static void ApplyCardEffectSteps(BattleState state, DeterministicRandom rng, FighterState owner, FighterState target,
            EffectDefinition rootEffect, string cardId, CardEffectTiming timing, bool isUltimate = false, int triggeringDamage = 0,
            bool includeGroupEffects = true, int actualHealthDamage = 0,
            bool suppressDebuffStatusEffects = false, bool onlyDebuffStatusEffects = false)
        {
            if (rootEffect?.Sequence == null) return;
            foreach (var step in rootEffect.Sequence)
            {
                if (step?.Effect == null || step.Timing != timing || step.Effect.Kind == EffectKind.Damage) continue;
                var isDebuffStatus = IsDebuffStatusEffect(step.Effect);
                if ((suppressDebuffStatusEffects && isDebuffStatus) || (onlyDebuffStatusEffects && !isDebuffStatus)) continue;
                if (!includeGroupEffects && (step.Effect.Target == EffectTargetScope.AllAllies ||
                    step.Effect.Target == EffectTargetScope.Self || step.Effect.Target == EffectTargetScope.AllEnemies)) continue;
                var resolvedEffect = step.Effect.ToEffectDefinition();
                ApplyCardUtilityEffect(state, rng, owner, target, resolvedEffect, cardId, isUltimate, triggeringDamage,
                    actualHealthDamage);
            }
        }

        private static bool IsDebuffStatusEffect(EffectOperationDefinition effect) =>
            effect?.Kind == EffectKind.ApplyStatus && effect.StatusRecipe?.Polarity == StatusPolarity.Debuff;

        private static BattleEvent FindCardDamageEvent(BattleState state, string cardId, string targetId) =>
            state.Events.FindLast(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == cardId && e.TargetId == targetId);

        private static int CardDamageTotal(BattleState state, string cardId, string targetId)
        {
            long total = 0;
            for (var i = state.Events.Count - 1; i >= 0; i--)
            {
                var item = state.Events[i];
                if (item.CardId != cardId) continue;
                if (item.Kind == BattleEventKind.CardPlayed || item.Kind == BattleEventKind.CounterStarted) break;
                if (item.Kind == BattleEventKind.DamageApplied && item.TargetId == targetId) total += item.Amount;
            }
            return (int)Math.Min(int.MaxValue, total);
        }

        private static void ApplySuccessfulHitDebuffSteps(BattleState state, DeterministicRandom rng, FighterState owner,
            FighterState target, EffectDefinition effect, CardState card, CardEffectTiming timing, int triggeringDamage,
            int actualHealthDamage, bool includeGroupEffects)
        {
            ApplyCardEffectSteps(state, rng, owner, target, effect, card.Id, timing, card.Kind == CardKind.Ultimate,
                triggeringDamage, includeGroupEffects, actualHealthDamage, onlyDebuffStatusEffects: true);
        }

        private static void ApplySuccessfulHitDebuffStepsForTargets(BattleState state, DeterministicRandom rng,
            FighterState owner, List<FighterState> targets, EffectDefinition effect, CardState card,
            CardEffectTiming timing, ActionResolutionContext resolution)
        {
            var firstSuccess = true;
            foreach (var target in targets)
            {
                var hit = FindCardDamageEvent(state, card.Id, target.Id);
                var total = CardDamageTotal(state, card.Id, target.Id);
                if (hit != null && total == 0) continue;
                ApplySuccessfulHitDebuffSteps(state, rng, owner, target, effect, card, timing,
                    total,
                    (int)Math.Min(int.MaxValue, resolution.HealthDamageBySource.GetValueOrDefault(owner.Id)),
                    includeGroupEffects: firstSuccess);
                firstSuccess = false;
            }
        }

        private static void ApplyCardUtilityEffect(BattleState state, DeterministicRandom rng, FighterState owner, FighterState target,
            EffectDefinition effect, string cardId, bool isUltimate = false, int triggeringDamage = 0, int actualHealthDamage = 0)
        {
            var targets = ResolveUtilityTargets(state, owner, target, effect.Target);
            foreach (var effectTarget in targets)
                ApplyCardUtilityEffectSingle(state, rng, owner, effectTarget, effect, cardId, isUltimate, triggeringDamage,
                    actualHealthDamage);
        }

        private static List<FighterState> ResolveUtilityTargets(BattleState state, FighterState owner,
            FighterState selectedTarget, EffectTargetScope targetScope)
        {
            var result = new List<FighterState>();
            switch (targetScope)
            {
                case EffectTargetScope.Self:
                    if (owner != null && owner.IsAlive && owner.Health > 0) result.Add(owner);
                    break;
                case EffectTargetScope.SelectedAlly:
                    if (selectedTarget != null && selectedTarget.Side == owner?.Side && selectedTarget.IsAlive && selectedTarget.Health > 0)
                        result.Add(selectedTarget);
                    else if (owner != null && owner.IsAlive && owner.Health > 0)
                        result.Add(owner);
                    break;
                case EffectTargetScope.AllAllies:
                    if (owner != null) result.AddRange(state.Team(owner.Side).LivingActive());
                    break;
                case EffectTargetScope.AllEnemies:
                    if (owner != null) result.AddRange(state.OtherTeam(owner.Side).LivingActive());
                    break;
                case EffectTargetScope.SelectedEnemy:
                default:
                    if (selectedTarget != null && selectedTarget.IsAlive && selectedTarget.Health > 0)
                        result.Add(selectedTarget);
                    break;
            }
            return result;
        }

        private static void ApplyCardUtilityEffectSingle(BattleState state, DeterministicRandom rng, FighterState owner, FighterState effectTarget,
            EffectDefinition effect, string cardId, bool isUltimate, int triggeringDamage = 0, int actualHealthDamage = 0)
        {
            if (effectTarget == null || !effectTarget.IsAlive || effectTarget.Health <= 0) return;
            if (!EffectConditionsPass(state, owner, effectTarget, effect, cardId, isUltimate)) return;
            var targetStats = StatusSystem.GetEffectiveStats(effectTarget);
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            if (effect.Kind == EffectKind.ApplyStatus || effect.Kind == EffectKind.Cleanse ||
                effect.Kind == EffectKind.RemoveDebuffs || effect.Kind == EffectKind.RemoveStance ||
                effect.Kind == EffectKind.RemoveBuffs)
                previousMaxHealth[effectTarget.Id] = targetStats.MaxHealth;
            switch (effect.Kind)
            {
                case EffectKind.ApplyStatus:
                    var statusInstanceId = state.MatchId + ":status:" + (state.Events.Count + 1);
                    var ownerStats = owner != null ? StatusSystem.GetEffectiveStats(owner) : new StatBlock();
                    var appliedRecipe = effect.StatusRecipe;
                    if (appliedRecipe != null && (appliedRecipe.Behavior & StatusBehavior.Stance) == 0 &&
                        (appliedRecipe.DurationClock == StatusDurationClock.TargetTurnStart ||
                         appliedRecipe.DurationClock == StatusDurationClock.TargetTurnEnd))
                    {
                        appliedRecipe = appliedRecipe.Clone();
                        appliedRecipe.DurationClock = effectTarget.Side == owner.Side
                            ? StatusDurationClock.TargetTurnStart
                            : StatusDurationClock.TargetTurnEnd;
                    }
                    var resolvedTriggeringDamage = triggeringDamage;
                    if (effectTarget != null && !string.IsNullOrEmpty(cardId))
                    {
                        var targetDamage = 0;
                        for (int i = 0; i < state.Events.Count; i++)
                        {
                            var e = state.Events[i];
                            if (e.Kind == BattleEventKind.DamageApplied && e.CardId == cardId && e.TargetId == effectTarget.Id)
                            {
                                targetDamage += e.Amount;
                            }
                        }
                        if (targetDamage > 0)
                        {
                            resolvedTriggeringDamage = targetDamage;
                        }
                    }
                    var snapshot = new StatusSnapshot
                    {
                        SourceAttack = ownerStats.Attack,
                        TargetMaxHealth = targetStats.MaxHealth,
                        TriggeringHealthDamage = resolvedTriggeringDamage > 0 ? resolvedTriggeringDamage : ownerStats.Attack,
                        FixedAmount = effect.Magnitude
                    };
                    var isDebuff = appliedRecipe != null && appliedRecipe.Polarity == StatusPolarity.Debuff;
                    var hasEvade = effectTarget.Statuses.Instances.Exists(s => s.Recipe?.EvadeAttacks == true);
                    if (isDebuff && hasEvade)
                    {
                        var alreadyEvaded = state.Events.Exists(e => e.Kind == BattleEventKind.AttackEvaded && e.CardId == cardId && e.TargetId == effectTarget.Id);
                        if (!alreadyEvaded)
                        {
                            AddEvent(state, BattleEventKind.AttackEvaded, owner?.Id, effectTarget.Id, cardId, 0, "Evade");
                        }
                        break;
                    }
                    var applyChanceBp = ResolveStatusApplyChanceBp(effect.StatusApplyChanceBp, ownerStats.ControlBp,
                        targetStats.AvoidanceBp, isDebuff);
                    if (applyChanceBp < 10000 && (applyChanceBp <= 0 || rng == null || rng.NextBasisPoints() >= applyChanceBp))
                        break;

                    var applyResult = appliedRecipe != null
                        ? StatusSystem.Apply(effectTarget, owner?.Id, owner?.Side ?? TeamSide.Player, appliedRecipe, statusInstanceId,
                            cardId, state.Events.Count + 1, snapshot: snapshot, potencyBp: effect.StatusPotencyBp,
                            stackCount: Math.Max(1, effect.StatusStackCount),
                            duration: effect.StatusDurationOverride,
                            skipNextDurationClock: (effect.StatusRecipe.Behavior & StatusBehavior.Stance) != 0 &&
                                effect.StatusRecipe.DurationClock == StatusDurationClock.TargetTurnEnd && effectTarget.Side == state.ActingSide)
                        : null;
                    if (applyResult != null && applyResult.Accepted)
                    {
                        var barrier = GrantBarrierShield(effectTarget, appliedRecipe, applyResult.Instance);
                        var recipeId = effect.StatusRecipe.Id;
                        AddEvent(state, BattleEventKind.StatusApplied, owner?.Id, effectTarget.Id, cardId, Math.Max(1, effect.StatusStackCount),
                            recipeId);
                        var statusEvt = state.Events[state.Events.Count - 1];
                        statusEvt.StatusInstanceId = statusInstanceId;
                        statusEvt.StatusRecipeId = recipeId;
                        statusEvt.StatusOutcome = applyResult.Outcome;
                        statusEvt.StatusesAfter = effectTarget.Statuses.Instances.ConvertAll(s => s.Clone());
                        if (barrier > 0)
                        {
                            statusEvt.ShieldAfter = effectTarget.Shield;
                            statusEvt.ShieldChanged = true;
                        }
                    }
                    else if (applyResult?.Outcome == StatusApplyOutcome.IgnoredWeaker)
                    {
                        AddEvent(state, BattleEventKind.StatusWeaker, owner?.Id, effectTarget.Id, cardId, 0, null);
                        var weakerEvent = state.Events[state.Events.Count - 1];
                        weakerEvent.StatusRecipeId = effect.StatusRecipe?.Id;
                        weakerEvent.StatusOutcome = applyResult.Outcome;
                    }
                    else if (isDebuff)
                    {
                        var alreadyImmuned = state.Events.Exists(e => e.Kind == BattleEventKind.StatusImmuned && e.CardId == cardId && e.TargetId == effectTarget.Id);
                        if (!alreadyImmuned)
                        {
                            AddEvent(state, BattleEventKind.StatusImmuned, owner?.Id, effectTarget.Id, cardId, 0, "Immunity");
                        }
                    }
                    break;
                case EffectKind.Cleanse:
                case EffectKind.RemoveDebuffs:
                    EmitStatusRemoval(state, owner, effectTarget, StatusPolarity.Debuff, true, cardId);
                    break;
                case EffectKind.RemoveStance:
                    RemoveStances(state, owner, effectTarget, cardId);
                    break;
                case EffectKind.RemoveBuffs:
                    EmitStatusRemoval(state, owner, effectTarget, StatusPolarity.Buff, true, cardId);
                    break;
                case EffectKind.Heal:
                    // targetStats is already computed at method scope
                    var requested = 0;
                    if (effect.HealValue != null)
                    {
                        var healContext = new CardEffectContext { Battle = state, EffectOwner = owner, Actor = owner,
                            SelectedTarget = effectTarget, ActualHealthDamage = actualHealthDamage };
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
                    TryApplyRecovery(state, owner, effectTarget, requested, targetStats.MaxHealth, cardId, "Card effect.");
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
                        var rankChange = effect.Magnitude == 0 ? 1 : effect.Magnitude;
                        var updatedRank = Math.Max(1, Math.Min(3, handCard.Rank + rankChange));
                        if (updatedRank == handCard.Rank) continue;
                        var oldRank = handCard.Rank;
                        handCard.Rank = updatedRank;
                        CardRules.RefreshCardKind(handCard, effectTarget.Definition);
                        AddEvent(state, BattleEventKind.CardRankChanged, owner.Id, effectTarget.Id, handCard.Id,
                            updatedRank, updatedRank > oldRank ? "Card rank increased." : "Card rank decreased.");
                        state.Events[state.Events.Count - 1].Card = handCard.Clone();
                    }
                    break;
            }
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
        }

        private static bool EffectConditionsPass(BattleState state, FighterState owner, FighterState target,
            EffectDefinition effect, string rootActionId, bool isUltimate, CardCategory category = CardCategory.Attack,
            int cardRank = 0)
        {
            if (effect?.Conditions == null || effect.Conditions.Count == 0) return true;
            var context = new CardEffectContext
            {
                Battle = state,
                EffectOwner = owner,
                Actor = owner,
                SelectedTarget = target,
                RootActionId = rootActionId,
                CardCategory = category,
                CardRank = cardRank,
                IsUltimate = isUltimate
            };
            return CardEffectSystem.ConditionsPass(effect.Conditions, context, target);
        }

        /// <summary>Resolves supported effect operations emitted by passive reactions against their authored targets.</summary>
        internal static void ExecutePassiveOperation(BattleState state, FighterState effectOwner,
            CardEffectContext triggerContext, CardEffectOperationDefinition operation, string rootActionId,
            string reactionId = null)
        {
            if (state == null || effectOwner == null || triggerContext == null || operation == null) return;
            rootActionId = rootActionId ?? triggerContext.RootActionId;
            if (operation.Window == CardEffectWindow.AfterAction && !string.IsNullOrEmpty(rootActionId))
            {
                if (state.PendingPassiveEffects == null) state.PendingPassiveEffects = new List<PendingPassiveEffect>();
                var pending = new PendingPassiveEffect {
                    EffectOwnerId = effectOwner.Id, ActorId = triggerContext.Actor?.Id,
                    SelectedTargetId = triggerContext.SelectedTarget?.Id, RootActionId = rootActionId,
                    CardCategory = triggerContext.CardCategory, CardRank = triggerContext.CardRank,
                    IsUltimate = triggerContext.IsUltimate, DamageFamily = triggerContext.DamageFamily,
                    WasCritical = triggerContext.WasCritical, WasBlocked = triggerContext.WasBlocked,
                    Operation = operation.Clone() };
                var exists = state.PendingPassiveEffects.Exists(item => item != null &&
                    item.RootActionId == pending.RootActionId && item.EffectOwnerId == pending.EffectOwnerId &&
                    item.ActorId == pending.ActorId && item.SelectedTargetId == pending.SelectedTargetId &&
                    item.Operation?.Id == operation.Id && item.ReactionId == reactionId);
                if (!exists)
                {
                    pending.ReactionId = reactionId;
                    state.PendingPassiveEffects.Add(pending);
                }
                return;
            }
            ExecutePassiveOperationNow(state, effectOwner, triggerContext, operation, rootActionId);
        }

        private static void ResolvePendingPassiveEffects(BattleState state, string rootActionId)
        {
            if (state?.PendingPassiveEffects == null || string.IsNullOrEmpty(rootActionId)) return;
            var processed = 0;
            while (true)
            {
                var index = state.PendingPassiveEffects.FindIndex(item => item != null && item.RootActionId == rootActionId);
                if (index < 0) break;
                if (++processed > 256)
                    throw new InvalidOperationException("Deferred passive effect chain exceeds the deterministic event budget.");
                var pending = state.PendingPassiveEffects[index];
                state.PendingPassiveEffects.RemoveAt(index);
                var effectOwner = FindFighter(state, pending.EffectOwnerId);
                if (effectOwner == null || !effectOwner.IsAlive || pending.Operation == null) continue;
                var context = new CardEffectContext {
                    Battle = state, EffectOwner = effectOwner, Actor = FindFighter(state, pending.ActorId) ?? effectOwner,
                    SelectedTarget = FindFighter(state, pending.SelectedTargetId) ?? effectOwner,
                    RootActionId = pending.RootActionId, CardCategory = pending.CardCategory,
                    CardRank = pending.CardRank, IsUltimate = pending.IsUltimate,
                    DamageFamily = pending.DamageFamily, WasCritical = pending.WasCritical,
                    WasBlocked = pending.WasBlocked };
                ExecutePassiveOperationNow(state, effectOwner, context, pending.Operation, pending.RootActionId);
            }
        }

        private static void ExecutePassiveOperationNow(BattleState state, FighterState effectOwner,
            CardEffectContext triggerContext, CardEffectOperationDefinition operation, string rootActionId)
        {
            var recipe = new CardEffectRecipeDefinition { Id = "passive:" + operation.Id };
            recipe.Operations.Add(operation);
            var resolved = CardEffectSystem.ResolveWindow(recipe, operation.Window, triggerContext);
            foreach (var item in resolved)
            {
                var target = item.Target;
                if (target == null || !target.IsAlive || target.Health <= 0) continue;
                var effect = new EffectDefinition { Target = EffectTargetScope.Self };
                switch (item.Operation.Kind)
                {
                    case CardEffectOperationKind.TransferStats:
                        ApplyPassiveStatTransfer(state, effectOwner, target, item.Operation.Magnitude,
                            item.Operation.StatusDurationOverride, rootActionId);
                        continue;
                    case CardEffectOperationKind.Damage:
                        ApplyPassiveDamageOperation(state, effectOwner, target, item.Operation.Damage, rootActionId);
                        continue;
                    case CardEffectOperationKind.ApplyStatus:
                        effect.Kind = EffectKind.ApplyStatus;
                        effect.StatusRecipe = item.Operation.Status?.InlineRecipe?.Clone();
                        if (effect.StatusRecipe == null && !string.IsNullOrWhiteSpace(item.Operation.Status?.StatusRecipeId))
                            effect.StatusRecipe = StandardEffectDatabase.CreateStatusRecipes()
                                .Find(status => status?.Id == item.Operation.Status.StatusRecipeId)?.Clone();
                        if (effect.StatusRecipe == null) continue;
                        effect.StatusApplyChanceBp = item.Operation.Status.ProcChanceBp;
                        effect.StatusDurationOverride = item.Operation.Status.DurationOverride;
                        effect.StatusStackCount = item.Operation.Status.StackCount;
                        effect.StatusPotencyBp = ResolvePassiveStatusPotency(
                            item.Operation.Status, triggerContext, target);
                        break;
                    case CardEffectOperationKind.Cleanse:
                        effect.Kind = EffectKind.Cleanse;
                        break;
                    case CardEffectOperationKind.RemoveStatus:
                        effect.Kind = item.Operation.RemovePolarity == StatusPolarity.Buff
                            ? EffectKind.RemoveBuffs : EffectKind.RemoveDebuffs;
                        break;
                    case CardEffectOperationKind.Heal:
                        effect.Kind = EffectKind.Heal;
                        effect.HealValue = item.Operation.Value?.Clone();
                        break;
                    case CardEffectOperationKind.ChangePowerGauge:
                        effect.Kind = EffectKind.ChangePowerGauge;
                        effect.PowerGaugeAmount = item.Operation.Magnitude;
                        break;
                    case CardEffectOperationKind.ModifyCardRank:
                        effect.Kind = EffectKind.ModifyCardRank;
                        effect.Magnitude = item.Operation.Magnitude;
                        break;
                    default:
                        continue;
                }
                ApplyCardUtilityEffectSingle(state, null, effectOwner, target, effect, rootActionId,
                    isUltimate: triggerContext.IsUltimate);
            }
        }

        private static void ApplyPassiveStatTransfer(BattleState state, FighterState source,
            FighterState target, int percentBp, int duration, string rootActionId)
        {
            if (state == null || source == null || target == null || source == target ||
                !source.IsAlive || !target.IsAlive || target.Health <= 0 || percentBp <= 0) return;
            var targetStats = StatusSystem.GetEffectiveStats(target);
            var attackAmount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, targetStats.Attack) * percentBp / 10000);
            var defenseAmount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, targetStats.Defense) * percentBp / 10000);
            var appliedDuration = duration > 0 ? duration : 2;

            var targetRecipe = CreateTransferStatus("status.debuff.iori95.extort", StatusPolarity.Debuff,
                attackAmount, defenseAmount);
            targetRecipe.DurationClock = StatusDurationClock.TargetTurnEnd;
            var targetResult = ApplyPassiveTransferStatus(state, source, target, targetRecipe,
                appliedDuration, rootActionId);
            if (targetResult == null || !targetResult.Accepted) return;

            var sourceRecipe = CreateTransferStatus("status.buff.iori95.extort", StatusPolarity.Buff,
                attackAmount, defenseAmount);
            sourceRecipe.DurationClock = StatusDurationClock.TargetTurnStart;
            ApplyPassiveTransferStatus(state, source, source, sourceRecipe, appliedDuration, rootActionId);
            CharacterPassiveRuntime.Refresh(state);
        }

        private static StatusRecipeDefinition CreateTransferStatus(string id, StatusPolarity polarity,
            int attackAmount, int defenseAmount)
        {
            var recipe = new StatusRecipeDefinition
            {
                Id = id,
                NameKey = id,
                Polarity = polarity,
                Behavior = StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.RefreshDuration,
                DurationClock = StatusDurationClock.TargetTurnEnd,
                DefaultDuration = 2,
                MaxStacks = 1,
                Tags = new List<string> { "status.extort" }
            };
            if (attackAmount > 0) recipe.Modifiers.Add(new StatModifierDefinition
            {
                Target = ModifierTarget.Stat,
                Stat = StatId.Attack,
                Operation = ModifierOperation.Flat,
                Amount = polarity == StatusPolarity.Debuff ? -attackAmount : attackAmount
            });
            if (defenseAmount > 0) recipe.Modifiers.Add(new StatModifierDefinition
            {
                Target = ModifierTarget.Stat,
                Stat = StatId.Defense,
                Operation = ModifierOperation.Flat,
                Amount = polarity == StatusPolarity.Debuff ? -defenseAmount : defenseAmount
            });
            return recipe;
        }

        private static StatusApplyResult ApplyPassiveTransferStatus(BattleState state, FighterState source,
            FighterState target, StatusRecipeDefinition recipe, int duration, string rootActionId)
        {
            var instanceId = state.MatchId + ":status:" + (state.Events.Count + 1);
            var result = StatusSystem.Apply(target, source.Id, source.Side, recipe, instanceId,
                rootActionId, state.Events.Count + 1, duration: duration);
            if (!result.Accepted) return result;
            AddEvent(state, BattleEventKind.StatusApplied, source.Id, target.Id, rootActionId, 1, recipe.Id);
            var statusEvent = state.Events[state.Events.Count - 1];
            statusEvent.StatusInstanceId = instanceId;
            statusEvent.StatusRecipeId = recipe.Id;
            statusEvent.StatusOutcome = result.Outcome;
            statusEvent.StatusesAfter = target.Statuses.Instances.ConvertAll(status => status.Clone());
            return result;
        }

        private static void ApplyPassiveDamageOperation(BattleState state, FighterState source,
            FighterState target, DamageEffectRecipe recipe, string rootActionId)
        {
            if (state == null || source == null || target == null || recipe == null ||
                !source.IsAlive || !target.IsAlive || target.Health <= 0) return;

            var calculation = AttackEffectSystem.Prepare(null, state, source, target, recipe.Family);
            var attacker = calculation.Attacker;
            var defender = calculation.Defender;
            var baseAmount = recipe.ScaleFromTargetMaxHealth
                ? defender.MaxHealth
                : recipe.Scaling switch
                {
                    StatScaling.Attack => attacker.Attack,
                    StatScaling.Defense => attacker.Defense,
                    StatScaling.MaxHealth => attacker.MaxHealth,
                    StatScaling.SpecificStat => attacker.Get(recipe.ScalingStat),
                    _ => recipe.FixedAmount
                };
            var policy = calculation.Policy;
            policy.CannotCrit |= recipe.CannotCrit;
            policy.CannotBlock |= recipe.CannotBlock;
            policy.BypassDefense |= recipe.ScaleFromTargetMaxHealth;
            if (recipe.Family == DamageFamily.Additional)
            {
                policy.FamilyDealtIncreaseBp += policy.OutgoingIncreaseBp;
                policy.FamilyDealtDecreaseBp += policy.OutgoingDecreaseBp;
            }
            var keywordFactor = (int)Math.Min(int.MaxValue,
                (long)Math.Max(0, recipe.KeywordFactorBp) * calculation.KeywordFactorBp / 10000);
            var packet = new DamagePacket
            {
                BaseAmount = Math.Max(0, baseAmount),
                CoefficientBp = Math.Max(0, recipe.CoefficientBp),
                KeywordFactorBp = keywordFactor,
                Policy = policy
            };
            var damage = DamageResolver.Resolve(packet, attacker, defender, target.Health, target.Shield, -1, -1);
            if (!target.CannotDie) target.Health = damage.RemainingHealth;
            target.Shield = damage.RemainingShield;
            var barrierBroken = ConsumeBarrierShield(target, damage.ShieldLost);
            AddEvent(state, BattleEventKind.DamageApplied, source.Id, target.Id, rootActionId,
                damage.CalculatedDamage, "Passive additional damage.");
            var damageEvent = state.Events[state.Events.Count - 1];
            damageEvent.HealthAfter = target.Health;
            damageEvent.ShieldAfter = target.Shield;
            damageEvent.ShieldLost = damage.ShieldLost;
            if (barrierBroken)
                damageEvent.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
            damageEvent.WasEndured = damage.WasEndured;
            damageEvent.Affinity = AttributeRules.GetAffinity(source.Definition?.AttributeId,
                target.Definition?.AttributeId);
            CharacterPassiveRuntime.NotifyDamageResolved(state, source, target, damage.CalculatedDamage,
                rootActionId, cardOrigin: false, family: recipe.Family);

            if (!target.CannotDie && target.Health <= 0 && target.IsAlive)
            {
                target.Health = 0;
                target.IsAlive = false;
                target.PowerGauge = 0;
                ApplyFighterDefeat(state, target, rootActionId, source);
            }
        }

        private static int ResolvePassiveStatusPotency(StatusApplicationRecipe application,
            CardEffectContext context, FighterState operationTarget)
        {
            if (application == null || application.PotencySource == StatusPotencySource.Authored)
                return application?.PotencyBp ?? 0;

            var source = application.PotencySource switch
            {
                StatusPotencySource.EffectOwnerStatus => context?.EffectOwner,
                StatusPotencySource.TriggerActorStatus => context?.Actor,
                StatusPotencySource.TriggerTargetStatus => context?.SelectedTarget,
                StatusPotencySource.OperationTargetStatus => operationTarget,
                _ => null
            };
            var recipeId = string.IsNullOrWhiteSpace(application.PotencySourceRecipeId)
                ? application.StatusRecipeId
                : application.PotencySourceRecipeId;
            if (source?.Statuses?.Instances == null || string.IsNullOrWhiteSpace(recipeId))
                return application.PotencyBp;

            var resolved = application.PotencyBp;
            foreach (var status in source.Statuses.Instances)
                if (status != null && string.Equals(status.RecipeId, recipeId, StringComparison.Ordinal))
                    resolved = Math.Max(resolved, status.PotencyBp);
            return resolved;
        }

        private static int ResolveStatusApplyChanceBp(int authoredChanceBp, int controlBp, int avoidanceBp, bool targetIsDebuff)
        {
            var baseChance = Math.Max(0, Math.Min(10000, authoredChanceBp));
            if (!targetIsDebuff) return baseChance;
            if (controlBp <= 0) return 0;
            // Avoidance is a divisor: 100% is neutral and lower values raise the chance.
            // Zero means no avoidance, so keep the neutral divisor instead of guaranteeing application.
            var effectiveAvoidanceBp = avoidanceBp > 0 ? avoidanceBp : 10000;
            var chance = new BigInteger(baseChance) * controlBp / effectiveAvoidanceBp;
            return (int)BigInteger.Min(10000, BigInteger.Max(0, chance));
        }

        private static void RemoveStances(BattleState state, FighterState owner, FighterState target, string cardId)
        {
            var statuses = new List<StatusInstance>(target.Statuses.Instances);
            foreach (var status in statuses)
                if (status.Recipe != null && ((status.Recipe.Behavior & StatusBehavior.Stance) != 0 ||
                    status.Recipe.Tags.Contains(CombatTags.Stance)))
                    target.Statuses.RemoveInstance(status.InstanceId);
            if (statuses.Count == target.Statuses.Instances.Count) return;
            AddEvent(state, BattleEventKind.StatusRemoved, owner.Id, target.Id, cardId, 0, "Stance removed.");
            state.Events[state.Events.Count - 1].StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
        }

        private static void EmitStatusRemoval(BattleState state, FighterState owner, FighterState target,
            StatusPolarity polarity, bool removeAll, string cardId)
        {
            var previous = new List<StatusInstance>(target.Statuses.Instances);
            var removed = StatusSystem.Remove(target, polarity, removeAll);
            var removedBarrierShield = RemoveExpiredBarrierShield(target, previous);
            if (removed > 0) AddEvent(state, BattleEventKind.StatusRemoved, owner.Id, target.Id, cardId, removed,
                polarity == StatusPolarity.Buff ? "Buffs removed." : "Debuffs cleansed.");
            if (removed > 0)
            {
                var statusEvent = state.Events[state.Events.Count - 1];
                statusEvent.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
                if (removedBarrierShield > 0)
                {
                    statusEvent.ShieldAfter = target.Shield;
                    statusEvent.ShieldChanged = true;
                }
            }
        }

        private static int GrantBarrierShield(FighterState target, StatusRecipeDefinition recipe,
            StatusInstance instance)
        {
            if (target == null || recipe == null || instance == null ||
                (recipe.Behavior & StatusBehavior.Barrier) == 0 || recipe.BarrierCoefficientBp <= 0)
                return 0;
            var amount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, instance.Snapshot?.SourceAttack ?? 0) *
                recipe.BarrierCoefficientBp / 10000);
            if (amount <= 0) return 0;
            instance.ShieldRemaining = (int)Math.Min(int.MaxValue, (long)instance.ShieldRemaining + amount);
            target.Shield = (int)Math.Min(int.MaxValue, (long)Math.Max(0, target.Shield) + amount);
            return amount;
        }

        private static bool ConsumeBarrierShield(FighterState target, int amount)
        {
            var remaining = Math.Max(0, amount);
            if (target?.Statuses?.Instances == null || remaining == 0) return false;
            var depletedBarrierIds = new List<string>();
            foreach (var status in target.Statuses.Instances)
            {
                if (status == null || status.ShieldRemaining <= 0) continue;
                var consumed = Math.Min(status.ShieldRemaining, remaining);
                status.ShieldRemaining -= consumed;
                remaining -= consumed;
                if (consumed > 0 && status.ShieldRemaining == 0 && status.Recipe != null &&
                    (status.Recipe.Behavior & StatusBehavior.Barrier) != 0)
                    depletedBarrierIds.Add(status.InstanceId);
                if (remaining == 0) break;
            }
            foreach (var instanceId in depletedBarrierIds)
                target.Statuses.RemoveInstance(instanceId);
            return depletedBarrierIds.Count > 0;
        }

        private static int RemoveExpiredBarrierShield(FighterState target, List<StatusInstance> previous)
        {
            if (target == null || previous == null) return 0;
            long removedShield = 0;
            foreach (var status in previous)
            {
                if (status?.ShieldRemaining <= 0 || target.Statuses.Instances.Exists(active => active.InstanceId == status.InstanceId))
                    continue;
                removedShield += status.ShieldRemaining;
            }
            var removed = (int)Math.Min(Math.Max(0, target.Shield), removedShield);
            target.Shield -= removed;
            return removed;
        }

        private static void ResolveActionReflect(BattleState state, ActionResolutionContext resolution, string rootActionId)
        {
            if (resolution == null) return;
            foreach (var entry in resolution.ReflectNumeratorByOwner)
            {
                var reflector = FindFighter(state, entry.Key);
                if (reflector == null || entry.Value <= 0) continue;
                var sourceId = resolution.ReflectSourceByOwner.TryGetValue(reflector.Id, out var reflectedTo) ? reflectedTo : null;
                var source = FindFighter(state, sourceId);
                if (source == null || !source.IsAlive || source.Health <= 0) continue;
                var reflected = (int)Math.Min(int.MaxValue, entry.Value / 10000);
                if (reflected <= 0) continue;
                var shieldLost = Math.Min(Math.Max(0, source.Shield), reflected);
                source.Shield -= shieldLost;
                var barrierBroken = ConsumeBarrierShield(source, shieldLost);
                var healthLost = source.CannotDie ? 0 : Math.Min(Math.Max(0, source.Health), reflected - shieldLost);
                source.Health -= healthLost;
                AddEvent(state, BattleEventKind.DamageApplied, reflector.Id, source.Id, rootActionId, reflected,
                    "Reflected damage.");
                var evt = state.Events[state.Events.Count - 1];
                evt.HealthAfter = source.Health;
                evt.ShieldAfter = source.Shield;
                evt.ShieldLost = shieldLost;
                if (barrierBroken)
                    evt.StatusesAfter = source.Statuses.Instances.ConvertAll(s => s.Clone());
                CharacterPassiveRuntime.NotifyDamageResolved(state, reflector, source, reflected, rootActionId);
                if (source.Health <= 0 && source.IsAlive && !source.CannotDie)
                {
                    source.Health = 0;
                    source.IsAlive = false;
                    source.PowerGauge = 0;
                    ApplyFighterDefeat(state, source, rootActionId);
                }
            }
        }

        private static void ResolveActionLifesteal(BattleState state, ActionResolutionContext resolution, string rootActionId)
        {
            if (resolution == null) return;
            foreach (var entry in resolution.HealthDamageBySource)
            {
                var owner = FindFighter(state, entry.Key);
                ApplyLifesteal(state, owner, owner == null ? null : StatusSystem.GetEffectiveStats(owner), entry.Value, rootActionId);
            }
        }

        private static void ApplyLifesteal(BattleState state, FighterState owner, StatBlock stats, long actualHealthLost, string rootActionId)
        {
            if (owner == null || stats == null || !owner.IsAlive || owner.Health <= 0 || actualHealthLost <= 0 ||
                stats.LifeStealBp <= 0 || stats.RecoveryBp <= 0) return;

            var numerator = new BigInteger(actualHealthLost) * stats.LifeStealBp * stats.RecoveryBp;
            var requestedHeal = (int)BigInteger.Min(int.MaxValue, BigInteger.Divide(numerator, 100000000));
            TryApplyRecovery(state, owner, owner, requestedHeal, stats.MaxHealth, rootActionId, "Lifesteal.");
        }

        private static bool TryApplyRecovery(BattleState state, FighterState source, FighterState target,
            int requestedAmount, int maximumHealth, string cardId, string message)
        {
            if (target == null || !target.IsAlive || target.Health <= 0 || requestedAmount < 0) return false;
            if (StatusSystem.IsRecoveryBlocked(target))
            {
                AddEvent(state, BattleEventKind.RecoveryBlocked, source?.Id, target.Id, cardId, 0,
                    StatusSystem.RecoveryBlockedMessage);
                return false;
            }

            target.Health = (int)Math.Min(Math.Max(1, maximumHealth), (long)target.Health + requestedAmount);
            AddEvent(state, BattleEventKind.HealApplied, source?.Id, target.Id, cardId, requestedAmount, message);
            state.Events[state.Events.Count - 1].HealthAfter = target.Health;
            return true;
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
                if (tier < 0 || tier > 5) throw new ArgumentOutOfRangeException(nameof(tiers), "Constellation tier must be 0-5.");
                var initialHealth = health != null && i < health.Count ? health[i] : definition.BaseStats.MaxHealth;
                var frozen = definition.Clone();
                var passiveErrors = PassiveRuleValidator.Validate(frozen.Passive);
                if (passiveErrors.Count > 0) throw new ArgumentException(string.Join("; ", passiveErrors), nameof(characters));
                team.Fighters.Add(new FighterState { Id = team.Side + ":" + i + ":" + definition.Id, Side = team.Side, Definition = frozen,
                    TeamIndex = i, FormationSlot = Math.Min(i, 2), IsReserve = i >= 3,
                    Health = Math.Max(0, initialHealth), IsAlive = initialHealth > 0, ConstellationTier = tier });
            }
        }

        private static void BeginTurn(BattleState state, TeamSide side, DeterministicRandom cardRng, bool drawTurnCards = true)
        {
            var team = state.Team(side);
            var activeCount = team.LivingActive().Count;
            BattleEvent reserveEntry = null;
            var reserveEntered = false;
            if (activeCount < 3)
            {
                var reserve = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
                if (reserve != null)
                {
                    var openSlot = FindOpenFormationSlot(team);
                    reserveEntry = DeployReserve(state, side, openSlot);
                    reserveEntered = reserveEntry != null;
                    activeCount++;
                }
            }
            if (activeCount == 0) { CheckOutcome(state); return; }
            state.ActingSide = side;
            state.Phase = BattlePhase.TurnStart;
            state.ActionBudget = state.IsTraining ? 1 : activeCount > 2 ? 3 : 2;
            AddEvent(state, BattleEventKind.TurnStarted, side.ToString(), null, null, state.ActionBudget, "Turn started.");
            AddEvent(state, BattleEventKind.StatusResolutionStarted, side.ToString(), null, null, 0, "Turn-start status resolution.");
            if (reserveEntry != null) AppendTimeline(state, new List<BattleEvent> { reserveEntry });
            CharacterPassiveRuntime.Refresh(state);
            CharacterPassiveRuntime.Notify(state, PassiveEventKind.TeamTurnStarted);
            ApplyStatusTicks(state, team, StatusTickTiming.TargetTurnStart);
            ApplyStanceRecovery(state, team);
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var fighter in team.Fighters)
                AdvanceStatuses(state, fighter, StatusDurationClock.TargetTurnStart, null, previousMaxHealth);
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
            if (team.LivingActive().Count < 3)
            {
                var replacement = team.Fighters.Find(f => f.IsAlive && f.IsReserve);
                if (replacement != null)
                {
                    var entry = DeployReserve(state, side, FindOpenFormationSlot(team));
                    if (entry != null)
                    {
                        AppendTimeline(state, new List<BattleEvent> { entry });
                        reserveEntered = true;
                    }
                }
            }
            CharacterPassiveRuntime.Refresh(state);
            team.HandCapacity = CardRules.GetHandCapacity(team);
            ApplyTurnStartRecovery(state, team);
            AddEvent(state, BattleEventKind.StatusResolutionEnded, side.ToString(), null, null, 0, "Turn-start statuses settled.");
            CheckOutcome(state);
            if (state.Winner.HasValue || state.IsDraw) return;
            // The normal refill is performed at the owner's previous turn end. If a fighter
            // is defeated during the opposing turn, those cards are removed after that refill.
            // Refill once more after SUB deployment so the new active fighter is draw-eligible.
            if (drawTurnCards || reserveEntered || team.Hand.Count < CardRules.GetHandCapacity(team))
            {
                var timeline = new List<BattleEvent>();
                CardRules.StartTurn(team, cardRng, timeline: timeline);
                AppendTimeline(state, timeline);
            }
            // Three active fighters expose three ordered action slots. A reduced formation
            // keeps two slots so a surviving fighter can still form a meaningful turn.
            state.ActionBudget = state.IsTraining ? 1 : team.LivingActive().Count > 2 ? 3 : 2;
            state.Phase = BattlePhase.Planning;
        }

        private static void ApplyStanceRecovery(BattleState state, BattleTeamState team)
        {
            foreach (var fighter in team.LivingActive())
                foreach (var status in fighter.Statuses.Instances)
                {
                    if (status.Recipe?.RecoverDamageTakenBp <= 0 || status.DamageTaken <= 0) continue;
                    var amount = (int)Math.Min(int.MaxValue, (long)status.DamageTaken * status.Recipe.RecoverDamageTakenBp / 10000);
                    status.DamageTaken = 0;
                    if (amount <= 0) continue;
                    var maxHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth;
                    TryApplyRecovery(state, fighter, fighter, amount, maxHealth, null, "Stance recovery.");
                }
        }

        private static void AdvanceStatuses(BattleState state, FighterState fighter, StatusDurationClock clock,
            string source = null, Dictionary<string, int> previousMaxHealthOverrides = null)
        {
            if (fighter?.Statuses == null) return;
            var advances = fighter.Statuses.Instances.Exists(s => s.Recipe?.DurationClock == clock &&
                (clock != StatusDurationClock.SourceTurnEnd || s.SourceFighterId == source));
            if (advances && previousMaxHealthOverrides != null && !previousMaxHealthOverrides.ContainsKey(fighter.Id))
                previousMaxHealthOverrides.Add(fighter.Id, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
            var expired = fighter.Statuses.Advance(clock, source);
            if (!advances) return;
            var expiredBarrierShield = 0L;
            foreach (var status in expired)
                if ((status?.Recipe?.Behavior & StatusBehavior.Barrier) != 0)
                    expiredBarrierShield += Math.Max(0, status.ShieldRemaining);
            var shieldRemoved = (int)Math.Min(Math.Max(0, fighter.Shield), expiredBarrierShield);
            fighter.Shield -= shieldRemoved;
            AddEvent(state, expired.Count > 0 ? BattleEventKind.StatusRemoved : BattleEventKind.StatusesChanged,
                fighter.Id, fighter.Id, null, expired.Count, expired.Count > 0 ? "Status expired." : null);
            var statusEvent = state.Events[state.Events.Count - 1];
            statusEvent.StatusesAfter = fighter.Statuses.Instances.ConvertAll(s => s.Clone());
            if (shieldRemoved > 0)
            {
                statusEvent.ShieldAfter = fighter.Shield;
                statusEvent.ShieldChanged = true;
            }
        }

        private static void ApplyTurnStartRecovery(BattleState state, BattleTeamState team)
        {
            foreach (var fighter in team.Fighters)
            {
                var stats = StatusSystem.GetEffectiveStats(fighter);
                if (fighter == null || !fighter.IsAlive || fighter.Health <= 0 || stats == null) continue;

                var turnStartRecovery = 0;
                var missingHealth = Math.Max(0, stats.MaxHealth - fighter.Health);
                if (missingHealth > 0 && stats.RegenerationBp > 0 && stats.RecoveryBp > 0)
                {
                    // 0.3x tuning factor to lower turn-start recovery to a healthy sustain rate
                    var numerator = new BigInteger(missingHealth) * stats.RegenerationBp * stats.RecoveryBp * 30;
                    turnStartRecovery = (int)BigInteger.Min(int.MaxValue, BigInteger.Divide(numerator, 10000000000L));
                    if (turnStartRecovery > 0)
                    {
                        TryApplyRecovery(state, fighter, fighter, turnStartRecovery, stats.MaxHealth, null, "Turn-start Recovery.");
                    }
                }

                ApplyStatusHealing(state, fighter, stats, turnStartRecovery, StatusTickTiming.TargetTurnStart);
            }
        }

        private static void ApplyStatusHealing(BattleState state, FighterState target, StatBlock targetStats,
            int turnStartRecovery, StatusTickTiming timing)
        {
            var statuses = target?.Statuses?.Instances;
            if (statuses == null) return;
            foreach (var status in statuses)
            {
                var recipe = status?.Recipe;
                var healing = recipe?.PeriodicHealing;
                if (recipe == null || (recipe.Behavior & StatusBehavior.Heal) == 0 || healing == null ||
                    healing.Timing != timing) continue;

                var source = FindFighter(state, status.SourceFighterId);
                var basis = healing.Scaling switch
                {
                    StatusHealScaling.SourceAttack => source == null ? 0 : StatusSystem.GetEffectiveStats(source).Attack,
                    StatusHealScaling.TargetMaxHealth => targetStats.MaxHealth,
                    StatusHealScaling.TurnStartRecovery => turnStartRecovery,
                    _ => healing.FixedAmount
                };
                var amount = (int)BigInteger.Min(int.MaxValue,
                    BigInteger.Divide(new BigInteger(Math.Max(0, basis)) * Math.Max(0, healing.CoefficientBp), 10000));
                var stacks = recipe.Stacking == StatusStackingPolicy.AddStacks ? Math.Max(1, status.StackCount) : 1;
                amount = (int)Math.Min(int.MaxValue, (long)amount * stacks);
                TryApplyRecovery(state, source ?? target, target, amount, targetStats.MaxHealth, null,
                    string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.Id : recipe.NameKey);
            }
        }

        private static void EndTurn(BattleState state, DeterministicRandom cardRng)
        {
            state.Phase = BattlePhase.TurnEnd;
            var actingTeam = state.Team(state.ActingSide);
            AddEvent(state, BattleEventKind.StatusResolutionStarted, state.ActingSide.ToString(), null, null, 0, "Turn-end status resolution.");
            ApplyStatusTicks(state, actingTeam, StatusTickTiming.TargetTurnEnd);
            foreach (var fighter in actingTeam.Fighters)
                if (fighter != null && fighter.IsAlive && fighter.Health > 0)
                    ApplyStatusHealing(state, fighter, StatusSystem.GetEffectiveStats(fighter), 0, StatusTickTiming.TargetTurnEnd);
            CheckOutcome(state);
            if (state.Winner.HasValue || state.IsDraw)
            {
                AddEvent(state, BattleEventKind.StatusResolutionEnded, state.ActingSide.ToString(), null, null, 0, "Statuses settled.");
                return;
            }
            CharacterPassiveRuntime.Notify(state, PassiveEventKind.TeamTurnEnded);
            var previousMaxHealth = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var fighter in actingTeam.Fighters)
                if (fighter != null && fighter.IsAlive)
                {
                    fighter.OwnerTurnsCompleted++;
                    AdvanceStatuses(state, fighter, StatusDurationClock.TargetTurnEnd, null, previousMaxHealth);
                }
            foreach (var source in actingTeam.Fighters)
            {
                if (source == null) continue;
                foreach (var target in state.Player.Fighters)
                    AdvanceStatuses(state, target, StatusDurationClock.SourceTurnEnd, source.Id, previousMaxHealth);
                foreach (var target in state.Opponent.Fighters)
                    AdvanceStatuses(state, target, StatusDurationClock.SourceTurnEnd, source.Id, previousMaxHealth);
            }
            CharacterPassiveRuntime.Refresh(state, previousMaxHealthOverrides: previousMaxHealth);
            AddEvent(state, BattleEventKind.StatusResolutionEnded, state.ActingSide.ToString(), null, null, 0, "Turn-end statuses settled.");
            // The outgoing team's ticks, recovery and duration changes have all settled.
            // Playback can now switch the camera/banner before any incoming start effects.
            AddEvent(state, BattleEventKind.TurnEnded, state.ActingSide.ToString(), null, null, 0, "Turn ended.");
            state.CompletedTurnCount++;
            if (!state.IsTraining && state.CompletedTurnCount >= MaximumCompletedTurns)
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
                CardRules.StartTurn(state.Player, cardRng, timeline: timeline);
                AppendTimeline(state, timeline);
            }
            else
            {
                var timeline = new List<BattleEvent>();
                CardRules.StartTurn(state.Opponent, cardRng, timeline: timeline);
                AppendTimeline(state, timeline);
            }
            var next = state.ActingSide == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player;
            if (state.ActingSide == TeamSide.Player) state.TurnNumber++;
            BeginTurn(state, next, cardRng, drawTurnCards: false);
        }

        private static void ApplyStatusTicks(BattleState state, BattleTeamState team, StatusTickTiming timing)
        {
            if (team?.Fighters == null) return;
            var allTicks = new List<(FighterState target, StatusTick tick)>();
            foreach (var target in team.Fighters)
            {
                if (target == null || !target.IsAlive || target.Statuses == null) continue;
                var ticks = target.Statuses.CollectTicks(timing);
                foreach (var tick in ticks) allTicks.Add((target, tick));
            }

            var defeatedFromTicks = new List<(FighterState target, StatusTick tick)>();
            foreach (var (target, tick) in allTicks)
            {
                if (!target.IsAlive) continue;
                var source = FindFighter(state, tick.SourceFighterId);
                var attacker = source == null ? new StatBlock() : StatusSystem.GetEffectiveStats(source);
                var defender = StatusSystem.GetEffectiveStats(target);
                var policy = StatusSystem.BuildDamagePolicy(source, target, tick.Family);
                policy.CannotCrit = true;
                policy.CannotBlock = true;
                var damage = DamageResolver.Resolve(new DamagePacket { BaseAmount = tick.Amount, Policy = policy },
                    attacker, defender, target.Health, target.Shield, -1, -1);
                if (!target.CannotDie) target.Health = damage.RemainingHealth;
                target.Shield = damage.RemainingShield;
                var barrierBroken = ConsumeBarrierShield(target, damage.ShieldLost);
                foreach (var status in target.Statuses.Instances)
                    if (status.Recipe?.RecoverDamageTakenBp > 0)
                        status.DamageTaken = (int)Math.Min(int.MaxValue, (long)status.DamageTaken + damage.HealthLost);
                AddEvent(state, BattleEventKind.DamageApplied, tick.SourceFighterId, target.Id, null,
                    damage.CalculatedDamage, "Status tick: " + tick.RecipeId);
                var battleEvent = state.Events[state.Events.Count - 1];
                battleEvent.StatusInstanceId = tick.StatusInstanceId;
                battleEvent.StatusRecipeId = tick.RecipeId;
                battleEvent.HealthAfter = target.Health;
                battleEvent.ShieldAfter = target.Shield;
                battleEvent.ShieldLost = damage.ShieldLost;
                if (barrierBroken)
                    battleEvent.StatusesAfter = target.Statuses.Instances.ConvertAll(s => s.Clone());
                battleEvent.WasEndured = damage.WasEndured;
                CharacterPassiveRuntime.NotifyDamageResolved(state, source, target, damage.CalculatedDamage,
                    family: tick.Family);
                if (tick.ConsumeOnTrigger)
                {
                    target.Statuses.RemoveInstance(tick.StatusInstanceId);
                    AddEvent(state, BattleEventKind.StatusRemoved, tick.SourceFighterId, target.Id, null, 1,
                        "Status expired: " + tick.RecipeId);
                    var removeEvt = state.Events[state.Events.Count - 1];
                    removeEvt.StatusInstanceId = tick.StatusInstanceId;
                    removeEvt.StatusRecipeId = tick.RecipeId;
                }
                if (target.Health <= 0 && !target.CannotDie && !defeatedFromTicks.Exists(d => d.target.Id == target.Id))
                    defeatedFromTicks.Add((target, tick));
            }

            foreach (var (target, tick) in defeatedFromTicks)
            {
                DefeatFromStatus(state, target, tick);
            }
        }

        private static void DefeatFromStatus(BattleState state, FighterState target, StatusTick tick)
        {
            if (target?.CannotDie == true) return;
            target.Health = 0;
            target.IsAlive = false;
            target.PowerGauge = 0;
            var team = state.Team(target.Side);
            var removedCards = team.Hand.FindAll(card => card.OwnerFighterId == target.Id);
            foreach (var card in removedCards) team.Hand.Remove(card);
            var defeatedEventIndex = state.Events.Count;
            AddEvent(state, BattleEventKind.FighterDefeated, target.Id, null, null, 0, "Fighter defeated by status.");
            state.Events[defeatedEventIndex].StatusInstanceId = tick.StatusInstanceId;
            state.Events[defeatedEventIndex].StatusRecipeId = tick.RecipeId;
            CharacterPassiveRuntime.NotifyFighterDefeated(state, target);
            foreach (var card in removedCards)
            {
                AddEvent(state, BattleEventKind.CardRemoved, target.Id, target.Id, null, 0, "Owner defeated.");
                state.Events[state.Events.Count - 1].CardId = card.Id;
                state.Events[state.Events.Count - 1].Card = card.Clone();
            }
            var merged = new List<BattleEvent>();
            CardRules.MergeAdjacent(team, null, merged);
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
