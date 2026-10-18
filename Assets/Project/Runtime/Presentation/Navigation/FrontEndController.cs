using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using FightingAllstar.Presentation;

namespace FightingAllstar.Presentation.Navigation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FrontEndController : MonoBehaviour
    {
        [SerializeField] private SummonBannerConfigurationSO bannerConfiguration;
        [SerializeField, Tooltip("Banners offered by this screen, in display order. The default banner is included once.")]
        private List<SummonBannerConfigurationSO> availableBanners = new List<SummonBannerConfigurationSO>();
        private VisualElement _root, _banner;
        private Label _wallet, _notice, _guarantee;
        private bool _busy;
        private bool _uiInitialized;
        private int _summonPresentationVersion;
        private Button _summonOpener;
        private SummonSequenceView _summonSequence;
        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("Summon UI has no root visual element.", this);
                return;
            }
            _uiInitialized = false;
            PlayerInventoryService.InventoryChanged -= InventoryChanged;
            PlayerInventoryService.InventoryChanged += InventoryChanged;
            _root.schedule.Execute(InitializeUI).ExecuteLater(1);
        }
        private void OnDisable()
        {
            PlayerInventoryService.InventoryChanged -= InventoryChanged;
            _uiInitialized = false;
            _summonPresentationVersion++;
            _summonSequence?.Dispose();
            _summonSequence = null;
            _busy = false;
            _root?.Q("gacha-stage")?.SetEnabled(true);
            if (_banner != null) _banner.style.display = DisplayStyle.Flex;
        }
        private void InventoryChanged() { if (_uiInitialized) Refresh(); }

        private void InitializeUI()
        {
            if (!isActiveAndEnabled || _root == null || _uiInitialized) return;
            _uiInitialized = true;
            Debug.Log("Summon UI initialized; binding visible controls.", this);
            _wallet = QueryLabel("wallet", "economy-wallet");
            _notice = QueryLabel("notice", "economy-notice");
            _guarantee = QueryLabel("guarantee", "economy-guarantee");
            _banner = _root.Q("banner-screen");
            BindIfPresent("battle", () => Navigate("Combat"));
            BindIfPresent("training", () => TrainingCharacterPicker.Show(_root));
            BindIfPresent("summon", () => Navigate("Scene-Gacha"));
            BindIfPresent("back", () => Navigate("MainMenu"));
            BindFirst(new[] { "summon-one", "summon-single" }, () => Summon(1));
            BindIfPresent("summon-ten", () => Summon(10));
            BindIfPresent("details", ShowDetails);
            BindIfPresent("selector", ShowSelectors);
            var banners = new[] { bannerConfiguration }.Concat(availableBanners ?? new List<SummonBannerConfigurationSO>())
                .Where(b => b != null).Distinct().ToList();
            if (bannerConfiguration == null) bannerConfiguration = banners.FirstOrDefault();
            if (_notice != null)
            {
                if (bannerConfiguration == null) _notice.text = "Assign a summon banner configuration in the Inspector.";
                else if (!bannerConfiguration.Validate(out var bannerError)) _notice.text = bannerError;
            }
            MainMenuView.Initialize(_root);
            CharacterInventoryView.Initialize(_root);
            GachaBannerView.Initialize(_root);
            GachaBannerView.PopulateBanners(_root, banners, bannerConfiguration, SelectBanner);
            AccountHeaderView.Bind(_root, _root.Q("gacha-stage") == null ? (System.Action)null : () => Navigate("MainMenu"));
            Refresh();
        }
        private void SelectBanner(SummonBannerConfigurationSO banner)
        {
            if (_busy || banner == null) return;
            bannerConfiguration = banner;
            SetNotice(banner.Validate(out var error) ? null : error);
            Refresh();
        }
        private Label QueryLabel(params string[] names)
        {
            foreach (var name in names)
            {
                var label = _root.Q<Label>(name);
                if (label != null) return label;
            }
            return null;
        }

        private void BindIfPresent(string name, System.Action action)
        {
            var button = _root.Q<Button>(name);
            if (button == null) return;
            button.clicked -= action;
            button.clicked += action;
        }

        private void BindFirst(string[] names, System.Action action)
        {
            foreach (var name in names)
            {
                var button = _root.Q<Button>(name);
                if (button == null) continue;
                button.clicked -= action;
                button.clicked += action;
                return;
            }
            if (_root.Q("banner-screen") != null || _root.Q("banner-actions") != null)
                Debug.LogError("No single-summon button found. Expected one of: " + string.Join(", ", names), this);
        }
        private void Navigate(string scene)
        {
            if (_busy) return;
            _busy = true;
            _root.SetEnabled(false);
            StartCoroutine(Transition(scene));
        }
        private IEnumerator Transition(string scene)
        {
            var loading = new Label("Opening " + (scene == "Combat" ? "Dungeon" : scene == "Scene-Gacha" ? "Summon" : "Home") + "…");
            loading.AddToClassList("loading-message"); _root.Add(loading);
            var operation = SceneManager.LoadSceneAsync(scene);
            operation.allowSceneActivation = false;
            while (operation.progress < .9f) yield return null;
            ScreenMotion.FadeSceneToBlack(() => operation.allowSceneActivation = true);
            while (!operation.isDone) yield return null;
        }
        private void Refresh()
        {
            AccountHeaderView.Refresh(_root);
            var inventory = PlayerInventoryService.Instance;
            if (_wallet != null)
            {
                var isArtworkMenu = _root.Q("menu-stage") != null || _root.Q("gacha-stage") != null;
                _wallet.text = inventory == null ? "—" : inventory.Diamonds.ToString("N0") + (isArtworkMenu ? "" : "  DIAMONDS");
                if (isArtworkMenu)
                {
                    _wallet.tooltip = inventory == null ? "Inventory unavailable" : inventory.Diamonds.ToString("N0") + " Diamonds";
                    _wallet.style.fontSize = _wallet.text.Length > 10 ? 17 : 23;
                }
            }
            if (_guarantee != null) _guarantee.text = "Featured selector " + (inventory?.GuaranteeProgress ?? 0) + " / 300 · Selectors " + (inventory?.SelectorEntitlements ?? 0);
            GachaBannerView.Refresh(_root, bannerConfiguration);
        }
        private void Summon(int count)
        {
            if (_busy) return;
            Debug.Log("Summon button clicked: " + count, this);
            _busy = true;
            var keepBusy = false;
            List<CharacterObject> committedCharacters = null;
            List<SummonDrawPresentation> committedDraws = null;
            var ownedBefore = System.Array.Empty<string>();
            Dictionary<string, int> constellationBefore = null;
            try
            {
                var inventory = PlayerInventoryService.Instance;
                if (inventory == null)
                {
                    SetNotice("Inventory is unavailable.");
                    Debug.LogError("Inventory is unavailable.", this);
                    return;
                }
                // Materialize ownership before TrySummon grants every draw and raises InventoryChanged.
                ownedBefore = inventory.GetOwnedDefinitions().Where(character => character != null)
                    .Select(character => character.DefinitionId).Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(System.StringComparer.Ordinal).ToArray();
                constellationBefore = ownedBefore.ToDictionary(id => id,
                    id => inventory.FindOwnedByDefinition(id)?.constellationTier ?? 0, System.StringComparer.Ordinal);
                if (!inventory.TrySummon(count, bannerConfiguration, out var draws, out var error))
                {
                    SetNotice(error);
                    Debug.LogWarning("Summon failed: " + error, this);
                    return;
                }
                // The purchase and rewards are already saved. Everything below only presents this receipt.
                committedCharacters = draws;
                committedDraws = SummonDrawPresentation.Classify(draws, ownedBefore, constellationBefore);
                SetNotice("Fighters added to your collection. Duplicates advance constellation up to C5, then grant tokens.");
                Debug.Log("Summon completed successfully: " + draws.Count + " fighter(s).", this);
                Refresh();
                keepBusy = true;
                _summonOpener = _root.Q<Button>(count == 10 ? "summon-ten" : "summon-one") ?? _root.Q<Button>("summon-single");
                _root.Q("gacha-stage")?.SetEnabled(false);
                var version = ++_summonPresentationVersion;
                if (_summonSequence == null) _summonSequence = new SummonSequenceView(_root);
                _summonSequence.Play(committedDraws, () =>
                {
                    if (!isActiveAndEnabled || version != _summonPresentationVersion) return;
                    _summonPresentationVersion++;
                    CompleteSummonPresentation();
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
                _summonPresentationVersion++;
                _summonSequence?.Dispose();
                _summonSequence = null;
                if (committedCharacters != null && isActiveAndEnabled)
                {
                    // A presentation failure must still expose the committed rewards without another purchase.
                    if (committedDraws == null)
                        committedDraws = SummonDrawPresentation.Classify(committedCharacters, ownedBefore, constellationBefore);
                    try
                    {
                        var version = ++_summonPresentationVersion;
                        _root.Q("gacha-stage")?.SetEnabled(false);
                        _summonSequence = new SummonSequenceView(_root);
                        _summonSequence.Play(committedDraws, () =>
                        {
                            if (isActiveAndEnabled && version == _summonPresentationVersion) CompleteSummonPresentation();
                        });
                        _summonSequence.Skip();
                        keepBusy = true;
                    }
                    catch (System.Exception presentationError)
                    {
                        Debug.LogException(presentationError, this);
                        _summonSequence?.Dispose(); _summonSequence = null;
                        ShowSummonReceipt(committedDraws);
                        keepBusy = false;
                    }
                }
                else SetNotice("Summon could not be displayed. Please reopen this screen.");
            }
            finally { if (!keepBusy) ReleaseSummonInput(); }
        }

        private void ShowSummonReceipt(IReadOnlyList<SummonDrawPresentation> draws)
        {
            var text = "Summoned: " + string.Join(", ", draws.Where(draw => draw?.Character != null).Select(SummonReceiptText));
            var receipt = _root?.Q<Label>("summon-results");
            if (receipt != null) receipt.text = text;
            else SetNotice(text);
        }

        private static string SummonReceiptText(SummonDrawPresentation draw)
        {
            var reward = draw.IsNew ? "NEW" : draw.FragmentCount > 0 ? "MEMORY ×" + draw.FragmentCount :
                draw.ConstellationTier >= 0 ? "MEMORY · C" + draw.ConstellationTier : "MEMORY";
            return draw.Character.FighterRarity + " " + draw.Character.FighterName + " · " + reward;
        }

        private void CompleteSummonPresentation()
        {
            _summonPresentationVersion++;
            ReleaseSummonInput();
            Refresh();
            if (_summonOpener != null && _summonOpener.enabledInHierarchy) _summonOpener.Focus();
            else
            {
                var target = new[] { "summon-one", "summon-single", "summon-ten", "account-back", "details", "back" }
                    .Select(name => _root?.Q<Button>(name)).FirstOrDefault(button => button != null && button.enabledInHierarchy);
                target?.Focus();
            }
        }

        private void ReleaseSummonInput()
        {
            _busy = false;
            _root?.Q("gacha-stage")?.SetEnabled(true);
            if (_banner != null) _banner.style.display = DisplayStyle.Flex;
        }
        private void ShowDetails()
        {
            if (_busy) return;
            var panel = _root.Q("details-panel");
            if (panel == null) return;
            var opening = panel.style.display.value == DisplayStyle.None;
            panel.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;
            if (_root.Q("gacha-stage") != null)
            {
                if (opening) GachaBannerView.OpenDetails(_root, "RATES & DETAILS", "details");
                else GachaBannerView.CloseDetails(_root);
            }
            if (!opening) return;
            panel.Clear();
            if (bannerConfiguration == null || !bannerConfiguration.Validate(out _)) return;
            panel.Add(new Label("SSR " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.SSR)) +
                " · SR " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.SR)) +
                " · R " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.R)) +
                ". Independent draws; ten summons have no rarity guarantee.\n300 paid pulls earn an SSR selector."));
            foreach (var pool in bannerConfiguration.RarityPools)
            {
                panel.Add(new Label(pool.Rarity + " · " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(pool.Rarity))));
                var grid = new VisualElement(); panel.Add(grid);
                CharacterGridView.Populate(grid, pool.Characters, badge: c => c.RuntimeReady ? null : "DRAFT");
            }
        }
        private void ShowSelectors()
        {
            if (_busy) return;
            var inventory = PlayerInventoryService.Instance;
            var panel = _root.Q("details-panel");
            if (panel == null) return;
            panel.Clear(); panel.style.display = DisplayStyle.Flex;
            GachaBannerView.OpenDetails(_root, "FEATURED SELECTORS", "selector");
            panel.Add(new Label("Choose an SSR using an earned selector."));
            if (bannerConfiguration == null || !bannerConfiguration.Validate(out _)) return;
            foreach (var pool in bannerConfiguration.RarityPools.Where(x => x.Rarity == FighterRarity.SSR))
            {
                var grid = new VisualElement(); panel.Add(grid);
                CharacterGridView.Populate(grid, pool.Characters,
                    ClaimSelector,
                    _ => inventory != null && inventory.SelectorEntitlements > 0);
            }
        }

        private void ClaimSelector(CharacterObject selected)
        {
            if (_busy || selected == null) return;
            var inventory = PlayerInventoryService.Instance;
            if (inventory == null) { SetNotice("Inventory is unavailable."); return; }
            _busy = true;
            List<SummonDrawPresentation> committed = null;
            try
            {
                // Capture ownership before the selector grants/advances this fighter.
                var previous = inventory.FindOwnedByDefinition(selected.DefinitionId);
                var owned = previous == null ? System.Array.Empty<string>() : new[] { selected.DefinitionId };
                var tiers = new Dictionary<string, int>(System.StringComparer.Ordinal);
                if (previous != null) tiers[selected.DefinitionId] = previous.constellationTier;
                if (!inventory.TryClaimSelector(selected, out var error))
                { SetNotice(error); ReleaseSummonInput(); return; }
                committed = SummonDrawPresentation.Classify(new[] { selected }, owned, tiers);
                SetNotice("Selector reward added to your collection."); Refresh();
                GachaBannerView.CloseDetails(_root);
                var details = _root.Q("details-panel");
                if (details != null) details.style.display = DisplayStyle.None;
                _summonOpener = _root.Q<Button>("selector");
                _root.Q("gacha-stage")?.SetEnabled(false);
                var version = ++_summonPresentationVersion;
                if (_summonSequence == null) _summonSequence = new SummonSequenceView(_root);
                _summonSequence.Play(committed, () =>
                {
                    if (!isActiveAndEnabled || version != _summonPresentationVersion) return;
                    CompleteSummonPresentation();
                });
            }
            catch (System.Exception error)
            {
                Debug.LogException(error, this);
                _summonPresentationVersion++;
                _summonSequence?.Dispose(); _summonSequence = null;
                if (committed != null) ShowSummonReceipt(committed);
                else SetNotice("Selector claim could not be displayed. Check your collection before retrying.");
                ReleaseSummonInput();
            }
        }

        private void SetNotice(string message)
        {
            if (_notice != null)
            {
                _notice.text = message;
                if (_root.Q("gacha-stage") != null) _notice.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private static string FormatRate(int basisPoints) => (basisPoints / 100f).ToString("0.##") + "%";
    }
}
