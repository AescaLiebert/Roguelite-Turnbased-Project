using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    internal static class CharacterDetailPopup
    {
        public static bool Close(VisualElement panel)
        {
            var overlay = panel.Q("hub-detail-overlay");
            if (overlay == null) return false;
            var previous = overlay.userData as VisualElement;
            ScreenMotion.Remove(overlay, () => { if (panel.Q("hub-detail-overlay") == null && previous?.panel != null) previous.Focus(); }); return true;
        }

        private static VisualElement Open(VisualElement panel, string title, bool reference = false)
        {
            var previous = panel.Q("hub-detail-overlay")?.userData as VisualElement ?? panel.panel?.focusController.focusedElement as VisualElement;
            Close(panel);
            var overlay = new VisualElement { name = "hub-detail-overlay", focusable = true, userData = previous };
            overlay.AddToClassList("hub-detail-overlay");
            var dialog = new VisualElement(); dialog.AddToClassList("hub-detail-dialog");
            if (reference) dialog.AddToClassList("hub-reference-dialog");
            var heading = new VisualElement(); heading.AddToClassList("hub-detail-heading");
            var titleLabel = Label(title, "hub-detail-title");
            titleLabel.style.flexShrink = 1;
            titleLabel.style.minWidth = 0;
            heading.Add(titleLabel);
            var close = new Button(() => Close(panel)) { text = "×", name = "hub-detail-close" }; close.AddToClassList("action-button"); heading.Add(close);
            dialog.Add(heading);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden }; scroll.AddToClassList("hub-detail-scroll");
            scroll.contentContainer.style.flexShrink = 0;
            scroll.contentContainer.style.minHeight = Length.Percent(100);
            dialog.Add(scroll);
            overlay.Add(dialog); panel.Add(overlay); ScreenMotion.ShowModal(overlay, dialog);
            overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) Close(panel); evt.StopPropagation(); });
            overlay.Focus(); return scroll.contentContainer;
        }

        public static void Stats(VisualElement panel, CharacterObject character)
        {
            var body = Open(panel, "ADDITIONAL STATS");
            var stats = PlayerInventoryService.Instance.GetCharacterStats(character);
            var visibleStats = new[]
            {
                StatId.Pierce, StatId.Resistance, StatId.Regeneration, StatId.CritChance, StatId.CritDamage,
                StatId.CritResistance, StatId.CritDefense, StatId.Recovery, StatId.BlockChance, StatId.BlockPower,
                StatId.LifeSteal, StatId.DamageOverTimeDealt, StatId.DamageDealt, StatId.AdditionalDamageDealt
            };
            foreach (var stat in visibleStats)
            {
                var row = new VisualElement { name = "hub-detail-stat-" + stat }; row.AddToClassList("hub-detail-stat");
                row.Add(new Label(StatName(stat)));
                row.Add(new Label(stat >= StatId.Pierce ? (stats.Get(stat) / 100d).ToString("0.##") + "%" : stats.Get(stat).ToString("N0")));
                body.Add(row);
            }
        }

        public static string StatName(StatId stat) => CombatTextCatalog.StatName(stat);

        public static void Card(VisualElement panel, CharacterObject character, int slot, int rank, int currentTier)
        {
            var definition = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var owner = new FighterState { Definition = definition, ConstellationTier = currentTier };
            var skill = definition.Skills.FirstOrDefault(s => s.Slot == slot);
            var title = slot == 0 ? character.Ultimate?.ultimateName : slot == 1 ? character.Skill1?.cardName : character.Skill2?.cardName;
            var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            var body = Open(panel, slot == 0 ? "ULTIMATE DETAILS" : "SKILL DETAILS", true);
            var preview = new VisualElement { name = "hub-card-rank-preview" }; preview.AddToClassList("hub-reference-preview"); body.Add(preview);
            var choices = new VisualElement(); choices.AddToClassList(slot == 0 ? "hub-ultimate-levels" : "hub-rank-cards");
            if (slot == 0)
            {
                preview.Add(CardFace(character, slot, 3, art));
                preview.Add(Label("CURRENT LEVEL " + currentTier, "hub-current-ultimate-level"));
            }
            preview.Add(choices);
            var copy = new VisualElement { name = "hub-card-rank-description" }; copy.AddToClassList("hub-reference-copy"); body.Add(copy);
            var buttons = new Dictionary<int, Button>();
            Action<int> inspect = chosen =>
            {
                foreach (var choice in buttons) choice.Value.EnableInClassList("selected", choice.Key == chosen);
                copy.Clear();
                var data = skill?.Ranks.FirstOrDefault(r => r.Rank == chosen);
                var card = new CardState { Kind = slot == 0 ? CardKind.Ultimate : CardKind.Skill, Rank = chosen,
                    SkillId = skill?.Id, UltimateTier = chosen, Category = slot == 0 ? CardCategory.Ultimate : data?.Category ?? CardCategory.Attack,
                    EffectCategory = data?.Category ?? CardCategory.Ultimate };
                var details = CombatDetailFormatter.ExtractCardDetails(card, owner, character);
                Header(copy, title, details.statuses, art?.SkillIcon(card, owner));
                copy.Add(Label(details.desc, "hub-tooltip-description", true));
                if (!string.IsNullOrEmpty(details.keywords)) copy.Add(Label(details.keywords, "hub-tooltip-keywords", true));
            };
            for (var i = slot == 0 ? 0 : 1; i <= (slot == 0 ? 5 : 3); i++)
            {
                var chosen = i;
                if (slot != 0 && i > 1) choices.Add(Label("→", "hub-rank-arrow"));
                var button = new Button(() => inspect(chosen)) { name = "hub-detail-rank-" + i, tooltip = slot == 0 ? "Inspect ultimate level " + i : "Inspect rank " + i };
                button.AddToClassList(slot == 0 ? "hub-ultimate-level" : "hub-rank-card-choice");
                if (slot != 0) button.Add(CardFace(character, slot, i, art));
                button.Add(Label(slot == 0 ? "Lv " + i : "R" + i, "hub-rank-caption"));
                if (slot == 0 && i == currentTier) { button.AddToClassList("current-level"); button.Add(Label("CURRENT", "hub-level-current-marker")); }
                choices.Add(button); buttons.Add(i, button);
            }
            inspect(Mathf.Clamp(rank, slot == 0 ? 0 : 1, slot == 0 ? 5 : 3));
        }

        public static void Passive(VisualElement panel, CharacterObject character, int tier)
        {
            var owner = new FighterState { Definition = CharacterObjectCatalogBuilder.CreateDefinition(character), ConstellationTier = tier };
            var details = CombatDetailFormatter.ExtractPassiveDetails(owner, character);
            var body = Open(panel, "PASSIVE", true);
            var preview = new VisualElement(); preview.AddToClassList("hub-reference-preview"); body.Add(preview);
            var icon = new Image { name = "hub-passive-preview", sprite = details.Icon != null ? details.Icon : character.FighterIcon, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("hub-passive-preview"); preview.Add(icon);
            var copy = new VisualElement(); copy.AddToClassList("hub-reference-copy"); body.Add(copy);
            var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            Header(copy, details.Title, details.Statuses, null, art?.PassiveConditions(details.SourceType, details.Restriction, details.Availability));
            copy.Add(Label(details.FormattedDescription, "hub-tooltip-description", true));
            if (!string.IsNullOrEmpty(details.Keywords)) copy.Add(Label(details.Keywords, "hub-tooltip-keywords", true));
        }

        private static VisualElement CardFace(CharacterObject character, int slot, int rank, CharacterKitArtSO art)
        {
            var face = new VisualElement(); face.AddToClassList("hub-detail-card-face");
            var skill = slot == 1 ? character.Skill1 : character.Skill2;
            var artwork = slot == 0 ? character.Ultimate?.icon : skill?.cardIcon;
            if (artwork == null) artwork = character.FighterIcon;
            var image = new Image { sprite = artwork, scaleMode = ScaleMode.ScaleAndCrop };
            image.AddToClassList("hub-detail-card-art"); face.Add(image);
            var frame = new Image { image = art?.Rank(rank), scaleMode = ScaleMode.StretchToFill }; frame.AddToClassList("hub-detail-card-frame"); face.Add(frame);
            var type = slot == 0 ? null : art?.Type(skill?.GetRankData(rank)?.skillType ?? SkillType.Attack);
            if (type != null) { var badge = new Image { sprite = type, scaleMode = ScaleMode.ScaleToFit }; badge.AddToClassList("hub-detail-card-type"); face.Add(badge); }
            face.pickingMode = PickingMode.Ignore; foreach (var child in face.Children()) child.pickingMode = PickingMode.Ignore;
            return face;
        }

        private static void Header(VisualElement body, string title, IEnumerable<CombatStatusBadgeData> badges, Sprite type, IEnumerable<(Sprite icon, string label)> conditions = null)
        {
            var header = new VisualElement(); header.AddToClassList("hub-tooltip-header");
            var iconGroup = new VisualElement(); iconGroup.AddToClassList("hub-tooltip-icons");
            if (type != null) iconGroup.Add(new Image { sprite = type, scaleMode = ScaleMode.ScaleToFit });
            if (type != null) header.Add(iconGroup);
            var titleLabel = new Label(title); titleLabel.AddToClassList("hub-tooltip-title");
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.flexShrink = 1;
            titleLabel.style.minWidth = 0;
            header.Add(titleLabel);
            if (conditions != null)
            {
                var conditionGroup = new VisualElement { name = "hub-passive-conditions" }; conditionGroup.AddToClassList("hub-passive-conditions");
                foreach (var condition in conditions.Where(c => c.icon != null))
                    conditionGroup.Add(new Image { sprite = condition.icon, tooltip = condition.label, scaleMode = ScaleMode.ScaleToFit });
                header.Add(conditionGroup);
            }
            var badgeList = badges?.ToList();
            if (badgeList != null && badgeList.Count > 0)
            {
                var statusGroup = new VisualElement(); statusGroup.AddToClassList("hub-tooltip-status-grid");
                foreach (var badge in badgeList)
                    statusGroup.Add(new Image { sprite = badge.Icon, tooltip = badge.Name, scaleMode = ScaleMode.ScaleToFit });
                header.Add(statusGroup);
            }
            body.Add(header);
        }

        private static Label Label(string text, string style, bool rich = false)
        {
            var label = new Label(text ?? string.Empty) { enableRichText = rich };
            label.AddToClassList(style);
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static string TargetLabel(EffectTargetScope? target) => target switch
        {
            EffectTargetScope.Self => "Self", EffectTargetScope.SelectedEnemy => "Single enemy",
            EffectTargetScope.SelectedAlly => "Single ally", EffectTargetScope.AllEnemies => "All enemies",
            EffectTargetScope.AllAllies => "All allies", EffectTargetScope.TriggerActor => "Triggering fighter",
            EffectTargetScope.TriggerTarget => "Trigger target", EffectTargetScope.StatusOwner => "Affected fighter", _ => "Not authored"
        };
    }
}
