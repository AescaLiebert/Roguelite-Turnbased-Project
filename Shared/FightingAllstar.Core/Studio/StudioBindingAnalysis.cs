using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    /// <summary>Definite assignment over ordered actions. A loop or one branch cannot promise a value to later siblings.</summary>
    public sealed class StudioBindingAnalysis
    {
        public readonly List<StudioDiagnostic> Diagnostics = new List<StudioDiagnostic>();
        public readonly Dictionary<string, List<string>> Values = new Dictionary<string, List<string>>();
        public readonly Dictionary<string, List<string>> Targets = new Dictionary<string, List<string>>();
        private StudioDocument document;
        private sealed class Scope
        {
            public HashSet<string> Values = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> Targets = new HashSet<string>(StringComparer.Ordinal);
            public bool Removed;
            public Scope Copy() => new Scope { Values = new HashSet<string>(Values), Targets = new HashSet<string>(Targets), Removed = Removed };
            public static Scope Both(Scope a, Scope b)
            { var s = a.Copy(); s.Values.IntersectWith(b.Values); s.Targets.IntersectWith(b.Targets); s.Removed &= b.Removed; return s; }
        }
        public static StudioBindingAnalysis Analyze(StudioDocument document)
        {
            var result = new StudioBindingAnalysis { document = document };
            foreach (var rule in document.Rules ?? new List<StudioRule>())
            {
                var scope = new Scope { Removed = rule.Trigger == PassiveEventKind.StatusRemoved };
                result.Condition(rule.ConditionRootId, scope, new HashSet<string>());
                if (!string.IsNullOrEmpty(rule.ActionRootId)) result.Visit(rule.ActionRootId, scope.Copy(), rule.Trigger, new HashSet<string>(), 0);
                if (!string.IsNullOrEmpty(rule.ElseActionRootId)) result.Visit(rule.ElseActionRootId, scope.Copy(), rule.Trigger, new HashSet<string>(), 0);
            }
            return result;
        }
        private void Error(string id, string key, string type) => Diagnostics.Add(new StudioDiagnostic {
            Severity = StudioDiagnosticSeverity.Error, SourceId = id, Code = "binding.undefined-" + type,
            Message = "The " + type + " binding '" + key + "' is not available on every path here. Select an earlier producer in this rule." });
        private void Remember(Dictionary<string, List<string>> table, string id, HashSet<string> names)
        {
            if (table.TryGetValue(id, out var previous)) table[id] = previous.Intersect(names).OrderBy(n => n).ToList();
            else table[id] = names.OrderBy(n => n).ToList();
        }
        private void Condition(string id, Scope scope, HashSet<string> path)
        {
            if (id == null || !path.Add(id)) return;
            var node = document.Conditions?.Find(n => n.Id == id);
            if (node == null) return;
            Remember(Values, id, scope.Values); Remember(Targets, id, scope.Targets);
            if (node.Logic == ConditionLogic.Leaf && node.Kind == EffectConditionKind.BindingValueAtLeast && !scope.Values.Contains(node.StringValue ?? ""))
                Error(id, node.StringValue, "value");
            foreach (var child in node.Children ?? new List<string>()) Condition(child, scope, path);
            path.Remove(id);
        }
        private void Query(TargetQueryDefinition q, string id, Scope scope)
        {
            if (q == null) return;
            if (!string.IsNullOrEmpty(q.ReuseBinding) && !scope.Targets.Contains(q.ReuseBinding)) Error(id, q.ReuseBinding, "target");
            if (!string.IsNullOrEmpty(q.ExcludeBinding) && !scope.Targets.Contains(q.ExcludeBinding)) Error(id, q.ExcludeBinding, "target");
            if (!string.IsNullOrWhiteSpace(q.StoreBinding)) scope.Targets.Add(q.StoreBinding);
        }
        private Scope Children(List<string> children, Scope scope, PassiveEventKind trigger, HashSet<string> path, int depth)
        { foreach (var child in children ?? new List<string>()) scope = Visit(child, scope, trigger, path, depth + 1); return scope; }
        private Scope Visit(string id, Scope input, PassiveEventKind trigger, HashSet<string> path, int depth)
        {
            if (depth > 64 || id == null || !path.Add(id)) return input;
            var node = document.Actions?.Find(a => a.Id == id);
            if (node == null) { path.Remove(id); return input; }
            Remember(Values, id, input.Values); Remember(Targets, id, input.Targets);
            Condition(node.ConditionRootId, input, new HashSet<string>());
            var success = input.Copy();
            if (node.Kind == StudioActionKind.Module)
            {
                var module = document.Modules?.Find(m => m.Id == node.ModuleId);
                if (module != null) success = Visit(module.RootActionId, success, trigger, path, depth + 1);
            }
            else if (node.Kind == StudioActionKind.ForEachTarget)
            {
                Query(node.Query, id, success);
                Children(node.Children, success.Copy(), trigger, path, depth);
                // Zero iterations are legal. Only the selection itself is definitely assigned.
            }
            else
            {
                if (node.Kind == StudioActionKind.Command && node.Command != null)
                {
                    var command = node.Command;
                    if (command.ValueSource == PassiveValueSource.Binding && !success.Values.Contains(command.ValueBinding ?? ""))
                        Error(id, command.ValueBinding, "value");
                    if (command.ValueSource == PassiveValueSource.RemovedStatusLayers && !success.Removed)
                        Error(id, "removed status snapshot", "context");
                    if (command.ValueSource == PassiveValueSource.EventDamage && trigger != PassiveEventKind.DamageResolved ||
                        command.ValueSource == PassiveValueSource.EventHealing && trigger != PassiveEventKind.Healed)
                        Error(id, command.ValueSource.ToString(), "context");
                    Query(command.Effect?.TargetQuery, id, success);
                }
                if (node.Kind == StudioActionKind.ConsumeStatusLayers)
                {
                    if (!string.IsNullOrWhiteSpace(node.ResultBinding)) success.Values.Add(node.ResultBinding);
                    success.Removed = true;
                }
                var after = Children(node.Children, success.Copy(), trigger, path, depth);
                if (node.Kind == StudioActionKind.Branch || node.Kind == StudioActionKind.SpendResource ||
                    node.Kind == StudioActionKind.TransferResource || node.Kind == StudioActionKind.ConsumeStatusLayers)
                    success = Scope.Both(after, Children(node.ElseChildren, success.Copy(), trigger, path, depth));
                else success = after;
                success.Removed = input.Removed;
            }
            if (!string.IsNullOrEmpty(node.ConditionRootId) && node.Kind != StudioActionKind.Branch)
                success = Scope.Both(success, Children(node.ElseChildren, input.Copy(), trigger, path, depth));
            path.Remove(id);
            return success;
        }
    }
}
