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
        private void BuildResources()
        {
            Heading(_host, "Resources & statuses");
            Body(_host, "Shared definitions keep resource limits, status lifetimes and local reactions consistent across every use.", "studio-subtle");
            Section(_host, "RESOURCES");
            foreach (var resource in _document.Resources)
                SemanticCard(_host, "RESOURCE", resource.Name + " · maximum " + resource.Cap, resource.Id, "to");
            Button(_host, "+ Resource", () => Edit("Create resource", () => {
                var r = new StudioResource { Id = "resource." + Guid.NewGuid().ToString("N"), Name = "New resource", Cap = 6 };
                _document.Resources.Add(r); _selectedNode = r.Id;
            }, true)).name = "studio-add-resource";
            Section(_host, "STATUSES");
            foreach (var status in _document.Statuses)
                SemanticCard(_host, "STATUS", status.Name + " · " + status.Definition.MaxStacks + " layers · " + Humanize(status.Definition.DurationClock.ToString()), status.Id, "if");
            Button(_host, "+ Status", () => Edit("Create shared status", () => {
                var id = "status." + Guid.NewGuid().ToString("N");
                _document.Statuses.Add(new StudioStatus { Id = id, Name = "New status", Definition = new StatusRecipeDefinition {
                    Id = id, NameKey = "New status", Stacking = StatusStackingPolicy.AddStacks, MaxStacks = 3 } }); _selectedNode = id;
            }, true)).name = "studio-add-status";
            Section(_host, "INTERCEPTION POLICIES");
            foreach (var policy in _document.Policies)
                SemanticCard(_host, "POLICY", policy.Name + " · " + Humanize(policy.Definition.Kind.ToString()), policy.Id, "when");
            Button(_host, "+ Interception policy", () => Edit("Create policy", () => {
                var p = new StudioPolicy { Id = "policy." + Guid.NewGuid().ToString("N"), Name = "Incoming HP cap" };
                _document.Policies.Add(p); _selectedNode = p.Id;
            }, true)).name = "studio-add-policy";
            var resourceSelection = _document.Resources.Find(r => r.Id == _selectedNode);
            if (resourceSelection != null)
            {
                Section(_inspector, "RESOURCE DEFINITION");
                TextControl(_inspector, "Name", resourceSelection.Name, v => resourceSelection.Name = v);
                IntControl(_inspector, "Maximum amount", resourceSelection.Cap, v => resourceSelection.Cap = v);
                Body(_inspector, "Every counter gain/set and transfer referencing this resource uses this cap at compile time.", "studio-subtle");
                Body(_inspector, resourceSelection.Id, "studio-code");
                DrawResourceReferences(resourceSelection.Id);
                var used = _document.Actions.Any(a => a.ResourceKey == resourceSelection.Id || a.Command?.CounterKey == resourceSelection.Id) ||
                    _document.Policies.Any(p => p.Definition.ResourceKey == resourceSelection.Id) || _document.AbilityVariants.Any(v => v.Definition.RequiredResourceKey == resourceSelection.Id);
                Button(_inspector, "Remove definition", () => Edit("Remove resource", () => {
                    _document.Resources.Remove(resourceSelection); _selectedNode = null;
                }, true)).SetEnabled(!used);
                if (used) Body(_inspector, "Remove or reassign these references before deleting the resource.", "studio-subtle");
            }
            var statusSelection = _document.Statuses.Find(s => s.Id == _selectedNode);
            if (statusSelection != null)
            {
                Section(_inspector, "STATUS DEFINITION");
                TextControl(_inspector, "Name", statusSelection.Name, v => { statusSelection.Name = v; statusSelection.Definition.NameKey = v; });
                DrawStatusApplication(_inspector, new StatusApplicationRecipe { InlineRecipe = statusSelection.Definition }, true);
                Section(_inspector, "LOCAL EVENT RULES");
                Body(_inspector, "Owner is the fighter carrying this status. The original source is retained by the instance, including its removal snapshot.", "studio-subtle");
                foreach (var rule in _document.Rules.Where(r => r.StatusId == statusSelection.Id))
                {
                    var r = rule; Button(_inspector, r.Name + " · " + Humanize(r.Trigger.ToString()), () => { _selectedRule = r.Id; Navigate("rules", r.Id); });
                }
                Button(_inspector, "+ Status event rule", () => Edit("Add status-owned rule", () => {
                    var r = new StudioRule { Id = "rule." + Guid.NewGuid().ToString("N"), Name = statusSelection.Name + " · on removal", StatusId = statusSelection.Id,
                        Trigger = PassiveEventKind.StatusRemoved, ConditionRootId = NewLeaf(), ActionRootId = NewAction(StudioActionKind.Sequence).Id };
                    r.Settings.EventStatusRecipeId = statusSelection.Id; r.Settings.AllowReactionOrigin = true;
                    _document.Rules.Add(r); _selectedRule = r.Id; _selectedNode = r.Id; _page = "rules";
                }, true));
                foreach (var a in _document.Actions.Where(a => (a.Command?.Effect?.Status?.StatusRecipeId ?? a.Command?.Effect?.Status?.InlineRecipe?.Id) == statusSelection.Id))
                { var captured = a.Id; Button(_inspector, "Used by " + (a.Name ?? a.Id), () => SelectSource(captured)); }
                Button(_inspector, "Remove definition", () => Edit("Remove status", () => {
                    _document.Statuses.Remove(statusSelection); _selectedNode = null;
                }, true));
            }
            if (_document.Policies.Any(p => p.Id == _selectedNode)) DrawPolicies(_inspector, true);
            if (resourceSelection == null && statusSelection == null && !_document.Policies.Any(p => p.Id == _selectedNode))
                Body(_inspector, "Select a resource, status or policy to edit its definition and inspect references.", "studio-subtle");
        }
        private void DrawResourceReferences(string id)
        {
            Section(_inspector, "REFERENCES");
            foreach (var action in _document.Actions.Where(a => a.ResourceKey == id || a.Command?.CounterKey == id))
            { var selected = action.Id; Button(_inspector, action.Name ?? ActionSentence(action), () => SelectSource(selected)); }
            foreach (var policy in _document.Policies.Where(p => p.Definition.ResourceKey == id))
            { var selected = policy.Id; Button(_inspector, policy.Name, () => Navigate("resources", selected)); }
        }
    }
}
#endif
