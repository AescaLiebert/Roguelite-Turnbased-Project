using System;
using System.Collections.Generic;
using System.Linq;

namespace FightingAllstar.Core.Studio
{
    public enum DescriptionBlockKind { Paragraph, Section, Bullet, NumberedItem, ConditionNote }
    public enum DescriptionParameterUnit { Integer, BasisPoints, Scalar, Turns }
    public enum DescriptionInputKind { ValueParameter, Term, Text }
    public enum DescriptionSeverity { Info, Warning, Error }
    public enum DescriptionSpanKind { Literal, OpenTag, CloseTag, Term, Value, Snippet, Style, TextInput, Invalid }

    [Serializable]
    public sealed class DescriptionDocument
    {
        public int SchemaVersion = 1;
        public string Id = "description.default";
        public string OwnerId = "";
        public string DefaultLocale = "en";
        public List<DescriptionLocale> Locales = new List<DescriptionLocale>();
        public List<DescriptionValueBinding> Bindings = new List<DescriptionValueBinding>();

        public static DescriptionDocument FromSource(string source, string locale = "en") => new DescriptionDocument
        {
            DefaultLocale = locale,
            Locales = new List<DescriptionLocale> { new DescriptionLocale { Locale = locale,
                Blocks = new List<DescriptionBlock> { new DescriptionBlock { Id = "paragraph.main", Source = source ?? "" } } } }
        };
    }

    [Serializable]
    public sealed class DescriptionLocale
    {
        public string Locale = "en";
        public string ReviewState = "Draft";
        public string ReviewedTextHash = "";
        public string ReviewedGameplayHash = "";
        public List<DescriptionBlock> Blocks = new List<DescriptionBlock>();
    }

    [Serializable]
    public sealed class DescriptionBlock
    {
        public string Id = "";
        public DescriptionBlockKind Kind;
        public string StyleId = "";
        // Source is authoritative, including invalid drafts. Parser output is a derived, flat table.
        public string Source = "";
        public List<string> MechanicIds = new List<string>();
        public bool HasFallback = true;
        public List<DescriptionBranch> Branches = new List<DescriptionBranch>();
        public List<string> DetachedSnippetOrigins = new List<string>();
    }

    [Serializable]
    public sealed class DescriptionLocalizedText
    {
        public string Locale = "en";
        public string Text = "";
    }

    [Serializable]
    public sealed class DescriptionTerm
    {
        public string Id = "";
        public int Revision = 1;
        public string Category = "Glossary";
        public string GameplayDefinitionId = "";
        public string StyleId = "style.keyword";
        public List<DescriptionLocalizedText> Names = new List<DescriptionLocalizedText>();
        public List<DescriptionLocalizedText> Definitions = new List<DescriptionLocalizedText>();
        public List<DescriptionLocalizedText> Aliases = new List<DescriptionLocalizedText>();
        public bool Deprecated;
        public DescriptionDocument Tooltip;
    }

    [Serializable]
    public sealed class DescriptionStyle
    {
        public string Id = "";
        public int Revision = 1;
        public string Name = "";
        public string Color = "#FFFFFF";
        public bool Bold;
        public bool Italic;
    }

    [Serializable]
    public sealed class DescriptionSnippetInput
    {
        public string Id = "";
        public DescriptionInputKind Kind;
    }

    [Serializable]
    public sealed class DescriptionSnippet
    {
        public string Id = "";
        public string Name = "";
        public string Category = "Wording";
        public string UsageNotes = "";
        public int Revision = 1;
        public List<DescriptionSnippetInput> Inputs = new List<DescriptionSnippetInput>();
        public List<DescriptionLocalizedText> Templates = new List<DescriptionLocalizedText>();
    }

    [Serializable]
    public sealed class DescriptionLibrary
    {
        public int SchemaVersion = 1;
        public List<DescriptionTerm> Terms = new List<DescriptionTerm>();
        public List<DescriptionStyle> Styles = new List<DescriptionStyle>();
        public List<DescriptionSnippet> Snippets = new List<DescriptionSnippet>();

        public static DescriptionLibrary CreateOpeningGaugeLibrary() => new DescriptionLibrary
        {
            Terms = new List<DescriptionTerm> { new DescriptionTerm { Id = "keyword.power-gauge", Category = "Resource",
                Names = new List<DescriptionLocalizedText> {
                    new DescriptionLocalizedText { Locale = "en", Text = "Power Gauge" },
                    new DescriptionLocalizedText { Locale = "th", Text = "เกจพลัง" } },
                Definitions = new List<DescriptionLocalizedText> {
                    new DescriptionLocalizedText { Locale = "en", Text = "Resource used to activate an ultimate." },
                    new DescriptionLocalizedText { Locale = "th", Text = "ทรัพยากรที่ใช้เพื่อออกท่าไม้ตาย" } } } },
            Styles = new List<DescriptionStyle> {
                new DescriptionStyle { Id = "style.keyword", Name = "Keyword", Color = "#70D6FF", Bold = true },
                new DescriptionStyle { Id = "style.value", Name = "Bound value", Color = "#FFD580", Bold = true } },
            Snippets = new List<DescriptionSnippet> { new DescriptionSnippet {
                Id = "snippet.opening-gauge", Revision = 1,
                Inputs = new List<DescriptionSnippetInput> {
                    new DescriptionSnippetInput { Id = "amount", Kind = DescriptionInputKind.ValueParameter },
                    new DescriptionSnippetInput { Id = "resource", Kind = DescriptionInputKind.Term } },
                Templates = new List<DescriptionLocalizedText> {
                    new DescriptionLocalizedText { Locale = "en", Text = "At battle start, active allies gain {{value:$amount|integer}} {{term:$resource}}." },
                    new DescriptionLocalizedText { Locale = "th", Text = "เมื่อเริ่มต่อสู้ เพิ่ม{{term:$resource}} {{value:$amount|integer}} จุดให้พันธมิตรที่อยู่ในสนามและยังมีชีวิต" } } } }
        };

        public const string OpeningGaugeSource = "{{snippet:snippet.opening-gauge@1|amount=param.opening-gauge|resource=keyword.power-gauge}}";
    }

    public sealed class DescriptionParameterValue
    {
        public decimal Value;
        public DescriptionParameterUnit Unit;
        public string Clock = "";
    }

    public sealed class DescriptionCompileContext
    {
        public string Locale = "en";
        public string FallbackLocale = "en";
        public IReadOnlyDictionary<string, DescriptionParameterValue> Parameters = new Dictionary<string, DescriptionParameterValue>();
        public int MaxSnippetDepth = 16;
        public int MaxExpandedCharacters = 65536;
        public int Level = 1;
        public int Rank = 1;
        public int Tier;
        public string VariantId = "";
        public IReadOnlyCollection<string> KnownMechanicIds;

        public static DescriptionCompileContext FromParameters(IEnumerable<StudioParameter> parameters, string locale = "en") => new DescriptionCompileContext
        {
            Locale = locale,
            Parameters = (parameters ?? Enumerable.Empty<StudioParameter>()).ToDictionary(p => p.Id,
                p => new DescriptionParameterValue { Value = p.Value,
                    Unit = p.Unit == StudioParameterUnit.BasisPoints ? DescriptionParameterUnit.BasisPoints : DescriptionParameterUnit.Integer }, StringComparer.Ordinal)
        };
    }

    public sealed class DescriptionRange
    {
        public string BlockId = "";
        public string SourceId = "";
        public string BranchId = "";
        // UTF-16 offsets match Unity TextField selection indices, preserving Thai combining marks.
        public int Start;
        public int Length;
    }

    public sealed class DescriptionDiagnostic
    {
        public DescriptionSeverity Severity;
        public string Code = "";
        public string Message = "";
        public DescriptionRange Range = new DescriptionRange();
    }

    public sealed class DescriptionSpan
    {
        public string Id = "";
        public DescriptionSpanKind Kind;
        public DescriptionRange Range = new DescriptionRange();
        public int ParentIndex = -1;
        public string Text = "";
    }

    public sealed class DescriptionSource
    {
        public string Source { get; internal set; } = "";
        public List<DescriptionSpan> Spans { get; } = new List<DescriptionSpan>();
        public List<DescriptionDiagnostic> Diagnostics { get; } = new List<DescriptionDiagnostic>();
        public string Serialize() => Source;
        public static DescriptionSource Parse(string source, string blockId = "paragraph.main", string sourceId = "") => DescriptionParser.Parse(source, blockId, sourceId);
    }

    public sealed class DescriptionDependency
    {
        public string Kind = "";
        public string Id = "";
        public int Revision;
    }

    public sealed class DescriptionSemanticReference
    {
        public string Kind = "";
        public string Id = "";
        public DescriptionRange Range = new DescriptionRange();
        public int PlainStart;
        public int PlainLength;
    }

    public sealed class DescriptionSourceMapping
    {
        public DescriptionRange Range = new DescriptionRange();
        public int PlainStart;
        public int PlainLength;
    }

    public sealed class DescriptionCompileResult
    {
        public string ToolkitRichText = "";
        public string TmpRichText = "";
        public string PlainText = "";
        public string TextHash = "";
        public List<DescriptionDiagnostic> Diagnostics = new List<DescriptionDiagnostic>();
        public List<DescriptionDependency> Dependencies = new List<DescriptionDependency>();
        public List<DescriptionSemanticReference> SemanticReferences = new List<DescriptionSemanticReference>();
        public List<DescriptionSourceMapping> SourceMap = new List<DescriptionSourceMapping>();
        public bool Success => !Diagnostics.Any(d => d.Severity == DescriptionSeverity.Error);
    }

    public sealed class DescriptionTagCapability
    {
        public string Syntax;
        public string Description;
        public string Verification = "Candidate: pending project Toolkit/TMP render verification";
    }

    public static class DescriptionCapabilities
    {
        // This is the compiler subset, not a claim of project-wide renderer certification.
        public static readonly IReadOnlyList<DescriptionTagCapability> Tags = new[] {
            new DescriptionTagCapability { Syntax = "<b>text</b>", Description = "Bold emphasis" },
            new DescriptionTagCapability { Syntax = "<i>text</i>", Description = "Italic emphasis" },
            new DescriptionTagCapability { Syntax = "<color=#RRGGBB>text</color>", Description = "Six- or eight-digit hex color" },
            new DescriptionTagCapability { Syntax = @"\<b\>", Description = "Literal angle brackets (compiler emits noparse safety spans)" }
        };
    }
}
