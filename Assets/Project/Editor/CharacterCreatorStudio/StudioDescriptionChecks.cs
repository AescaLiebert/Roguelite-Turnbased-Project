using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Renderer evidence for the exact compiler subset, never a project-wide rich-text certification.</summary>
    public static class StudioDescriptionChecks
    {
        public static StudioDescriptionEvidence LastReport { get; private set; }

        public static string VerificationFor(string syntax)
        {
            try
            {
                var report = LastReport;
                var path = Path.GetFullPath("Artifacts/CharacterCreatorStudio/DescriptionRendererEvidence/evidence.json");
                if (report == null && File.Exists(path)) report = JsonConvert.DeserializeObject<StudioDescriptionEvidence>(File.ReadAllText(path));
                var editorFontPath = AssetDatabase.GetAssetPath(Resources.Load<Font>(StudioPreviewText.EditorFontResource));
                if (report == null || report.EvidenceVersion != 2 || report.UnityVersion != Application.unityVersion || !report.ToolkitCompleted ||
                    report.StandardEditorFontDependencyHash != AssetDatabase.GetAssetDependencyHash(editorFontPath).ToString())
                    return "Enabled compiler subset · renderer verification pending";
                var key = syntax.StartsWith("<b>", StringComparison.Ordinal) ? "b" : syntax.StartsWith("<i>", StringComparison.Ordinal) ? "i" :
                    syntax.StartsWith("<color", StringComparison.Ordinal) ? "color=#RRGGBB" : "escaped literal angles / generated noparse";
                var capability = report.Capabilities.FirstOrDefault(item => item.Syntax == key);
                if (report.Errors.Count == 0 && capability != null && capability.TmpStatus.StartsWith("Verified", StringComparison.Ordinal) &&
                    report.ToolkitMeasurementPassed && report.ToolkitVisualReview != "Pending" && report.EditorFontUnchanged)
                    return "Verified: Studio Toolkit (zh-cn) and BattleInspector TMP preview · Unity " + report.UnityVersion;
                return "Enabled compiler subset · " + (capability?.ToolkitStatus ?? "verification pending");
            }
            catch { return "Enabled compiler subset · verification report unavailable"; }
        }
        private static DescriptionRendererEvidenceWindow window;
        internal const string TestSource = "<b>B</b> <i>I</i> <color=#4EE2A3>C</color>\nLiteral: \\<b\\>safe\\</b\\>";

        /// <summary>Runs TMP checks now; attached Toolkit measurement and capture finish after repaint. Read evidence.json afterward.</summary>
        public static StudioDescriptionEvidence Run()
        {
            if (window != null) window.Close();
            LastReport = new StudioDescriptionEvidence {
                EvidenceVersion = 2,
                UnityVersion = Application.unityVersion,
                CapturedUtc = DateTime.UtcNow.ToString("O"),
                OutputDirectory = Path.GetFullPath("Artifacts/CharacterCreatorStudio/DescriptionRendererEvidence"),
                Scope = "Studio actual Label and isolated TextMeshProUGUI using the standard UI/zh-cn.ttf Editor font; explicit BattleInspector font checks retain runtime-font coverage evidence. Other runtime consumers remain unaudited."
            };
            Directory.CreateDirectory(LastReport.OutputDirectory);
            LastReport.StandardEditorFontAssetPath = AssetDatabase.GetAssetPath(Resources.Load<Font>(StudioPreviewText.EditorFontResource));
            LastReport.StandardEditorFontDependencyHash = AssetDatabase.GetAssetDependencyHash(LastReport.StandardEditorFontAssetPath).ToString();
            var context = DescriptionCompileContext.FromParameters(new[] {
                new StudioParameter { Id = "param.opening-gauge", Unit = StudioParameterUnit.Integer, Value = 2 } });
            var library = DescriptionLibrary.CreateOpeningGaugeLibrary();
            var compiled = DescriptionCompiler.Compile(TestSource, library, context);
            Record(compiled.Success, "The compiler accepts only approved tags and escaped literal brackets.");
            LastReport.CompilerSource = TestSource;
            LastReport.CompiledRichText = compiled.TmpRichText;
            LastReport.ExpectedPlainText = compiled.PlainText;
            var original = Resources.Load<TMP_FontAsset>(StudioPreviewText.BattleFontResource);
            if (original == null)
            {
                Record(false, "The battle inspector source TMP font is unavailable.");
                Write();
                return LastReport;
            }
            var originalState = EditorJsonUtility.ToJson(original);
            var sourceFile = original.sourceFontFile;
            var sourceFontState = sourceFile == null ? "" : EditorJsonUtility.ToJson(sourceFile);
            using (var preview = new StudioPreviewText(StudioTextFontProfile.BattleInspector))
            {
                preview.SetText(compiled.TmpRichText);
                var tmp = preview.TmpText;
                tmp.ForceMeshUpdate(true, true);
                LastReport.TmpParsedText = tmp.GetParsedText();
                Record(Normalize(LastReport.TmpParsedText) == Normalize(compiled.PlainText), "Actual TMP parsed text equals semantic plain output, including literal angle brackets and line breaks.");
                var information = tmp.textInfo;
                var b = Glyph(information, 'B');
                var i = Glyph(information, 'I');
                var c = Glyph(information, 'C');
                var bold = b.HasValue && (b.Value.style & FontStyles.Bold) != 0 && b.Value.isVisible;
                var italic = i.HasValue && (i.Value.style & FontStyles.Italic) != 0 && i.Value.isVisible;
                var color = c.HasValue && c.Value.color.r == 0x4E && c.Value.color.g == 0xE2 && c.Value.color.b == 0xA3 && c.Value.isVisible;
                Record(bold, "Actual TMP B glyph has the Bold style and visible geometry.");
                Record(italic, "Actual TMP I glyph has the Italic style and visible geometry.");
                Record(color, "Actual TMP C glyph has the authored #4EE2A3 vertex color and visible geometry.");
                var literal = Normalize(LastReport.TmpParsedText).Contains("<b>safe</b>");
                Record(literal, "Compiler-generated noparse safety spans preserve literal approved-looking tags in TMP.");
                LastReport.Capabilities.Add(Capability("b", bold));
                LastReport.Capabilities.Add(Capability("i", italic));
                LastReport.Capabilities.Add(Capability("color=#RRGGBB", color));
                LastReport.Capabilities.Add(Capability("escaped literal angles / generated noparse", literal));
                preview.CaptureTmp(Path.Combine(LastReport.OutputDirectory, "portable-subset-tmp.png"));
                var slice = DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, context);
                preview.SetText(slice.TmpRichText);
                Record(Normalize(preview.TmpText.GetParsedText()) == Normalize(slice.PlainText) && slice.PlainText.Contains("2 Power Gauge"),
                    "The actual TMP slice resolves its shared gameplay parameter, keyword, semantic colors, and pinned snippet.");
                preview.CaptureTmp(Path.Combine(LastReport.OutputDirectory, "opening-gauge-tmp.png"));
                const string thaiSource = "เมื่อเริ่มต่อสู้ เพิ่ม<b>เกจพลัง</b> {{value:param.opening-gauge|integer}} จุด";
                var thai = DescriptionCompiler.Compile(thaiSource, library, DescriptionCompileContext.FromParameters(new[] {
                    new StudioParameter { Id = "param.opening-gauge", Unit = StudioParameterUnit.Integer, Value = 2 } }, "th"));
                Record(DescriptionSource.Parse(thaiSource).Serialize() == thaiSource, "Thai source and combining characters survive parsing unchanged.");
                var saved = SnapshotJson.Deserialize<DescriptionDocument>(SnapshotJson.Serialize(DescriptionDocument.FromSource(thaiSource, "th")));
                LastReport.ThaiSourcePreserved = saved.Locales[0].Blocks[0].Source == thaiSource && thai.Success;
                Record(LastReport.ThaiSourcePreserved, "Save/reload preserves exact Thai source independently of font glyph availability.");
                preview.SetText(thai.TmpRichText);
                LastReport.ThaiTmpParsedText = preview.TmpText.GetParsedText();
                LastReport.MissingThaiGlyphs = preview.MissingCodepoints.Select(x => "U+" + x.ToString("X4")).ToList();
                var rendererReplacedThai = Normalize(LastReport.ThaiTmpParsedText) != Normalize(thai.PlainText);
                Record(!rendererReplacedThai || LastReport.MissingThaiGlyphs.Count > 0,
                    "Coverage diagnostics inspect requested Thai codepoints, not replacement squares emitted by TMP.");
                LastReport.ThaiGlyphCoverage = LastReport.MissingThaiGlyphs.Count == 0 ? "Verified for tested text in BattleInspector profile" : "Unsupported by the BattleInspector font profile; use EditorStandard for authoring or supply a runtime fallback font";
                LastReport.ThaiDiagnostic = preview.Diagnostics;
                preview.CaptureTmp(Path.Combine(LastReport.OutputDirectory, "thai-glyph-coverage-tmp.png"));
            }
            var editorFontSource = Resources.Load<Font>(StudioPreviewText.EditorFontResource);
            var editorFontBefore = editorFontSource == null ? "" : EditorJsonUtility.ToJson(editorFontSource);
            const string editorThaiSource = "เมื่อเริ่มต่อสู้ เพิ่ม<b>เกจพลัง</b> {{value:param.opening-gauge|integer}} จุด";
            var editorThai = DescriptionCompiler.Compile(editorThaiSource, library, DescriptionCompileContext.FromParameters(new[] {
                new StudioParameter { Id = "param.opening-gauge", Unit = StudioParameterUnit.Integer, Value = 2 } }, "th"));
            LastReport.EditorThaiRichText = editorThai.ToolkitRichText;
            LastReport.EditorThaiExpectedPlainText = editorThai.PlainText;
            using (var editorPreview = new StudioPreviewText())
            {
                Record(editorPreview.FontProfile == StudioTextFontProfile.EditorStandard, "Default text preview uses the standard Thai-capable Editor font profile.");
                editorPreview.SetText(editorThai.TmpRichText);
                LastReport.EditorThaiTmpParsedText = editorPreview.TmpText.GetParsedText();
                LastReport.EditorMissingThaiGlyphs = editorPreview.MissingCodepoints.Select(x => "U+" + x.ToString("X4")).ToList();
                var info = editorPreview.TmpText.textInfo;
                for (var index = 0; index < info.characterCount; index++)
                    if (info.characterInfo[index].character >= '\u0E00' && info.characterInfo[index].character <= '\u0E7F' && info.characterInfo[index].isVisible)
                        LastReport.EditorVisibleThaiGlyphCount++;
                var matches = Normalize(LastReport.EditorThaiTmpParsedText) == Normalize(editorThai.PlainText);
                var editorSupported = matches && LastReport.EditorMissingThaiGlyphs.Count == 0 && LastReport.EditorVisibleThaiGlyphCount > 0;
                LastReport.EditorThaiGlyphCoverage = editorSupported ? "Verified for tested Thai source in default EditorStandard UI/zh-cn.ttf profile" : "Failed";
                LastReport.EditorThaiDiagnostic = editorPreview.Diagnostics;
                Record(matches, "Default Editor TMP renders the exact Thai semantic plain text without replacement squares.");
                Record(LastReport.EditorMissingThaiGlyphs.Count == 0 && LastReport.EditorVisibleThaiGlyphCount > 0,
                    "Default Editor font supplies Thai combining characters and visible Thai glyph geometry with no missing-codepoint diagnostics.");
                Record(editorPreview.SourceText == editorThai.TmpRichText, "Renderer font selection preserves the compiled Thai source payload.");
                editorPreview.CaptureTmp(Path.Combine(LastReport.OutputDirectory, "thai-editor-standard.png"));
                editorPreview.SetFontProfile(StudioTextFontProfile.BattleInspector);
                Record(editorPreview.SourceText == editorThai.TmpRichText, "Changing to BattleInspector font profile preserves the source text.");
                editorPreview.SetFontProfile(StudioTextFontProfile.EditorStandard);
                Record(editorPreview.MissingCodepoints.Count == 0 && Normalize(editorPreview.TmpText.GetParsedText()) == Normalize(editorThai.PlainText),
                    "Switching back to EditorStandard restores the same complete Thai glyph output.");
            }
            LastReport.EditorFontUnchanged = editorFontSource != null && EditorJsonUtility.ToJson(editorFontSource) == editorFontBefore;
            Record(LastReport.EditorFontUnchanged, "The default Editor preview does not mutate the persistent UI/zh-cn.ttf source font.");
            LastReport.PersistentFontUnchanged = EditorJsonUtility.ToJson(original) == originalState &&
                (sourceFile == null || EditorJsonUtility.ToJson(sourceFile) == sourceFontState);
            Record(LastReport.PersistentFontUnchanged, "Renderer checks do not populate or mutate the persistent game TMP/source font.");
            window = ScriptableObject.CreateInstance<DescriptionRendererEvidenceWindow>();
            window.Initialize(compiled.ToolkitRichText, compiled.PlainText, LastReport);
            window.ShowUtility();
            window.rootVisualElement.schedule.Execute(window.MeasureAndCapture).StartingIn(300);
            Write();
            return LastReport;
        }

        /// <summary>Call only after inspecting the captured Toolkit screenshot; records explicit visual review evidence.</summary>
        public static StudioDescriptionEvidence ConfirmToolkitVisualReview(string reviewerNote)
        {
            if (LastReport == null || !LastReport.ToolkitMeasurementPassed || !File.Exists(LastReport.ToolkitScreenshot))
                throw new InvalidOperationException("Run the attached Toolkit measurements and capture before recording a visual review.");
            if (string.IsNullOrWhiteSpace(reviewerNote)) throw new ArgumentException("Record what the screenshot confirmed.", nameof(reviewerNote));
            LastReport.ToolkitVisualReview = reviewerNote;
            foreach (var entry in LastReport.Capabilities) entry.ToolkitStatus = "Verified in Studio preview: attached measurement plus recorded screenshot review";
            Write();
            return LastReport;
        }

        internal static void Record(bool condition, string message)
        {
            LastReport.Checks.Add(new StudioDescriptionCheck { Passed = condition, Message = message });
            if (!condition) LastReport.Errors.Add(message);
        }
        internal static void Write() => File.WriteAllText(Path.Combine(LastReport.OutputDirectory, "evidence.json"), JsonConvert.SerializeObject(LastReport, Formatting.Indented));
        internal static string Normalize(string value) => (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        private static TMP_CharacterInfo? Glyph(TMP_TextInfo info, char target)
        {
            for (var index = 0; index < info.characterCount; index++) if (info.characterInfo[index].character == target) return info.characterInfo[index];
            return null;
        }
        private static StudioDescriptionCapabilityEvidence Capability(string tag, bool tmpPassed) => new StudioDescriptionCapabilityEvidence {
            Syntax = tag, CompilerStatus = "Enabled subset", TmpStatus = tmpPassed ? "Verified by actual parsed glyphs" : "Failed",
            ToolkitStatus = "Candidate: awaiting attached measurement and screenshot review" };
    }

    [Serializable]
    public sealed class StudioDescriptionEvidence
    {
        public int EvidenceVersion;
        public string StandardEditorFontAssetPath;
        public string StandardEditorFontDependencyHash;
        public string UnityVersion;
        public string CapturedUtc;
        public string OutputDirectory;
        public string Scope;
        public string CompilerSource;
        public string CompiledRichText;
        public string ExpectedPlainText;
        public string TmpParsedText;
        public string ThaiTmpParsedText;
        public string ThaiDiagnostic;
        public string ThaiGlyphCoverage;
        public string EditorThaiRichText;
        public string EditorThaiExpectedPlainText;
        public string EditorThaiTmpParsedText;
        public string EditorThaiDiagnostic;
        public string EditorThaiGlyphCoverage;
        public bool EditorFontUnchanged;
        public int EditorVisibleThaiGlyphCount;
        public bool ThaiSourcePreserved;
        public bool PersistentFontUnchanged;
        public bool ToolkitCompleted;
        public bool ToolkitMeasurementPassed;
        public string ToolkitScreenshot;
        public string ToolkitVisualReview = "Pending";
        public float ToolkitRichWidth;
        public float ToolkitLiteralMarkupWidth;
        public float ToolkitLiteralEscapedWidth;
        public float ToolkitLiteralPlainWidth;
        public List<string> MissingThaiGlyphs = new List<string>();
        public List<string> EditorMissingThaiGlyphs = new List<string>();
        public List<string> Errors = new List<string>();
        public List<StudioDescriptionCheck> Checks = new List<StudioDescriptionCheck>();
        public List<StudioDescriptionCapabilityEvidence> Capabilities = new List<StudioDescriptionCapabilityEvidence>();
    }

    [Serializable]
    public sealed class StudioDescriptionCheck { public bool Passed; public string Message; }
    [Serializable]
    public sealed class StudioDescriptionCapabilityEvidence { public string Syntax; public string CompilerStatus; public string TmpStatus; public string ToolkitStatus; }

    internal sealed class DescriptionRendererEvidenceWindow : EditorWindow
    {
        private StudioPreviewText preview;
        private StudioDescriptionEvidence evidence;
        private string markup;
        private int captureAttempts;

        public void Initialize(string richText, string plainText, StudioDescriptionEvidence report)
        {
            evidence = report; markup = richText;
            titleContent = new GUIContent("Studio description renderer evidence");
            position = new Rect(120, 120, 1040, 640);
            rootVisualElement.style.paddingLeft = 20;
            rootVisualElement.style.paddingRight = 20;
            rootVisualElement.style.paddingTop = 20;
            rootVisualElement.style.backgroundColor = new Color(.07f, .085f, .12f);
            var editorFont = Resources.Load<Font>(StudioPreviewText.EditorFontResource);
            if (editorFont != null) rootVisualElement.style.unityFontDefinition = FontDefinition.FromFont(editorFont);
            var header = new Label("Compiler portable subset · actual Toolkit and TMP");
            header.style.fontSize = 22; header.style.color = Color.white;
            rootVisualElement.Add(header);
            rootVisualElement.Add(new Label("Expected literal plain text: " + plainText) { style = { whiteSpace = WhiteSpace.Normal, color = Color.white } });
            rootVisualElement.Add(new Label(report.EditorThaiRichText) { enableRichText = true,
                style = { whiteSpace = WhiteSpace.Normal, color = Color.white, fontSize = 22, minHeight = 42 } });
            preview = new StudioPreviewText();
            preview.SetText(richText);
            rootVisualElement.Add(preview.Root);
        }

        internal void MeasureAndCapture()
        {
            try
            {
                if (preview == null || preview.ToolkitLabel.panel == null) throw new InvalidOperationException("Toolkit Label is not attached to a live panel.");
                var label = preview.ToolkitLabel;
                label.enableRichText = true;
                evidence.ToolkitRichWidth = Measure(label, markup).x;
                label.enableRichText = false;
                evidence.ToolkitLiteralMarkupWidth = Measure(label, markup).x;
                evidence.ToolkitLiteralPlainWidth = Measure(label, "<b>safe</b>").x;
                label.enableRichText = true;
                evidence.ToolkitLiteralEscapedWidth = Measure(label, "<noparse><</noparse>b>safe<noparse><</noparse>/b>").x;
                var stripped = evidence.ToolkitRichWidth > 0 && evidence.ToolkitLiteralMarkupWidth > evidence.ToolkitRichWidth + 20;
                var literal = evidence.ToolkitLiteralPlainWidth > 0 && Mathf.Abs(evidence.ToolkitLiteralEscapedWidth - evidence.ToolkitLiteralPlainWidth) < 3;
                evidence.ToolkitMeasurementPassed = stripped && literal;
                StudioDescriptionChecks.Record(stripped, "Attached UI Toolkit measures formatting tags as styling, with smaller glyph width than literal markup.");
                StudioDescriptionChecks.Record(literal, "Attached UI Toolkit measures generated noparse literal brackets like the corresponding plain literal text.");
                foreach (var entry in evidence.Capabilities)
                    entry.ToolkitStatus = evidence.ToolkitMeasurementPassed ? "Measured in attached actual Label; candidate pending screenshot review" : "Measurement failed";
                label.text = markup;
                label.enableRichText = true;
                Repaint();
                rootVisualElement.schedule.Execute(Capture).StartingIn(300);
            }
            catch (Exception error)
            {
                StudioDescriptionChecks.Record(false, "Toolkit measurement: " + error.Message);
                evidence.ToolkitCompleted = true;
                StudioDescriptionChecks.Write();
                Close();
            }
        }

        private void Capture()
        {
            try
            {
                evidence.ToolkitScreenshot = Path.Combine(evidence.OutputDirectory, "portable-subset-toolkit-window.png");
                StudioPreviewChecks.CaptureEditorWindowEvidence(this, evidence.ToolkitScreenshot);
                evidence.ToolkitCompleted = true;
                StudioDescriptionChecks.Write();
                Close();
            }
            catch (Exception error)
            {
                captureAttempts++;
                if (captureAttempts < 3) { Repaint(); rootVisualElement.schedule.Execute(Capture).StartingIn(400); return; }
                StudioDescriptionChecks.Record(false, "Toolkit screenshot: " + error.Message);
                evidence.ToolkitCompleted = true;
                StudioDescriptionChecks.Write();
                Close();
            }
        }

        private static Vector2 Measure(Label label, string source) => label.MeasureTextSize(source, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
        private void OnDisable() { preview?.Dispose(); preview = null; }
    }
}
