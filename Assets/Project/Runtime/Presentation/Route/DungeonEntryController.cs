using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
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
        private DropdownField _phase, _subPhase;
        private string _selectedSeries = "series.kof";
        private VisualElement _modal, _browser;
        private ScrollView _cards;
        private Label _seriesTitle, _difficultyValue, _browserStatus;
        private Button _selectedCard;
        private SliderInt _difficulty;
        private Label _status, _description, _reward;
        private Button _start;
        private DungeonProfile Profile => EnemyTemplateLibrarySO.ApplyResourcesTo(
            DungeonProfile.Filtered(_selectedSeries, _phase.index + 1, _subPhase.index));

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _cards = root.Q<ScrollView>("series-cards");
            _cards.contentViewport.RegisterCallback<GeometryChangedEvent>(evt => ResizeSeriesCards(evt.newRect.height));
            _browser = root.Q("series-browser");
            _modal = root.Q("configuration-overlay");
            _seriesTitle = root.Q<Label>("configuration-title");
            _browserStatus = root.Q<Label>("series-status");
            _difficultyValue = root.Q<Label>("difficulty-value");
            _phase = root.Q<DropdownField>("phase-filter");
            _subPhase = root.Q<DropdownField>("subphase-filter");
            _phase.choices = new List<string>(DungeonProfile.PhaseNames); _phase.index = 0;
            _subPhase.choices = new List<string>(DungeonProfile.SubPhaseNames); _subPhase.index = 0;
            _status = root.Q<Label>("entry-status"); _description = root.Q<Label>("profile-description");
            _reward = root.Q<Label>("reward-preview"); _difficulty = root.Q<SliderInt>("difficulty-slider");
            _start = root.Q<Button>("start-run-button"); _start.clicked += StartRun;
            _phase.RegisterValueChangedCallback(_ => Refresh());
            _subPhase.RegisterValueChangedCallback(_ => Refresh());
            _difficulty.RegisterValueChangedCallback(evt => { _difficulty.SetValueWithoutNotify(Mathf.RoundToInt(evt.newValue / 5f) * 5); Refresh(); });
            root.Q<Button>("back-menu").clicked += () => SceneManager.LoadScene("MainMenu");
            root.Q<Button>("close-configuration").clicked += CloseConfiguration;
            root.Q<Button>("cancel-configuration").clicked += CloseConfiguration;
            _modal.RegisterCallback<ClickEvent>(evt => { if (evt.target == _modal) CloseConfiguration(); });
            root.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { CloseConfiguration(); evt.StopPropagation(); } });
            CloseConfiguration();
            RenderSeries();
            Refresh();
        }

        public void Bind(string userId, string contentVersion, string contentHash, ulong seed,
            IReadOnlyList<CharacterDefinition> catalog, IReadOnlyList<RunFighterSeed> roster)
        {
            _userId = userId; _version = contentVersion; _hash = contentHash;
            _catalog = catalog?.Select(c => c.Clone()).ToList(); _unavailable = null;
            RenderSeries();
            Refresh();
        }
        private static string SeriesName(string series) => series.Replace("series.", "").ToUpperInvariant();
        private void RenderSeries()
        {
            if (_cards == null) return;
            _cards.Clear();
            var seriesIds = _catalog?.Select(c => c.SeriesId).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s).ToList()
                ?? new List<string>();
            if (seriesIds.Count == 0) seriesIds.Add("series.kof");
            foreach (var series in seriesIds)
            {
                var card = new Button(); card.AddToClassList("series-card");
                card.name = "series-card-" + series.Replace("series.", "");
                card.tooltip = "Configure " + SeriesName(series) + " dungeon";
                var fighters = PlayerInventoryService.Instance?.GetCatalog().Where(c => c != null && c.RuntimeReady && c.SeriesId == series).ToList();
                var featured = fighters?.FirstOrDefault(c => c.DefinitionId == "fighter.kyo94") ?? fighters?.FirstOrDefault();
                var art = new Image { sprite = featured == null ? null : featured.FighterPic != null ? featured.FighterPic : featured.FighterIcon,
                    scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                art.AddToClassList("series-art"); card.Add(art);
                var heading = new VisualElement(); heading.AddToClassList("series-heading");
                heading.Add(new Label(SeriesName(series)) { name = "series-name" });
                heading.Add(new Label("DUNGEON") { name = "series-subtitle" }); card.Add(heading);
                var footer = new VisualElement(); footer.AddToClassList("series-card-footer");
                footer.Add(new Label((fighters?.Count ?? 0) + " fighters · 9 phases"));
                footer.Add(new Label("CONFIGURE  →") { name = "series-action" }); card.Add(footer);
                card.clicked += () => OpenConfiguration(series, card);
                _cards.Add(card);
            }
            ResizeSeriesCards(_cards.contentViewport.layout.height);
        }
        private void ResizeSeriesCards(float viewportHeight)
        {
            if (float.IsNaN(viewportHeight) || viewportHeight <= 0) return;
            var height = Mathf.Clamp(viewportHeight - 50, 220, 470);
            foreach (var card in _cards.contentContainer.Children())
            {
                card.style.height = height;
                card.style.width = Mathf.Max(200, height * 300f / 470f);
            }
        }
        private void OpenConfiguration(string series, Button card)
        {
            _selectedSeries = series; _selectedCard = card;
            _seriesTitle.text = SeriesName(series) + " / DUNGEON";
            _modal.style.display = DisplayStyle.Flex;
            _browser.SetEnabled(false);
            Refresh(); _phase.Focus();
        }
        private void CloseConfiguration()
        {
            if (_modal == null) return;
            _modal.style.display = DisplayStyle.None;
            _browser.SetEnabled(true);
            _selectedCard?.Focus();
        }
        public void ShowUnavailable(string message) { _unavailable = message; Refresh(); }
        private void Refresh()
        {
            if (_start == null) return;
            var profile = Profile;
            _browserStatus.text = _unavailable ?? "Select a series to configure your dungeon.";
            _difficultyValue.text = "+" + _difficulty.value + "% enemy strength";
            var count = _catalog?.Count(c => c.RuntimeReady && profile.Accepts(c)) ?? 0;
            var owned = PlayerInventoryService.Instance?.GetOwnedDefinitions().Count(c => c.RuntimeReady && DungeonFlowContext.IsCharacterEligible(profile, c)) ?? 0;
            _description.text = profile.DisplayName + " · " + (_phase.index + 1) + _subPhase.index + "XX · " +
                (_catalog == null ? "Enemy catalog unavailable" : count + " enemy fighters") + " · " + owned + " owned fighters";
            _reward.text = "Completion reward · " + ((long)baseCompletionDiamonds * (100 + _difficulty.value) / 100) + " Diamonds";
            _start.SetEnabled(_unavailable == null && count >= 4 && owned > 0);
            _status.text = _unavailable ?? (count < 4 ? "This category needs four distinct runtime-ready enemy fighters." : owned == 0 ? "Summon a fighter in this category to enter." : "A new seed and enemy formation will be saved for this run. Choose your route, then prepare your team.");
        }
        private void StartRun()
        {
            if (!_start.enabledSelf || _catalog == null) return;
            try
            {
                var seed = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
                var profile = Profile;
                var run = DungeonRunEngine.CreateRun(Guid.NewGuid().ToString("N"), _userId, Guid.NewGuid().ToString("N"),
                    profile, _difficulty.value, seed, _version, _hash, new List<RunFighterSeed>(), _catalog, baseCompletionDiamonds, deferFormation: true);
                DungeonFlowContext.BeginDungeonFlow(profile, _difficulty.value, _userId, seed, _version, _hash, _catalog, baseCompletionDiamonds);
                new LocalRunStateStore().Save(run);
                RunStarted?.Invoke(run);
            }
            catch (Exception ex) { _status.text = ex.Message; }
        }
    }
}
