using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Economy;
using FightingAllstar.Presentation.Content;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Economy
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class LocalEconomyController : MonoBehaviour
    {
        private UIDocument _document;
        private ContentCatalog _catalog;
        private BannerDefinition _banner;
        private LocalEconomyState _state;
        private LocalEconomyService _service;
        private LocalEconomyStore _store;
        private VisualElement _root;
        private VisualElement _pool;
        private VisualElement _collection;
        private VisualElement _rates;
        private VisualElement _purchasePanel;
        private VisualElement _upgradePanel;
        private VisualElement _selectorPanel;
        private VisualElement _selectorList;
        private VisualElement _inventory;
        private VisualElement _formation;
        private Button _playDungeon;
        private Label _wallet;
        private Label _guarantee;
        private Label _selectorCount;
        private Label _tokens;
        private Label _notice;
        private Label _results;
        private Label _purchaseSummary;
        private Label _upgradeSummary;
        private Label _selectorSummary;
        private Button _single;
        private Button _ten;
        private Button _confirmPurchase;
        private Button _confirmUpgrade;
        private Button _confirmSelector;
        private int _pendingCount;
        private string _pendingUpgradeCharacter;
        private string _pendingSelectorId;
        private string _pendingSelectorCharacter;
        private CharacterGrantOutcome _pendingSelectorGrant;
        private bool _initialized;

        private void Awake() { _document = GetComponent<UIDocument>(); }

        private void OnEnable()
        {
            if (_initialized) return;
            if (_document == null) _document = GetComponent<UIDocument>();
            if (_document == null || _document.rootVisualElement == null) return;
            try { Initialize(); }
            catch (Exception exception)
            {
                var status = _document.rootVisualElement.Q<Label>("economy-notice");
                if (status != null) status.text = "Local economy unavailable: " + exception.Message;
                Debug.LogError("Local economy could not initialize: " + exception.Message, this);
            }
        }

        private void Initialize()
        {
            _catalog = CharacterObjectCatalogBuilder.Load(null);
            _banner = BannerDefinition.CreateKofPhaseE();
            _store = new LocalEconomyStore();
            _state = _store.LoadOrCreateLocalProfile();
            _service = new LocalEconomyService(_state, _banner, _catalog);
            _root = _document.rootVisualElement;
            _wallet = _root.Q<Label>("economy-wallet");
            _guarantee = _root.Q<Label>("economy-guarantee");
            _selectorCount = _root.Q<Label>("economy-selector-count");
            _tokens = _root.Q<Label>("economy-tokens");
            _notice = _root.Q<Label>("economy-notice");
            _results = _root.Q<Label>("summon-results");
            _pool = _root.Q<VisualElement>("banner-pool");
            _collection = _root.Q<VisualElement>("collection-panel");
            _rates = _root.Q<VisualElement>("rates-panel");
            _inventory = _root.Q<VisualElement>("collection-list");
            _formation = _root.Q<VisualElement>("formation-list");
            _playDungeon = _root.Q<Button>("play-dungeon");
            _selectorList = _root.Q<VisualElement>("selector-list");
            _purchasePanel = _root.Q<VisualElement>("purchase-review");
            _upgradePanel = _root.Q<VisualElement>("upgrade-review");
            _selectorPanel = _root.Q<VisualElement>("selector-review");
            _purchaseSummary = _root.Q<Label>("purchase-review-summary");
            _upgradeSummary = _root.Q<Label>("upgrade-review-summary");
            _selectorSummary = _root.Q<Label>("selector-review-summary");
            _single = _root.Q<Button>("summon-single");
            _ten = _root.Q<Button>("summon-ten");
            _confirmPurchase = _root.Q<Button>("confirm-summon");
            _confirmUpgrade = _root.Q<Button>("confirm-upgrade");
            _confirmSelector = _root.Q<Button>("confirm-selector");
            if (_single != null) _single.clicked += () => ReviewPurchase(1);
            if (_ten != null) _ten.clicked += () => ReviewPurchase(10);
            if (_root.Q<Button>("cancel-summon") is Button cancelSummon) cancelSummon.clicked += CancelPurchaseReview;
            if (_root.Q<Button>("cancel-upgrade") is Button cancelUpgrade) cancelUpgrade.clicked += () => Show(_upgradePanel, false);
            if (_root.Q<Button>("cancel-selector") is Button cancelSelector) cancelSelector.clicked += () => Show(_selectorPanel, false);
            if (_confirmPurchase != null) _confirmPurchase.clicked += ConfirmPurchase;
            if (_confirmUpgrade != null) _confirmUpgrade.clicked += ConfirmUpgrade;
            if (_confirmSelector != null) _confirmSelector.clicked += ConfirmSelector;
            if (_root.Q<Button>("tab-banner") is Button bannerTab) bannerTab.clicked += () => SelectTab("banner");
            if (_root.Q<Button>("tab-rates") is Button ratesTab) ratesTab.clicked += () => SelectTab("rates");
            if (_root.Q<Button>("tab-collection") is Button collectionTab) collectionTab.clicked += () => SelectTab("collection");
            if (_root.Q<Button>("tab-formation") is Button formationTab) formationTab.clicked += () => SelectTab("formation");
            if (_playDungeon != null) _playDungeon.clicked += EnterDungeon;
            SelectTab("banner");
            RenderPool();
            RenderRates();
            _initialized = true;
            Refresh();
        }

        private void ReviewPurchase(int count)
        {
            _pendingCount = count;
            var cost = count == 1 ? _banner.SinglePullPrice : _banner.TenPullPrice;
            var sufficient = _state.Diamonds >= cost;
            if (_purchaseSummary != null) _purchaseSummary.text = string.Format(
                "{0} pull{1} · {2} Diamonds\nBalance after purchase: {3}\nRates: 4% featured SSR, 36% SR, 60% R. Draws are independent.",
                count, count == 1 ? string.Empty : "s", cost, Math.Max(0, _state.Diamonds - cost));
            if (_confirmPurchase != null) _confirmPurchase.SetEnabled(sufficient);
            if (_single != null) _single.SetEnabled(false);
            if (_ten != null) _ten.SetEnabled(false);
            Show(_purchasePanel, true);
            SetNotice(sufficient ? "Review the exact cost before confirming." : "Not enough Diamonds. No currency has been spent.");
        }

        private void ConfirmPurchase()
        {
            if (_pendingCount != 1 && _pendingCount != 10) return;
            try
            {
                var count = _pendingCount;
                var receipt = _service.Summon(Guid.NewGuid().ToString("N"), count);
                _store.Save(_state);
                Show(_purchasePanel, false);
                _pendingCount = 0;
                if (_single != null) _single.SetEnabled(true);
                if (_ten != null) _ten.SetEnabled(true);
                RenderReceipt(receipt);
                Refresh();
                SetNotice("Local receipt committed. The offline prototype does not grant online entitlements.");
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private void CancelPurchaseReview()
        {
            Show(_purchasePanel, false);
            _pendingCount = 0;
            if (_single != null) _single.SetEnabled(true);
            if (_ten != null) _ten.SetEnabled(true);
            SetNotice("Purchase canceled. No currency has been spent.");
        }

        private void RenderReceipt(SummonReceipt receipt)
        {
            var lines = new List<string>();
            foreach (var draw in receipt.Draws)
            {
                var fighter = FindCharacter(draw.CharacterId);
                lines.Add(draw.Rarity + " · " + (fighter?.DisplayName ?? draw.CharacterId) + " — " + GrantLabel(draw.Grant));
            }
            var selectorLine = receipt.SelectorsEarned > 0
                ? "\nFeatured selector earned: " + receipt.SelectorsEarned + " (claim when ready)."
                : string.Empty;
            if (_results != null) _results.text = string.Format("Receipt {0}\n−{1} Diamonds · balance {2} · guarantee {3}/300\n{4}{5}",
                receipt.RequestId, receipt.Debit, receipt.BalanceAfter, receipt.GuaranteeAfter,
                string.Join("\n", lines), selectorLine);
        }

        private void RenderPool()
        {
            if (_pool == null) return;
            _pool.Clear();
            foreach (var entry in _banner.Pool)
            {
                var fighter = FindCharacter(entry.CharacterId);
                var row = new VisualElement();
                row.AddToClassList("pool-row");
                var name = new Label((fighter?.DisplayName ?? entry.CharacterId) + " · " + entry.Rarity);
                name.AddToClassList("pool-name");
                var rate = new Label(FormatRate(entry.IndividualRateBp));
                rate.AddToClassList("pool-rate");
                var readiness = new Label(fighter != null && fighter.RuntimeReady ? "READY" : "KIT DRAFT · BATTLE LOCKED");
                readiness.AddToClassList("draft-badge");
                row.Add(name);
                row.Add(rate);
                row.Add(readiness);
                _pool.Add(row);
            }
        }

        private void RenderRates()
        {
            if (_rates == null) return;
            _rates.Clear();
            AddRateLine("Featured SSR", _banner.SSRRateBp);
            AddRateLine("SR", _banner.SRRateBp);
            AddRateLine("R", _banner.RRateBp);
            _rates.Add(new Label("Each individual fighter rate is shown in the Banner tab. Draws are independent; there is no guaranteed SSR on a ten-pull."));
            _rates.Add(new Label("KOF pool: Athena 4%; Kyo, King, Mai, Shingo 9% each; Chin, Kensou, Benimaru 20% each."));
            _rates.Add(new Label("300-pull featured selector is a proposed milestone policy. Early SSRs do not reset it. Progress carries only within the KOF guarantee group."));
            _rates.Add(new Label("This local profile is saved on this device. Online account sync and token exchange are not connected yet."));
        }

        private void AddRateLine(string bucket, int rate)
        {
            var label = new Label(bucket + " pool total  ·  " + FormatRate(rate));
            label.AddToClassList("rate-line");
            _rates.Add(label);
        }

        private void RenderCollection()
        {
            if (_inventory == null || _selectorList == null) return;
            _inventory.Clear();
            foreach (var entry in _banner.Pool)
            {
                var fighter = FindCharacter(entry.CharacterId);
                var owned = _state.Roster.Find(x => x != null && x.DefinitionId == entry.CharacterId);
                var row = new VisualElement();
                row.AddToClassList("collection-row");
                var description = new Label((fighter?.DisplayName ?? entry.CharacterId) + " · " + entry.Rarity +
                    (owned == null ? " · Not owned" : " · C" + owned.ConstellationTier + " · " + owned.CrestCount + " crest(s)") +
                    (fighter != null && !fighter.RuntimeReady ? " · Not playable" : string.Empty));
                description.AddToClassList("collection-name");
                row.Add(description);
                if (owned != null)
                {
                    var upgrade = new Button(() => ReviewUpgrade(entry.CharacterId))
                    {
                        text = owned.ConstellationTier >= 5 ? "C5 · capped" : "Upgrade to C" + (owned.ConstellationTier + 1)
                    };
                    upgrade.SetEnabled(owned.ConstellationTier < 5 && owned.CrestCount > 0);
                    upgrade.AddToClassList("secondary-button");
                    row.Add(upgrade);
                }
                _inventory.Add(row);
            }
            _selectorList.Clear();
            foreach (var selector in _state.Selectors.Where(x => x != null && !x.Claimed))
            {
                var title = new Label("Featured selector · earned on " + selector.BannerRevision);
                title.AddToClassList("selector-title");
                _selectorList.Add(title);
                foreach (var id in selector.EligibleCharacterIds ?? new List<string>())
                {
                    var fighter = FindCharacter(id);
                    var claim = new Button(() => ReviewSelector(selector.Id, id)) { text = "Choose " + (fighter?.DisplayName ?? id) };
                    claim.AddToClassList("secondary-button");
                    _selectorList.Add(claim);
                }
            }
            if (_selectorList.childCount == 0) _selectorList.Add(new Label("No unclaimed featured selectors."));
        }

        private void ReviewUpgrade(string characterId)
        {
            var owned = _state.Roster.Find(x => x != null && x.DefinitionId == characterId);
            if (owned == null || owned.CrestCount < 1 || owned.ConstellationTier >= 5) return;
            _pendingUpgradeCharacter = characterId;
            var fighter = FindCharacter(characterId);
            if (_upgradeSummary != null) _upgradeSummary.text = string.Format("{0}\nC{1} → C{2}\nCost: 1 {3} crest.",
                fighter?.DisplayName ?? characterId, owned.ConstellationTier, owned.ConstellationTier + 1, fighter?.DisplayName ?? "character");
            Show(_upgradePanel, true);
            SetNotice("Confirming spends one crest and advances one tier.");
        }

        private void ConfirmUpgrade()
        {
            if (string.IsNullOrEmpty(_pendingUpgradeCharacter)) return;
            try
            {
                var receipt = _service.UpgradeConstellation(_pendingUpgradeCharacter, Guid.NewGuid().ToString("N"));
                _store.Save(_state);
                Show(_upgradePanel, false);
                _pendingUpgradeCharacter = null;
                Refresh();
                SetNotice("Constellation advanced from C" + receipt.TierBefore + " to C" + receipt.TierAfter + ".");
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private void ReviewSelector(string selectorId, string characterId)
        {
            try
            {
                _pendingSelectorGrant = _service.PreviewSelector(selectorId, characterId);
                _pendingSelectorId = selectorId;
                _pendingSelectorCharacter = characterId;
                var fighter = FindCharacter(characterId);
                if (_selectorSummary != null) _selectorSummary.text = "Choose " + (fighter?.DisplayName ?? characterId) +
                    "\nPreview: " + GrantLabel(_pendingSelectorGrant) + "\nThis choice is earned and can be claimed once.";
                Show(_selectorPanel, true);
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private void ConfirmSelector()
        {
            if (string.IsNullOrEmpty(_pendingSelectorId) || string.IsNullOrEmpty(_pendingSelectorCharacter)) return;
            try
            {
                var outcome = _service.ClaimSelector(_pendingSelectorId, _pendingSelectorCharacter, Guid.NewGuid().ToString("N"));
                _store.Save(_state);
                Show(_selectorPanel, false);
                _pendingSelectorId = null;
                _pendingSelectorCharacter = null;
                _pendingSelectorGrant = null;
                Refresh();
                SetNotice("Selector claimed: " + GrantLabel(outcome) + ".");
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private void SelectTab(string tab)
        {
            Show(_pool, tab == "banner");
            Show(_collection, tab == "collection");
            Show(_rates, tab == "rates");
            Show(_root.Q<VisualElement>("formation-panel"), tab == "formation");
            Show(_root.Q<VisualElement>("banner-actions"), tab == "banner");
            Show(_root.Q<VisualElement>("summon-results-panel"), tab == "banner");
            Show(_root.Q<VisualElement>("collection-actions"), tab == "collection");
            if (tab == "formation") RenderFormation();
        }

        private void Refresh()
        {
            if (_wallet != null) _wallet.text = "LOCAL WALLET · " + _state.Diamonds.ToString("N0") + " Diamonds";
            if (_guarantee != null) _guarantee.text = "Featured guarantee · " + _service.GuaranteePulls + " / 300";
            if (_selectorCount != null) _selectorCount.text = "Selectors · " + _service.UnclaimedSelectors;
            if (_tokens != null) _tokens.text = "Collection Tokens · " + _state.CollectionTokens;
            RenderCollection();
            RenderFormation();
        }

        private void RenderFormation()
        {
            if (_formation == null || _service == null) return;
            _formation.Clear();
            var owned = new List<CharacterDefinition>();
            foreach (var record in _state.Roster)
            {
                var fighter = FindCharacter(record.DefinitionId);
                if (fighter != null && fighter.RuntimeReady && !owned.Exists(item => item.Id == fighter.Id)) owned.Add(fighter);
            }
            var choices = new List<string>();
            foreach (var fighter in owned) choices.Add(fighter.DisplayName + " · " + fighter.Id.Replace("fighter.", string.Empty));
            var ids = new List<string>();
            foreach (var fighter in owned) ids.Add(fighter.Id);

            for (var slot = 0; slot < 4; slot++)
            {
                var selectedId = slot < _state.FormationDefinitionIds.Count ? _state.FormationDefinitionIds[slot] : string.Empty;
                var selectedIndex = ids.IndexOf(selectedId);
                if (choices.Count == 0)
                {
                    _formation.Add(new Label("Summon or unlock a runtime-ready fighter to fill this formation."));
                    break;
                }
                var dropdown = new DropdownField("Slot " + (slot + 1), choices, Mathf.Max(0, selectedIndex));
                dropdown.AddToClassList("formation-slot");
                var capturedSlot = slot;
                dropdown.RegisterValueChangedCallback(change => SaveFormationSlot(capturedSlot, choices, ids, change.newValue));
                _formation.Add(dropdown);
            }
            if (_playDungeon != null)
                _playDungeon.SetEnabled(_state.Roster != null && _state.Roster.Count > 0);
        }

        private void SaveFormationSlot(int slot, List<string> choices, List<string> ids, string label)
        {
            var index = choices.IndexOf(label);
            if (index < 0) return;
            var formation = new List<string>(_state.FormationDefinitionIds);
            while (formation.Count < 4) formation.Add(string.Empty);
            formation[slot] = ids[index];
            try
            {
                _service.SaveFormation(formation);
                _store.Save(_state);
                SetNotice("Formation saved. Slot 4 is the reserve fighter.");
                RenderFormation();
            }
            catch (Exception exception)
            {
                SetNotice(exception.Message);
                RenderFormation();
            }
        }

        private void EnterDungeon()
        {
            try
            {
                _store.Save(_state);
                SceneManager.LoadScene("Combat");
            }
            catch (Exception exception) { SetNotice(exception.Message); }
        }

        private CharacterDefinition FindCharacter(string id) => _catalog.Characters.Find(x => x != null && x.Id == id);
        private static string FormatRate(int basisPoints) => (basisPoints / 100f).ToString("0.##") + "%";
        private static string GrantLabel(CharacterGrantOutcome outcome)
        {
            if (outcome == null) return "Unknown result";
            switch (outcome.Kind)
            {
                case CharacterGrantKind.NewCharacter: return "NEW · unlocked C0";
                case CharacterGrantKind.DuplicateCrest: return "+1 crest (C" + outcome.ConstellationTier + ")";
                default: return "+1 Collection Token (C6 duplicate)";
            }
        }
        private void SetNotice(string text) { if (_notice != null) _notice.text = text ?? string.Empty; }
        private static void Show(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
