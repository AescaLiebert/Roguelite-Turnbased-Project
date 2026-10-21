using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    /// <summary>One allow-list for picker choices and compiler acceptance.</summary>
    public static class StudioAuthoringCatalog
    {
        public static IReadOnlyList<PassiveEventKind> Triggers => Enum.GetValues(typeof(PassiveEventKind))
            .Cast<PassiveEventKind>().ToList().AsReadOnly();

        public static IReadOnlyList<EffectConditionKind> Conditions => Enum.GetValues(typeof(EffectConditionKind))
            .Cast<EffectConditionKind>().ToList().AsReadOnly();

        public static IReadOnlyList<CardEffectOperationKind> Operations => new[] {
            CardEffectOperationKind.Damage, CardEffectOperationKind.ApplyStatus, CardEffectOperationKind.RemoveStatus,
            CardEffectOperationKind.Cleanse, CardEffectOperationKind.Heal, CardEffectOperationKind.ChangePowerGauge,
            CardEffectOperationKind.ModifyCardRank, CardEffectOperationKind.Summon }.ToList().AsReadOnly();

        public static string Summary(StudioActionNode node)
        {
            if (node == null) return "Missing action";
            if (node.Kind == StudioActionKind.Module) return "Use module " + node.ModuleId;
            if (node.Kind == StudioActionKind.Command)
            {
                var command = node.Command;
                if (command == null) return "Missing command";
                if (command.Kind == PassiveCommandKind.ExecuteEffect)
                    return command.Effect?.Kind + " → " + command.Effect?.Target + " · " +
                        (string.IsNullOrEmpty(node.AmountParameterId) ? (command.Effect?.Magnitude ?? 0).ToString() : node.AmountParameterId);
                return command.Kind + " · " + command.CounterKey + " · " +
                    (string.IsNullOrEmpty(node.AmountParameterId) ? command.Amount.ToString() : node.AmountParameterId);
            }
            return node.Kind + (string.IsNullOrEmpty(node.ResourceKey) ? "" : " · " + node.ResourceKey) +
                (node.Kind == StudioActionKind.SpendResource || node.Kind == StudioActionKind.TransferResource || node.Kind == StudioActionKind.ConsumeStatusLayers
                    ? " · " + (string.IsNullOrEmpty(node.AmountParameterId) ? node.Amount.ToString() : node.AmountParameterId) : "");
        }
    }
}
