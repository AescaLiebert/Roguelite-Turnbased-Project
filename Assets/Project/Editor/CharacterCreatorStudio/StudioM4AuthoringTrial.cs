#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Exercises M4 with real widgets and pointer events. Graph/library access is readback only.</summary>
    public static class StudioM4AuthoringTrial
    {
        private static CharacterCreatorStudioWindow window;
        private static readonly List<KeyValuePair<string, Action>> steps = new List<KeyValuePair<string, Action>>();
        private static readonly List<string> completed = new List<string>(), passed = new List<string>();
        private static int index; private static double next; private static bool pending;
        private static string error, heal, cap, keyword, resourceSource, snippetToken, variant, beforeUndo, sourcePath, sourceHash, scenePath;
        private static bool sceneDirty;
        private static StudioDraftAsset originalDraft;
        public static object Status => new { Running = index < steps.Count && error == null, Step = index, Total = steps.Count, Current = index < steps.Count ? steps[index].Key : "Finished", Error = error, Assertions = passed.Count };
        private static StudioDocument Read() { StudioDraftRepository.TryRead(window.Draft, out var d, out _, out var e); if (e != null) throw new InvalidOperationException(e); return d; }
        private static DescriptionLibrary LibraryData() => SnapshotJson.Deserialize<DescriptionLibrary>(window.Draft.LibraryJson);
        private static void Step(string name, Action action) => steps.Add(new KeyValuePair<string, Action>(name, action));
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); passed.Add(message); }
        private static string Hash(string p) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        public static object Start()
        {
            EditorApplication.update -= Tick; window = CharacterCreatorStudioWindow.Open(); steps.Clear(); completed.Clear(); passed.Clear(); index = 0; error = null; pending = false;
            sourcePath = AssetDatabase.GetAssetPath(window.Draft.SourceCharacter); sourceHash = Hash(sourcePath); scenePath = SceneManager.GetActiveScene().path; sceneDirty = SceneManager.GetActiveScene().isDirty;
            originalDraft = window.Draft;
            Step("Import isolated description trial", () => Click("Import opening gauge"));
            Step("Open parameters", () => { Check(window.Draft != originalDraft, "A separate synthetic draft is imported by pointer events."); window.ShowWorkspace("parameters"); });
            Step("Create bound recovery percentage", () => Click("+ Percentage parameter"));
            Step("Set recovery to twenty percent", () => { var p = Read().Parameters.Last(); heal = p.Id; Text("Name", "Case-study recovery", window.rootVisualElement.Q<VisualElement>(heal)); Number("Value", 2000, window.rootVisualElement.Q<VisualElement>(heal)); });
            Step("Create bound resource cap", () => Click("+ Integer parameter"));
            Step("Set cap to six", () => { var p = Read().Parameters.Last(); cap = p.Id; Text("Name", "Case-study cap", window.rootVisualElement.Q<VisualElement>(cap)); Number("Value", 6, window.rootVisualElement.Q<VisualElement>(cap)); });
            Step("Open writer", () => { window.ShowWorkspace("descriptions"); Panel("Keywords"); });
            Step("Import project wording and vocabulary", () => Click("Import project vocabulary"));
            Step("Check seed identities", () => { var l = LibraryData(); Check(l.Terms.Any(t => t.Id == "keyword.attack.shatter") && l.Terms.Any(t => t.Id == "keyword.reaction.shatter") && l.Snippets.Any(s => s.Id == "snippet.protection"), "Project vocabulary distinguishes Shatter meanings and includes bilingual typed wording."); });
            Step("Create case-study keyword", () => Click("+ Keyword"));
            Step("Name case-study Face Mask", () => { keyword = LibraryData().Terms.Last().Id; var f = KeywordFold(keyword); Text("Name (en)", "Face Mask · case study", f); Text("Category", "Resource", f); Text("Definition", "A case-study resource, not an implemented character kit.", f); });
            Step("Author resource description", () => {
                resourceSource = "Gain {{value:param.opening-gauge|integer}} {{term:" + keyword + "}} (maximum {{value:" + cap + "|integer}}). Recover {{value:" + heal + "|percent}} of Max HP.\nทดสอบข้อความภาษาไทย";
                Source().value = resourceSource; Check(window.Compilation.Success && window.Compilation.Description.PlainText.Contains("20%") && window.Compilation.Description.PlainText.Contains("6"), "Bound cap and recovery render from authored parameters.");
            });
            Step("Format a selected source range", () => { SelectRange(0, 4); Click("Bold"); });
            Step("Verify selection wrapping", () => { Check(Source().value.StartsWith("<b>Gain</b>"), "Selection formatting wraps exactly the selected UTF-16 range."); resourceSource = Source().value; });
            Step("Break a supported tag", () => { Source().value += "<size=900>unsupported</size>"; Check(!window.Compilation.Success && Source().value.Contains("size=900"), "Unsupported markup is preserved as an invalid draft."); });
            Step("Repair supported markup", () => { Source().value = resourceSource; Check(window.Compilation.Success, "Markup repair restores valid compilation."); });
            Step("Break a value reference", () => { Source().value += "{{value:missing.value|percent}}"; Check(!window.Compilation.Success, "Missing binding is visible and blocks acceptance."); });
            Step("Repair value reference", () => Source().value = resourceSource);
            Step("Test Undo grouping", () => { Undo.IncrementCurrentGroup(); beforeUndo = window.Draft.DocumentJson; Source().value += " Undo marker"; Undo.FlushUndoRecordObjects(); });
            Step("Undo source edit", () => Click("Undo"));
            Step("Verify source Undo", () => Check(window.Draft.DocumentJson == beforeUndo, "Toolbar Undo restores source and token identity."));
            Step("Redo source edit", () => Click("Redo"));
            Step("Repair marker after Redo", () => { Check(Source().value.Contains("Undo marker"), "Toolbar Redo restores the authored edit."); Source().value = resourceSource; });
            Step("Open protection wording", () => { Panel("Snippets"); Search("snippet.protection"); });
            Step("Bind typed snippet inputs", () => { var f = SnippetFold("snippet.protection@1"); Popup("Input heal", heal, f); Text("Input eligible", "one eligible recovery-block debuff", f); Text("Input recipient", "the hit ally", f); SelectRange(Source().value.Length, Source().value.Length); Click("Insert this pinned revision", f); });
            Step("Read linked instance", () => { snippetToken = Source().value.Substring(resourceSource.Length); Check(snippetToken.StartsWith("{{snippet:snippet.protection@1|") && window.Compilation.Success, "A reusable protection instance is explicitly bound and pinned."); });
            Step("Create next wording revision", () => Click("Create next editable revision", SnippetFold("snippet.protection@1")));
            Step("Edit new unpublished wording", () => { Text("Template (en)", "After cleansing {{text:$eligible}}, heal {{text:$recipient}} for {{value:$heal|percent}} Max HP.", SnippetFold("snippet.protection@2")); Check(!window.Compilation.Description.PlainText.Contains("After cleansing"), "Editing a new snippet revision leaves pinned consumers unchanged."); });
            Step("Select linked instance for update", () => { var s = Source(); var start = s.value.IndexOf(snippetToken, StringComparison.Ordinal); SelectRange(start, start + snippetToken.Length); Click("Adopt revision in selected instance", SnippetFold("snippet.protection@2")); });
            Step("Verify deliberate revision adoption", () => { Check(Source().value.Contains("snippet.protection@2") && window.Compilation.Description.PlainText.Contains("After cleansing"), "Explicit adoption updates only the selected instance."); });
            Step("Detach selected linked wording", () => { var s = Source(); var start = s.value.IndexOf("{{snippet:", StringComparison.Ordinal); SelectRange(start, s.value.Length); Click("Detach selected snippet as local wording"); });
            Step("Verify detached provenance", () => { Check(!Source().value.Contains("{{snippet:") && Read().Description.Locales[0].Blocks[0].DetachedSnippetOrigins.Count == 1 && Source().value.Contains("{{value:"), "Detaching preserves semantic references and records origin."); });
            Step("Add a layered-mark paragraph", () => Click("+ Paragraph"));
            Step("Write layered mark prose", () => { Source().value = "Facial Impressions · case study: each layer increases the authored damage coefficient. This paragraph is review prose, not a gameplay rule."; Popup("Structure", DescriptionBlockKind.Paragraph); });
            Step("Move paragraph up", () => Click("Move block up"));
            Step("Verify document order", () => Check(Read().Description.Locales[0].Blocks[0].Source.StartsWith("Facial Impressions"), "Structured block reordering preserves source."));
            Step("Create a configuration branch", () => { Panel("Branches"); Click("+ Configuration branch"); });
            Step("Set level branch", () => { Text("Branch name", "Level two wording"); Number("Minimum level", 2); Source().value = "Level two: recover {{value:" + heal + "|percent}}."; Number("Preview level", 2); });
            Step("Verify selected branch", () => { var label = window.rootVisualElement.Q<Label>("toolkit-description-preview"); Check(label.text.Contains("Level two"), "Preview selects authored level wording without executing combat."); });
            Step("Remove explicit fallback", () => { Toggle("Explicit fallback", false); Check(!window.Compilation.Success, "Coverage rejects an uncovered configuration even when preview matches."); });
            Step("Restore explicit fallback", () => Toggle("Explicit fallback", true));
            Step("Preview missing Thai translation", () => { Popup("Language", "th"); });
            Step("Verify visible fallback", () => { Check(Source().isReadOnly && window.rootVisualElement.Q<Label>("studio-description-diagnostics").text.Contains("locale-fallback"), "Missing translation fallback is visible and read-only."); });
            Step("Create reorderable Thai variant", () => Click("Create th translation"));
            Step("Write Thai prose", () => { Source().value = "เมื่อถึงระดับสอง ฟื้นฟู {{value:" + heal + "|percent}} ของ HP สูงสุด"; Check(!Source().isReadOnly && Source().value.StartsWith("เมื่อ"), "Thai source remains editable with intact combining characters."); });
            Step("Return to English", () => Popup("Language", "en"));
            Step("Open authored EX composer", () => window.ShowWorkspace("abilities"));
            Step("Create EX definition for preview", () => Click("+ Variant from base skill"));
            Step("Set EX coefficient", () => { var f = window.rootVisualElement.Query<Foldout>().ToList().First(x => x.text == "Variant card rank 1"); f.value = true; Number("Coefficient (bp)", 20000, f); variant = Read().AbilityVariants.Last().Definition.Id; });
            Step("Return to resolved values", () => { window.ShowWorkspace("descriptions"); Panel("Values"); Search(""); });
            Step("Add resolved damage binding", () => Click("+ Resolved skill binding"));
            Step("Insert resolved coefficient", () => { Source().value = "Resolved damage: "; SelectRange(Source().value.Length, Source().value.Length); Click("Insert resolved value"); });
            Step("Preview EX configuration", () => Popup("Authored variant", variant));
            Step("Verify resolved EX coefficient", () => { Check(window.rootVisualElement.Q<Label>("toolkit-description-preview").text.Contains("200%"), "EX wording uses the selected compiled coefficient."); });
            Step("Preview base configuration", () => Popup("Authored variant", "Base"));
            Step("Check base value differs", () => Check(!window.rootVisualElement.Q<Label>("toolkit-description-preview").text.Contains("200%"), "Base/EX selection does not conflate authored values."));
            Step("Choose compact surface", () => { Toggle("Compact surface", true); Number("Surface width", 360); Popup("Target surface", "Battle card"); });
            Step("Inspect runtime-font glyph diagnostics", () => { Check(window.rootVisualElement.Q<DropdownField>().choices != null, "Renderer profile is selectable with a real TMP preview."); Popup("TMP font profile", "Editor standard (zh-cn)"); });
            Step("Return to expanded Studio", () => { Popup("Target surface", "Studio"); Toggle("Compact surface", false); });
            Step("Review exact wording revision", () => { Panel("Review"); Search(""); Click("Mark selected locale reviewed"); });
            Step("Verify review receipt", () => Check(Read().Description.Locales.First(l => l.Locale == "en").ReviewedTextHash.Length > 0, "Review stores exact text and gameplay revision receipts."));
            Step("Change reviewed wording", () => Source().value += " Changed wording.");
            Step("Inspect stale review", () => { Panel("Values"); Panel("Review"); Check(window.rootVisualElement.Query<Label>().ToList().Any(l => l.text != null && l.text.StartsWith("Draft / review required")), "Text changes invalidate the displayed review receipt."); });
            Step("Save authored document", () => Click("Save draft"));
            Step("Reopen saved writer", () => { var draft = window.Draft; var json = draft.DocumentJson; window.Close(); window = CharacterCreatorStudioWindow.Open(); window.ShowWorkspace("descriptions"); Check(window.Draft == draft && json == draft.DocumentJson && window.Compilation.Success, "Save/reopen retains valid locales, branches, bindings, snippets and provenance."); });
            Step("Verify source and scene isolation", () => { Check(Hash(sourcePath) == sourceHash, "The production character asset is unchanged."); Check(SceneManager.GetActiveScene().path == scenePath && SceneManager.GetActiveScene().isDirty == sceneDirty && !EditorApplication.isPlaying, "Authoring preserves the scene and never enters Play Mode."); });
            next = EditorApplication.timeSinceStartup + .3; EditorApplication.update += Tick; return Status;
        }
        private static void Tick()
        {
            if (pending || EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (index >= steps.Count) { EditorApplication.update -= Tick; Write(); return; }
            try { steps[index].Value(); completed.Add(steps[index].Key); index++; next = EditorApplication.timeSinceStartup + .3; }
            catch (Exception e) { error = steps[index].Key + ": " + e.Message; EditorApplication.update -= Tick; Write(); }
        }
        private static void Write()
        {
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/M4");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/M4/ui-author-trial.json", JsonConvert.SerializeObject(new { Success = error == null && index == steps.Count, Error = error, CompletedSteps = completed, Passed = passed,
                Draft = AssetDatabase.GetAssetPath(window.Draft), Interaction = "UI Toolkit fields and real pointer events; graph and library readback only.", RuntimeAssemblyHash = Hash(typeof(StudioDocument).Assembly.Location), EditorAssemblyHash = Hash(typeof(CharacterCreatorStudioWindow).Assembly.Location), CapturedUtc = DateTime.UtcNow.ToString("O") }, Formatting.Indented));
        }
        private static void Reveal(VisualElement e) { for (var p = e.parent; p != null; p = p.parent) if (p is Foldout f) f.value = true; for (var p = e.parent; p != null; p = p.parent) if (p is ScrollView s) { s.ScrollTo(e); break; } }
        private static void Click(string label, VisualElement root = null)
        {
            var b = (root ?? window.rootVisualElement).Query<Button>().ToList().First(x => x.text == label); Reveal(b); if (!b.enabledInHierarchy) throw new InvalidOperationException("Disabled button: " + label); pending = true;
            b.schedule.Execute(() => { var acknowledged = false; Action observed = () => acknowledged = true; b.clicked += observed; b.Focus(); var p = b.worldBound.center;
                using (var e = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = p })) b.SendEvent(e);
                using (var e = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = p })) b.SendEvent(e);
                b.clicked -= observed; pending = false; if (!acknowledged) { error = "Pointer activation failed: " + label; EditorApplication.update -= Tick; Write(); }
            }).StartingIn(80);
        }
        private static void Text(string label, string value, VisualElement root = null) { var f = (root ?? window.rootVisualElement).Query<TextField>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Number(string label, int value, VisualElement root = null) { var f = (root ?? window.rootVisualElement).Query<IntegerField>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Popup(string label, object value, VisualElement root = null)
        {
            if (value is Enum en) { var f = (root ?? window.rootVisualElement).Query<EnumField>().ToList().First(x => x.label == label); Reveal(f); f.value = en; return; }
            var field = (root ?? window.rootVisualElement).Query<PopupField<string>>().ToList().FirstOrDefault(x => x.label == label);
            if (field != null) { Reveal(field); field.value = (string)value; return; }
            var dropdown = (root ?? window.rootVisualElement).Query<DropdownField>().ToList().First(x => x.label == label); Reveal(dropdown); dropdown.value = (string)value;
        }
        private static void Toggle(string label, bool value) { var f = window.rootVisualElement.Query<Toggle>().ToList().First(x => x.label == label); Reveal(f); f.value = value; }
        private static void Panel(string panel) => Popup("Library", panel);
        private static void Search(string text) => Text("Search library", text);
        private static void SelectRange(int start, int end) { var f = Source(); f.Focus(); f.schedule.Execute(() => f.SelectRange(start, end)).StartingIn(30); }
        private static TextField Source() => window.rootVisualElement.Q<TextField>("studio-description-source");
        private static Foldout SnippetFold(string key) { var f = window.rootVisualElement.Query<Foldout>().ToList().First(x => x.text == key); f.value = true; return f; }
        private static Foldout KeywordFold(string id) { var f = window.rootVisualElement.Query<Foldout>().ToList().First(x => x.Query<Label>().ToList().Any(l => l.text != null && l.text.StartsWith(id + " · revision"))); f.value = true; return f; }
    }
}
#endif


