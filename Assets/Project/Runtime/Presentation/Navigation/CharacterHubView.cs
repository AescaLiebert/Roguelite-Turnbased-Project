using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Owned fighter details and persisted level/cap progression.</summary>
    public static class CharacterHubView
    {
        private static readonly string[] Tabs = { "kit", "upgrade", "constellation", "artifacts" };
        public static void Show(VisualElement root, CharacterObject character)
        {
            var panel = root?.Q("character-hub");
            if (panel == null || character == null || PlayerInventoryService.Instance?.FindOwnedByDefinition(character.DefinitionId) == null) return;
            if (!(panel.userData is Binding)) panel.userData = new Binding(root, panel);
            ((Binding)panel.userData).Show(character);
        }

        private sealed class Binding
        {
            private readonly VisualElement _root, _panel, _inventory;
            private readonly CombatClassFeedback _ccFeedback;
            private CharacterObject _character;
            private int _previewTier;
            private bool _open, _subscribed, _upgrading;
            private string _previousSection;
            private OwnedCharacterRecord Owned => PlayerInventoryService.Instance?.FindOwnedByDefinition(_character.DefinitionId);
            private int Level => Owned?.level ?? 1;
            private int Cap => Owned?.levelCap ?? 10;
            private int Tier => Mathf.Clamp(Owned?.constellationTier ?? 0, 0, 5);

            public Binding(VisualElement root, VisualElement panel)
            {
                _root = root; _panel = panel; _inventory = root.Q("character-inventory");
                _ccFeedback = new CombatClassFeedback(root);
                Click("hub-close", Close);
                foreach (var tab in Tabs) { var selected = tab; Click("hub-tab-" + tab, () => SelectTab(selected)); }
                Click("hub-stat-details", () => CharacterDetailPopup.Stats(_panel, _character));
                Click("hub-passive-button", () => CharacterDetailPopup.Passive(_panel, _character, Tier));
                for (var slot = 0; slot <= 2; slot++)
                {
                    var selected = slot;
                    var card = panel.Q("hub-card-" + slot)?.Q<Button>("card-button");
                    if (card != null) card.clicked += () => CharacterDetailPopup.Card(_panel, _character, selected, selected == 0 ? Tier : 1, Tier);
                }
                Click("hub-upgrade-action", () => Upgrade(Level == Cap && Cap < CharacterProgression.MaxLevel));
                for (var tier = 0; tier <= 5; tier++)
                {
                    var selected = tier; Click("hub-constellation-" + tier, () => PreviewTier(selected));
                }
                panel.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (!_open || evt.keyCode != KeyCode.Escape) return;
                    if (!ArtifactWorkshopView.CloseOverlays(_root) && !CharacterDetailPopup.Close(_panel)) Close(); evt.StopPropagation();
                }, TrickleDown.TrickleDown);
                panel.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
                panel.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
                if (panel.panel != null) Subscribe();
            }

            public void Show(CharacterObject character)
            {
                if (!_open) _previousSection = _root.Q<Label>("section-title")?.text;
                _open = true; _character = character; _previewTier = Tier;
                _ccFeedback.Reset(PlayerInventoryService.Instance.SubjectId + ":" + Owned.instanceId, PlayerInventoryService.Instance.GetCharacterStats(character).CombatClass);
                CharacterDetailPopup.Close(_panel); ItemRewardView.CloseTooltip(_root);
                ScreenMotion.Hide(_inventory);
                var section = _root.Q<Label>("section-title"); if (section != null) section.text = "FIGHTER DETAILS";
                Text("hub-upgrade-result", ""); var result = _panel.Q("hub-upgrade-result"); if (result != null) result.style.display = DisplayStyle.None;
                AccountHeaderView.OverrideBack(_root, this, Back, "Back to characters");
                Refresh(); ScreenMotion.Show(_panel); SelectTab("kit");
            }

            private void Back()
            {
                if (ArtifactWorkshopView.CloseOverlays(_root) || CharacterDetailPopup.Close(_panel)) return;
                if (_root.Q("item-tooltip-overlay") != null) { ItemRewardView.CloseTooltip(_root); return; }
                Close();
            }

            private void Close()
            {
                if (!_open) return;
                _open = false; AccountHeaderView.ClearBackOverride(_root, this); ArtifactWorkshopView.CloseAll(_root); CharacterDetailPopup.Close(_panel); ItemRewardView.CloseTooltip(_root); ScreenMotion.Hide(_panel);
                _ccFeedback.Hide();
                ScreenMotion.Show(_inventory);
                var section = _root.Q<Label>("section-title"); if (section != null) section.text = _previousSection ?? "CHARACTERS";
                _root.Q("inventory-grid")?.Q<Button>("character-" + _character.DefinitionId)?.Focus();
            }

            private void SelectTab(string selected)
            {
                if (!Tabs.Contains(selected)) return;
                ArtifactWorkshopView.CloseAll(_root);
                _panel.Q(className: "hub-page")?.EnableInClassList("artifact-mode", selected == "artifacts");
                CharacterDetailPopup.Close(_panel); ItemRewardView.CloseTooltip(_root);
                foreach (var tab in Tabs)
                {
                    _panel.Q<Button>("hub-tab-" + tab)?.EnableInClassList("selected", tab == selected);
                    var page = _panel.Q("hub-page-" + tab);
                    if (page != null) { if (tab == selected) ScreenMotion.Show(page); else ScreenMotion.Hide(page); }
                }
                var scroll = _panel.Q<ScrollView>("hub-scroll"); if (scroll != null) { scroll.scrollOffset = Vector2.zero; scroll.contentContainer.EnableInClassList("hub-fitted-content", selected == "upgrade" || selected == "artifacts"); }
                var slots = _panel.Q("artifact-character-stage");
                if (slots != null) { if (selected == "artifacts") ScreenMotion.Show(slots); else ScreenMotion.Hide(slots); }
                _panel.Q<Button>("hub-tab-" + selected)?.Focus();
            }

            private void Refresh()
            {
                Text("hub-name", _character.FighterName);
                Text("hub-meta", _character.FighterRarity + " · " + _character.FighterAttribute.ToString().ToUpperInvariant() + "\n" + DungeonProfile.SeriesDisplayName(_character.SeriesId));
                Text("hub-progression", "LEVEL " + Level + " / " + Cap + "   ·   C" + Tier + "/5");
                Text("hub-kit-level", "LEVEL " + Level + " / " + Cap);
                var portrait = _panel.Q<Image>("hub-portrait");
                if (portrait != null) { portrait.sprite = _character.FighterFull != null ? _character.FighterFull : _character.FighterPic != null ? _character.FighterPic : _character.FighterIcon; portrait.scaleMode = ScaleMode.ScaleToFit; }
                var icon = _panel.Q("hub-character-icon"); if (icon != null) { icon.Clear(); icon.Add(CharacterIconView.Create(_character, 84)); }
                var stats = PlayerInventoryService.Instance.GetCharacterStats(_character);
                _ccFeedback.Observe(PlayerInventoryService.Instance.SubjectId + ":" + Owned.instanceId, stats.CombatClass);
                BasicStatPreview.Render(_panel.Q("hub-stats"), stats);
                RefreshCards(); RefreshUpgrade(stats); BuildConstellation();
                ArtifactWorkshopView.Display(_root, _panel.Q("hub-page-artifacts"), _character);
            }

            private void RefreshCards()
            {
                var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
                for (var slot = 0; slot <= 2; slot++)
                {
                    var card = _panel.Q("hub-card-" + slot); if (card == null) continue;
                    var face = card.Q<Button>("card-button");
                    if (face != null) { face.style.width = Length.Percent(100); face.style.height = Length.Percent(100); face.style.minHeight = 0; face.style.flexShrink = 0; }
                    var skill = slot == 1 ? _character.Skill1 : _character.Skill2;
                    var image = card.Q<Image>("artwork");
                    if (image != null) { image.sprite = slot == 0 ? (_character.Ultimate?.icon != null ? _character.Ultimate.icon : _character.FighterIcon) : (skill?.cardIcon != null ? skill.cardIcon : _character.FighterIcon); image.scaleMode = ScaleMode.ScaleAndCrop; image.pickingMode = PickingMode.Ignore; }
                    var frame = card.Q<Image>("card-rank-frame");
                    if (frame != null) { frame.image = slot == 0 ? art?.rankThree : art?.rankOne; frame.scaleMode = ScaleMode.StretchToFill; }
                    var label = card.Q<Label>("card-skill-slot"); if (label != null) label.text = slot == 0 ? "ULT" : slot.ToString();
                    var type = card.Q<Image>("card-skill-type");
                    if (type != null)
                    {
                        type.sprite = slot == 0 ? null : art?.Type(skill?.ranks?.FirstOrDefault()?.skillType ?? SkillType.Attack);
                        type.style.display = type.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                        if (slot != 0) { type.style.top = 5; type.style.right = 5; type.style.width = 26; type.style.height = 26; }
                    }
                    var glow = card.Q("card-holo-glow"); if (glow != null) glow.style.display = slot == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                    var button = card.Q<Button>("card-button"); if (button != null) button.tooltip = slot == 0 ? "Inspect ultimate · C" + Tier : "Inspect skill " + slot + " · R1–R3";
                }
                var passive = _panel.Q<Image>("hub-passive-image"); if (passive != null) { passive.sprite = _character.FighterPassive != null ? _character.FighterPassive : _character.FighterIcon; passive.scaleMode = ScaleMode.ScaleToFit; }
            }

            private void RefreshUpgrade(StatBlock current)
            {
                ItemRewardView.AttachStyle(_root);
                var atMax = Level >= CharacterProgression.MaxLevel;
                var limitBreak = !atMax && Level == Cap;
                var next = current.Clone();
                if (!atMax && !limitBreak)
                {
                    var basis = FightingAllstar.Presentation.Content.CharacterObjectCatalogBuilder.CreateDefinition(_character).BaseStats;
                    next = ArtifactRules.ApplyStats(CharacterProgression.StatsAtLevel(basis, _character.LevelGrowth, Level + 1), PlayerInventoryService.Instance.GetEquippedArtifacts(Owned.instanceId));
                }
                Text("hub-upgrade-title", atMax ? "MAX LEVEL" : limitBreak ? "LIMIT BREAK" : "LEVEL UP");
                Text("hub-upgrade-fighter", _character.FighterName);
                Text("hub-current-level", "LEVEL " + Level + " / " + Cap);
                Text("hub-upgrade-cap-preview", atMax ? "Final level reached" : limitBreak ? "Level cap  " + Cap + " → " + (Cap + 10) : "Next level  " + Level + " → " + (Level + 1));
                BasicStatPreview.Render(_panel.Q("hub-upgrade-stats"), current, !limitBreak && !atMax ? next : null);
                var valid = CharacterProgression.TryCosts(_character.ID, _character.SeriesId, Level, Cap, limitBreak, out var costs, out var error);
                var affordable = valid && Affordable(costs);
                Materials("hub-upgrade-materials", costs.Where(c => c.ItemId != ItemCatalog.Coin));
                var materials = _panel.Q("hub-upgrade-material-section");
                if (materials != null) materials.style.display = limitBreak ? DisplayStyle.Flex : DisplayStyle.None;
                var coinCost = costs.Where(c => c.ItemId == ItemCatalog.Coin).Sum(c => c.Amount);
                var coins = PlayerInventoryService.Instance.GetItemAmount(ItemCatalog.Coin);
                Text("hub-upgrade-coin-cost", atMax ? "—" : "Required  " + coinCost.ToString("N0"));
                Text("hub-upgrade-coin-owned", "Owned  " + coins.ToString("N0"));
                var coinIcon = _panel.Q("hub-upgrade-coin-icon"); if (coinIcon != null) coinIcon.style.display = atMax ? DisplayStyle.None : DisplayStyle.Flex;
                var coinCopy = _panel.Q(className: "hub-upgrade-coin-copy"); if (coinCopy != null) coinCopy.style.display = atMax ? DisplayStyle.None : DisplayStyle.Flex;
                _panel.Q("hub-upgrade-coin-cost")?.EnableInClassList("material-shortage", coins < coinCost);
                Text("hub-upgrade-state", atMax ? "Maximum level reached." : !valid ? error : !affordable ? "Gather the missing " + (limitBreak ? "materials or Coins." : "Coins.") :
                    limitBreak ? "Unlocks the next 10 levels. Current stats stay unchanged." : "Raise CC, ATK, DEF and HP.");
                var action = _panel.Q<Button>("hub-upgrade-action");
                if (action != null) { action.text = atMax ? "MAX LEVEL" : limitBreak ? "LIMIT BREAK" : "LEVEL UP"; action.SetEnabled(affordable && !_upgrading); }
            }

            private bool Affordable(IEnumerable<ItemAmount> costs) => costs.All(c => PlayerInventoryService.Instance.GetItemAmount(c.ItemId) >= c.Amount);
            private void Materials(string name, IEnumerable<ItemAmount> costs)
            {
                var grid = _panel.Q(name); if (grid == null) return; grid.Clear();
                foreach (var cost in costs.OrderByDescending(c => ItemCatalog.Find(c.ItemId)?.FrameTier ?? 0)
                    .ThenBy(c => DungeonRewards.RewardOrder(ItemCatalog.Find(c.ItemId)?.Kind ?? ItemKind.Coin)))
                {
                    var item = ItemCatalog.Find(cost.ItemId); if (item == null) continue;
                    var amount = PlayerInventoryService.Instance.GetItemAmount(cost.ItemId);
                    var tile = ItemRewardView.CreateTile(_root, item, null, "Required: " + cost.Amount.ToString("N0"), true,
                        footerText: amount.ToString("N0") + " / " + cost.Amount.ToString("N0"));
                    tile.EnableInClassList("material-shortage", amount < cost.Amount); grid.Add(tile);
                }
            }

            private void Upgrade(bool limitBreak)
            {
                if (_upgrading || Owned == null) return;
                _upgrading = true; var previousLevel = Level; var previousCap = Cap;
                try
                {
                    var success = PlayerInventoryService.Instance.TryUpgradeCharacter(Owned.instanceId, limitBreak, out var error);
                    Text("hub-upgrade-result", success ? limitBreak ? "LIMIT BREAK COMPLETE · Level cap " + previousCap + " → " + Cap : "LEVEL UP · " + previousLevel + " → " + Level : error);
                    var result = _panel.Q("hub-upgrade-result"); if (result != null) result.style.display = DisplayStyle.Flex;
                    var scroll = _panel.Q<ScrollView>("hub-scroll"); if (scroll != null) scroll.scrollOffset = Vector2.zero;
                }
                finally { _upgrading = false; Refresh(); }
            }

            private void BuildConstellation()
            {
                for (var tier = 0; tier <= 5; tier++) { _panel.Q("hub-constellation-" + tier)?.EnableInClassList("unlocked", tier <= Tier); Text("hub-node-state-" + tier, tier == Tier ? "CURRENT" : tier < Tier ? "UNLOCKED" : "LOCKED"); }
                Text("hub-current-constellation", "CURRENT · C" + Tier + "/5"); Text("hub-current-ultimate", UltimateDescription(Tier)); PreviewTier(_previewTier);
            }
            private void PreviewTier(int tier)
            {
                _previewTier = Mathf.Clamp(tier, 0, 5);
                for (var i = 0; i <= 5; i++) _panel.Q("hub-constellation-" + i)?.EnableInClassList("selected", i == _previewTier);
                Text("hub-preview-constellation", "PREVIEW · C" + _previewTier + "/5"); Text("hub-preview-ultimate", UltimateDescription(_previewTier));
            }
            private string UltimateDescription(int tier)
            {
                var level = _character.Ultimate?.levels?.ElementAtOrDefault(tier);
                return level == null ? "Details have not been authored for this fighter yet." :
                    Combat.CardDescriptionText.Format(level.description, level.runtimeEffect);
            }
            private void Click(string name, Action action) { var button = _panel.Q<Button>(name); if (button != null) button.clicked += action; }
            private void Text(string name, string value) { var label = _panel.Q<Label>(name); if (label != null) label.text = value ?? string.Empty; }
            private void Subscribe() { if (_subscribed) return; PlayerInventoryService.InventoryChanged += Changed; _subscribed = true; }
            private void Unsubscribe() { _ccFeedback.Hide(); AccountHeaderView.ClearBackOverride(_root, this); if (!_subscribed) return; PlayerInventoryService.InventoryChanged -= Changed; _subscribed = false; }
            private void Changed() { if (!_open) return; if (Owned == null) { Close(); return; } Refresh(); }
        }
    }
}
