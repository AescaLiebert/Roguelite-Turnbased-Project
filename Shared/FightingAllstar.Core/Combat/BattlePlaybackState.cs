using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Display-only event reducer. Never submitted back to the battle authority.</summary>
    public static class BattlePlaybackState
    {
        public static BattleState BeforeOpeningDeal(BattleState initial)
        {
            var display = initial.Clone();
            display.Player.Hand.Clear();
            display.Opponent.Hand.Clear();
            foreach (var fighter in display.Player.Fighters) fighter.PowerGauge = 0;
            foreach (var fighter in display.Opponent.Fighters) fighter.PowerGauge = 0;
            display.Events.Clear();
            return display;
        }

        public static void Apply(BattleState display, BattleEvent item)
        {
            var source = display.Player.FindFighter(item.SourceId) ?? display.Opponent.FindFighter(item.SourceId);
            var target = display.Player.FindFighter(item.TargetId) ?? display.Opponent.FindFighter(item.TargetId);
            var team = source == null ? null : display.Team(source.Side);
            if (source != null && item.PowerGaugeAfter >= 0 && item.Kind != BattleEventKind.PowerGaugeChanged)
                source.PowerGauge = item.PowerGaugeAfter;
            switch (item.Kind)
            {
                case BattleEventKind.PowerGaugeChanged:
                    if (target != null) target.PowerGauge = item.PowerGaugeAfter;
                    break;
                case BattleEventKind.CardRemoved:
                    (target == null ? team : display.Team(target.Side))?.Hand.RemoveAll(c => c.Id == item.CardId);
                    break;
                case BattleEventKind.CardRankChanged:
                    var cardTeam = target == null ? team : display.Team(target.Side);
                    var rankedCard = cardTeam?.Hand.Find(c => c.Id == item.CardId);
                    if (rankedCard != null)
                    { rankedCard.Rank = item.Amount; if (item.Card != null) { rankedCard.Category = item.Card.Category; rankedCard.EffectCategory = item.Card.EffectCategory; rankedCard.TargetScope = item.Card.TargetScope; } }
                    break;
                case BattleEventKind.PassiveStatsChanged:
                    if (target != null)
                    {
                        target.Health = item.HealthAfter;
                        target.PassiveContributions.Clear();
                        foreach (var contribution in item.PassiveContributions) target.PassiveContributions.Add(contribution.Clone());
                    }
                    break;
                case BattleEventKind.TurnStarted:
                    display.ActingSide = item.SourceId == TeamSide.Player.ToString() ? TeamSide.Player : TeamSide.Opponent;
                    display.ActionBudget = item.Amount;
                    break;
                case BattleEventKind.TurnEnded:
                    // Duration changes arrive as authority snapshots, including skipped activation turns.
                    break;
                case BattleEventKind.StatusesChanged:
                    if (target != null && item.StatusesAfter != null)
                        target.Statuses.Instances = item.StatusesAfter.ConvertAll(status => status.Clone());
                    break;
                case BattleEventKind.CardDrawn:
                    if (team != null && item.Card != null && !team.Hand.Exists(c => c.Id == item.CardId))
                        team.Hand.Add(item.Card.Clone());
                    break;
                case BattleEventKind.CardMoved:
                    var moved = team?.Hand.Find(c => c.Id == item.CardId);
                    if (moved != null)
                    {
                        team.Hand.Remove(moved);
                        team.Hand.Insert(System.Math.Max(0, System.Math.Min(item.DestinationIndex, team.Hand.Count)), moved);
                    }
                    break;
                case BattleEventKind.CardPlayed:
                    team?.Hand.RemoveAll(c => c.Id == item.CardId);
                    break;
                case BattleEventKind.CardsMerged:
                    team?.Hand.RemoveAll(c => c.Id == item.ConsumedCardId);
                    var survivor = team?.Hand.Find(c => c.Id == item.CardId);
                    if (survivor != null)
                    { survivor.Rank = item.Amount; if (item.Card != null) { survivor.Category = item.Card.Category; survivor.EffectCategory = item.Card.EffectCategory; survivor.TargetScope = item.Card.TargetScope; } }
                    break;
                case BattleEventKind.DamageApplied:
                    if (target != null) { target.Health = item.HealthAfter; target.Shield = item.ShieldAfter; }
                    break;
                case BattleEventKind.HealApplied:
                    if (target != null) target.Health = item.HealthAfter;
                    break;
                case BattleEventKind.FighterDefeated:
                    if (source != null)
                    {
                        source.IsAlive = false;
                        source.Health = source.PowerGauge = source.Shield = 0;
                    }
                    break;
                case BattleEventKind.ReserveEntered:
                    if (source != null) { source.IsReserve = false; source.FormationSlot = item.DestinationIndex; }
                    break;
                case BattleEventKind.StatusApplied:
                    if (target != null && item.StatusesAfter != null)
                    { target.Statuses.Instances = item.StatusesAfter.ConvertAll(status => status.Clone()); break; }
                    if (target != null && target.Statuses != null)
                    {
                        var recipeId = item.StatusRecipeId ?? item.Message;
                        var recipe = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == recipeId);
                        var isIndependent = recipe != null && recipe.Stacking == StatusStackingPolicy.IndependentStacks;

                        StatusInstance existing = null;
                        if (!string.IsNullOrEmpty(item.StatusInstanceId))
                        {
                            existing = target.Statuses.Instances.Find(x => x.InstanceId == item.StatusInstanceId);
                        }
                        else if (!isIndependent && !string.IsNullOrEmpty(recipeId))
                        {
                            existing = target.Statuses.Instances.Find(x => x.RecipeId == recipeId);
                        }

                        if (existing != null)
                        {
                            var addStacks = !isIndependent && (existing.Recipe == null || existing.Recipe.Stacking == StatusStackingPolicy.AddStacks);
                            if (addStacks)
                                existing.StackCount += System.Math.Max(1, item.Amount);
                            else
                                existing.StackCount = System.Math.Max(existing.StackCount, item.Amount);

                            if (existing.Recipe != null && existing.Recipe.MaxStacks > 0)
                                existing.StackCount = System.Math.Min(existing.Recipe.MaxStacks, existing.StackCount);
                            existing.RemainingDuration = existing.Recipe?.DefaultDuration ?? 2;
                        }
                        else
                        {
                            if (isIndependent && recipe != null && recipe.MaxStacks > 0)
                            {
                                var matches = target.Statuses.Instances.FindAll(x => x.RecipeId == recipeId);
                                while (matches.Count >= recipe.MaxStacks)
                                {
                                    target.Statuses.Instances.Remove(matches[0]);
                                    matches.RemoveAt(0);
                                }
                            }

                            target.Statuses.Instances.Add(new StatusInstance
                            {
                                InstanceId = item.StatusInstanceId ?? (target.Id + ":" + recipeId + ":" + (target.Statuses.Instances.Count + 1)),
                                RecipeId = recipeId,
                                Recipe = recipe,
                                SourceFighterId = item.SourceId,
                                TargetFighterId = item.TargetId,
                                StackCount = isIndependent ? 1 : System.Math.Max(1, item.Amount),
                                RemainingDuration = recipe?.DefaultDuration ?? 2
                            });
                        }
                    }
                    break;
                case BattleEventKind.StatusRemoved:
                    if (target != null && item.StatusesAfter != null)
                    { target.Statuses.Instances = item.StatusesAfter.ConvertAll(status => status.Clone()); break; }
                    if (target != null)
                    {
                        if (!string.IsNullOrEmpty(item.StatusInstanceId))
                            target.Statuses.Instances.RemoveAll(x => x.InstanceId == item.StatusInstanceId);
                        else if (item.Message == "Buffs removed.")
                            target.Statuses.Instances.RemoveAll(x => x.Recipe != null && x.Recipe.Polarity == StatusPolarity.Buff);
                        else if (item.Message == "Debuffs cleansed.")
                            target.Statuses.Instances.RemoveAll(x => x.Recipe != null && x.Recipe.Polarity == StatusPolarity.Debuff);
                        else if (!string.IsNullOrEmpty(item.StatusRecipeId))
                            target.Statuses.Instances.RemoveAll(x => x.RecipeId == item.StatusRecipeId);
                        else if (!string.IsNullOrEmpty(item.Message) && item.Message.StartsWith("Status expired: "))
                            target.Statuses.Instances.RemoveAll(x => x.RecipeId == item.Message.Substring("Status expired: ".Length));
                    }
                    break;
            }
        }
    }
}
