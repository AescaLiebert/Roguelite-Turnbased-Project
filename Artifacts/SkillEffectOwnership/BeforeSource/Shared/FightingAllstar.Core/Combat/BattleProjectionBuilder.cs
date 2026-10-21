using System;
using FightingAllstar.Contracts.Projections;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Creates a recipient-scoped view without exposing the authoritative snapshot or the other hand.</summary>
    public static class BattleProjectionBuilder
    {
        public static BattleView Build(BattleState state, TeamSide recipientSide, string recipientSubjectId, long afterEventId = 0)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(recipientSubjectId)) throw new ArgumentException("A verified recipient subject is required.", nameof(recipientSubjectId));
            if (afterEventId < 0) throw new ArgumentOutOfRangeException(nameof(afterEventId));

            var own = state.Team(recipientSide);
            var opponent = state.OtherTeam(recipientSide);
            var view = new BattleView
            {
                ElementalSchemaVersion = state.ElementalSchemaVersion,
                ElementalRulesVersion = state.ElementalRules?.ContentVersion,
                MatchId = state.MatchId,
                RecipientSubjectId = recipientSubjectId,
                Revision = state.Revision,
                TurnNumber = state.TurnNumber,
                ActingSide = state.ActingSide.ToString(),
                Phase = state.Phase.ToString(),
                ActionBudget = state.ActingSide == recipientSide ? state.ActionBudget : 0,
                Winner = state.Winner.HasValue ? state.Winner.Value.ToString() : null,
                IsDraw = state.IsDraw,
                OwnTeam = BuildTeam(own, true, state.ElementalRules),
                OpponentTeam = BuildTeam(opponent, false, state.ElementalRules),
                OpponentHandSize = opponent.Hand == null ? 0 : opponent.Hand.Count
            };

            if (state.Entities != null)
                foreach (var entity in state.Entities)
                    if (entity != null) view.Entities.Add(BuildEntity(state, entity));

            if (state.Events != null)
                foreach (var battleEvent in state.Events)
                {
                    if (battleEvent == null || battleEvent.Id <= afterEventId) continue;
                    view.Events.Add(BuildEvent(state, battleEvent, recipientSide));
                    view.LatestEventId = Math.Max(view.LatestEventId, battleEvent.Id);
                }
            return view;
        }

        private static BattleTeamView BuildTeam(BattleTeamState team, bool includeHand, Content.ElementalRuleSet rules)
        {
            var view = new BattleTeamView { ElementalReactionsEnabled = team?.ElementalReactionsEnabled ?? false };
            if (team == null) return view;
            if (team.Fighters != null)
                foreach (var fighter in team.Fighters)
                {
                    if (fighter == null) continue;
                    var fighterView = new FighterView
                    {
                        FighterId = fighter.Id, Level = fighter.Level,
                        DefinitionId = fighter.Definition == null ? null : fighter.Definition.Id,
                        Side = fighter.Side.ToString(),
                        TeamIndex = fighter.TeamIndex,
                        FormationSlot = fighter.FormationSlot,
                        IsReserve = fighter.IsReserve,
                        IsAlive = fighter.IsAlive,
                        Health = fighter.Health,
                        MaxHealth = StatusSystem.GetEffectiveStats(fighter).MaxHealth,
                        Shield = fighter.Shield,
                        PowerGauge = fighter.PowerGauge,
                        PowerGaugeRemainderBp = fighter.PowerGaugeRemainderBp,
                        EvadesAttacks = fighter.Statuses.Instances.Exists(s => s.Recipe?.EvadeAttacks == true),
                        HasTaunt = fighter.Statuses.Instances.Exists(s => s.Recipe?.HasTaunt == true),
                        DebuffImmune = fighter.Statuses.Instances.Exists(s => s.Recipe?.DebuffImmunity == true),
                        FreezeImmune = StatusSystem.IsFreezeRecoveryActive(fighter) || StatusSystem.IsFrozen(fighter) || fighter.Statuses.Instances.Exists(s =>
                            s.Recipe?.DebuffImmunity == true || s.Recipe?.ImmuneStatusTags?.Contains("status.freeze") == true)
                    };
                    foreach (var status in fighter.Statuses.Instances)
                        fighterView.Statuses.Add(new PublicStatusView { InstanceId = status.InstanceId, RecipeId = status.RecipeId,
                            Name = status.Recipe?.NameKey, Element = Content.ElementId.None.ToString(), EligibleFuel = false,
                            StackCount = status.StackCount, RemainingDuration = status.RemainingDuration,
                            LinkedEntityId = status.EntityInstanceId,
                            Polarity = status.Recipe?.Polarity.ToString(), Color = status.Recipe?.Color.ToString(),
                            IsRemovable = status.Recipe?.StructuralEntityMarker != true && status.Recipe?.Color != Content.StatusColor.Grey });
                    foreach (var aura in fighter.ElementStatuses.Instances)
                        fighterView.ElementStatuses.Add(new PublicElementStatusView { InstanceId = aura.InstanceId,
                            Element = aura.Element.ToString(), RemainingDuration = aura.RemainingDuration });
                    foreach (var status in fighter.ElementStatuses.ReactionStatuses)
                        fighterView.ElementalReactionStatuses.Add(new PublicElementalReactionStatusView {
                            InstanceId = status.InstanceId, Reaction = status.Reaction.ToString(),
                            RemainingDuration = status.RemainingDuration });
                    view.Fighters.Add(fighterView);
                }
            if (includeHand && team.Hand != null)
                foreach (var card in team.Hand)
                {
                    if (card == null) continue;
                    view.Hand.Add(new CardView
                    {
                        CardId = card.Id,
                        OwnerFighterId = card.OwnerFighterId,
                        SkillId = card.SkillId,
                        Rank = card.Rank,
                        Kind = card.Kind.ToString(),
                        Category = card.Category.ToString(),
                        EffectCategory = CardRules.GetEffectCategory(card).ToString(),
                        TargetScope = card.TargetScope.ToString(),
                        UltimateTier = card.UltimateTier
                    });
                }
            return view;
        }

        private static BattleEventView BuildEvent(BattleState state, BattleEvent battleEvent, TeamSide recipientSide)
        {
            var source = FindFighter(state, battleEvent.SourceId);
            var privateCardDraw = battleEvent.Kind == BattleEventKind.CardDrawn && source != null && source.Side != recipientSide;
            return new BattleEventView
            {
                Entity = battleEvent.Entity == null ? null : BuildEntity(state, battleEvent.Entity, battleEvent.EntityRemainingDuration),
                EntityId = battleEvent.EntityId,
                EntityOwnerFighterId = battleEvent.EntityOwnerFighterId,
                EntityDefinitionId = battleEvent.EntityDefinitionId,
                EntityRemovalCause = battleEvent.EntityRemovalCause.ToString(),
                EntityOccurrence = battleEvent.EntityOccurrence,
                EntityCapacityAfter = battleEvent.EntityCapacityAfter,
                EntityMaximumCapacity = battleEvent.EntityMaximumCapacity,
                EntityAbsorbedAmount = battleEvent.EntityAbsorbedAmount,
                EntityRemainingDuration = battleEvent.EntityRemainingDuration,
                DamageElement = battleEvent.DamageElement.ToString(),
                TriggerElement = battleEvent.TriggerElement.ToString(),
                ReactionOutputBlocked = battleEvent.ReactionOutputBlocked,
                ElementalReactionStatusApplied = battleEvent.ElementalReactionStatusApplied,
                ElementalReactionStatusAlreadyActive = battleEvent.ElementalReactionStatusAlreadyActive,
                ElementalReaction = battleEvent.ElementalReaction.ToString(),
                DamageOrigin = battleEvent.DamageOrigin.ToString(),
                ParentEventId = battleEvent.ParentEventId,
                ConsumedStatusInstanceIds = battleEvent.ConsumedStatusInstanceIds == null ? new System.Collections.Generic.List<string>() :
                    new System.Collections.Generic.List<string>(battleEvent.ConsumedStatusInstanceIds),
                ElementStatusesAfter = battleEvent.ElementStatusesAfter?.ConvertAll(aura => new PublicElementStatusView {
                    InstanceId = aura.InstanceId, Element = aura.Element.ToString(), RemainingDuration = aura.RemainingDuration }),
                ElementalReactionStatusesAfter = battleEvent.ElementalReactionStatusesAfter?.ConvertAll(status =>
                    new PublicElementalReactionStatusView { InstanceId = status.InstanceId,
                        Reaction = status.Reaction.ToString(), RemainingDuration = status.RemainingDuration }),
                EventId = battleEvent.Id,
                HitIndex = battleEvent.HitIndex,
                HitCount = battleEvent.HitCount,
                AttackRange = battleEvent.AttackRange.ToString(),
                HasHitReaction = battleEvent.HasHitReaction,
                Reaction = battleEvent.Reaction.ToString(),
                WasReactionResisted = battleEvent.WasReactionResisted,
                WasStanceCancelled = battleEvent.WasStanceCancelled,
                Kind = battleEvent.Kind.ToString(),
                SourceId = battleEvent.SourceId,
                TargetId = battleEvent.TargetId,
                TargetIds = battleEvent.TargetIds == null ? new System.Collections.Generic.List<string>() :
                    new System.Collections.Generic.List<string>(battleEvent.TargetIds),
                CardId = privateCardDraw ? null : battleEvent.CardId,
                Amount = battleEvent.Amount,
                FormulaDamage = battleEvent.FormulaDamage,
                ConstructAbsorbed = battleEvent.ConstructAbsorbed,
                HealthAfter = battleEvent.HealthAfter,
                ShieldAfter = battleEvent.ShieldAfter,
                ShieldLost = battleEvent.ShieldLost,
                EffectiveMaxHealth = battleEvent.EffectiveMaxHealth,
                PowerGaugeAfter = battleEvent.PowerGaugeAfter,
                PowerGaugeRemainderAfter = battleEvent.PowerGaugeRemainderAfter,
                GaugeDeltaBp = battleEvent.GaugeDeltaBp,
                RootActionId = battleEvent.RootActionId,
                WasCritical = battleEvent.WasCritical,
                WasBlocked = battleEvent.WasBlocked,
                WasEndured = battleEvent.WasEndured,
                Message = battleEvent.Message
            };
        }

        private static BattleEntityView BuildEntity(BattleState state, BattleEntityInstance entity, int? remainingDuration = null)
        {
            var owner = FindFighter(state, entity.OwnerFighterId);
            var marker = owner?.Statuses?.Instances.Find(status => status.InstanceId == entity.MarkerStatusInstanceId);
            return new BattleEntityView {
                EntityId = entity.Id, DefinitionId = entity.Definition?.Id, OwnerFighterId = entity.OwnerFighterId,
                MarkerStatusInstanceId = entity.MarkerStatusInstanceId, Name = entity.Definition?.NameKey,
                Category = entity.Definition?.Category.ToString(), PresentationKey = entity.Definition?.PresentationKey,
                Side = entity.Side.ToString(), SpawnOrder = entity.SpawnOrder,
                RemainingDuration = remainingDuration ?? marker?.RemainingDuration ?? 0,
                Capacity = entity.Capacity, MaximumCapacity = entity.MaximumCapacity
            };
        }

        private static FighterState FindFighter(BattleState state, string fighterId)
        {
            if (string.IsNullOrEmpty(fighterId)) return null;
            return state.Player.FindFighter(fighterId) ?? state.Opponent.FindFighter(fighterId);
        }
    }
}
