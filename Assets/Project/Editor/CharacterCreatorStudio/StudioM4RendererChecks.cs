#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed class StudioM4RendererChecks : EditorWindow
    {
        private const string Root = "Artifacts/CharacterCreatorStudio/M4/Renderers";
        private readonly List<object> rows = new List<object>();
        private readonly List<Label> labels = new List<Label>();
        private readonly List<string> assertions = new List<string>();
        private readonly List<string> failures = new List<string>();
        private string sourceFontBefore, gameFontBefore;
        private int attempts;
        public static object LastReport;
        public static object Start()
        {
            var w = CreateInstance<StudioM4RendererChecks>(); w.titleContent = new GUIContent("M4 renderer acceptance"); w.position = new Rect(70, 70, 1050, 850); w.ShowUtility();
            w.rootVisualElement.schedule.Execute(w.Run).StartingIn(300); return new { Running = true };
        }
        private void Check(bool ok, string text) { assertions.Add(text); if (!ok) failures.Add(text); }
        private void Run()
        {
            try
            {
                Directory.CreateDirectory(Root);
                var sourceFont = Resources.Load<Font>(StudioPreviewText.EditorFontResource);
                var gameFont = Resources.Load<TMP_FontAsset>(StudioPreviewText.BattleFontResource);
                sourceFontBefore = EditorJsonUtility.ToJson(sourceFont); gameFontBefore = EditorJsonUtility.ToJson(gameFont);
                rootVisualElement.style.backgroundColor = new Color(.06f,.08f,.11f); rootVisualElement.style.paddingLeft = 20;
                rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(sourceFont);
                var scroll = new ScrollView(); rootVisualElement.Add(scroll); scroll.style.flexGrow = 1;
                foreach (var surface in StudioDescriptionSurfaces.All)
                foreach (var locale in new[] { "en", "th" })
                {
                    var source = locale == "en" ? "<b>Damage</b> <i>resolved</i> <color=#70D6FF>20%</color> \\<literal\\>" : "<b>ฟื้นฟู</b> <i>พลังชีวิต</i> <color=#70D6FF>20%</color> \\<ข้อความ\\>";
                    var compiled = DescriptionCompiler.Compile(source, new DescriptionLibrary(), new DescriptionCompileContext { Locale = locale });
                    Check(compiled.Success, surface.Name + "/" + locale + " compiled portable markup.");
                    scroll.Add(new Label(surface.Name + " / " + locale + " · " + surface.Consumer));
                    Label label;
                    if (surface.Name == "Character detail")
                    {
                        var type = typeof(FightingAllstar.Presentation.Combat.CombatTextCatalog).Assembly.GetType("FightingAllstar.Presentation.Navigation.CharacterDetailPopup");
                        label = (Label)type.GetMethod("Label", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { compiled.ToolkitRichText, "hub-tooltip-description", true });
                        var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Project/UI/CharacterHub.uss"); if (stylesheet != null) label.styleSheets.Add(stylesheet);
                    }
                    else label = new Label(compiled.ToolkitRichText) { enableRichText = true };
                    label.style.fontSize = surface.Size; label.style.whiteSpace = WhiteSpace.Normal; label.style.width = surface.Width; label.style.color = Color.white;
                    scroll.Add(label); labels.Add(label);
                    using (var preview = new StudioPreviewText(surface.Font))
                    {
                        preview.SetSurfaceSize(surface.Width, 240); preview.SetTypography(surface.Size); preview.SetText(compiled.TmpRichText);
                        var missing = preview.MissingCodepoints.ToList();
                        Check(missing.Count == 0 ? preview.TmpText.GetParsedText().Replace("\r", "") == compiled.PlainText.Replace("\r", "") : preview.Diagnostics.Contains("Missing glyphs"),
                            surface.Name + "/" + locale + (missing.Count == 0 ? " real TMP parses semantic plain output." : " missing runtime glyphs are explicitly diagnosed; profile remains blocked."));
                        // The runtime TMP text factory is exercised in the preview scene, then assigned an owned atlas before rendering.
                        if (surface.Font == StudioTextFontProfile.BattleInspector)
                        {
                            var type = typeof(FightingAllstar.Presentation.Combat.BattleCharacterInspector);
                            var method = type.GetMethod("Text", BindingFlags.Static | BindingFlags.NonPublic);
                            var fontField = type.GetField("inspectorFont", BindingFlags.Static | BindingFlags.NonPublic); var previousFont = fontField.GetValue(null);
                            var inactive = new GameObject("M4 isolated factory", typeof(RectTransform)); inactive.hideFlags = HideFlags.HideAndDontSave; inactive.SetActive(false); inactive.transform.SetParent(preview.TmpText.transform.parent, false);
                            try
                            {
                                fontField.SetValue(null, preview.TmpText.font);
                                var actual = (TextMeshProUGUI)method.Invoke(null, new object[] { inactive.transform, compiled.TmpRichText, (float)surface.Size, null });
                                actual.richText = true; inactive.SetActive(true); actual.ForceMeshUpdate(true, true);
                                Check(actual.GetParsedText() == preview.TmpText.GetParsedText(), surface.Name + "/" + locale + " runtime TMP factory matches the isolated adapter, including glyph limitations.");
                            }
                            finally { fontField.SetValue(null, previousFont); DestroyImmediate(inactive); }
                        }
                        Check(surface.Font != StudioTextFontProfile.EditorStandard || missing.Count == 0, surface.Name + "/" + locale + " standard font covers every requested glyph.");
                        preview.CaptureTmp(Path.GetFullPath(Root + "/" + surface.Name.Replace(" ", "-") + "-" + locale + ".png"), surface.Width, 240);
                        rows.Add(new { Surface = surface.Name, Locale = locale, surface.Consumer, Font = surface.Font.ToString(), Status = missing.Count == 0 ? "Verified markup and glyph coverage" : "Blocked for release: missing runtime-font glyphs", MissingCodepoints = missing.Select(v => "U+" + v.ToString("X4")).ToArray(), Plain = compiled.PlainText });
                    }
                }
                Check(EditorJsonUtility.ToJson(sourceFont) == sourceFontBefore && EditorJsonUtility.ToJson(gameFont) == gameFontBefore, "Renderer matrix never populates or modifies persistent source/game font assets.");
                rootVisualElement.schedule.Execute(Measure).StartingIn(300);
            }
            catch (Exception e) { failures.Add(e.ToString()); Write(); }
        }
        private void Measure()
        {
            foreach (var label in labels)
            {
                var rich = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                var old = label.enableRichText; label.enableRichText = false; var literal = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined); label.enableRichText = old;
                Check(rich.x > 0 && literal.x > rich.x, "Attached actual Toolkit Label treats approved tags as formatting.");
            }
            Repaint(); rootVisualElement.schedule.Execute(Capture).StartingIn(250);
        }
        private void Capture()
        {
            try { StudioPreviewChecks.CaptureEditorWindowEvidence(this, Path.GetFullPath(Root + "/toolkit-matrix.png")); Write(); Close(); }
            catch (Exception e) { if (++attempts < 3) { rootVisualElement.schedule.Execute(Capture).StartingIn(300); return; } failures.Add(e.Message); Write(); }
        }
        private void Write()
        {
            LastReport = new { Success = failures.Count == 0, Checks = assertions, Errors = failures, Profiles = rows,
                UnityVersion = Application.unityVersion, CapturedUtc = DateTime.UtcNow.ToString("O"), Scope = "Actual Toolkit Labels including CharacterDetailPopup factory; actual isolated TMP and BattleCharacterInspector factory. Missing Thai glyphs in the runtime font remain explicit release blockers. No production activation." };
            Directory.CreateDirectory(Root); File.WriteAllText(Root + "/evidence.json", JsonConvert.SerializeObject(LastReport, Formatting.Indented));
        }
    }
}
#endif

