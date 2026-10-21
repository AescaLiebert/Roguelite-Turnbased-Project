#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Navigation;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private int _kitRank = 1, _kitTier;
        [SerializeField] private int _overviewSkill1Rank = 1, _overviewSkill2Rank = 1;
        private static readonly DefaultContractResolver KitContracts = new DefaultContractResolver();
        private VisualElement _kitProfileIconPreview;
        private CharacterObject _kitTrackedSource;
        private string _kitSavedSourceFingerprint;
        private bool _kitSourceArtDirty;
        private CharacterDefinition EditableKit() => StudioDocumentService.BeginKitEditing(_document);

        private void KitHeader(string title, string subtitle, bool includeAssetSave = true)
        {
            Body(_host, "KIT / " + title.ToUpperInvariant(), "studio-breadcrumb");
            Heading(_host, title);
            if (!string.IsNullOrWhiteSpace(subtitle)) Body(_host, subtitle, "studio-subtle");
            if (!includeAssetSave) return;
            Section(_inspector, "SOURCE & OWNERSHIP");
            Body(_inspector, _document.SourceCharacterId, "studio-code");
            Body(_inspector, _draft.IsDirectSession ? "Unsaved edits stay in memory until Save character or Ctrl+S commits this CharacterObject and its four referenced data SOs." : "Compatibility draft: edits remain separate until saved or published.", "studio-subtle");
            Button(_inspector, "Create new character", () => ShowWorkspace("bundle"));
            Button(_inspector, "Test in Battle Lab", () => ShowWorkspace("lab"));
        }

        private void BuildKitOverview()
        {
            var c = _document.EditingCharacter;
            KitHeader("Overview", null);
            var source = _draft?.SourceCharacter;
            BuildKitProfileBanner(_host, c, source, true, true);

            Section(_host, "LIVE GAME DESCRIPTION PREVIEW");
            var skillPreviews = new VisualElement(); skillPreviews.AddToClassList("studio-preview-grid"); _host.Add(skillPreviews);
            BuildSkillDescriptionPreview(skillPreviews, c, source, 1);
            BuildSkillDescriptionPreview(skillPreviews, c, source, 2);
            BuildUltimateDescriptionPreview(skillPreviews, c, source);
            BuildPassiveDescriptionPreview(_host, c, source);
        }

        private void BuildKitIdentity()
        {
            var c = EditableKit();
            KitHeader("Identity & stats", null);
            var source = _draft?.SourceCharacter;
            BuildKitProfileBanner(_host, c, source, true, false);

            Section(_host, "ART ASSETS");
            var art = new VisualElement(); art.AddToClassList("studio-art-grid"); art.AddToClassList("studio-art-grid-compact"); _host.Add(art);
            AddKitSpriteEditorTile(art, "Character icon sprite", source, "fighterIcon", source?.FighterIcon, false, "FighterIcon");
            AddKitSpriteEditorTile(art, "Full artwork", source, "fighterFull", source?.FighterFull, true, "FighterFull");
            AddKitSpriteEditorTile(art, "Skill 1 card", source?.Skill1, "cardIcon", source?.Skill1?.cardIcon, true, "SkillCardSO.cardIcon");
            AddKitSpriteEditorTile(art, "Skill 2 card", source?.Skill2, "cardIcon", source?.Skill2?.cardIcon, true, "SkillCardSO.cardIcon");
            AddKitSpriteEditorTile(art, "Ultimate card", source?.Ultimate, "icon", source?.Ultimate?.icon, true, "UltimateCardSO.icon");
            AddKitSpriteEditorTile(art, "Passive art", source, "fighterPassive", source?.FighterPassive, false, "FighterPassive");

            var identityColumns = new VisualElement(); identityColumns.AddToClassList("studio-kit-columns"); _host.Add(identityColumns);
            var identity = Node(identityColumns, "CHARACTER IDENTITY");
            identity.AddToClassList("studio-kit-column");
            TextControl(identity, "Display name", c.DisplayName, v => { c.DisplayName = v; SetNativeCharacterStringPreview(source, "fighterName", v, "Edit fighter name"); });
            TextControl(identity, "Family ID", c.FamilyId, v => { c.FamilyId = v; SetNativeCharacterStringPreview(source, "familyId", v, "Edit family ID"); });
            TextControl(identity, "Series ID", c.SeriesId, v => { c.SeriesId = v; SetNativeCharacterStringPreview(source, "seriesId", v, "Edit series ID"); });
            TextControl(identity, "Role", c.Role, v => { c.Role = v; SetNativeCharacterStringPreview(source, "role", v, "Edit role"); });
            EnumControl(identity, "Attribute", ParseContentEnum(c.AttributeId, "attribute.", source?.FighterAttribute ?? FighterAttribute.Blue), value =>
            {
                var selected = (FighterAttribute)value;
                c.AttributeId = ContentEnumId("attribute.", selected);
                SetNativeCharacterPreview(source, "Edit character attribute", () => source.FighterAttribute = selected);
            });
            EnumControl(identity, "Rarity", ParseContentEnum(c.RarityId, "rarity.", source?.FighterRarity ?? FighterRarity.SR), value =>
            {
                var selected = (FighterRarity)value;
                c.RarityId = ContentEnumId("rarity.", selected);
                SetNativeCharacterPreview(source, "Edit character rarity", () => source.FighterRarity = selected);
            });
            BuildTraitTagEditor(identity, c);

            var stats = Node(_host, "LEVEL-ONE STATS");
            var statGrid = new VisualElement(); statGrid.AddToClassList("studio-stat-grid"); stats.Add(statGrid);
            KitStatTile(statGrid, "ATK", "Attack", c.BaseStats.Attack, v => c.BaseStats.Attack = v, "kit.stats.Attack");
            KitStatTile(statGrid, "DEF", "Defense", c.BaseStats.Defense, v => c.BaseStats.Defense = v, "kit.stats.Defense");
            KitStatTile(statGrid, "HP", "Max Health", c.BaseStats.MaxHealth, v => c.BaseStats.MaxHealth = v, "kit.stats.MaxHealth");
            KitStatTile(statGrid, "CC", "Combat Class", c.BaseStats.CombatClass, v => c.BaseStats.CombatClass = v, "kit.stats.CombatClass");
            KitFold(stats, "Secondary stats (%)", "stats.secondary", p => BuildSecondaryStatEditors(p, c.BaseStats));
            var growth = Node(_host, "GROWTH PER LEVEL (%)");
            GrowthPercentControl(growth, "Attack growth", c.StatGrowth.AttackBp, v => c.StatGrowth.AttackBp = v);
            GrowthPercentControl(growth, "Defense growth", c.StatGrowth.DefenseBp, v => c.StatGrowth.DefenseBp = v);
            GrowthPercentControl(growth, "Health growth", c.StatGrowth.HealthBp, v => c.StatGrowth.HealthBp = v);
        }

        private void BuildKitProfileBanner(VisualElement parent, CharacterDefinition character, CharacterObject source, bool includeStats, bool includeEditAction)
        {
            EnsureKitSourceTracking(source);
            var banner = new VisualElement(); banner.AddToClassList("studio-profile-banner"); parent.Add(banner);
            var main = new VisualElement(); main.AddToClassList("studio-profile-main-row"); banner.Add(main);
            var icon = source == null ? new VisualElement() : CharacterIconView.Create(source, 132);
            icon.name = "studio-character-icon";
            icon.AddToClassList("studio-profile-icon");
            icon.tooltip = source == null ? "No character icon assigned" : source.FighterName;
            main.Add(icon);
            _kitProfileIconPreview = icon;
            var details = new VisualElement(); details.AddToClassList("studio-profile-details"); main.Add(details);
            KitLiveLabel(details, () => character.DisplayName, "studio-profile-name");
            KitLiveLabel(details, () => character.SeriesId + "   ·   " + character.Role, "studio-profile-meta");
            var tags = new VisualElement(); tags.AddToClassList("studio-profile-tags"); details.Add(tags);
            AddProfileTag(tags, () => character.AttributeId);
            AddProfileTag(tags, () => character.RarityId);
            AddProfileTag(tags, () => character.FamilyId);
            if (!includeStats) return;
            var stats = new VisualElement(); stats.AddToClassList("studio-profile-stats"); banner.Add(stats);
            AddProfileStat(stats, "ATK", () => character.BaseStats.Attack.ToString());
            AddProfileStat(stats, "DEF", () => character.BaseStats.Defense.ToString());
            AddProfileStat(stats, "HP", () => character.BaseStats.MaxHealth.ToString());
            AddProfileStat(stats, "CC", () => character.BaseStats.CombatClass.ToString());
            if (includeEditAction)
            {
                var actions = new VisualElement(); actions.AddToClassList("studio-profile-actions"); banner.Add(actions);
                Button(actions, "Edit identity & stats", () => ShowWorkspace("identity"));
            }
        }

        private static void AddProfileTag(VisualElement parent, Func<string> getText)
        {
            var label = new Label(getText() ?? "—") { userData = getText };
            label.AddToClassList("studio-profile-tag"); label.AddToClassList("studio-kit-live-summary"); parent.Add(label);
        }

        private static void AddProfileStat(VisualElement parent, string label, Func<string> getValue)
        {
            var stat = new VisualElement(); stat.AddToClassList("studio-profile-stat"); parent.Add(stat);
            Body(stat, label, "studio-profile-stat-label");
            var value = new Label(getValue()) { userData = getValue };
            value.AddToClassList("studio-profile-stat-value"); value.AddToClassList("studio-kit-live-summary"); stat.Add(value);
        }

        private static Label KitLiveLabel(VisualElement parent, Func<string> getText, string css, bool richText = false)
        {
            var label = new Label(getText() ?? "") { enableRichText = richText, userData = getText };
            label.AddToClassList(css); label.AddToClassList("studio-kit-live-summary"); parent.Add(label);
            return label;
        }

        private static T ParseContentEnum<T>(string value, string prefix, T fallback) where T : struct
        {
            var raw = value ?? string.Empty;
            if (raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(prefix.Length);
            return Enum.TryParse(raw, true, out T parsed) ? parsed : fallback;
        }

        private static string ContentEnumId<T>(string prefix, T value) where T : struct =>
            prefix + value.ToString().ToLowerInvariant();

        private void GrowthPercentControl(VisualElement parent, string label, int basisPoints, Action<int> setter)
        {
            BasisPointPercentControl(parent, label, basisPoints, setter);
        }

        private void BasisPointPercentControl(VisualElement parent, string label, int basisPoints, Action<int> setter)
        {
            var field = new FloatField(label) { value = basisPoints / 100f };
            field.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(Mathf.RoundToInt(e.newValue * 100f))));
            parent.Add(field);
        }

        private void BuildSecondaryStatEditors(VisualElement parent, StatBlock stats)
        {
            BasisPointPercentControl(parent, "Pierce", stats.PierceBp, value => stats.PierceBp = value);
            BasisPointPercentControl(parent, "Resistance", stats.ResistanceBp, value => stats.ResistanceBp = value);
            BasisPointPercentControl(parent, "Regeneration", stats.RegenerationBp, value => stats.RegenerationBp = value);
            BasisPointPercentControl(parent, "Critical chance", stats.CritChanceBp, value => stats.CritChanceBp = value);
            BasisPointPercentControl(parent, "Critical damage", stats.CritDamageBp, value => stats.CritDamageBp = value);
            BasisPointPercentControl(parent, "Critical resistance", stats.CritResistanceBp, value => stats.CritResistanceBp = value);
            BasisPointPercentControl(parent, "Critical defense", stats.CritDefenseBp, value => stats.CritDefenseBp = value);
            BasisPointPercentControl(parent, "Recovery", stats.RecoveryBp, value => stats.RecoveryBp = value);
            BasisPointPercentControl(parent, "Block chance", stats.BlockChanceBp, value => stats.BlockChanceBp = value);
            BasisPointPercentControl(parent, "Block power", stats.BlockPowerBp, value => stats.BlockPowerBp = value);
            BasisPointPercentControl(parent, "Life steal", stats.LifeStealBp, value => stats.LifeStealBp = value);
            BasisPointPercentControl(parent, "Evade", stats.EvadeBp, value => stats.EvadeBp = value);
            BasisPointPercentControl(parent, "Perception", stats.PerceptionBp, value => stats.PerceptionBp = value);
            BasisPointPercentControl(parent, "Control", stats.ControlBp, value => stats.ControlBp = value);
            BasisPointPercentControl(parent, "Avoidance", stats.AvoidanceBp, value => stats.AvoidanceBp = value);
            BasisPointPercentControl(parent, "Reflect damage", stats.ReflectDamageBp, value => stats.ReflectDamageBp = value);
            BasisPointPercentControl(parent, "Damage over time dealt", stats.DamageOverTimeDealtBp, value => stats.DamageOverTimeDealtBp = value);
            BasisPointPercentControl(parent, "Damage dealt", stats.DamageDealtBp, value => stats.DamageDealtBp = value);
            BasisPointPercentControl(parent, "Additional damage dealt", stats.AdditionalDamageDealtBp, value => stats.AdditionalDamageDealtBp = value);
        }

        private void BuildTraitTagEditor(VisualElement parent, CharacterDefinition character)
        {
            if (character.TraitIds == null) character.TraitIds = new List<string>();
            var box = Node(parent, "TRAITS"); box.AddToClassList("studio-trait-editor");
            var selected = new VisualElement(); selected.AddToClassList("studio-trait-tags"); box.Add(selected);
            var search = new TextField("Search traits") { name = "kit.traits.search" };
            box.Add(search);
            var suggestions = new VisualElement(); suggestions.AddToClassList("studio-trait-suggestions"); box.Add(suggestions);

            Action renderSelected = null;
            Action<string> renderSuggestions = null;
            Action<string> addTrait = null;
            addTrait = raw =>
            {
                var traitId = NormalizeTraitId(raw);
                if (string.IsNullOrWhiteSpace(traitId) || character.TraitIds.Contains(traitId, StringComparer.OrdinalIgnoreCase)) return;
                Edit("Add character trait", () => character.TraitIds.Add(traitId));
                SetNativeCharacterTraitsPreview(_draft?.SourceCharacter, character.TraitIds);
                search.SetValueWithoutNotify(string.Empty);
                renderSelected(); renderSuggestions(string.Empty);
            };

            renderSelected = () =>
            {
                selected.Clear();
                for (var i = 0; i < character.TraitIds.Count; i++)
                {
                    var traitId = character.TraitIds[i];
                    var chip = new VisualElement(); chip.AddToClassList("studio-trait-chip"); chip.tooltip = traitId; selected.Add(chip);
                    var label = new Label(traitId); label.AddToClassList("studio-trait-chip-label"); chip.Add(label);
                    var earlier = Button(chip, "↑", () =>
                    {
                        var index = character.TraitIds.IndexOf(traitId);
                        if (index <= 0) return;
                        Edit("Move character trait", () => { character.TraitIds.RemoveAt(index); character.TraitIds.Insert(index - 1, traitId); });
                        SetNativeCharacterTraitsPreview(_draft?.SourceCharacter, character.TraitIds);
                        renderSelected();
                    }); earlier.AddToClassList("studio-trait-chip-action"); earlier.tooltip = "Move trait earlier";
                    var later = Button(chip, "↓", () =>
                    {
                        var index = character.TraitIds.IndexOf(traitId);
                        if (index < 0 || index >= character.TraitIds.Count - 1) return;
                        Edit("Move character trait", () => { character.TraitIds.RemoveAt(index); character.TraitIds.Insert(index + 1, traitId); });
                        SetNativeCharacterTraitsPreview(_draft?.SourceCharacter, character.TraitIds);
                        renderSelected();
                    }); later.AddToClassList("studio-trait-chip-action"); later.tooltip = "Move trait later";
                    var remove = Button(chip, "×", () =>
                    {
                        Edit("Remove character trait", () => character.TraitIds.Remove(traitId));
                        SetNativeCharacterTraitsPreview(_draft?.SourceCharacter, character.TraitIds);
                        renderSelected(); renderSuggestions(search.value);
                    }); remove.AddToClassList("studio-trait-chip-action"); remove.tooltip = "Remove trait";
                }
            };

            renderSuggestions = query =>
            {
                suggestions.Clear();
                query = (query ?? string.Empty).Trim();
                var known = KnownTraitIds(character).Where(id => !character.TraitIds.Contains(id, StringComparer.OrdinalIgnoreCase));
                var matches = string.IsNullOrEmpty(query) ? known.Take(8).ToList() : known.Where(id => id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
                foreach (var traitId in matches) Button(suggestions, traitId, () => addTrait(traitId)).AddToClassList("studio-trait-suggestion");
                if (!string.IsNullOrEmpty(query))
                {
                    var customId = NormalizeTraitId(query);
                    if (!character.TraitIds.Contains(customId, StringComparer.OrdinalIgnoreCase) && !matches.Contains(customId, StringComparer.OrdinalIgnoreCase))
                        Button(suggestions, "+ " + customId, () => addTrait(customId)).AddToClassList("studio-trait-suggestion");
                }
            };
            search.RegisterValueChangedCallback(e => renderSuggestions(e.newValue));
            renderSelected(); renderSuggestions(string.Empty);
        }

        private static IEnumerable<string> KnownTraitIds(CharacterDefinition character)
        {
            return CharacterObjectRegistrySO.LoadAll()
                .Where(item => item != null && item.TraitIds != null)
                .SelectMany(item => item.TraitIds)
                .Concat(character?.TraitIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeTraitId(string value)
        {
            var id = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '-');
            return string.IsNullOrEmpty(id) || id.StartsWith("trait.", StringComparison.Ordinal) ? id : "trait." + id;
        }

        private void AddKitSpriteEditorTile(VisualElement parent, string title, UnityEngine.Object owner, string propertyName, Sprite sprite, bool portrait, string fieldLabel)
        {
            var tile = new VisualElement(); tile.AddToClassList("studio-art-tile"); parent.Add(tile);
            Body(tile, title, "studio-art-title");
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, name = "studio-art-image" };
            image.AddToClassList("studio-art-image");
            image.AddToClassList(portrait ? "studio-art-portrait-preview" : "studio-art-square-preview");
            if (sprite == null) image.AddToClassList("studio-art-missing");
            tile.Add(image);
            AddKitSpriteWireField(tile, fieldLabel, owner, propertyName, sprite, value =>
            {
                image.sprite = value;
                image.EnableInClassList("studio-art-missing", value == null);
                image.tooltip = value == null ? "No sprite assigned" : value.name;
            });
        }

        private void AddKitSpriteWireField(VisualElement parent, string label, UnityEngine.Object owner, string propertyName, Sprite current, Action<Sprite> onApplied)
        {
            var field = new ObjectField(label) { objectType = typeof(Sprite), allowSceneObjects = false, value = current };
            field.AddToClassList("studio-sprite-wire-field");
            field.SetEnabled(owner != null);
            field.tooltip = owner == null ? "Open a character with this native asset assigned to set a sprite." : "Assign or replace the Sprite asset.";
            field.RegisterValueChangedCallback(e =>
            {
                if (owner == null) return;
                var serialized = new SerializedObject(owner);
                serialized.Update();
                var property = serialized.FindProperty(propertyName);
                if (property == null) { SetStatus("Missing sprite field: " + propertyName); return; }
                var value = e.newValue as Sprite;
                if (property.objectReferenceValue == value) return;
                Undo.RecordObject(owner, "Assign " + label);
                property.objectReferenceValue = value;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(owner);
                onApplied?.Invoke(value);
                RecordKitSourcePreviewChange(_draft?.SourceCharacter);
            });
            parent.Add(field);
        }

        private void SetNativeCharacterPreview(CharacterObject source, string undoLabel, Action change)
        {
            if (source == null) return;
            Undo.RecordObject(source, undoLabel);
            change?.Invoke();
            EditorUtility.SetDirty(source);
            if (_kitProfileIconPreview != null) CharacterIconView.Bind(_kitProfileIconPreview, source);
            RecordKitSourcePreviewChange(source);
        }

        private void SetNativeCharacterStringPreview(CharacterObject source, string propertyName, string value, string undoLabel)
        {
            if (source == null) return;
            var serialized = new SerializedObject(source);
            serialized.Update();
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            Undo.RecordObject(source, undoLabel);
            property.stringValue = value ?? string.Empty;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(source);
            if (_kitProfileIconPreview != null) CharacterIconView.Bind(_kitProfileIconPreview, source);
            RecordKitSourcePreviewChange(source);
        }

        private void SetNativeCharacterTraitsPreview(CharacterObject source, IList<string> traits)
        {
            if (source == null) return;
            var serialized = new SerializedObject(source);
            serialized.Update();
            var property = serialized.FindProperty("traitIds");
            if (property == null || !property.isArray) return;
            Undo.RecordObject(source, "Edit character traits");
            property.arraySize = traits?.Count ?? 0;
            for (var i = 0; i < property.arraySize; i++) property.GetArrayElementAtIndex(i).stringValue = traits[i];
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(source);
            RecordKitSourcePreviewChange(source);
        }

        private void EnsureKitSourceTracking(CharacterObject source)
        {
            if (source == null)
            {
                _kitTrackedSource = null; _kitSavedSourceFingerprint = null; _kitSourceArtDirty = false;
                return;
            }
            if (source == _kitTrackedSource) return;
            _kitTrackedSource = source;
            _kitSavedSourceFingerprint = StudioProductionBridge.SourceFingerprint(source);
            _kitSourceArtDirty = false;
        }

        private void RecordKitSourcePreviewChange(CharacterObject source)
        {
            if (source == null) return;
            EnsureKitSourceTracking(source);
            var fingerprint = StudioProductionBridge.SourceFingerprint(source);
            if (_draft != null && _draft.IsDirectSession && _draft.SourceCharacter == source)
                _draft.ImportedSourceHash = fingerprint;
            _kitSourceArtDirty = !string.Equals(fingerprint, _kitSavedSourceFingerprint, StringComparison.Ordinal);
            RefreshSaveState();
        }

        private void KitStatTile(VisualElement parent, string shortName, string label, int value, Action<int> setter, string path)
        {
            var tile = new VisualElement(); tile.AddToClassList("studio-stat-tile"); parent.Add(tile);
            Body(tile, shortName, "studio-stat-short");
            var valueLabel = new Label(value.ToString());
            valueLabel.AddToClassList("studio-stat-value");
            tile.Add(valueLabel);
            var field = new IntegerField(label) { value = value, name = path };
            field.RegisterValueChangedCallback(e =>
            {
                Edit("Edit " + label, () => setter(e.newValue));
                valueLabel.text = e.newValue.ToString();
            });
            tile.Add(field);
        }

        private void BuildSkillDescriptionPreview(VisualElement parent, CharacterDefinition character, CharacterObject source, int slot)
        {
            var skill = character.Skills.FirstOrDefault(s => s.Slot == slot);
            var cardAsset = slot == 1 ? source?.Skill1 : source?.Skill2;
            var selectedRank = slot == 1 ? _overviewSkill1Rank : _overviewSkill2Rank;
            var card = new VisualElement(); card.AddToClassList("studio-live-card"); card.name = "studio-live-skill-" + slot; parent.Add(card);
            var heading = new VisualElement(); heading.AddToClassList("studio-live-card-heading"); card.Add(heading);
            AddLivePreviewArtwork(heading, cardAsset?.cardIcon != null ? cardAsset.cardIcon : source?.FighterIcon, "studio-skill-card-art-" + slot, true);
            var title = new VisualElement(); title.AddToClassList("studio-live-card-title-group"); heading.Add(title);
            Body(title, "Skill " + slot, "studio-live-card-title");
            var badgeRow = new VisualElement(); badgeRow.AddToClassList("studio-live-card-badges"); heading.Add(badgeRow);

            var rankRow = new VisualElement(); rankRow.AddToClassList("studio-rank-preview-row"); card.Add(rankRow);
            var description = new Label { name = "studio-live-description", enableRichText = true };
            description.AddToClassList("studio-live-description"); card.Add(description);
            var keywords = LiveKeywordsLabel(card, "studio-live-keywords");
            for (var rank = 1; rank <= 3; rank++)
            {
                var previewRank = rank;
                var rankButton = Button(rankRow, "R" + rank, () =>
                {
                    if (slot == 1) _overviewSkill1Rank = previewRank; else _overviewSkill2Rank = previewRank;
                    selectedRank = previewRank;
                    UpdateCardDescriptionPreview(description, keywords, badgeRow, character, source,
                        new CardState { Kind = CardKind.Skill, Rank = selectedRank, SkillId = skill?.Id });
                    for (var i = 0; i < rankRow.childCount; i++) rankRow[i].EnableInClassList("studio-rank-preview-active", i + 1 == selectedRank);
                });
                rankButton.AddToClassList("studio-rank-preview");
                rankButton.EnableInClassList("studio-rank-preview-active", rank == selectedRank);
            }
            UpdateCardDescriptionPreview(description, keywords, badgeRow, character, source,
                new CardState { Kind = CardKind.Skill, Rank = selectedRank, SkillId = skill?.Id });
            Button(card, "Edit Skill " + slot, () => { _kitRank = selectedRank; ShowWorkspace("skill" + slot); });
        }

        private void BuildUltimateDescriptionPreview(VisualElement parent, CharacterDefinition character, CharacterObject source)
        {
            var ultimateAsset = source?.Ultimate;
            var selectedTier = _kitTier;
            var card = new VisualElement(); card.AddToClassList("studio-live-card"); card.name = "studio-live-ultimate"; parent.Add(card);
            var heading = new VisualElement(); heading.AddToClassList("studio-live-card-heading"); card.Add(heading);
            AddLivePreviewArtwork(heading, ultimateAsset?.icon != null ? ultimateAsset.icon : source?.FighterIcon, "studio-ultimate-card-art", true);
            var title = new VisualElement(); title.AddToClassList("studio-live-card-title-group"); heading.Add(title);
            Body(title, "Ultimate", "studio-live-card-title");
            var badgeRow = new VisualElement(); badgeRow.AddToClassList("studio-live-card-badges"); heading.Add(badgeRow);

            var tierRow = new VisualElement(); tierRow.AddToClassList("studio-rank-preview-row"); card.Add(tierRow);
            var description = new Label { name = "studio-live-ultimate-description", enableRichText = true };
            description.AddToClassList("studio-live-description"); card.Add(description);
            var keywords = LiveKeywordsLabel(card, "studio-live-keywords");
            for (var tier = 0; tier <= 5; tier++)
            {
                var previewTier = tier;
                var tierButton = Button(tierRow, "C" + tier, () =>
                {
                    _kitTier = previewTier;
                    selectedTier = previewTier;
                    UpdateCardDescriptionPreview(description, keywords, badgeRow, character, source,
                        new CardState { Kind = CardKind.Ultimate, Rank = selectedTier, UltimateTier = selectedTier });
                    for (var i = 0; i < tierRow.childCount; i++) tierRow[i].EnableInClassList("studio-rank-preview-active", i == selectedTier);
                });
                tierButton.AddToClassList("studio-rank-preview");
                tierButton.EnableInClassList("studio-rank-preview-active", tier == selectedTier);
            }
            UpdateCardDescriptionPreview(description, keywords, badgeRow, character, source,
                new CardState { Kind = CardKind.Ultimate, Rank = selectedTier, UltimateTier = selectedTier });
            Button(card, "Edit ultimate", () => { _kitTier = selectedTier; ShowWorkspace("ultimate"); });
        }

        private static Label LiveKeywordsLabel(VisualElement parent, string name)
        {
            var label = new Label { name = name, enableRichText = true };
            label.AddToClassList("studio-live-keywords");
            label.style.display = DisplayStyle.None;
            parent.Add(label);
            return label;
        }

        private static void UpdateCardDescriptionPreview(Label description, Label keywords, VisualElement badges,
            CharacterDefinition character, CharacterObject source, CardState card)
        {
            var owner = new FighterState { Definition = character };
            var details = CombatDetailFormatter.ExtractCardDetails(card, owner, source);
            description.text = details.desc ?? string.Empty;
            keywords.text = details.keywords ?? string.Empty;
            keywords.style.display = string.IsNullOrWhiteSpace(details.keywords) ? DisplayStyle.None : DisplayStyle.Flex;
            badges.Clear();

            var art = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            AddPreviewBadge(badges, art?.SkillIcon(card, owner), "Card type");
            foreach (var status in details.statuses ?? new List<CombatStatusBadgeData>())
                AddPreviewBadge(badges, status.Icon, status.Name);
        }

        private static void AddPreviewBadge(VisualElement parent, Sprite sprite, string label)
        {
            if (parent == null || sprite == null) return;
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, tooltip = label ?? sprite.name };
            image.AddToClassList("studio-live-card-badge");
            parent.Add(image);
        }

        private static void AddLivePreviewArtwork(VisualElement parent, Sprite sprite, string name, bool portrait)
        {
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, name = name };
            image.AddToClassList("studio-live-card-icon");
            if (portrait) image.AddToClassList("studio-live-card-icon-portrait");
            if (sprite == null) image.AddToClassList("studio-art-missing");
            image.tooltip = sprite == null ? "No card artwork assigned" : sprite.name;
            parent.Add(image);
        }

        private void BuildPassiveDescriptionPreview(VisualElement parent, CharacterDefinition character, CharacterObject source)
        {
            var card = new VisualElement(); card.AddToClassList("studio-live-card"); card.AddToClassList("studio-passive-preview"); parent.Add(card);
            var heading = new VisualElement(); heading.AddToClassList("studio-live-card-heading"); card.Add(heading);
            var owner = new FighterState { Definition = character, ConstellationTier = _kitTier };
            var details = CombatDetailFormatter.ExtractPassiveDetails(owner, source);
            AddLivePreviewArtwork(heading, details.Icon != null ? details.Icon : source?.FighterIcon, "studio-passive-art", false);
            var title = new VisualElement(); title.AddToClassList("studio-live-card-title-group"); heading.Add(title);
            Body(title, "Passive", "studio-live-card-title");
            var passiveBadges = new VisualElement(); passiveBadges.AddToClassList("studio-live-card-badges"); heading.Add(passiveBadges);
            var kitArt = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            foreach (var condition in kitArt?.PassiveConditions(details.SourceType, details.Restriction, StudioPassiveView.Gates(_document, true)) ?? Enumerable.Empty<(Sprite icon, string label)>())
                AddPreviewBadge(passiveBadges, condition.icon, condition.label);
            foreach (var status in details.Statuses ?? new List<CombatStatusBadgeData>())
                AddPreviewBadge(passiveBadges, status.Icon, status.Name);

            var description = new Label(details.FormattedDescription ?? string.Empty) { name = "studio-live-passive-description", enableRichText = true };
            description.AddToClassList("studio-live-description"); card.Add(description);
            if (!string.IsNullOrWhiteSpace(details.Keywords))
            {
                var keywords = LiveKeywordsLabel(card, "studio-live-passive-keywords");
                keywords.text = details.Keywords;
                keywords.style.display = DisplayStyle.Flex;
            }
            Button(card, "Edit passive", () => ShowWorkspace("passive"));
        }

        private void RankTabs(bool ultimate)
        {
            var row = Row(_host);
            for (var i = ultimate ? 0 : 1; i <= (ultimate ? 5 : 3); i++)
            {
                var index = i;
                var button = Button(row, (ultimate ? "C" : "Rank ") + i, () => { if (ultimate) _kitTier = index; else _kitRank = index; Rebuild(); ResetWorkspaceScroll(); });
                button.name = "studio-kit-" + (ultimate ? "tier-" : "rank-") + i;
                button.EnableInClassList("studio-active", (ultimate ? _kitTier : _kitRank) == i);
            }
        }

        private void BuildKitSkill(int slot)
        {
            BuildKitAbilityEditor(slot);
        }

        private void BuildKitUltimate()
        {
            BuildKitAbilityEditor(0);
        }

        private static string EffectSummary(EffectOperationDefinition e) => e == null ? "Missing effect" :
            Humanize(e.Kind.ToString()) + " · " + Humanize(e.Target.ToString()) + (e.Kind == EffectKind.Damage ? " · " + (e.CoefficientBp / 100f).ToString("0.##") + "% · " + (e.Attack?.ResolvedHitCount ?? 1) + " hits" : e.Kind == EffectKind.ApplyStatus ? " · " + e.StatusRecipe?.Id : "");

        private void DrawKitModifier(VisualElement parent, StatModifierDefinition m, bool continuous = false)
        {
            KitSummary(parent, () => StudioPassiveView.Modifier(m));
            var fields = new VisualElement(); parent.Add(fields);
            Action render = null;
            render = () => {
                fields.Clear();
                EnumControl(fields, "Modifier target", m.ResolvedTarget, v => {
                    m.Target = (ModifierTarget)v; m.SerializedTarget = -1;
                    if (m.Target != ModifierTarget.Stat && m.Target != ModifierTarget.StatBundle)
                    { m.Operation = m.Target == ModifierTarget.FlatDamageReduction ? ModifierOperation.Flat : ModifierOperation.PercentagePoints; m.SerializedOperation = -1; }
                    if (m.Target == ModifierTarget.StatBundle && m.ResolvedBundle == StatBundleKind.None)
                    { m.Bundle = StatBundleKind.BasicStats; m.SerializedBundle = -1; }
                    render();
                });
                fields[fields.childCount - 1].name = "studio-modifier-target";
                var target = m.ResolvedTarget;
                if (target == ModifierTarget.Stat)
                {
                    EnumControl(fields, "Stat", m.ResolvedStat, v => { m.Stat = (StatId)v; m.SerializedStat = -1; });
                    EnumControl(fields, "Operation", m.ResolvedOperation, v => { m.Operation = (ModifierOperation)v; m.SerializedOperation = -1; render(); });
                }
                if (target == ModifierTarget.StatBundle)
                {
                    EnumControl(fields, "Stat bundle", m.ResolvedBundle, v => { m.Bundle = (StatBundleKind)v; m.SerializedBundle = -1; });
                    fields[fields.childCount - 1].name = "studio-modifier-bundle";
                    Body(fields, "Primary stats use % of base; other bundle members use percentage points.", "studio-subtle");
                }
                if (target == ModifierTarget.FamilyDamageDealt || target == ModifierTarget.FamilyDamageReceived)
                {
                    EnumControl(fields, "Damage family", m.ResolvedFamily, v => { m.Family = (DamageFamily)v; m.SerializedFamily = -1; });
                    fields[fields.childCount - 1].name = "studio-modifier-family";
                }
                var flat = target == ModifierTarget.FlatDamageReduction || target == ModifierTarget.Stat && m.ResolvedOperation == ModifierOperation.Flat;
                if (!m.ScaleByStatusPotency) IntControl(fields, flat ? "Amount (flat units)" : "Amount (100 bp = 1%)", m.Amount, v => m.Amount = v);
                if (!continuous)
                {
                    ToggleControl(fields, "Scale by status potency", m.ScaleByStatusPotency, v => { m.ScaleByStatusPotency = v; render(); });
                    if (m.ScaleByStatusPotency)
                    { IntControl(fields, "Potency coefficient (100 bp = 1%)", m.PotencyCoefficientBp, v => m.PotencyCoefficientBp = v); fields[fields.childCount - 1].name = "studio-modifier-potency"; }
                }
                else if (target == ModifierTarget.FlatDamageReduction || m.ScaleByStatusPotency)
                {
                    Body(fields, "Flat reduction and status-potency scaling require a status modifier; continuous rules do not support them.", "studio-warning");
                    if (m.ScaleByStatusPotency) Button(fields, "Clear unsupported potency scaling", () => Edit("Clear potency scaling", () => { m.ScaleByStatusPotency = false; render(); }));
                }
                ApplyEditorFont();
            };
            render();
        }

        private void KitWording(VisualElement parent, string label, string value, Action<string> setter)
        {
            var field = new TextField(label) { value = value ?? "", multiline = true, isDelayed = true }; field.AddToClassList("studio-source");
            field.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); parent.Add(field);
            var preview = Body(parent, value ?? "", "studio-preview"); preview.enableRichText = true;
            field.RegisterValueChangedCallback(e => preview.text = e.newValue);
        }

        private void KitSummary(VisualElement parent, Func<string> text)
        {
            var label = Body(parent, text(), "studio-preview"); label.userData = text; label.AddToClassList("studio-kit-live-summary");
        }
        private void RefreshKitSummaries()
        {
            rootVisualElement.Query<Label>(className: "studio-kit-live-summary").ForEach(label => { if (label.userData is Func<string> text) label.text = text(); });
            _kitAbilityPreviewRefresh?.Invoke();
        }

        // Typed, lazy schema controls provide access to less common payloads without JSON editing.
        // Json.NET's writable contract omits computed/read-only runtime convenience properties.
        private Foldout KitFold(VisualElement parent, string title, string key, Action<VisualElement> populate)
        {
            var keepOpen = _page == "skill1" || _page == "skill2" || _page == "ultimate";
            var fold = new Foldout { text = title, value = keepOpen && _kitAbilityOpenFolds.Contains(key), name = "kit." + key };
            var loaded = false;
            Action load = () => { if (loaded) return; loaded = true; populate(fold); ApplyEditorFont(); };
            fold.RegisterValueChangedCallback(e => {
                if (e.target != fold) return;
                if (keepOpen) { if (e.newValue) _kitAbilityOpenFolds.Add(key); else _kitAbilityOpenFolds.Remove(key); }
                if (e.newValue) load();
            });
            parent.Add(fold);
            if (fold.value) load();
            return fold;
        }
        private void KitMembers(VisualElement parent, object model, string path, int depth, params string[] excluded)
        {
            if (model == null) return;
            if (model is StatModifierDefinition modifier) { DrawKitModifier(parent, modifier); return; }
            var contract = KitContracts.ResolveContract(model.GetType()) as JsonObjectContract;
            if (contract == null) return;
            foreach (var property in contract.Properties.Where(p => !p.Ignored && p.Readable && p.Writable && !excluded.Contains(p.PropertyName)))
            {
                var member = property;
                KitValue(parent, Humanize(member.PropertyName), member.PropertyType, member.ValueProvider.GetValue(model), v => member.ValueProvider.SetValue(model, v), path + "." + member.PropertyName, depth);
            }
        }
        private void KitValue(VisualElement parent, string label, Type type, object value, Action<object> setter, string path, int depth = 0)
        {
            if (type == typeof(string)) { TextControl(parent, label, (string)value, v => setter(v)); parent[parent.childCount - 1].name = path; return; }
            if (type == typeof(int)) { IntControl(parent, label, value == null ? 0 : (int)value, v => setter(v)); parent[parent.childCount - 1].name = path; return; }
            if (type == typeof(bool)) { ToggleControl(parent, label, value != null && (bool)value, v => setter(v)); return; }
            if (type.IsEnum) { EnumControl(parent, label, (Enum)(value ?? Activator.CreateInstance(type)), v => setter(v)); return; }
            if (type == typeof(long)) { var f = new LongField(label) { value = value == null ? 0 : (long)value }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); parent.Add(f); return; }
            if (type == typeof(float)) { var f = new FloatField(label) { value = value == null ? 0 : (float)value }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); parent.Add(f); return; }
            if (depth > 32) { Body(parent, "Open fewer nested levels before editing this payload.", "studio-warning"); return; }
            KitFold(parent, label + (value is IList list ? " · " + list.Count + " entries" : value == null ? " · none" : ""), path, content => {
                if (value == null)
                {
                    Button(content, "Create " + label, () => Edit("Create " + label, () => setter(Activator.CreateInstance(type)), true)); return;
                }
                if (value is IList entries)
                {
                    var element = type.GetGenericArguments().FirstOrDefault(); if (element == null) return;
                    for (var i = 0; i < entries.Count; i++)
                    {
                        var index = i; var row = Node(content, label + " " + (i + 1));
                        KitValue(row, "Entry " + (i + 1), element, entries[i], v => entries[index] = v, path + "." + i, depth + 1);
                        Button(row, "Remove entry", () => Edit("Remove " + label, () => entries.RemoveAt(index), true));
                        Button(row, "Move earlier", () => Edit("Reorder " + label, () => { if (index > 0) { var item = entries[index]; entries.RemoveAt(index); entries.Insert(index - 1, item); } }, true));
                    }
                    Button(content, "+ Entry", () => Edit("Add " + label, () => entries.Add(element == typeof(string) ? "" : Activator.CreateInstance(element)), true));
                }
                else KitMembers(content, value, path, depth + 1);
            });
        }
    }
}
#endif
