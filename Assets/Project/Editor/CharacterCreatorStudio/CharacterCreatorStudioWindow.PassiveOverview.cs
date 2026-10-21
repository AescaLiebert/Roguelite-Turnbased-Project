#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [UnityEngine.SerializeField] private string _passiveMechanic;
        private void BuildKitPassive()
        {
            var c = EditableKit();
            KitHeader("Passive Overview", "Choose a mechanic to edit. Collapse it to return to the card overview.", false);
            if (c.Passive == null) { Button(_host, "Create passive", () => Edit("Create passive", () => { c.Passive = new PassiveDefinition { Id = c.Id + ".passive" }; c.PassiveId = c.Passive.Id; }, true)); return; }
            if (c.PassiveSource == null) c.PassiveSource = new PassiveSourceDefinition();
            _inspector.AddToClassList("studio-ability-inspector");
            BuildNativePassiveDescriptionPreview(_inspector);
            Button(_inspector, "Open Battle Lab", () => ShowWorkspace("lab"));
            BuildPassiveAssetSaveFoldout(_inspector);
            var wording = new Foldout { text = "Description wording", value = false, name = "studio-passive-wording-foldout" };
            _inspector.Add(wording);
            BuildNativeWordingEditor(wording, NativeWordingTarget("Passive"));
            BuildPassiveAvailability(_host);
            var row = Row(_host);
            Button(row, "+ Event rule", AddRule).name = "studio-passive-add-event";
            Button(row, "+ Continuous rule", () => Edit("Add continuous rule", () => {
                var aura = new PassiveAuraDefinition { Id = "aura." + Guid.NewGuid().ToString("N"), Gate = NewPassiveGate() };
                c.Passive.Auras.Add(aura); _passiveMechanic = aura.Id;
            }, true));
            Button(row, "Interception policies", () => ShowWorkspace("resources"));
            Button(row, "Collapse all", () => { _passiveMechanic = null; Rebuild(); });
            var diagnostics = _compiled?.Diagnostics.Where(d => d.Severity != StudioDiagnosticSeverity.Info).ToArray();
            if (diagnostics?.Length > 0) Body(_host, diagnostics.Length + " diagnostic(s) in this character. Inspect Problems before saving.", "studio-warning");
            Section(_host, "ALL PASSIVE MECHANICS");
            var inventory = new VisualElement { name = "studio-passive-inventory" }; _host.Add(inventory);
            StudioPassiveOverviewPresenter.Build(inventory, StudioPassiveView.Mechanics(_document), _passiveMechanic,
                (mechanic, expanded) => { _passiveMechanic = expanded ? mechanic.Id : null; Rebuild(); }, BuildPassiveMechanicEditor);
            if (StudioPassiveView.Mechanics(_document).Count == 0) Body(_host, "No mechanics. Add an event or continuous rule.", "studio-subtle");
        }

        private void BuildPassiveMechanicEditor(VisualElement parent, StudioPassiveMechanicView mechanic)
        {
            var c = EditableKit();
            var eventRule = _document.Rules.FirstOrDefault(r => r.Id == mechanic.Id);
            if (eventRule != null)
            {
                Body(parent, StudioPassiveView.Rule(eventRule).Execution, "studio-subtle");
                Button(parent, "Open trigger, conditions & actions", () => { _selectedRule = eventRule.Id; _ruleSection = "when"; Navigate("rules", eventRule.Id); }).name = "passive.edit." + eventRule.Id;
                return;
            }
            var auraSelection = c.Passive.Auras.FirstOrDefault(a => a.Id == _passiveMechanic);
            if (auraSelection != null)
            {
                var box = new VisualElement { name = "studio-kit-aura" }; parent.Add(box);
                var settings = PassiveEditorGrid(box);
                var when = Node(settings, "WHEN · AVAILABILITY"); DrawGate(when, auraSelection.Gate);
                var who = Node(settings, "WHO · RECIPIENTS"); DrawFilter(who, auraSelection.Targets);
                Section(box, "DO · MODIFIERS");
                var modifiers = PassiveEditorGrid(box);
                foreach (var modifier in auraSelection.Modifiers.ToArray())
                { var m = Node(modifiers, "MODIFIER"); DrawKitModifier(m, modifier, true); Button(m, "Remove modifier", () => Edit("Remove modifier", () => auraSelection.Modifiers.Remove(modifier), true)); }
                Button(box, "+ Modifier", () => Edit("Add modifier", () => auraSelection.Modifiers.Add(new StatModifierDefinition()), true));
                BuildPassiveScaling(box, auraSelection);
                Button(box, "Remove continuous rule", () => Edit("Remove continuous rule", () => c.Passive.Auras.Remove(auraSelection), true));
            }
            var nativePolicy = c.Passive.Policies.FirstOrDefault(p => p.Id == _passiveMechanic);
            if (nativePolicy != null)
            {
                var box = Node(parent, "INTERCEPTION POLICY"); DrawGate(box, nativePolicy.Gate);
                KitMembers(box, nativePolicy, "passive.policy." + nativePolicy.Id, 0, "Id", "Gate");
                Button(box, "Remove policy", () => Edit("Remove native policy", () => c.Passive.Policies.Remove(nativePolicy), true));
            }
            if (_document.Policies.Any(p => p.Id == _passiveMechanic)) Button(parent, "Edit selected interception policy", () => ShowWorkspace("resources"));
            if (_passiveMechanic?.StartsWith("compatibility.support.", StringComparison.Ordinal) == true && c.Passive.Support != null)
            {
                var box = Node(parent, "COMPATIBILITY SUPPORT · READ ONLY"); box.name = "studio-passive-support";
                var clause = StudioPassiveView.Mechanics(_document).FirstOrDefault(m => m.Id == _passiveMechanic);
                if (clause != null) { Heading(box, clause.Name); Body(box, clause.Summary); }
                Body(box, "This preserved block executes dedicated calculation/team hooks. It cannot be replaced automatically without clause-by-clause parity, including forecast. New calculation rules support synchronous fixed modifiers; they do not yet reproduce action-start target snapshots.", "studio-warning");
                var fields = new Foldout { text = "Preserved declaration · read only", value = false }; box.Add(fields); KitMembers(fields, c.Passive.Support, "passive.support", 0); fields.SetEnabled(false);
                Button(box, "Remove this compatibility mechanic", () => Edit("Remove support clause", () => StudioPassiveEditing.RemoveSupportClause(_document, _passiveMechanic), true)).name = "studio-passive-remove-support";
            }
            var compatibility = c.Passive.Reactions.FirstOrDefault(r => r.Id == _passiveMechanic && !_document.ReplacedNativeReactionIds.Contains(r.Id));
            if (compatibility != null)
            {
                Body(parent, "This native declaration cannot yet be imported losslessly. Its timing/program is preserved read-only; no competing raw trigger editor is exposed.", "studio-warning");
                Button(parent, "Remove compatibility declaration", () => Edit("Remove compatibility reaction", () => c.Passive.Reactions.Remove(compatibility), true));
            }
        }

        private VisualElement PassiveEditorGrid(VisualElement parent)
        {
            var grid = new VisualElement(); grid.AddToClassList("studio-passive-editor-grid"); parent.Add(grid);
            grid.RegisterCallback<GeometryChangedEvent>(e => grid.EnableInClassList("studio-passive-grid-narrow", e.newRect.width < 640));
            return grid;
        }

        private void BuildPassiveScaling(VisualElement parent, PassiveAuraDefinition aura)
        {
            var box = Node(parent, "SCALING");
            var fields = new VisualElement(); box.Add(fields);
            Action render = null;
            render = () => {
                fields.Clear();
                EnumControl(fields, "Scale with", aura.Scaling, v => { aura.Scaling = (PassiveScaling)v; render(); });
                if (aura.Scaling == PassiveScaling.FieldStatusStacks || aura.Scaling == PassiveScaling.OwnerCounter)
                    TextControl(fields, aura.Scaling == PassiveScaling.OwnerCounter ? "Counter key" : "Status tag", aura.ScalingKey, v => aura.ScalingKey = v);
                if (aura.Scaling == PassiveScaling.FieldStatusStacks)
                    EnumControl(fields, "Count stacks on", aura.ScalingRelation, v => aura.ScalingRelation = (PassiveRelation)v);
                if (aura.Scaling == PassiveScaling.OwnerStat)
                    EnumControl(fields, "Source stat", aura.SourceStat, v => aura.SourceStat = (StatId)v);
                if (aura.Scaling != PassiveScaling.Constant) IntControl(fields, "Maximum units", aura.MaximumUnits, v => aura.MaximumUnits = v);
                ApplyEditorFont();
            }; render();
        }

        private void BuildPassiveAssetSaveFoldout(VisualElement parent)
        {
            var assets = new Foldout { text = "Asset & save", value = false, name = "studio-passive-asset-save" };
            assets.AddToClassList("studio-passive-asset-save");
            parent.Add(assets);
            Section(assets, "SOURCE & OWNERSHIP");
            Body(assets, _document.SourceCharacterId, "studio-code");
            Body(assets, _draft.IsDirectSession
                ? "Unsaved edits stay in memory until you use Save character or Ctrl+S to commit this CharacterObject and its four referenced data SOs."
                : "Compatibility draft: edits remain separate until native publication.", "studio-subtle");
            Button(assets, "Create new character", () => ShowWorkspace("bundle"));
            Button(assets, "Test in Battle Lab", () => ShowWorkspace("lab"));
        }

        private PassiveGate NewPassiveGate()
        {
            var metadata = _document.EditingCharacter?.PassiveSource;
            var source = CharacterPassiveMetadata.ParseSourceType(metadata?.SourceType);
            var mode = CharacterPassiveMetadata.ParseRestriction(metadata?.Restriction);
            var gate = new PassiveGate {
                Presence = source == PassiveSourceType.Sub || source == PassiveSourceType.SubPvpOnly ? PassivePresence.LivingReserve : PassivePresence.LivingActive,
                Modes = mode == PassiveRestriction.PvP ? BattleModeMask.PvP : mode == PassiveRestriction.PvE ? BattleModeMask.PvE :
                    mode == PassiveRestriction.All ? BattleModeMask.All : source == PassiveSourceType.SubPvpOnly ? BattleModeMask.PvP : BattleModeMask.All
            };
            var existing = StudioPassiveView.Gates(_document).Where(g => g != null).ToArray();
            var positions = existing.Select(g => g.Presence).Distinct().ToArray();
            var modes = existing.Select(g => g.Modes).Distinct().ToArray();
            if (positions.Length == 1 && positions[0] != PassivePresence.LivingRoster) gate.Presence = positions[0];
            if (modes.Length == 1) gate.Modes = modes[0];
            return gate;
        }

        private string PassivePositionChoice()
        {
            var positions = StudioPassiveView.Gates(_document).Where(g => g != null).Select(g => g.Presence).Distinct().ToArray();
            if (positions.Length == 0) positions = new[] { NewPassiveGate().Presence };
            return positions.Length != 1 ? "Mixed · per mechanic" : positions[0] == PassivePresence.LivingActive ? "On-Field" : positions[0] == PassivePresence.LivingReserve ? "Off-Field" : "Both positions (existing)";
        }

        private string PassiveModeChoice()
        {
            var modes = StudioPassiveView.Gates(_document).Where(g => g != null).Select(g => g.Modes).Distinct().ToArray();
            if (modes.Length == 0) modes = new[] { NewPassiveGate().Modes };
            return modes.Length != 1 ? "Mixed · per mechanic" : modes[0] == BattleModeMask.PvP ? "PVP" : modes[0] == BattleModeMask.PvE ? "PVE" : "ALL";
        }

        private void RefreshPassiveAvailability()
        {
            void Refresh(string name, string value)
            {
                var field = _host.Q<PopupField<string>>(name);
                if (field == null) return;
                if (!field.choices.Contains(value)) field.choices.Add(value);
                field.SetValueWithoutNotify(value);
            }
            Refresh("studio-passive-position", PassivePositionChoice());
            Refresh("studio-passive-mode", PassiveModeChoice());
        }

        private void BuildPassiveAvailability(VisualElement parent)
        {
            var box = Node(parent, "PASSIVE AVAILABILITY"); box.name = "studio-passive-availability-settings";
            var controls = new VisualElement(); controls.AddToClassList("studio-passive-availability-controls"); box.Add(controls);
            ChoiceControl(controls, "Active position", new List<string> { "On-Field", "Off-Field" },
                PassivePositionChoice(),
                v => { if (v == "On-Field" || v == "Off-Field") SetPassiveAvailability(v == "On-Field" ? PassivePresence.LivingActive : PassivePresence.LivingReserve, null); }, true);
            controls[controls.childCount - 1].name = "studio-passive-position";
            ChoiceControl(controls, "Battle mode", new List<string> { "ALL", "PVP", "PVE" },
                PassiveModeChoice(),
                v => { if (v == "ALL" || v == "PVP" || v == "PVE") SetPassiveAvailability(null, v == "PVP" ? BattleModeMask.PvP : v == "PVE" ? BattleModeMask.PvE : BattleModeMask.All); }, true);
            controls[controls.childCount - 1].name = "studio-passive-mode";
            if (_document.EditingCharacter?.Passive?.Support?.IsConfigured == true)
            {
                controls[controls.childCount - 1].SetEnabled(false);
                Body(box, "Preserved support hooks run in all battle modes. Migrate or remove those compatibility mechanics before changing the entire passive's battle mode.", "studio-subtle");
            }
            Body(box, "Changing either choice applies it to every character passive mechanic. Configure recipient filters inside each card.", "studio-subtle");
        }

        private void SetPassiveAvailability(PassivePresence? presence, BattleModeMask? modes)
            => StudioPassiveEditing.SetAvailability(_document, presence, modes);
    }
}
#endif
