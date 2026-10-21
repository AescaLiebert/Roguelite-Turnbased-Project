using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FightingAllstar.Core.Studio
{
    internal static class DescriptionParser
    {
        public static DescriptionSource Parse(string source, string blockId, string sourceId)
        {
            var result = new DescriptionSource { Source = source ?? "" };
            var stack = new Stack<int>();
            var text = result.Source;
            var cursor = 0;
            while (cursor < text.Length)
            {
                var start = cursor;
                var kind = DescriptionSpanKind.Literal;
                string value;
                if (text[cursor] == '\\' && cursor + 1 < text.Length && IsEscapable(text[cursor + 1]))
                {
                    value = text.Substring(cursor + 1, 1);
                    cursor += 2;
                }
                else if (cursor + 1 < text.Length && text[cursor] == '{' && text[cursor + 1] == '{')
                {
                    var end = FindTokenEnd(text, cursor + 2);
                    if (end < 0)
                    {
                        cursor = text.Length;
                        value = text.Substring(start);
                        kind = DescriptionSpanKind.Invalid;
                        Error("text.unclosed-token", "Close the reference with }}.", start, cursor - start);
                    }
                    else
                    {
                        value = text.Substring(cursor + 2, end - cursor - 2);
                        cursor = end + 2;
                        var colon = value.IndexOf(':');
                        var command = colon < 0 ? "" : value.Substring(0, colon);
                        switch (command)
                        {
                            case "term": kind = DescriptionSpanKind.Term; break;
                            case "value": kind = DescriptionSpanKind.Value; break;
                            case "snippet": kind = DescriptionSpanKind.Snippet; break;
                            case "style": kind = DescriptionSpanKind.Style; break;
                            case "text": kind = DescriptionSpanKind.TextInput; break;
                            default:
                                kind = DescriptionSpanKind.Invalid;
                                Error("text.unknown-token", "Use term, value, snippet, style, or text references.", start, cursor - start);
                                break;
                        }
                        if (colon >= 0) value = value.Substring(colon + 1);
                    }
                }
                else if (text[cursor] == '<')
                {
                    var end = text.IndexOf('>', cursor + 1);
                    if (end < 0)
                    {
                        cursor = text.Length;
                        value = text.Substring(start);
                        kind = DescriptionSpanKind.Invalid;
                        Error("text.unclosed-tag", "Close the formatting tag or escape a literal angle bracket with \\<.", start, cursor - start);
                    }
                    else
                    {
                        value = text.Substring(cursor + 1, end - cursor - 1);
                        cursor = end + 1;
                        var lower = value.ToLowerInvariant();
                        if (lower == "b" || lower == "i" || (lower.StartsWith("color=", StringComparison.Ordinal) && IsColor(value.Substring(6))))
                            kind = DescriptionSpanKind.OpenTag;
                        else if (lower == "/b" || lower == "/i" || lower == "/color") kind = DescriptionSpanKind.CloseTag;
                        else
                        {
                            kind = DescriptionSpanKind.Invalid;
                            Error("text.unsupported-tag", "Only b, i and color=#RRGGBB[AA] are enabled in this slice. Escape literal markup with \\< and \\>.", start, cursor - start);
                        }
                    }
                }
                else
                {
                    cursor++;
                    while (cursor < text.Length && text[cursor] != '<' &&
                           !(text[cursor] == '\\' && cursor + 1 < text.Length && IsEscapable(text[cursor + 1])) &&
                           !(text[cursor] == '{' && cursor + 1 < text.Length && text[cursor + 1] == '{')) cursor++;
                    value = text.Substring(start, cursor - start);
                }
                var span = new DescriptionSpan { Id = blockId + ":" + start, Kind = kind, Text = value,
                    ParentIndex = stack.Count == 0 ? -1 : stack.Peek(),
                    Range = new DescriptionRange { BlockId = blockId, SourceId = sourceId, Start = start, Length = cursor - start } };
                var index = result.Spans.Count;
                result.Spans.Add(span);
                if (kind == DescriptionSpanKind.OpenTag) stack.Push(index);
                if (kind == DescriptionSpanKind.CloseTag)
                {
                    if (stack.Count == 0 || TagName(result.Spans[stack.Peek()].Text) != value.Substring(1).ToLowerInvariant())
                    {
                        span.Kind = DescriptionSpanKind.Invalid;
                        Error("text.tag-nesting", "Close formatting tags in reverse opening order.", start, cursor - start);
                    }
                    else stack.Pop();
                }
            }
            foreach (var index in stack)
            {
                var span = result.Spans[index];
                Error("text.unclosed-tag", "Missing </" + TagName(span.Text) + ">.", span.Range.Start, span.Range.Length);
            }
            return result;

            void Error(string code, string message, int start, int length) => result.Diagnostics.Add(new DescriptionDiagnostic {
                Code = code, Message = message, Severity = DescriptionSeverity.Error,
                Range = new DescriptionRange { BlockId = blockId, SourceId = sourceId, Start = start, Length = length } });
        }

        internal static string TagName(string text)
        {
            var equals = text.IndexOf('=');
            return (equals < 0 ? text : text.Substring(0, equals)).ToLowerInvariant();
        }

        internal static bool IsColor(string color)
        {
            if (color == null || (color.Length != 7 && color.Length != 9) || color[0] != '#') return false;
            for (var i = 1; i < color.Length; i++)
                if (!((color[i] >= '0' && color[i] <= '9') || (color[i] >= 'A' && color[i] <= 'F') || (color[i] >= 'a' && color[i] <= 'f'))) return false;
            return true;
        }

        internal static List<string> Split(string source, char separator)
        {
            var pieces = new List<string>();
            var current = new StringBuilder();
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] == '\\' && i + 1 < source.Length && IsEscapable(source[i + 1])) { current.Append(source[++i]); continue; }
                if (source[i] == separator) { pieces.Add(current.ToString()); current.Clear(); }
                else current.Append(source[i]);
            }
            pieces.Add(current.ToString());
            return pieces;
        }

        internal static List<string> SplitPreservingEscapes(string source, char separator)
        {
            var pieces = new List<string>();
            var start = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] == '\\' && i + 1 < source.Length && IsEscapable(source[i + 1])) { i++; continue; }
                if (source[i] != separator) continue;
                pieces.Add(source.Substring(start, i - start)); start = i + 1;
            }
            pieces.Add(source.Substring(start));
            return pieces;
        }

        private static int FindTokenEnd(string source, int start)
        {
            for (var i = start; i + 1 < source.Length; i++)
            {
                if (source[i] == '\\' && i + 1 < source.Length && IsEscapable(source[i + 1])) { i++; continue; }
                if (source[i] == '}' && source[i + 1] == '}') return i;
            }
            return -1;
        }

        private static bool IsEscapable(char value) => value == '\\' || value == '{' || value == '}' || value == '<' || value == '>' || value == '|';
    }

    public static class DescriptionSourceCodec
    {
        // Mutates a deep-cloned document only. External term/style/snippet IDs never change.
        public static void RemapLocalReferences(DescriptionDocument document, IReadOnlyDictionary<string, string> ids)
        {
            if (document == null || ids == null) return;
            document.Id = Map(document.Id);
            document.OwnerId = Map(document.OwnerId);
            foreach (var binding in document.Bindings ?? new List<DescriptionValueBinding>()) { binding.Id = Map(binding.Id); binding.SkillId = Map(binding.SkillId); binding.EffectId = Map(binding.EffectId); }
            foreach (var locale in (document.Locales ?? new List<DescriptionLocale>()).Where(x => x != null))
            foreach (var block in (locale.Blocks ?? new List<DescriptionBlock>()).Where(x => x != null))
            {
                block.Id = Map(block.Id);
                block.MechanicIds = (block.MechanicIds ?? new List<string>()).Select(Map).ToList();
                block.Source = RemapParameterReferences(block.Source, ids);
                foreach (var branch in block.Branches ?? new List<DescriptionBranch>()) { branch.Id = Map(branch.Id); branch.VariantId = Map(branch.VariantId); branch.Source = RemapParameterReferences(branch.Source, ids); }
            }
            string Map(string value) => value != null && ids.TryGetValue(value, out var mapped) ? mapped : value;
        }

        public static string RemapParameterReferences(string source, IReadOnlyDictionary<string, string> ids)
        {
            var parsed = DescriptionSource.Parse(source);
            var result = new StringBuilder(parsed.Source);
            foreach (var span in parsed.Spans.AsEnumerable().Reverse())
            {
                string replacement = null;
                if (span.Kind == DescriptionSpanKind.Value)
                {
                    var parts = DescriptionParser.SplitPreservingEscapes(span.Text, '|');
                    if (ids.TryGetValue(parts[0], out var mapped)) { parts[0] = mapped; replacement = "{{value:" + string.Join("|", parts) + "}}"; }
                }
                else if (span.Kind == DescriptionSpanKind.Snippet)
                {
                    var parts = DescriptionParser.SplitPreservingEscapes(span.Text, '|');
                    var changed = false;
                    for (var i = 1; i < parts.Count; i++)
                    {
                        var equals = parts[i].IndexOf('=');
                        if (equals < 0 || !ids.TryGetValue(parts[i].Substring(equals + 1), out var mapped)) continue;
                        parts[i] = parts[i].Substring(0, equals + 1) + mapped;
                        changed = true;
                    }
                    if (changed) replacement = "{{snippet:" + string.Join("|", parts) + "}}";
                }
                if (replacement != null) { result.Remove(span.Range.Start, span.Range.Length); result.Insert(span.Range.Start, replacement); }
            }
            return result.ToString();
        }
    }
}
