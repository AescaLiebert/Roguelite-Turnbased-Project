using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void StatTransferChecks()
    {
        var transfer = new StatTransferRecipeDefinition
        {
            CoefficientBp = 5000,
            Stats = new List<StatId> { StatId.Attack, StatId.Defense, StatId.Pierce },
            SourceStatus = new StatusRecipeDefinition
            {
                Id = "status.buff.custom-loot", Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.RefreshDuration, DurationClock = StatusDurationClock.TargetTurnStart,
                DefaultDuration = 2
            },
            TargetStatus = new StatusRecipeDefinition
            {
                Id = "status.debuff.custom-loss", Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.Stat,
                Stacking = StatusStackingPolicy.RefreshDuration, DurationClock = StatusDurationClock.TargetTurnEnd,
                DefaultDuration = 3
            }
        };
        var sourceDefinition = Fighter("generic-owner", 10000, 100);
        var targetDefinition = Fighter("generic-target", 10000, 200);
        targetDefinition.BaseStats.Defense = 120;
        targetDefinition.BaseStats.PierceBp = 3000;
        sourceDefinition.Passive = new PassiveDefinition { Id = "generic-transfer" };
        sourceDefinition.Passive.Reactions.Add(new PassiveReactionDefinition
        {
            Id = "transfer-on-ultimate", Trigger = PassiveEventKind.DamageResolved,
            ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies,
            CardOriginOnly = true, FilterCategory = true, Category = CardCategory.Ultimate,
            LimitScope = PassiveLimitScope.RootActionTarget,
            Commands = new List<PassiveCommandDefinition>
            {
                new PassiveCommandDefinition
                {
                    Kind = PassiveCommandKind.ExecuteEffect,
                    Effect = new CardEffectOperationDefinition
                    {
                        Id = "generic-transfer-operation", Kind = CardEffectOperationKind.TransferStats,
                        Target = EffectTargetScope.TriggerTarget, Window = CardEffectWindow.Damaging,
                        StatTransfer = transfer
                    }
                }
            }
        });
        Check(PassiveRuleValidator.Validate(sourceDefinition.Passive).Count == 0, "Authored transfer must validate.");
        var clonedTransfer = sourceDefinition.Passive.Clone().Reactions[0].Commands[0].Effect.StatTransfer;
        clonedTransfer.Stats.Clear();
        clonedTransfer.SourceStatus.Id = "changed";
        Check(transfer.Stats.Count == 3 && transfer.SourceStatus.Id == "status.buff.custom-loot",
            "Passive cloning must isolate transfer stats and templates.");
        var jsonCopy = Newtonsoft.Json.JsonConvert.DeserializeObject<PassiveDefinition>(
            Newtonsoft.Json.JsonConvert.SerializeObject(sourceDefinition.Passive));
        Check(jsonCopy.Reactions[0].Commands[0].Effect.StatTransfer.TargetStatus.DefaultDuration == 3,
            "Authored transfer metadata must survive JSON serialization.");

        foreach (var immune in new[] { false, true })
        {
            var state = BattleEngine.Create("transfer-check", new[] { sourceDefinition }, null,
                new[] { targetDefinition }, null, 31);
            var source = state.Player.Fighters[0];
            var target = state.Opponent.Fighters[0];
            if (immune)
                StatusSystem.Apply(target, target.Id, target.Side, new StatusRecipeDefinition
                {
                    Id = "status.buff.immunity", Polarity = StatusPolarity.Buff, DebuffImmunity = true,
                    DurationClock = StatusDurationClock.Permanent
                }, "immunity-instance", "setup", 1);
            var display = state.Clone();
            var firstEvent = state.Events.Count;
            CharacterPassiveRuntime.NotifyDamageResolved(state, source, target, 1, "ultimate-transfer",
                cardOrigin: true, isUltimate: true, category: CardCategory.Ultimate);
            if (immune)
            {
                Check(!source.Statuses.Instances.Any(s => s.RecipeId == transfer.SourceStatus.Id),
                    "Blocked target debuff must not grant a stolen-stat buff.");
                Check(!target.Statuses.Instances.Any(s => s.RecipeId == transfer.TargetStatus.Id),
                    "Transfer must respect standard debuff immunity.");
                Check(state.Events.Skip(firstEvent).Any(e => e.Kind == BattleEventKind.StatusImmuned &&
                    e.StatusRecipeId == transfer.TargetStatus.Id), "Blocked transfer must emit normal immunity feedback.");
                continue;
            }
            var buff = source.Statuses.Instances.Single(s => s.RecipeId == transfer.SourceStatus.Id);
            var debuff = target.Statuses.Instances.Single(s => s.RecipeId == transfer.TargetStatus.Id);
            Check(buff.RemainingDuration == 2 && debuff.RemainingDuration == 3,
                "Source and target must use their independent authored durations.");
            Check(buff.Recipe.Modifiers.Count == 3 && debuff.Recipe.Modifiers.Count == 3,
                "Each authored stat must become a snapshot modifier in the standard status container.");
            var sourceStats = StatusSystem.GetEffectiveStats(source);
            var targetStats = StatusSystem.GetEffectiveStats(target);
            Check(sourceStats.Attack == 200 && targetStats.Attack == 100, "Transferred Attack must balance across both fighters.");
            Check(sourceStats.Defense == 60 && targetStats.Defense == 60, "Transferred Defense must balance across both fighters.");
            Check(sourceStats.PierceBp == 1500 && targetStats.PierceBp == 1500, "Transfer must support the authored Pierce stat.");
            foreach (var evt in state.Events.Skip(firstEvent)) BattlePlaybackState.Apply(display, evt);
            Check(StatusSystem.GetEffectiveStats(display.Player.Fighters[0]).Attack == sourceStats.Attack &&
                StatusSystem.GetEffectiveStats(display.Opponent.Fighters[0]).PierceBp == targetStats.PierceBp,
                "Standard status event payloads must replay both halves of the transfer.");
            Check(transfer.SourceStatus.Modifiers.Count == 0 && transfer.TargetStatus.Modifiers.Count == 0,
                "Snapshot modifiers must not mutate authored templates.");
            var beforeRepeat = state.Events.Count;
            CharacterPassiveRuntime.NotifyDamageResolved(state, source, target, 1, "ultimate-transfer",
                cardOrigin: true, isUltimate: true, category: CardCategory.Ultimate);
            Check(source.Statuses.Instances.Count == 1 && target.Statuses.Instances.Count == 1 &&
                state.Events.Skip(beforeRepeat).Count(e => e.Kind == BattleEventKind.StatusApplied &&
                    e.StatusOutcome == StatusApplyOutcome.Refreshed) == 2 &&
                StatusSystem.GetEffectiveStats(source).Attack == 200 && StatusSystem.GetEffectiveStats(target).Attack == 100,
                "RefreshDuration must refresh the existing statuses without multiplying stolen stats.");
            var expiryCopy = target.Clone();
            expiryCopy.Statuses.Advance(StatusDurationClock.TargetTurnEnd);
            expiryCopy.Statuses.Advance(StatusDurationClock.TargetTurnEnd);
            Check(expiryCopy.Statuses.Instances.Count == 1, "Target debuff must last its authored three clocks.");
            expiryCopy.Statuses.Advance(StatusDurationClock.TargetTurnEnd);
            Check(expiryCopy.Statuses.Instances.Count == 0 && StatusSystem.GetEffectiveStats(expiryCopy).Attack == 200,
                "Normal status expiry must restore target stats.");
            Check(StatusSystem.Remove(target, StatusPolarity.Debuff, true) == 1 &&
                StatusSystem.GetEffectiveStats(target).Attack == 200, "Standard cleanse must restore stolen target stats.");
            source.Statuses.Advance(StatusDurationClock.TargetTurnStart);
            Check(source.Statuses.Instances.Count == 1, "Buff must last its first authored duration clock.");
            source.Statuses.Advance(StatusDurationClock.TargetTurnStart);
            Check(source.Statuses.Instances.Count == 0 && StatusSystem.GetEffectiveStats(source).Attack == 100,
                "Normal status expiry must remove stolen source stats.");
        }
        var invalid = transfer.Clone();
        invalid.TargetStatus.Id = null;
        Check(EffectRecipeValidator.ValidateStatTransfer(invalid).Count > 0, "Missing target status IDs must fail authoring validation.");
        Check(EffectRecipeValidator.ValidateStatTransfer(null).Count > 0, "Legacy unauthored transfer must fail validation.");
    }
}
