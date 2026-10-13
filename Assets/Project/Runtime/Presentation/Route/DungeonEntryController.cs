using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Route
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class DungeonEntryController : MonoBehaviour
    {
        public event Action<RunState> RunStarted;
        [SerializeField] private int baseCompletionDiamonds = 320;
        private string _userId, _version, _hash, _unavailable;
        private List<CharacterDefinition> _catalog;
        private DropdownField _phase, _subPhase, _mode;
        private sealed class Stage
        {
            public readonly string Card, Series;
            public Stage(string card, string series) { Card = card; Series = series; }
        }
        private static readonly Stage[] Stages = {
            new Stage("open-circuit", null), new Stage("kof", "series.kof"),
            new Stage("tekken", "series.tekken"), new Stage("soul-calibur", "series.soulcalibur"),
            new Stage("genshin-impact", "series.genshin-impact") };
        private string _selectedStage = "open-circuit";
        private bool _busy, _initialized;
        private VisualElement _root, _modal, _browser;
        private SliderInt _difficulty;
        private Label _status, _description, _reward;
        private Button _start;
        public string SelectedStageId => _selectedStage;
        private DungeonProfile Profile => DungeonFlowContext.CreateEntryProfile(Stages.First(s => s.Card == _selectedStage).Series, _phase.index + 1, _subPhase.index, seriesBoss: _mode?.index == 1);

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _initialized = false; _busy = false;
            _root.schedule.Execute(InitializeUI).ExecuteLater(1);
        }
        private void InitializeUI()
        {
            if (!isActiveAndEnabled || _initialized) return;
            _browser = _root.Q("series-browser"); _modal = _root.Q("configuration-overlay");
            _phase = _root.Q<DropdownField>("phase-filter"); _subPhase = _root.Q<DropdownField>("subphase-filter");
            _phase.choices = new List<string>(DungeonProfile.PhaseNames); _phase.index = 0;
            _subPhase.choices = new List<string>(DungeonProfile.SubPhaseNames); _subPhase.index = 0;
            // These copy labels are optional in the current compact dungeon entry layout.
            _status = _root.Q<Label>("entry-status"); _description = _root.Q<Label>("profile-description");
            _reward = _root.Q<Label>("reward-preview"); _difficulty = _root.Q<SliderInt>("difficulty-slider");
            _difficulty.lowValue = 1; _difficulty.highValue = 5; _difficulty.SetValueWithoutNotify(1);
            _start = _root.Q<Button>("start-run-button"); _start.clicked += StartRun;
            _phase.RegisterValueChangedCallback(_ => Refresh()); _subPhase.RegisterValueChangedCallback(_ => Refresh());
            _mode = _root.Q<DropdownField>("dungeon-mode");
            if (_mode != null) { _mode.choices = new List<string> { "Route Dungeon", "Series Challenge Boss" }; _mode.index = 0; _mode.RegisterValueChangedCallback(_ => Refresh()); }
            _difficulty.RegisterValueChangedCallback(_ => Refresh());
            foreach (var stage in Stages)
                _root.Q<Button>(stage.Card).clicked += () => SelectDungeonStage(stage.Card);
            _root.Q<Button>("preview-details").clicked += OpenPreview;
            AccountHeaderView.Bind(_root, () =>
            {
                if (_busy) return;
                _busy = true; _root.SetEnabled(false); SceneManager.LoadScene("MainMenu");
            });
            _root.Q<Button>("close-configuration").clicked += CloseConfiguration;
            var cancel = _root.Q<Button>("cancel-configuration");
            if (cancel != null) cancel.clicked += CloseConfiguration;
            _modal.RegisterCallback<ClickEvent>(evt => { if (evt.target == _modal) CloseConfiguration(); });
            _root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || _modal.style.display.value != DisplayStyle.Flex) return;
                CloseConfiguration(); evt.StopPropagation();
            });
            _initialized = true; _modal.style.display = DisplayStyle.None; Refresh();
        }
        public void Bind(string userId, string contentVersion, string contentHash, ulong seed,
            IReadOnlyList<CharacterDefinition> catalog, IReadOnlyList<RunFighterSeed> roster)
        {
            _userId = userId; _version = contentVersion; _hash = contentHash;
            _catalog = catalog?.Select(c => c.Clone()).ToList(); _unavailable = null;
            if (!_initialized) return;
            Refresh();
        }
        public void SelectDungeonStage(string stageId)
        {
            if (_busy || !Stages.Any(s => s.Card == stageId)) return;
            _selectedStage = stageId; Refresh();
        }
        private void OpenPreview()
        {
            if (_busy) return;
            Refresh(); ScreenMotion.Show(_modal); _browser.SetEnabled(false); _root.Q<Button>("close-configuration").Focus();
        }
        private void CloseConfiguration()
        {
            if (_modal == null) return;
            ScreenMotion.Hide(_modal); _browser.SetEnabled(true); _root.Q<Button>("preview-details").Focus();
        }
        public void ShowUnavailable(string message) { _unavailable = message; Refresh(); }
        private void Refresh()
        {
            if (!_initialized) return;
            var profile = Profile;
            var enemies = _catalog?.Where(c => c.RuntimeReady && c.BaseStats != null && profile.Accepts(c)).ToList() ?? new List<CharacterDefinition>();
            var owned = PlayerInventoryService.Instance?.GetOwnedDefinitions().Where(c => c.RuntimeReady && DungeonFlowContext.IsCharacterEligible(profile, c)).ToList() ?? new List<CharacterObject>();
            foreach (var stage in Stages)
            {
                var stageProfile = DungeonFlowContext.CreateEntryProfile(stage.Series, _phase.index + 1, _subPhase.index);
                var count = _catalog?.Count(c => c.RuntimeReady && c.BaseStats != null && stageProfile.Accepts(c)) ?? 0;
                _root.Q<Button>(stage.Card).EnableInClassList("selected", stage.Card == _selectedStage);
                _root.Q<Label>(stage.Card + "-availability").text = count == 0 ? "NO READY FIGHTERS IN THIS FILTER" : count + " READY FIGHTERS";
            }
            _root.Q<Label>("configuration-title").text = profile.DisplayName;
            _root.Q<Label>("difficulty-value").text = DungeonDifficulty.Display(_difficulty.value);
            _root.Q<Label>("series-status").text = _unavailable ?? string.Empty;
            AccountHeaderView.Refresh(_root);
            _root.Q<Label>("stage-route-info").text = profile.Mode == DungeonMode.SeriesChallengeBoss ? "SERIES BOSS · EXCLUSIVE RELIC CORE" : profile.Rows.Count + " ROUTE ROWS · BRANCHING PATHS";
            var difficulty = DungeonDifficulty.BonusPercent(_difficulty.value);
            _reward.text = "REWARD PREVIEW";
            _start.SetEnabled(!_busy && _unavailable == null && enemies.Select(c => c.Id).Distinct().Count() >= 4 && owned.Count >= 1);
            if (_status != null) _status.text = _unavailable ?? (enemies.Count == 0 ? "No runtime-ready fighters in this dungeon's stage filter yet." :
                enemies.Select(c => c.Id).Distinct().Count() < 4 ? "This stage filter needs four distinct runtime-ready enemy fighters." :
                owned.Count < 1 ? "Summon an eligible fighter to enter." :
                "Choose your route, then prepare your team.");
            if (profile.Mode == DungeonMode.SeriesChallengeBoss && string.IsNullOrEmpty(profile.SeriesId))
            { _start.SetEnabled(false); if (_status != null) _status.text = "Choose a specific series for Series Challenge Boss."; }
            ItemRewardView.Populate(_root.Q("dungeon-reward-grid"), DungeonRewards.Preview(profile, enemies.Select(c => c.SeriesId), difficulty), _root,
                rule: item => DungeonRewards.PreviewRule(item, difficulty, profile.Mode == DungeonMode.SeriesChallengeBoss ? RouteNodeType.Boss : RouteNodeType.Battle, profile),
                previewArtifactRarity: DungeonRewards.HighestArtifact(difficulty), previewOnly: true, previewArtifactSets: DungeonRewards.ArtifactBasket(profile.Phase, profile.SubPhase));
            var assets = PlayerInventoryService.Instance?.GetCatalog() ?? CharacterObjectRegistrySO.LoadAll();
            // Filter series and inclusive stage cutoff first. Ownership/readiness affects overlays, never membership or rarity order.
            var matching = assets.Where(c => c != null && DungeonFlowContext.IsCharacterEligible(profile, c))
                .OrderByDescending(c => c.FighterRarity).ThenBy(c => c.FighterName, StringComparer.Ordinal).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).ToList();
            var readyIds = new HashSet<string>(enemies.Select(c => c.Id));
            var ownedIds = new HashSet<string>(owned.Select(c => c.DefinitionId));
            Func<CharacterObject, string> unavailable = c => !c.RuntimeReady || !readyIds.Contains(c.DefinitionId) ? "Not runtime ready" :
                !ownedIds.Contains(c.DefinitionId) ? "Not owned" : null;
            CharacterGridView.Populate(_root.Q("dungeon-preview-grid"), matching, unavailableReason: unavailable, iconSize: 84,
                emptyMessage: "No characters in this stage filter.");
            if (_description != null) _description.text = profile.DisplayName + " · Through " + DungeonProfile.PhaseNames[_phase.index] + " / " + DungeonProfile.SubPhaseNames[_subPhase.index] + " (includes earlier stages)" +
                " · " + matching.Count + " eligible characters · " + matching.Count(c => unavailable(c) == null) + " usable.";
        }
        private void StartRun()
        {
            if (_busy || !_start.enabledSelf || _catalog == null) return;
            _busy = true; _start.SetEnabled(false);
            try
            {
                var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0); var profile = Profile;
                var run = DungeonRunEngine.CreateRun(Guid.NewGuid().ToString("N"), _userId, Guid.NewGuid().ToString("N"),
                    profile, DungeonDifficulty.BonusPercent(_difficulty.value), seed, _version, _hash, new List<RunFighterSeed>(), _catalog, baseCompletionDiamonds, deferFormation: true);
                DungeonFlowContext.BeginDungeonFlow(profile, DungeonDifficulty.BonusPercent(_difficulty.value), _userId, seed, _version, _hash, _catalog, baseCompletionDiamonds);
                new LocalRunStateStore().Save(run); RunStarted?.Invoke(run);
            }
            catch (Exception ex) { _busy = false; Refresh(); if (_status != null) _status.text = ex.Message; else Debug.LogException(ex, this); }
        }
    }
}
