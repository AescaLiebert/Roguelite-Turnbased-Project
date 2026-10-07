using System.Linq;
using System.Collections;
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
        private VisualElement _root, _banner, _results;
        private Label _wallet, _notice, _guarantee;
        private bool _busy;
        private bool _uiInitialized;
        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("Summon UI has no root visual element.", this);
                return;
            }
            _uiInitialized = false;
            _root.schedule.Execute(InitializeUI).ExecuteLater(1);
        }

        private void InitializeUI()
        {
            if (!isActiveAndEnabled || _root == null || _uiInitialized) return;
            _uiInitialized = true;
            Debug.Log("Summon UI initialized; binding visible controls.", this);
            _wallet = QueryLabel("wallet", "economy-wallet");
            _notice = QueryLabel("notice", "economy-notice");
            _guarantee = QueryLabel("guarantee", "economy-guarantee");
            _banner = _root.Q("banner-screen"); _results = _root.Q("result-screen");
            BindIfPresent("battle", () => Navigate("Combat"));
            BindIfPresent("summon", () => Navigate("Scene-Gacha"));
            BindIfPresent("back", () => Navigate("MainMenu"));
            BindFirst(new[] { "summon-one", "summon-single" }, () => Summon(1));
            BindIfPresent("summon-ten", () => Summon(10));
            BindIfPresent("result-close", () =>
            {
                if (_results != null) _results.style.display = DisplayStyle.None;
                if (_banner != null) _banner.style.display = DisplayStyle.Flex;
            });
            BindIfPresent("details", ShowDetails);
            BindIfPresent("selector", ShowSelectors);
            if (_notice != null)
            {
                if (bannerConfiguration == null) _notice.text = "Assign a summon banner configuration in the Inspector.";
                else if (!bannerConfiguration.Validate(out var bannerError)) _notice.text = bannerError;
            }
            var hero = _root.Q<Image>("hero-art");
            var character = PlayerInventoryService.Instance?.GetCatalog().FirstOrDefault(c => c != null && c.RuntimeReady && c.FighterRarity == FighterRarity.SSR);
            if (hero != null && character != null) hero.sprite = character.FighterPic != null ? character.FighterPic : character.FighterIcon;
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
            // Starting value: a brief fade, independent of frame rate and game time scale.
            for (var elapsed = 0f; elapsed < .2f; elapsed += Time.unscaledDeltaTime)
            {
                _root.style.opacity = 1f - elapsed / .2f;
                yield return null;
            }
            operation.allowSceneActivation = true;
        }
        private void Refresh()
        {
            var inventory = PlayerInventoryService.Instance;
            if (_wallet != null) _wallet.text = (inventory?.Diamonds ?? 0).ToString("N0") + "  DIAMONDS";
            if (_guarantee != null) _guarantee.text = "Featured selector " + (inventory?.GuaranteeProgress ?? 0) + " / 300 · Selectors " + (inventory?.SelectorEntitlements ?? 0);
        }
        private void Summon(int count)
        {
            if (_busy) return;
            Debug.Log("Summon button clicked: " + count, this);
            _busy = true;
            try
            {
                var inventory = PlayerInventoryService.Instance;
                if (inventory == null)
                {
                    SetNotice("Inventory is unavailable.");
                    Debug.LogError("Inventory is unavailable.", this);
                    return;
                }
                if (!inventory.TrySummon(count, bannerConfiguration, out var draws, out var error))
                {
                    SetNotice(error);
                    Debug.LogWarning("Summon failed: " + error, this);
                    return;
                }
                var cards = _root.Q("result-cards");
                if (cards != null && _results != null && _banner != null)
                {
                    cards.Clear();
                    foreach (var character in draws)
                    {
                        var card = new VisualElement(); card.AddToClassList("result-card");
                        card.Add(CharacterIconView.CreateFilling(character));
                        cards.Add(card);
                    }
                    var resultTitle = _root.Q<Label>("result-title");
                    if (resultTitle != null) resultTitle.text = count == 1 ? "FIGHTER SUMMONED" : "10 FIGHTERS SUMMONED";
                    _banner.style.display = DisplayStyle.None;
                    _results.style.display = DisplayStyle.Flex;
                }
                else
                {
                    var receipt = _root.Q<Label>("summon-results");
                    if (receipt != null)
                        receipt.text = "Summoned: " + string.Join(", ", draws.Select(x => x.FighterRarity + " " + x.FighterName));
                }
                SetNotice("Fighters added to your collection. Duplicates advance constellation up to C6, then grant tokens.");
                Debug.Log("Summon completed successfully: " + draws.Count + " fighter(s).", this);
                Refresh();
            }
            finally { _busy = false; }
        }
        private void ShowDetails()
        {
            var panel = _root.Q("details-panel");
            if (panel == null) return;
            var opening = panel.style.display.value == DisplayStyle.None;
            panel.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;
            if (!opening) return;
            panel.Clear();
            if (bannerConfiguration == null || !bannerConfiguration.Validate(out _)) return;
            panel.Add(new Label("SSR " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.SSR)) +
                " · SR " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.SR)) +
                " · R " + FormatRate(bannerConfiguration.GetRarityRateBasisPoints(FighterRarity.R)) +
                ". Independent draws; ten summons have no rarity guarantee.\n300 paid pulls earn an SSR selector."));
            foreach (var pool in bannerConfiguration.RarityPools)
            {
                foreach (var character in pool.Characters)
                    panel.Add(new Label(character.FighterName + " · " + pool.Rarity +
                        (character.RuntimeReady ? string.Empty : " · Draft, battle locked")));
            }
        }
        private void ShowSelectors()
        {
            var inventory = PlayerInventoryService.Instance;
            var panel = _root.Q("details-panel");
            if (panel == null) return;
            panel.Clear(); panel.style.display = DisplayStyle.Flex;
            panel.Add(new Label("Choose an SSR using an earned selector."));
            if (bannerConfiguration == null || !bannerConfiguration.Validate(out _)) return;
            foreach (var pool in bannerConfiguration.RarityPools.Where(x => x.Rarity == FighterRarity.SSR))
            {
                foreach (var character in pool.Characters)
                {
                    var selectedCharacter = character;
                    var button = new Button(() => { inventory.TryClaimSelector(selectedCharacter, out var error); SetNotice(error ?? "Selector claimed."); Refresh(); ShowSelectors(); }) { text = character.FighterName };
                    button.SetEnabled(inventory.SelectorEntitlements > 0); panel.Add(button);
                }
            }
        }

        private void SetNotice(string message)
        {
            if (_notice != null) _notice.text = message;
        }

        private static string FormatRate(int basisPoints) => (basisPoints / 100f).ToString("0.##") + "%";
    }
}
