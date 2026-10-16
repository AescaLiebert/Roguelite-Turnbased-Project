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
        public string Condition = "On contact, if the shown targets remain alive. Critical/status applications and their follow-ups are possible outcomes.";
    }

    /// <summary>Bounded symbolic preview. Inputs contain public fighters and the recipient's known cards only; no RNG or authority snapshot.</summary>
    public static class ElementalForecast
    {
        private sealed class Branch
        {
            public List<FighterState> Targets;
            public bool Conditional;
            public Branch Clone() => new Branch { Targets = Targets.ConvertAll(t => t.Clone()), Conditional = Conditional };
        }

        public static ElementalForecastResult Build(BattleView view, ElementalRuleSet rules, IReadOnlyList<ElementalForecastAction> actions)
        {
            var result = new ElementalForecastResult();
            if (view?.OwnTeam?.ElementalReactionsEnabled != true || rules == null || actions == null) return result;
            var publicTargets = view.OpponentTeam.Fighters.Where(f => f.IsAlive && !f.IsReserve).OrderBy(f => f.FormationSlot).ToList();
            var targets = publicTargets.Select(f => {
                var target = new FighterState { Id = f.FighterId, FormationSlot = f.FormationSlot, Health = f.Health,
                    Definition = new CharacterDefinition() };
                foreach (var status in f.Statuses.Where(s => s.EligibleFuel))
                    target.Statuses.Instances.Add(new StatusInstance { InstanceId = status.InstanceId, RecipeId = status.RecipeId,
                        StackCount = status.StackCount, RemainingDuration = status.RemainingDuration,
                        Recipe = new StatusRecipeDefinition { Id = status.RecipeId, Polarity = StatusPolarity.Debuff } });
                return target;
            }).ToList();
            var branches = new List<Branch> { new Branch { Targets = targets } };
            var actionCount = Math.Min(actions.Count, Math.Max(0, view.ActionBudget));
            for (var actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                var action = actions[actionIndex];
                var effect = action?.Effect;
                if (effect?.Kind == EffectKind.ApplyStatus && effect.StatusApplyChanceBp > 0 &&
                    rules.Fuel.Exists(f => f.RecipeId == effect.StatusRecipe?.Id))
                {
                    var fuel = rules.Fuel.Find(f => f.RecipeId == effect.StatusRecipe.Id);
                    var setupTargets = publicTargets.Where(t => !t.DebuffImmune &&
                        (effect.Target == EffectTargetScope.AllEnemies || effect.Target == EffectTargetScope.SelectedEnemy && t.FighterId == action.TargetId)).ToList();
                    if (setupTargets.Count == 0) continue;
                    var prepared = new List<Branch>();
                    foreach (var branch in branches)
                    for (var mask = 0; mask < 1 << Math.Min(3, setupTargets.Count); mask++)
                    {
                        if (prepared.Count >= 64) { result.Truncated = true; break; }
                        var copy = branch.Clone(); copy.Conditional = true;
                        for (var i = 0; i < setupTargets.Count; i++)
                            if ((mask & 1 << i) != 0 && !StatusSystem.IsFrozen(copy.Targets.Find(t => t.Id == setupTargets[i].FighterId)))
                                AddFuel(copy.Targets.Find(t => t.Id == setupTargets[i].FighterId), rules, fuel.Element,
                                "forecast:preparation:" + actionIndex + ":" + i);
                        prepared.Add(copy);
                    }
                    branches = prepared;
                    continue; // Preparation adds fuel; only a subsequent contact may react.
                }
                if (effect?.Kind != EffectKind.Damage || effect.Family != DamageFamily.Normal) continue;
                var incoming = effect.ElementalApplication?.Element ?? effect.DamageElement;
                var scope = publicTargets.Where(f => effect.Target == EffectTargetScope.AllEnemies || f.FighterId == action.TargetId).ToList();
                if (effect.Target == EffectTargetScope.SelectedEnemy && publicTargets.Any(t => t.HasTaunt) && !scope.Any(t => t.HasTaunt))
                    scope = publicTargets.Where(t => t.HasTaunt).Take(1).ToList();
                var uncertainRider = effect.ElementalApplication?.StatusEffectId != null;
                var choices = uncertainRider ? 1 << Math.Min(3, scope.Count) : 1;
                var next = new List<Branch>();
                foreach (var branch in branches)
                {
                    for (var mask = 0; mask < choices; mask++)
                    {
                        if (next.Count >= 64) { result.Truncated = true; break; }
                        var copy = branch.Clone();
                        copy.Conditional |= uncertainRider;
                        Simulate(copy, scope, publicTargets, effect, incoming, actionIndex, mask, rules, result);
                        next.Add(copy);
                    }
                }
                if (next.Count > 0) branches = next;
            }
            return result;
        }

        private static void Simulate(Branch branch, List<FighterView> scope, List<FighterView> publicTargets, EffectDefinition effect, ElementId incoming,
            int action, int acceptedMask, ElementalRuleSet rules, ElementalForecastResult result)
        {
            var swirls = new List<(FighterState target, ElementalReactionDecision decision)>();
            var receipts = new HashSet<string>();
            foreach (var fighter in scope)
                if (!fighter.EvadesAttacks) StatusSystem.BreakFreezeOnAttack(branch.Targets.Find(t => t.Id == fighter.FighterId));
            // Snapshot every source before any root consumption, matching authoritative hit batches.
            var decisions = scope.Select(t => ElementalReactionRules.Evaluate(rules, branch.Targets.Find(f => f.Id == t.FighterId), incoming)).ToList();
            for (var i = 0; i < scope.Count; i++)
            {
                var target = branch.Targets.Find(t => t.Id == scope[i].FighterId);
                if (scope[i].EvadesAttacks) continue;
                var rider = effect.Sequence?.Find(s => s?.Effect?.Id == effect.ElementalApplication?.StatusEffectId)?.Effect;
                var accepted = (acceptedMask & (1 << i)) != 0 && !scope[i].DebuffImmune && rider?.StatusApplyChanceBp > 0;
                if (effect.DamageElement == ElementId.None && !accepted) continue;
                var decision = decisions[i];
                if (decision == null)
                {
                    if (accepted) AddFuel(target, rules, incoming, "forecast:" + action + ":" + i);
                    continue;
                }
                if (decision.Reaction.Id == ElementalReactionId.Swirl) { swirls.Add((target, decision)); continue; }
                var rootLine = Record(branch, target, decision, action, result);
                ApplyForecastFreeze(target, rootLine, scope[i].FreezeImmune);
                receipts.Add(target.Id + ":" + decision.Reaction.Id);
            }
            foreach (var swirl in swirls)
            {
                if (!swirl.target.Statuses.Instances.Any(s => swirl.decision.SourceInstanceIds.Contains(s.InstanceId))) continue;
                var adjacent = branch.Targets.Where(t => Math.Abs(t.FormationSlot - swirl.target.FormationSlot) == 1).ToList();
                var sourceLine = Record(branch, swirl.target, swirl.decision, action, result);
                foreach (var recipient in adjacent)
                {
                    if (!sourceLine.AdjacentTargetIds.Contains(recipient.Id)) sourceLine.AdjacentTargetIds.Add(recipient.Id);
                    var publicTarget = publicTargets.Find(t => t.FighterId == recipient.Id);
                    // A spread may reach a target outside a single-target root scope.
                    StatusSystem.BreakFreezeOnAttack(recipient);
                    var decision = ElementalReactionRules.Evaluate(rules, recipient, swirl.decision.Reaction.OutputElement);
                    if (decision != null)
                    {
                        if (receipts.Add(recipient.Id + ":" + decision.Reaction.Id))
                        {
                            var childLine = Record(branch, recipient, decision, action, result);
                            ApplyForecastFreeze(recipient, childLine, publicTarget?.FreezeImmune == true);
                        }
                    }
                    else if (publicTarget?.DebuffImmune != true && publicTarget?.EvadesAttacks != true)
                        AddFuel(recipient, rules, swirl.decision.Reaction.OutputElement, "forecast:" + action + ":spread:" + recipient.Id);
                }
            }
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
            var stacks = target.Statuses.Instances.Where(s => decision.ConsumedInstanceIds.Contains(s.InstanceId)).Sum(s => Math.Max(1, s.StackCount));
            line.ConsumedStacks = Math.Max(line.ConsumedStacks, stacks);
            foreach (var id in decision.ConsumedInstanceIds)
            {
                if (!line.ConsumedInstanceIds.Contains(id)) line.ConsumedInstanceIds.Add(id);
                target.Statuses.RemoveInstance(id);
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

        private static void AddFuel(FighterState target, ElementalRuleSet rules, ElementId element, string id)
        {
            var mapping = rules.Fuel.Find(f => f.Element == element);
            if (mapping == null) return;
            target.Statuses.Instances.Add(new StatusInstance { InstanceId = id, RecipeId = mapping.RecipeId,
                Recipe = new StatusRecipeDefinition { Id = mapping.RecipeId, Polarity = StatusPolarity.Debuff } });
        }
    }
}
