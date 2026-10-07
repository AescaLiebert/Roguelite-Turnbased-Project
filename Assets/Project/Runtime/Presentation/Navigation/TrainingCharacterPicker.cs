using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    public static class TrainingCharacterPicker
    {
        public static void Show(VisualElement root)
        {
            if (root.Q("training-picker") != null) return;
            var characters = CharacterObjectRegistrySO.LoadAll().Where(c => c != null).ToList();
            var overlay = new VisualElement { name = "training-picker" };
            overlay.AddToClassList("training-overlay");
            var panel = new VisualElement(); panel.AddToClassList("training-panel"); overlay.Add(panel);
            var heading = new VisualElement(); heading.AddToClassList("training-toolbar"); panel.Add(heading);
            heading.Add(new Label("TRAINING / CHOOSE A FIGHTER") { name = "training-title" });
            var close = new Button(() => { overlay.RemoveFromHierarchy(); root.Q<Button>("training")?.Focus(); }) { text = "CLOSE" };
            heading.Add(close);
            panel.Add(new Label("Practice with any ready fighter. One action each turn. All skill ranks and ultimate are replenished; ultimate is free. No rewards."));
            var filters = new VisualElement(); filters.AddToClassList("training-toolbar"); panel.Add(filters);
            var search = new TextField("Search"); filters.Add(search);
            var attribute = new DropdownField("Attribute", new[] { "All" }.Concat(Enum.GetNames(typeof(FighterAttribute))).ToList(), 0); filters.Add(attribute);
            var rarity = new DropdownField("Rarity", new[] { "All" }.Concat(Enum.GetNames(typeof(FighterRarity))).ToList(), 0); filters.Add(rarity);
            var series = new DropdownField("Series", new[] { "All" }.Concat(characters.Select(c => c.SeriesId).Where(s => !string.IsNullOrEmpty(s)).Distinct().OrderBy(s => s)).ToList(), 0); filters.Add(series);
            var sort = new DropdownField("Sort", new List<string> { "Name", "Rarity", "Character ID" }, 0); filters.Add(sort);
            var tier = new DropdownField("Constellation", Enumerable.Range(0, 7).Select(i => "C" + i).ToList(), 0); filters.Add(tier);
            var status = new Label(); status.AddToClassList("training-status"); panel.Add(status);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("training-scroll"); panel.Add(scroll);
            var grid = new VisualElement(); grid.AddToClassList("training-grid"); scroll.Add(grid);
            Action refresh = () =>
            {
                grid.Clear();
                var filtered = characters.Where(c => (string.IsNullOrWhiteSpace(search.value) ||
                    (c.FighterName ?? "").IndexOf(search.value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.DefinitionId ?? "").IndexOf(search.value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) &&
                    (attribute.index == 0 || c.FighterAttribute.ToString() == attribute.value) &&
                    (rarity.index == 0 || c.FighterRarity.ToString() == rarity.value) &&
                    (series.index == 0 || c.SeriesId == series.value));
                var ordered = sort.index == 1 ? filtered.OrderByDescending(c => c.FighterRarity).ThenBy(c => c.FighterName) :
                    sort.index == 2 ? filtered.OrderBy(c => c.ID).ThenBy(c => c.FighterName) : filtered.OrderBy(c => c.FighterName);
                var visible = ordered.ToList();
                status.text = visible.Count + " / " + characters.Count + " fighters · Draft fighters are unavailable";
                foreach (var character in visible)
                {
                    var button = new Button(() =>
                    {
                        try
                        {
                            TrainingContext.Prepare(character, tier.index);
                            SceneManager.LoadScene("Training");
                        }
                        catch (Exception exception) { status.text = exception.Message; }
                    });
                    button.AddToClassList("training-character");
                    button.Add(CharacterIconView.Create(character, 110));
                    button.Add(new Label(character.FighterName));
                    button.Add(new Label(character.RuntimeReady ? character.FighterRarity + " / " + character.FighterAttribute : "DRAFT"));
                    button.SetEnabled(character.RuntimeReady);
                    grid.Add(button);
                }
                if (visible.Count == 0) grid.Add(new Label("No fighters match these filters."));
            };
            search.RegisterValueChangedCallback(_ => refresh());
            attribute.RegisterValueChangedCallback(_ => refresh());
            rarity.RegisterValueChangedCallback(_ => refresh());
            series.RegisterValueChangedCallback(_ => refresh());
            sort.RegisterValueChangedCallback(_ => refresh());
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == UnityEngine.KeyCode.Escape) { overlay.RemoveFromHierarchy(); evt.StopPropagation(); } });
            root.Add(overlay);
            refresh(); search.Focus();
        }
    }
}
