#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        private void DrawAbilityConditions(VisualElement parent, List<EffectConditionDefinition> conditions, string path)
        {
            var box = Node(parent, "IF · " + (conditions.Count == 0 ? "Always" : "All conditions"));
            box.name = path;
            foreach (var condition in conditions.ToArray())
            {
                if (condition == null) continue;
                DrawAbilityCondition(box, condition, path + "." + conditions.IndexOf(condition), 0);
                Button(box, "Remove condition", () => Edit("Remove condition", () => conditions.Remove(condition), true));
            }
            Button(box, "+ Condition", () => Edit("Add condition", () => conditions.Add(new EffectConditionDefinition()), true));
        }

        private void DrawAbilityCondition(VisualElement parent, EffectConditionDefinition condition, string path, int depth)
        {
            if (depth > 32) { Body(parent, "Condition depth exceeds the supported limit.", "studio-error"); return; }
            var box = Node(parent, Humanize(condition.Logic == ConditionLogic.Leaf ? condition.Kind.ToString() : condition.Logic.ToString()));
            EnumControl(box, "Group", condition.Logic, v => {
                condition.Logic = (ConditionLogic)v;
                if (condition.Logic == ConditionLogic.Leaf) condition.Children.Clear();
                else if (condition.Children.Count == 0) condition.Children.Add(new EffectConditionDefinition());
                if (condition.Logic == ConditionLogic.Not && condition.Children.Count > 1)
                    condition.Children.RemoveRange(1, condition.Children.Count - 1);
            }, true);
            ToggleControl(box, "Negate result", condition.Negate, v => condition.Negate = v);
            if (condition.Logic == ConditionLogic.Leaf)
            {
                var node = new StudioConditionNode { Id = path, Logic = condition.Logic, Kind = condition.Kind,
                    Subject = condition.Subject, Threshold = condition.Threshold, Tag = condition.Tag,
                    RecipeId = condition.RecipeId, StringValue = condition.StringValue,
                    RosterFilter = condition.RosterFilter ?? new PassiveTargetFilter(), InitialRoster = condition.InitialRoster };
                DrawConditionPredicate(box, node, () => {
                    condition.Kind = node.Kind; condition.Subject = node.Subject; condition.Threshold = node.Threshold;
                    condition.Tag = node.Tag; condition.RecipeId = node.RecipeId; condition.StringValue = node.StringValue;
                    condition.RosterFilter = node.RosterFilter; condition.InitialRoster = node.InitialRoster;
                });
            }
            else
            {
                foreach (var child in condition.Children.ToArray())
                {
                    DrawAbilityCondition(box, child, path + "." + condition.Children.IndexOf(child), depth + 1);
                    Button(box, "Remove child", () => Edit("Remove condition child", () => condition.Children.Remove(child), true));
                }
                if (condition.Logic != ConditionLogic.Not || condition.Children.Count == 0)
                    Button(box, "+ Child condition", () => Edit("Add child condition", () => condition.Children.Add(new EffectConditionDefinition()), true));
            }
        }

        private void DrawGrantedReactions(VisualElement parent, StatusRecipeDefinition status, string path)
        {
            var box = Node(parent, "REACTIONS WHILE ACTIVE"); box.name = path + ".reactions";
            Body(box, "Owned by this skill rank / ultimate tier and this status. Expiry or removal ends its reactions.", "studio-subtle");
            foreach (var reaction in status.Reactions.ToArray())
            {
                var view = new Foldout { text = "WHEN " + Humanize(reaction.Trigger.ToString()) + " · " + reaction.Id, value = true };
                box.Add(view);
                EnumControl(view, "WHEN", reaction.Trigger, v => reaction.Trigger = (PassiveEventKind)v, true);
                var who = Node(view, "WHO · event participants");
                EnumControl(who, "Event actor", reaction.ActorRelation, v => reaction.ActorRelation = (PassiveRelation)v);
                EnumControl(who, "Event target", reaction.TargetRelation, v => reaction.TargetRelation = (PassiveRelation)v);
                Body(who, "Owner is the status holder. For an incoming attack, Actor is the attacker and Event target is the holder.", "studio-subtle");
                var filters = new Foldout { text = "Event filters", value = false }; view.Add(filters);
                DrawEventFilters(filters, new StudioRule { Trigger = reaction.Trigger, RuntimeVersion = reaction.ExecutionVersion, Settings = reaction });
                DrawAbilityConditions(view, reaction.Conditions, path + "." + reaction.Id + ".if");
                var actions = Node(view, "DO · ordered actions");
                foreach (var command in reaction.Commands.ToArray())
                {
                    DrawCommand(actions, new StudioActionNode { Id = path + "." + reaction.Id + ".command" + reaction.Commands.IndexOf(command), Command = command },
                        true, reaction.Trigger == PassiveEventKind.BeforeDamage);
                    Button(actions, "Remove action", () => Edit("Remove status action", () => reaction.Commands.Remove(command), true));
                }
                Button(actions, "+ Action", () => Edit("Add status action", () => reaction.Commands.Add(new PassiveCommandDefinition {
                    Kind = PassiveCommandKind.ExecuteEffect, Effect = NewOperation(CardEffectOperationKind.ApplyStatus) }), true));
                if (reaction.Actions.Count > 0 || reaction.ElseCommands.Count > 0)
                    KitFold(view, "Preserved action program / else branch", path + reaction.Id + ".program", p => {
                        KitValue(p, "Actions", typeof(List<PassiveActionDefinition>), reaction.Actions, v => reaction.Actions = (List<PassiveActionDefinition>)v, path + ".actions");
                        KitValue(p, "Else actions", typeof(List<PassiveCommandDefinition>), reaction.ElseCommands, v => reaction.ElseCommands = (List<PassiveCommandDefinition>)v, path + ".else");
                    });
                if (reaction.ExecutionVersion == 1)
                {
                    EnumControl(view, "Activation scope", reaction.LimitScope, v => reaction.LimitScope = (PassiveLimitScope)v);
                    IntControl(view, "Maximum activations", reaction.MaximumActivations, v => reaction.MaximumActivations = v);
                    IntControl(view, "Cooldown owner turns", reaction.CooldownOwnerTurns, v => reaction.CooldownOwnerTurns = v);
                }
                else Body(view, "Imported reaction: existing activation frequency is preserved.", "studio-subtle");
                Button(view, "Remove reaction", () => Edit("Remove granted reaction", () => status.Reactions.Remove(reaction), true));
            }
            Button(box, "+ Reaction while active", () => Edit("Add granted reaction", () => status.Reactions.Add(new PassiveReactionDefinition {
                Id = status.Id + ".reaction." + Guid.NewGuid().ToString("N"), ExecutionVersion = 1,
                Trigger = PassiveEventKind.DamageResolved, ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
                CardOriginOnly = true, LimitScope = PassiveLimitScope.RootAction, MaximumActivations = 1 }), true));
        }
    }
}
#endif
