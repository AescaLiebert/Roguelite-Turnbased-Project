using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Contracts.Projections;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public sealed class ElementalForecastAction
    {
        public string CardId;
        public string OwnerId;
        public string TargetId;
        public EffectDefinition Effect;
    }

    public sealed class ElementalForecastLine
    {
        public int ActionIndex;
        public string TargetId;
        public ElementalReactionId Reaction;
        public bool Conditional;
        public bool OutputBlocked;
        public List<string> ConsumedInstanceIds = new List<string>();
        public int ConsumedStacks;
        public List<string> AdjacentTargetIds = new List<string>();
    }

    public sealed class ElementalForecastResult
    {
        public readonly List<ElementalForecastLine> Lines = new List<ElementalForecastLine>();
        public bool Truncated;
        public string Condition = "On contact, if the shown targets remain alive. Elemental auras apply on contact independently of debuff rolls.";
    }

    /// <summary>Bounded symbolic preview. Inputs contain public fighters and the recipient's known cards only; no RNG or authority snapshot.</summary>
    public static class ElementalForecast
    {
        private sealed class Branch
        {
            public List<FighterState> Targets;
            public bool Conditional = false;
        }

        public static ElementalForecastResult Build(BattleView view, ElementalRuleSet rules, IReadOnlyList<ElementalForecastAction> actions)
        {
            var result = new ElementalForecastResult();
            if (view?.OwnTeam?.ElementalReactionsEnabled != true || rules == null || actions == null) return result;
            var publicTargets = view.OpponentTeam.Fighters.Where(f => f.IsAlive && !f.IsReserve).OrderBy(f => f.FormationSlot).ToList();
            var targets = publicTargets.Select(f => {
                var target = new FighterState { Id = f.FighterId, FormationSlot = f.FormationSlot, Health = f.Health,
                    Definition = new CharacterDefinition() };
                foreach (var aura in f.ElementStatuses)
                    if (Enum.TryParse<ElementId>(aura.Element, out var element))
                        target.ElementStatuses.Instances.Add(new ElementStatusInstance { InstanceId = aura.InstanceId,
                            Element = element, RemainingDuration = aura.RemainingDuration });
                foreach (var status in f.Statuses)
                {
                    var recipe = GenshinStatusRecipes.CreateOutputs().Find(r => r.Id == status.RecipeId);
                    if (recipe != null) target.Statuses.Instances.Add(new StatusInstance { InstanceId = status.InstanceId,
                        RecipeId = status.RecipeId, Recipe = recipe, RemainingDuration = status.RemainingDuration });
                }
                return target;
            }).ToList();
            var branch = new Branch { Targets = targets };
            var actionCount = Math.Min(actions.Count, Math.Max(0, view.ActionBudget));
            for (var actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                var action = actions[actionIndex];
                var effect = action?.Effect;
                if (effect?.Kind != EffectKind.Damage || effect.Family != DamageFamily.Normal) continue;
                var incoming = effect.DamageElement != ElementId.None ? effect.DamageElement : effect.ElementalApplication?.Element ?? ElementId.None;
                var scope = publicTargets.Where(f => effect.Target == EffectTargetScope.AllEnemies || f.FighterId == action.TargetId).ToList();
                if (effect.Target == EffectTargetScope.SelectedEnemy && publicTargets.Any(t => t.HasTaunt) && !scope.Any(t => t.HasTaunt))
                    scope = publicTargets.Where(t => t.HasTaunt).Take(1).ToList();
                Simulate(branch, scope, publicTargets, incoming, actionIndex, rules, result);
            }
            return result;
        }

        private static void Simulate(Branch branch, List<FighterView> scope, List<FighterView> publicTargets, ElementId incoming,
            int action, ElementalRuleSet rules, ElementalForecastResult result)
        {
            var swirls = new List<(FighterState target, ElementalReactionDecision decision)>();
            var receipts = new HashSet<string>();
            var deferredFreezes = new List<(FighterState target, ElementalForecastLine line, bool immune)>();
            foreach (var fighter in scope)
                if (!fighter.EvadesAttacks) RecordShatter(branch, branch.Targets.Find(t => t.Id == fighter.FighterId), action, result);
            // Snapshot every source before any root consumption, matching authoritative hit batches.
            var decisions = scope.Select(t => ElementalReactionRules.Evaluate(rules, branch.Targets.Find(f => f.Id == t.FighterId), incoming)).ToList();
            for (var i = 0; i < scope.Count; i++)
            {
                var target = branch.Targets.Find(t => t.Id == scope[i].FighterId);
                if (scope[i].EvadesAttacks) continue;
                var decision = decisions[i];
                if (decision == null)
                {
                    AddFuel(target, rules, incoming, "forecast:" + action + ":" + i);
                    continue;
                }
                if (decision.Reaction.Id == ElementalReactionId.Swirl) { swirls.Add((target, decision)); continue; }
                if (decision.Reaction.PreserveFuel) AddFuel(target, rules, incoming, "forecast:" + action + ":" + i);
                var rootLine = Record(branch, target, decision, action, result);
                ApplyForecastOwnStatus(branch, target, decision, action, result);
                deferredFreezes.Add((target, rootLine, scope[i].FreezeImmune));
                receipts.Add(target.Id + ":" + decision.Reaction.Id);
            }
            foreach (var swirl in swirls)
            {
                if (!swirl.target.ElementStatuses.Instances.Any(s => swirl.decision.SourceInstanceIds.Contains(s.InstanceId))) continue;
                var adjacent = branch.Targets.Where(t => Math.Abs(t.FormationSlot - swirl.target.FormationSlot) == 1).ToList();
                var sourceLine = Record(branch, swirl.target, swirl.decision, action, result);
                foreach (var recipient in adjacent)
                {
                    if (!sourceLine.AdjacentTargetIds.Contains(recipient.Id)) sourceLine.AdjacentTargetIds.Add(recipient.Id);
                    var publicTarget = publicTargets.Find(t => t.FighterId == recipient.Id);
                    // A spread may reach a target outside a single-target root scope.
                    RecordShatter(branch, recipient, action, result);
                    var decision = ElementalReactionRules.Evaluate(rules, recipient, swirl.decision.Reaction.OutputElement);
                    if (decision != null)
                    {
                        if (receipts.Add(recipient.Id + ":" + decision.Reaction.Id))
                        {
                            if (decision.Reaction.PreserveFuel) AddFuel(recipient, rules, swirl.decision.Reaction.OutputElement, "forecast:" + action + ":" + recipient.Id);
                            var childLine = Record(branch, recipient, decision, action, result);
                            ApplyForecastOwnStatus(branch, recipient, decision, action, result);
                            deferredFreezes.Add((recipient, childLine, publicTarget?.FreezeImmune == true));
                        }
                    }
                    else if (publicTarget?.EvadesAttacks != true)
                        AddFuel(recipient, rules, swirl.decision.Reaction.OutputElement, "forecast:" + action + ":spread:" + recipient.Id);
                }
            }
            foreach (var pending in deferredFreezes) ApplyForecastFreeze(pending.target, pending.line, pending.immune);
        }

        private static ElementalForecastLine Record(Branch branch, FighterState target, ElementalReactionDecision decision,
            int action, ElementalForecastResult result)
        {
            var line = result.Lines.Find(l => l.ActionIndex == action && l.TargetId == target.Id && l.Reaction == decision.Reaction.Id);
            if (line == null)
            {
                line = new ElementalForecastLine { ActionIndex = action, TargetId = target.Id, Reaction = decision.Reaction.Id, Conditional = branch.Conditional };
                result.Lines.Add(line);
            }
            line.Conditional |= branch.Conditional;
            var stacks = target.ElementStatuses.Instances.Count(s => decision.ConsumedInstanceIds.Contains(s.InstanceId));
            line.ConsumedStacks = Math.Max(line.ConsumedStacks, stacks);
            foreach (var id in decision.ConsumedInstanceIds)
            {
                if (!line.ConsumedInstanceIds.Contains(id)) line.ConsumedInstanceIds.Add(id);
                target.ElementStatuses.RemoveInstance(id);
            }
            return line;
        }

        private static void ApplyForecastFreeze(FighterState target, ElementalForecastLine line, bool publicImmune)
        {
            if (line.Reaction != ElementalReactionId.Freeze) return;
            var blocked = publicImmune || StatusSystem.IsFreezeRecoveryActive(target);
            line.OutputBlocked |= blocked;
            if (blocked) return;
            var freeze = GenshinStatusRecipes.CreateOutputs().Find(r => r.Id == StatusSystem.FreezeRecipeId);
            StatusSystem.Apply(target, "forecast", TeamSide.Player, freeze, "forecast:freeze:" + line.ActionIndex,
                "forecast", line.ActionIndex);
        }

        private static void ApplyForecastOwnStatus(Branch branch, FighterState target, ElementalReactionDecision decision,
            int action, ElementalForecastResult result)
        {
            var id = decision.Reaction.Id;
            if (id == ElementalReactionId.Overload)
            {
                foreach (var recipient in branch.Targets.Where(t => Math.Abs(t.FormationSlot - target.FormationSlot) <= 1))
                    RecordShatter(branch, recipient, action, result);
                return;
            }
            if (id != ElementalReactionId.ElectroCharged && id != ElementalReactionId.Superconduct) return;
            var recipeId = id == ElementalReactionId.ElectroCharged ? "status.reaction.charged" : "status.reaction.superconduct";
            var recipients = new List<FighterState> { target };
            if (id == ElementalReactionId.Superconduct)
                recipients = branch.Targets.Where(t => Math.Abs(t.FormationSlot - target.FormationSlot) <= 1).ToList();
            for (var index = 0; index < recipients.Count; index++)
            {
                var recipient = recipients[index];
                if (id == ElementalReactionId.ElectroCharged)
                    foreach (var neighbor in branch.Targets.Where(t => Math.Abs(t.FormationSlot - recipient.FormationSlot) == 1 && t.ElementStatuses.Has(ElementId.Hydro)))
                        if (!recipients.Contains(neighbor)) recipients.Add(neighbor);
                if (recipient.Statuses.Instances.Exists(s => s.RecipeId == recipeId)) continue;
                if (id == ElementalReactionId.Superconduct) RecordShatter(branch, recipient, action, result);
                var recipe = GenshinStatusRecipes.CreateOutputs().Find(r => r.Id == recipeId);
                StatusSystem.Apply(recipient, "forecast", TeamSide.Player, recipe,
                    "forecast:" + recipeId + ":" + action + ":" + recipient.Id, "forecast", action);
                if (id == ElementalReactionId.ElectroCharged && recipient != target)
                    Record(branch, recipient, new ElementalReactionDecision { Reaction = decision.Reaction.Clone() }, action, result);
            }
        }

        private static void RecordShatter(Branch branch, FighterState target, int action, ElementalForecastResult result)
        {
            if (!StatusSystem.BreakFreezeOnAttack(target)) return;
            Record(branch, target, new ElementalReactionDecision {
                Reaction = new ElementalReactionDefinition { Id = ElementalReactionId.Shatter } }, action, result);
        }

        private static void AddFuel(FighterState target, ElementalRuleSet rules, ElementId element, string id)
        {
            target.ElementStatuses.Apply(element, "forecast", id);
        }
    }
}
