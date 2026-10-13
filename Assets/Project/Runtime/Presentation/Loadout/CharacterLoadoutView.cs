using System;
using System.Linq;
using FightingAllstar.Presentation.Route;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Loadout
{
    [RequireComponent(typeof(UIDocument), typeof(CharacterSelectionManager))]
    public sealed class CharacterLoadoutView : MonoBehaviour
    {
        private VisualElement _root;
        private CharacterSelectionManager _manager;
        private int _selected;
        private bool _initialized;
        private Vector2 _pointerStart;
        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _initialized = false;
            _root.schedule.Execute(InitializeUI).ExecuteLater(1);
        }
        private void InitializeUI()
        {
            if (_initialized || !isActiveAndEnabled) return;
            _initialized = true; _manager = GetComponent<CharacterSelectionManager>();
            _manager.EnsureInitialized();
            AccountHeaderView.Bind(_root, _manager.OnBackButtonClicked);
            _root.Q<TextField>("fighter-search").RegisterValueChangedCallback(_ => RefreshCollection());
            _root.Q<Button>("clear-position").clicked += () => Assign(null);
            _root.Q<Button>("loadout-play").clicked += () =>
            {
                var scene = SceneManager.GetActiveScene().name;
                _manager.OnPlayButtonClicked();
                if (SceneManager.GetActiveScene().name == scene) SetOptionalLabelText("loadout-status", _manager.TeamStatus);
            };
            Refresh();
        }
        private void Assign(CharacterObject character)
        {
            if (!_manager.TryAssignToSlot(_selected, character, out var error))
            { SetOptionalLabelText("loadout-status", error); return; }
            Refresh();
        }
        private void Refresh()
        {
            var profile = DungeonFlowContext.ActiveProfile;
            var policyLabel = _root.Q<Label>("loadout-policy");
            if (policyLabel != null)
                policyLabel.text = profile == null ? "Three active positions and one optional reserve." :
                    profile.DisplayName + " · " + string.Join(" · ", profile.Restrictions.Select(r => r.Description));
            var slots = _root.Q("formation-slots"); slots.Clear();
            for (var i = 0; i < 4; i++)
            {
                var index = i; var character = _manager.GetSlot(i)?.AssignedCharacter;
                var button = new Button(() => { _selected = index; Refresh(); });
                button.name = "formation-position-" + i; button.userData = i;
                button.AddToClassList("formation-slot"); button.EnableInClassList("selected", i == _selected);
                button.EnableInClassList("reserve-position", i == 3);
                var role = new Label(i == 3 ? "RESERVE" : "ACTIVE " + (i + 1)); role.AddToClassList("formation-role"); button.Add(role);
                var body = new VisualElement(); body.AddToClassList("formation-body"); button.Add(body);
                var icon = new VisualElement(); icon.AddToClassList("formation-icon"); body.Add(icon);
                var details = new VisualElement(); details.AddToClassList("formation-details"); body.Add(details);
                if (character == null)
                {
                    var empty = new VisualElement(); empty.AddToClassList("empty-position"); empty.Add(new Label("+")); icon.Add(empty);
                }
                else icon.Add(CharacterIconView.CreateFilling(character, i == 3 ? "SUB" : null));
                var name = new Label(character == null ? "EMPTY POSITION" : character.FighterName); name.AddToClassList("formation-name"); details.Add(name);
                if (character != null)
                {
                    var owned = PlayerInventoryService.Instance?.FindOwnedByDefinition(character.DefinitionId);
                    var runFighter = DungeonFlowContext.ActiveRun?.Roster.Find(f => f.DefinitionId == character.DefinitionId);
                    var resolved = LoadoutStats.Resolve(character);
                    var health = resolved.MaxHealth.ToString("N0");
                    var stats = new Label("Lv. " + (owned?.level ?? 1) + " · C" + (owned?.constellationTier ?? 0) + "\nHP " + health +
                        "\nATK " + resolved.Attack.ToString("N0") + " · DEF " + resolved.Defense.ToString("N0"));
                    stats.AddToClassList("formation-character-stats"); details.Add(stats);
                    var healthBar = new VisualElement { name = "formation-health-" + i }; healthBar.AddToClassList("formation-health-bar");
                    var fill = new VisualElement(); fill.AddToClassList("formation-health-fill");
                    var remaining = runFighter == null ? resolved.MaxHealth : runFighter.IsDefeated ? 0 : runFighter.CurrentHealth;
                    fill.style.width = Length.Percent(100 * Mathf.Clamp01(remaining / (float)Math.Max(1, resolved.MaxHealth)));
                    healthBar.tooltip = "Remaining HP " + remaining.ToString("N0"); healthBar.Add(fill); details.Add(healthBar);
                    button.tooltip = character.FighterName + "\n" + stats.text;
                }
                button.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) { _pointerStart = evt.position; button.CapturePointer(evt.pointerId); } });
                button.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (!button.HasPointerCapture(evt.pointerId)) return;
                    button.ReleasePointer(evt.pointerId);
                    if (Vector2.Distance(_pointerStart, evt.position) < 10) return;
                    var hit = _root.panel?.Pick(evt.position);
                    while (hit != null && !hit.ClassListContains("formation-slot")) hit = hit.parent;
                    if (hit?.userData is int target && target != index)
                    { _manager.SwapFormationPositions(index, target); _selected = target; Refresh(); evt.StopPropagation(); }
                });
                slots.Add(button);
            }
            var assigned = Enumerable.Range(0, 4).Select(i => _manager.GetSlot(i)?.AssignedCharacter).Where(c => c != null).ToList();
            var teamCC = LoadoutStats.TeamCombatClass(assigned);
            var enemyCC = LoadoutStats.EnemyCombatClass;
            var ccLabel = _root.Q<Label>("formation-stats");
            ccLabel.text = LoadoutStats.Comparison(teamCC, enemyCC);
            ccLabel.EnableInClassList("cc-first", assigned.Count > 0 && teamCC >= enemyCC);
            ccLabel.EnableInClassList("cc-second", assigned.Count == 0 || teamCC < enemyCC);
            ccLabel.tooltip = teamCC >= enemyCC && assigned.Count > 0 ? "Your team acts first." : "The enemy team acts first.";
            SetOptionalLabelText("loadout-status", "Selected " + (_selected == 3 ? "reserve" : "active position " + (_selected + 1)) + ". Choose a fighter to assign.");
            RefreshCollection();
        }
        private void SetOptionalLabelText(string name, string value)
        {
            var label = _root?.Q<Label>(name);
            if (label != null) label.text = value;
        }
        private void RefreshCollection()
        {
            var search = _root.Q<TextField>("fighter-search").value?.Trim() ?? string.Empty;
            var profile = DungeonFlowContext.ActiveProfile;
            var owned = PlayerInventoryService.Instance?.GetOwnedDefinitions().Where(c => c != null && DungeonFlowContext.IsCharacterEligible(profile, c)).ToList();
            var visible = owned?.Where(c => string.IsNullOrEmpty(search) || c.FighterName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(DungeonFlowContext.IsCharacterDefeated).ThenBy(c => c.FighterName).ToList();
            _root.Q<Label>("collection-count").text = (visible?.Count ?? 0) + " / " + (owned?.Count ?? 0) + " eligible owned fighters";
            CharacterGridView.Populate(_root.Q("collection-grid"), visible, Assign, c => c.RuntimeReady,
                c => DungeonFlowContext.IsCharacterDefeated(c) ? "DEFEATED" : c.RuntimeReady ? null : "DRAFT",
                "No owned fighters match this dungeon and search.",
                unavailableReason: c => DungeonFlowContext.IsCharacterDefeated(c) ? "Defeated for this run" : null,
                defeated: DungeonFlowContext.IsCharacterDefeated);
        }
    }
}
