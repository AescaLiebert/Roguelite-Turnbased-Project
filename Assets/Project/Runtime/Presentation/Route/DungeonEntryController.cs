using System;
using System.Collections.Generic;
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
        private UIDocument _document;
        private string _userId;
        private string _contentVersion;
        private string _contentHash;
        private ulong _seed;
        private List<CharacterDefinition> _catalog;
        private string _catalogValidationError;
        private string _externalUnavailableMessage;
        private List<RunFighterSeed> _roster;
        private DungeonProfile _profile;
        private SliderInt _difficulty;
        private Label _reward;
        private Label _profileDescription;
        private Label _status;
        private Button _openCircuit;
        private Button _greenAccord;
        private Button _womenExhibition;
        private Button _start;

        private void Awake() { _document = GetComponent<UIDocument>(); }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            var root = _document.rootVisualElement;
            _difficulty = root.Q<SliderInt>("difficulty-slider");
            _reward = root.Q<Label>("reward-preview");
            _profileDescription = root.Q<Label>("profile-description");
            _status = root.Q<Label>("entry-status");
            _openCircuit = root.Q<Button>("profile-open");
            _greenAccord = root.Q<Button>("profile-green");
            _womenExhibition = root.Q<Button>("profile-women");
            _start = root.Q<Button>("start-run-button");
            if (_difficulty != null) _difficulty.RegisterValueChangedCallback(evt =>
            {
                var snapped = Mathf.Clamp(Mathf.RoundToInt(evt.newValue / 5f) * 5, 0, 100);
                if (_difficulty.value != snapped) _difficulty.SetValueWithoutNotify(snapped);
                RefreshPreview();
            });
            if (_openCircuit != null) _openCircuit.clicked += () => SelectProfile(DungeonProfile.OpenCircuit());
            if (_greenAccord != null) _greenAccord.clicked += () => SelectProfile(DungeonProfile.GreenAccord());
            if (_womenExhibition != null) _womenExhibition.clicked += () => SelectProfile(DungeonProfile.WomenExhibition());
            if (_start != null) _start.clicked += StartRun;
            RefreshProfileAvailability();
            RefreshPreview();
        }

        /// <summary>Supply frozen profile/content/loadout data from the app composition before enabling Start.</summary>
        public void Bind(string userId, string contentVersion, string contentHash, ulong seed,
            IReadOnlyList<CharacterDefinition> catalog, IReadOnlyList<RunFighterSeed> roster)
        {
            _userId = userId;
            _contentVersion = contentVersion;
            _contentHash = contentHash;
            _seed = seed;
            _externalUnavailableMessage = null;
            _catalog = catalog == null ? null : new List<CharacterDefinition>();
            if (catalog != null) foreach (var character in catalog) _catalog.Add(character.Clone());
            _catalogValidationError = null;
            if (_catalog == null) _catalogValidationError = "Pinned character catalog is missing.";
            else
            {
                var catalogErrors = ContentValidator.Validate(new ContentCatalog { Characters = _catalog });
                if (catalogErrors.Count > 0) _catalogValidationError = catalogErrors[0];
            }
            _roster = roster == null ? null : new List<RunFighterSeed>();
            if (roster != null)
                foreach (var fighter in roster)
                    _roster.Add(new RunFighterSeed { OwnedFighterId = fighter.OwnedFighterId,
                        Definition = fighter.Definition?.Clone(), ResolvedStats = fighter.ResolvedStats?.Clone(),
                        ConstellationTier = fighter.ConstellationTier, FormationSlot = fighter.FormationSlot,
                        IsReserve = fighter.IsReserve });
            RefreshProfileAvailability();
            RefreshPreview();
        }

        /// <summary>Disable entry and show a composition-level reason, such as unavailable runtime content.</summary>
        public void ShowUnavailable(string message)
        {
            _externalUnavailableMessage = string.IsNullOrWhiteSpace(message)
                ? "Dungeon entry is currently unavailable."
                : message;
            RefreshProfileAvailability();
        }

        private void SelectProfile(DungeonProfile profile)
        {
            _profile = profile;
            if (_profileDescription != null)
                _profileDescription.text = profile.DisplayName + (profile.Restrictions.Count == 0 ? " · any legal roster" : " · " + profile.Restrictions[0].Description);
            RefreshProfileAvailability();
            RefreshPreview();
        }

        private void RefreshProfileAvailability()
        {
            if (!string.IsNullOrEmpty(_externalUnavailableMessage))
            {
                if (_openCircuit != null) _openCircuit.SetEnabled(false);
                if (_greenAccord != null) _greenAccord.SetEnabled(false);
                if (_womenExhibition != null) _womenExhibition.SetEnabled(false);
                if (_start != null) _start.SetEnabled(false);
                if (_status != null) _status.text = _externalUnavailableMessage;
                return;
            }

            var open = DungeonProfile.OpenCircuit();
            var green = DungeonProfile.GreenAccord();
            var women = DungeonProfile.WomenExhibition();
            var canOpen = CatalogCanServe(open);
            var canGreen = CatalogCanServe(green);
            var canWomen = CatalogCanServe(women);
            if (_openCircuit != null) _openCircuit.SetEnabled(canOpen);
            if (_greenAccord != null) _greenAccord.SetEnabled(canGreen);
            if (_womenExhibition != null) _womenExhibition.SetEnabled(canWomen);

            var canProceed = _profile != null && CatalogCanServe(_profile);
            if (_start != null)
            {
                _start.SetEnabled(canProceed);
                _start.text = "Configure Team";
            }

            if (_status != null)
            {
                if (!string.IsNullOrEmpty(_catalogValidationError))
                    _status.text = "Dungeon catalog is unavailable: " + _catalogValidationError;
                else if (_profile == null)
                    _status.text = "Choose an available dungeon profile.";
                else if (!canProceed)
                    _status.text = "The pinned catalog needs four runtime-ready enemy definitions eligible for this profile.";
                else
                {
                    var restrictionDesc = _profile.Restrictions.Count == 0 ? "Any legal roster" : _profile.Restrictions[0].Description;
                    _status.text = $"{_profile.DisplayName}: {restrictionDesc}. Proceed to configure your team.";
                }
            }
        }

        private bool CatalogCanServe(DungeonProfile profile)
        {
            if (_catalog == null) return false;
            var candidates = new List<CharacterDefinition>();
            foreach (var character in _catalog)
            {
                if (character == null || !character.RuntimeReady || character.BaseStats == null) continue;
                if (profile.EligibleCharacterIds != null && profile.EligibleCharacterIds.Count > 0 &&
                    !profile.EligibleCharacterIds.Contains(character.Id)) continue;
                if (!candidates.Exists(candidate => candidate.Id == character.Id)) candidates.Add(character);
            }
            if (candidates.Count < 4) return false;
            foreach (var restriction in profile.Restrictions)
            {
                var matching = 0;
                foreach (var candidate in candidates)
                {
                    if (!string.IsNullOrEmpty(restriction.AttributeId) && candidate.AttributeId != restriction.AttributeId) continue;
                    if (!string.IsNullOrEmpty(restriction.TraitId) &&
                        (candidate.TraitIds == null || !candidate.TraitIds.Contains(restriction.TraitId))) continue;
                    matching++;
                }
                if (matching < restriction.MinimumCount) return false;
            }
            return true;
        }

        private bool MeetsRestrictions(DungeonProfile profile)
        {
            if (_roster == null || _catalog == null) return false;
            if (profile.EligibleCharacterIds != null && profile.EligibleCharacterIds.Count > 0)
                foreach (var fighter in _roster)
                    if (fighter?.Definition == null || !profile.EligibleCharacterIds.Contains(fighter.Definition.Id)) return false;
            foreach (var restriction in profile.Restrictions)
            {
                var matches = 0;
                foreach (var fighter in _roster)
                {
                    if (fighter?.Definition == null) continue;
                    var definition = _catalog.Find(character => character != null && character.Id == fighter.Definition.Id);
                    if (definition == null) continue;
                    if (!string.IsNullOrEmpty(restriction.AttributeId) && definition.AttributeId != restriction.AttributeId) continue;
                    if (!string.IsNullOrEmpty(restriction.TraitId) &&
                        (definition.TraitIds == null || !definition.TraitIds.Contains(restriction.TraitId))) continue;
                    matches++;
                }
                if (matches < restriction.MinimumCount) return false;
            }
            return true;
        }

        private void RefreshPreview()
        {
            if (_reward != null)
            {
                var difficulty = _difficulty == null ? 0 : _difficulty.value;
                var quote = (int)((long)baseCompletionDiamonds * (100 + difficulty) / 100);
                _reward.text = "Run completion reward · " + quote + " Diamonds";
            }
        }

        private void StartRun()
        {
            if (_profile == null) { ShowError("Choose a dungeon profile first."); return; }
            if (_catalog == null) { ShowError("Pinned dungeon catalog is not loaded yet."); return; }
            try
            {
                var difficulty = _difficulty == null ? 0 : _difficulty.value;
                DungeonFlowContext.BeginDungeonFlow(_profile, difficulty, _userId, _seed, _contentVersion, _contentHash, _catalog, baseCompletionDiamonds);
                SceneManager.LoadScene("Scene-CharacterLoadOut");
            }
            catch (Exception exception) { ShowError(exception.Message); }
        }

        private void ShowError(string message) { if (_status != null) _status.text = message ?? string.Empty; }
    }
}
