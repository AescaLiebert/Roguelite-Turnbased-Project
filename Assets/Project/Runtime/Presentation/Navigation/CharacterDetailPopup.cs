using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            overlay.RemoveFromHierarchy(); if (previous?.panel != null) previous.Focus(); return true;
        }

        private static VisualElement Open(VisualElement panel, string title)
        {
            var previous = panel.Q("hub-detail-overlay")?.userData as VisualElement ?? panel.panel?.focusController.focusedElement as VisualElement;
            Close(panel);
            var overlay = new VisualElement { name = "hub-detail-overlay", focusable = true, userData = previous };
            overlay.AddToClassList("hub-detail-overlay");
            var dialog = new VisualElement(); dialog.AddToClassList("hub-detail-dialog");
            var heading = new VisualElement(); heading.AddToClassList("hub-detail-heading");
            var titleLabel = Label(title, "hub-detail-title");
            titleLabel.style.flexShrink = 1;
            titleLabel.style.minWidth = 0;
            heading.Add(titleLabel);
            var close = new Button(() => Close(panel)) { text = "×", name = "hub-detail-close" }; close.AddToClassList("action-button"); heading.Add(close);
            dialog.Add(heading);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("hub-detail-scroll");
            scroll.contentContainer.style.flexShrink = 0;
            scroll.contentContainer.style.minHeight = Length.Percent(100);
            dialog.Add(scroll);
            overlay.Add(dialog); panel.Add(overlay);
            overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) Close(panel); evt.StopPropagation(); });
            overlay.Focus(); return scroll.contentContainer;
        }

        public static void Stats(VisualElement panel, CharacterObject character)
        {
            var body = Open(panel, "ALL CHARACTER STATS");
            var stats = PlayerInventoryService.Instance.GetCharacterStats(character);
            foreach (StatId stat in Enum.GetValues(typeof(StatId)))
            {
                var row = new VisualElement { name = "hub-detail-stat-" + stat }; row.AddToClassList("hub-detail-stat");
                row.Add(new Label(StatName(stat)));
                row.Add(new Label(stat >= StatId.Pierce ? (stats.Get(stat) / 100d).ToString("0.##") + "%" : stats.Get(stat).ToString("N0")));
                body.Add(row);
            }
        }

        public static string StatName(StatId stat) => stat == StatId.MaxHealth ? "Max HP" : stat == StatId.CombatClass ? "Combat Class" :
            Regex.Replace(stat.ToString(), "([a-z])([A-Z])", "$1 $2");

        public static void Card(VisualElement panel, CharacterObject character, int slot, int rank, int currentTier)
        {
            var definition = CharacterObjectCatalogBuilder.CreateDefinition(character);
            var owner = new FighterState { Definition = definition, ConstellationTier = currentTier };
            var skill = definition.Skills.FirstOrDefault(s => s.Slot == slot);
            var data = skill?.Ranks.FirstOrDefault(r => r.Rank == rank);
            var ultimate = definition.UltimateTiers.FirstOrDefault(t => t.Tier == rank);
            var card = new CardState { Kind = slot == 0 ? CardKind.Ultimate : CardKind.Skill, Rank = rank,
                SkillId = skill?.Id, UltimateTier = rank, Category = slot == 0 ? CardCategory.Ultimate : data?.Category ?? CardCategory.Attack,
                EffectCategory = data?.Category ?? CardCategory.Ultimate };
            var title = slot == 0 ? character.Ultimate?.ultimateName : slot == 1 ? character.Skill1?.cardName : character.Skill2?.cardName;
            var body = Open(panel, title ?? "CARD DETAILS");
            var choices = new VisualElement(); choices.AddToClassList("hub-detail-choices");
            for (var i = slot == 0 ? 0 : 1; i <= (slot == 0 ? 5 : 3); i++)
            {
                var chosen = i;
                var button = new Button(() => Card(panel, character, slot, chosen, currentTier)) { text = slot == 0 ? "C" + i : "R" + i, name = "hub-detail-rank-" + i };
                button.EnableInClassList("selected", rank == i); choices.Add(button);
            }
            body.Add(choices);
            var details = CombatDetailFormatter.ExtractCardDetails(card, owner, character);
            var type = slot == 0 ? SkillType.Ultimate : (slot == 1 ? character.Skill1 : character.Skill2)?.ranks?.ElementAtOrDefault(rank - 1)?.skillType ?? SkillType.Attack;
            Header(body, character, title, details.statuses, Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt")?.Type(type));
            var scope = slot == 0 ? ultimate?.Effect?.Target : data?.TargetScope;
            body.Add(Label((slot == 0 ? "ULTIMATE · C" : "RANK ") + rank + " · " + (type == SkillType.DebuffAtk ? "Attack + debuff" : type.ToString()) + " · " + TargetLabel(scope), "hub-note"));
            body.Add(Label(details.desc, "hub-tooltip-description", true));
            if (!string.IsNullOrEmpty(details.keywords)) body.Add(Label(details.keywords, "hub-tooltip-keywords", true));
        }

        public static void Passive(VisualElement panel, CharacterObject character, int tier)
        {
            var owner = new FighterState { Definition = CharacterObjectCatalogBuilder.CreateDefinition(character), ConstellationTier = tier };
            var details = CombatDetailFormatter.ExtractPassiveDetails(owner, character);
            var body = Open(panel, "PASSIVE");
            Header(body, character, details.Title, details.Statuses, details.Icon);
            body.Add(Label(details.SourceType + " · " + details.Restriction, "hub-note"));
            body.Add(Label(details.FormattedDescription, "hub-tooltip-description", true));
            if (!string.IsNullOrEmpty(details.Keywords)) body.Add(Label(details.Keywords, "hub-tooltip-keywords", true));
        }

        private static void Header(VisualElement body, CharacterObject character, string title, IEnumerable<CombatStatusBadgeData> badges, Sprite type)
        {
            var header = new VisualElement(); header.AddToClassList("hub-tooltip-header");
            var iconGroup = new VisualElement(); iconGroup.AddToClassList("hub-tooltip-icons");
            iconGroup.Add(new Image { sprite = character.FighterIcon, scaleMode = ScaleMode.ScaleToFit });
            if (type != null) iconGroup.Add(new Image { sprite = type, scaleMode = ScaleMode.ScaleToFit });
            header.Add(iconGroup);
            var titleLabel = new Label(title); titleLabel.AddToClassList("hub-tooltip-title");
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            titleLabel.style.flexShrink = 1;
            titleLabel.style.minWidth = 0;
            header.Add(titleLabel);
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
