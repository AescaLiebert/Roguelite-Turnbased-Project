using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Core.Combat;

namespace CharacterCreatorStudio.Checks
{
    internal static class M4DescriptionChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var count = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("M4: " + message); count++; }
            var library = DescriptionLibrary.CreateOpeningGaugeLibrary();
            var doc = StudioDocumentService.ImportOpeningGauge(baseline);
            var source = SnapshotJson.Serialize(doc);
            var block = doc.Description.Locales[0].Blocks[0];
            block.Source = "Base {{value:param.opening-gauge|integer}}";
            var branch = new DescriptionBranch { MinimumLevel = 2, VariantId = "ex", Source = "EX {{value:param.opening-gauge|integer}}", Name = "EX upgraded" }; block.Branches.Add(branch);
            var context = DescriptionCompileContext.FromParameters(doc.Parameters); context.Level = 2; context.VariantId = "ex";
            var result = DescriptionCompiler.Compile(doc.Description, library, context);
            Check(result.Success && result.PlainText == "EX 1", "Selected combined level/variant branch compiles.");
            context.Level = 1; Check(DescriptionCompiler.Compile(doc.Description, library, context).PlainText == "Base 1", "Fallback survives a nonmatching level.");
            context.Level = 2; context.VariantId = ""; Check(DescriptionCompiler.Compile(doc.Description, library, context).PlainText == "Base 1", "Base and EX remain independent.");
            context.VariantId = "ex";
            block.Branches.Add(new DescriptionBranch { MinimumLevel = 2, Source = "Second" });
            var overlap = DescriptionCompiler.Compile(doc.Description, library, context);
            Check(overlap.PlainText == "EX 1" && overlap.Diagnostics.Any(d => d.Code == "text.branch-overlap"), "First branch wins and overlap is visible.");
            block.Branches.Reverse(); Check(DescriptionCompiler.Compile(doc.Description, library, context).PlainText == "Second", "Reordering changes the winner."); block.Branches.Reverse(); block.Branches.RemoveAt(1);
            block.HasFallback = false; Check(!DescriptionCompiler.Compile(doc.Description, library, context).Success, "Coverage rejects holes beyond selected configuration.");
            block.Branches.Add(new DescriptionBranch { Source = "All remaining" });
            Check(DescriptionAuthoring.ValidateBranches(doc.Description).Count == 0, "Exhaustive wildcard covers the finite interval partition.");
            block.Branches.RemoveAt(1); block.HasFallback = true;
            branch.MaximumLevel = 1; Check(!DescriptionCompiler.Compile(doc.Description, library, context).Success, "Inverted branch range rejected."); branch.MaximumLevel = int.MaxValue;
            branch.Source = "<unknown>tag</unknown>"; context.Level = 1;
            Check(!DescriptionCompiler.Compile(doc.Description, library, context).Success, "Malformed hidden branch blocks acceptance."); branch.Source = "EX {{value:param.opening-gauge|integer}}";
            var rt = SnapshotJson.Deserialize<DescriptionDocument>(SnapshotJson.Serialize(doc.Description));
            Check(rt.Locales[0].Blocks[0].Branches[0].Id == branch.Id, "Branch identity round trips.");
            var dup = StudioDocumentService.Duplicate(doc);
            Check(dup.Description.Locales[0].Blocks[0].Branches[0].Id != branch.Id && dup.Description.Locales[0].Blocks[0].Branches[0].Source.Contains("{{value:" + dup.Parameters[0].Id + "|integer}}"), "Duplication remaps branch identities and parameters.");
            var semanticHash = DescriptionCompiler.Compile(doc.Description, library, context).TextHash;
            doc.Description.Locales[0].ReviewState = "Reviewed"; doc.Description.Locales[0].ReviewedTextHash = "receipt";
            Check(semanticHash == DescriptionCompiler.Compile(doc.Description, library, context).TextHash, "Review receipt does not change semantic text hash.");
            var snippet = new DescriptionSnippet { Id = "snippet.test", Inputs = new List<DescriptionSnippetInput> {
                new DescriptionSnippetInput { Id = "amount", Kind = DescriptionInputKind.ValueParameter }, new DescriptionSnippetInput { Id = "who", Kind = DescriptionInputKind.Term }, new DescriptionSnippetInput { Id = "note", Kind = DescriptionInputKind.Text } },
                Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = "{{text:$note}} {{term:$who}} {{value:$amount|integer}}" }, new DescriptionLocalizedText { Locale = "th", Text = "{{term:$who}} {{value:$amount|integer}} {{text:$note}}" } } };
            library.Snippets.Add(snippet);
            var inputs = new Dictionary<string, string> { ["amount"] = "param.opening-gauge", ["who"] = "keyword.power-gauge", ["note"] = "<b>literal|{ไทย}</b>" };
            var token = DescriptionAuthoring.SnippetToken(snippet, inputs);
            var linked = DescriptionCompiler.Compile(token, library, context);
            Check(linked.Success && linked.PlainText.StartsWith(inputs["note"]), "Typed text input is literal and delimiters remain safe.");
            var detached = DescriptionAuthoring.DetachSnippet(token, library, "en");
            var detachedResult = DescriptionCompiler.Compile(detached, library, context);
            Check(detachedResult.Success && detachedResult.PlainText == linked.PlainText && detached.Contains("{{value:"), "Detach retains semantic bindings and literal escaping.");
            var thContext = DescriptionCompileContext.FromParameters(doc.Parameters, "th");
            var thai = DescriptionCompiler.Compile(token, library, thContext);
            Check(thai.Success && thai.PlainText.EndsWith(inputs["note"]), "Thai template reorders complete inputs.");
            bool missing = false; try { DescriptionAuthoring.SnippetToken(snippet, new Dictionary<string, string>()); } catch (InvalidOperationException) { missing = true; }
            Check(missing, "No plausible defaults for unbound snippet inputs.");
            var next = SnapshotJson.Deserialize<DescriptionSnippet>(SnapshotJson.Serialize(snippet)); next.Revision = 2; next.Templates[0].Text = "New {{value:$amount|integer}}"; library.Snippets.Add(next);
            Check(DescriptionCompiler.Compile(token, library, context).PlainText == linked.PlainText, "New revision cannot change a pinned instance.");
            Check(DescriptionCompiler.Compile(token.Replace("@1", "@2"), library, context).PlainText == "New 1", "Explicit adoption selects new revision.");
            var useDoc = DescriptionDocument.FromSource(token); var uses = DescriptionAuthoring.FindUsages(useDoc, library, "snippet.test@1");
            Check(uses.Any(u => u.SourceId == useDoc.Id), "Pinned revision consumers are discoverable.");
            Check(DescriptionAuthoring.FindUsages(useDoc, library, "keyword.power-gauge").Any(), "Snippet-input term consumers are discoverable.");
            var kw = library.Terms[0]; kw.Names[0].Text = "Renamed gauge"; kw.Revision++;
            Check(DescriptionCompiler.Compile(token, library, context).PlainText.Contains("Renamed gauge"), "Keyword rename updates labels without changing identity.");
            kw.Deprecated = true; Check(DescriptionCompiler.Compile(token, library, context).Diagnostics.Any(d => d.Code == "text.deprecated-term"), "Deprecated keyword emits visible warning."); kw.Deprecated = false;
            var alias = new DescriptionTerm { Id = "keyword.other", Names = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = "Other" } }, Aliases = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = "Renamed gauge" } } }; library.Terms.Add(alias);
            Check(DescriptionAuthoring.ValidateLibrary(library).Any(d => d.Code == "text.ambiguous-alias"), "Ambiguous aliases cannot silently normalize prose."); library.Terms.Remove(alias);
            kw.Tooltip = DescriptionDocument.FromSource("See {{term:keyword.other}}."); Check(DescriptionAuthoring.ValidateLibrary(library).Any(d => d.Code == "text.tooltip-link"), "Dangling glossary reference rejected.");
            alias.Aliases.Clear(); library.Terms.Add(alias); alias.Tooltip = DescriptionDocument.FromSource("{{term:" + kw.Id + "}}");
            Check(DescriptionAuthoring.ValidateLibrary(library).Count == 0, "Glossary cycles are links, not recursive expansion.");
            var cycle = new DescriptionSnippet { Id = "snippet.cycle", Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = "{{snippet:snippet.cycle@1}}" } } }; library.Snippets.Add(cycle);
            Check(DescriptionAuthoring.ValidateLibrary(library).Any(d => d.Code == "text.snippet-cycle"), "Even unused snippet cycles are rejected."); library.Snippets.Remove(cycle);
            cycle.Templates[0].Text = "{{snippet:snippet.absent@1}}"; library.Snippets.Add(cycle);
            Check(DescriptionAuthoring.ValidateLibrary(library).Any(d => d.Code == "text.missing-snippet"), "Unused missing pinned dependency rejected."); library.Snippets.Remove(cycle);
            var contextDoc = StudioDocumentService.ImportOpeningGauge(baseline); var skill = contextDoc.LegacyCharacter.Skills[0];
            var effect = skill.Ranks.First(r => r.Rank == 1).Effect;
            skill.ValueOverrides.Add(new AbilityValueOverride { Id = "override.m4", MinimumLevel = 2, Field = AbilityValueField.CoefficientBp, Value = 7654 });
            contextDoc.Description.Bindings.Add(new DescriptionValueBinding { Id = "binding.damage", SkillId = skill.Id, Field = DescriptionBindingField.Coefficient });
            contextDoc.Description.Locales[0].Blocks[0].Source = "{{value:binding.damage|percent}}";
            var config1 = DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, "");
            var config2 = DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 2, 1, 0, "");
            Check(config1.Parameters["binding.damage"].Value == effect.CoefficientBp && config2.Parameters["binding.damage"].Value == 7654, "Resolved binding follows production level override.");
            Check(DescriptionCompiler.Compile(contextDoc.Description, library, config2).PlainText == "76.54%", "Resolved coefficient uses basis-point formatting.");
            var saved = SnapshotJson.Serialize(contextDoc);
            DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 2, 2, 5, "");
            Check(saved == SnapshotJson.Serialize(contextDoc), "Preview never mutates imported definitions.");
            var contextDuplicate = StudioDocumentService.Duplicate(contextDoc);
            Check(contextDuplicate.Description.Bindings[0].Id != "binding.damage" && contextDuplicate.Description.Locales[0].Blocks[0].Source.Contains(contextDuplicate.Description.Bindings[0].Id), "Resolved binding identity and tokens remap together.");
            contextDoc.Description.Bindings[0].EffectId = "missing"; missing = false;
            try { DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, ""); } catch (InvalidOperationException) { missing = true; }
            Check(missing, "Missing resolved effect is explicit failure."); contextDoc.Description.Bindings[0].EffectId = "";
            var linkedBlock = contextDoc.Description.Locales[0].Blocks[0]; linkedBlock.MechanicIds.Add("rule.absent");
            Check(DescriptionCompiler.Compile(contextDoc.Description, library, DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, "")).Diagnostics.Any(d => d.Code == "text.mechanic-link"), "Dangling coverage link rejected.");
            var fallbackContext = DescriptionCompileContext.FromParameters(doc.Parameters, "ja");
            Check(DescriptionCompiler.Compile(doc.Description, library, fallbackContext).Diagnostics.Any(d => d.Code == "text.locale-fallback"), "Missing translation fallback remains visible.");
            Check(DescriptionSource.Parse("ไทย \\<b\\> {{value:param.opening-gauge|integer}}\n<b>English</b>").Serialize() == "ไทย \\<b\\> {{value:param.opening-gauge|integer}}\n<b>English</b>", "Thai source, literals, token identity and whitespace round trip.");
            var mappedCopy = StudioDocumentService.Duplicate(contextDoc, out var mapping);
            var localLibrary = DescriptionLibrary.CreateOpeningGaugeLibrary();
            localLibrary.Terms[0].GameplayDefinitionId = contextDoc.Rules[0].Id;
            localLibrary.Snippets[0].Templates[0].Text = "{{value:" + contextDoc.Description.Bindings[0].Id + "|percent}}";
            var mappedLibrary = DescriptionAuthoring.RemapLibrary(localLibrary, mapping);
            Check(mappedLibrary.Terms[0].GameplayDefinitionId == mappedCopy.Rules[0].Id && mappedLibrary.Snippets[0].Templates[0].Text.Contains(mappedCopy.Description.Bindings[0].Id), "Bundle duplication remaps local keyword links and direct template bindings.");
            Check(localLibrary.Terms[0].GameplayDefinitionId == contextDoc.Rules[0].Id, "Library remapping preserves original bytes.");
            var localeDoc = StudioDocumentService.ImportOpeningGauge(baseline);
            localeDoc.Description.Locales.Add(new DescriptionLocale { Locale = "th", Blocks = new List<DescriptionBlock> { new DescriptionBlock { Id = "th.main", Source = "{{value:missing|integer}}" } } });
            Check(StudioCompiler.Compile(localeDoc).Diagnostics.Any(d => d.Code == "description.locale-invalid"), "An invalid unselected translation blocks bundle compilation.");
            var incorrectInput = new DescriptionSnippet { Id = "snippet.wrong-type", Inputs = new List<DescriptionSnippetInput> { new DescriptionSnippetInput { Id = "x", Kind = DescriptionInputKind.Text } }, Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = "{{value:$x|integer}}" } } };
            library.Snippets.Add(incorrectInput); Check(DescriptionAuthoring.ValidateLibrary(library).Any(d => d.Code == "text.snippet-input-definition"), "Unused snippet placeholder types are checked."); library.Snippets.Remove(incorrectInput);
            var gameplayLinkContext = DescriptionCompileContext.FromParameters(doc.Parameters); gameplayLinkContext.KnownMechanicIds = new List<string>();
            kw.GameplayDefinitionId = "absent.gameplay";
            Check(DescriptionCompiler.Compile("{{term:" + kw.Id + "}}", library, gameplayLinkContext).Diagnostics.Any(d => d.Code == "text.gameplay-link"), "Dangling mechanic-backed keyword link is rejected."); kw.GameplayDefinitionId = "";
            bool invalidVariant = false; try { DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, "unknown"); } catch (InvalidOperationException) { invalidVariant = true; }
            Check(invalidVariant, "Unknown authored variant does not silently preview Base.");
            contextDoc.Description.Bindings[0].Field = DescriptionBindingField.StatusDuration;
            bool wrongKind = false; try { DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, ""); } catch (InvalidOperationException) { wrongKind = true; }
            Check(wrongKind, "Resolved fields reject incompatible effect kinds.");
            effect.Kind = EffectKind.ApplyStatus; effect.StatusRecipe = new StatusRecipeDefinition { Id = "status.duration", DefaultDuration = 3, DurationClock = StatusDurationClock.SourceTurnEnd };
            var duration = DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, "").Parameters["binding.damage"];
            Check(duration.Value == 3 && duration.Unit == DescriptionParameterUnit.Turns && duration.Clock == "SourceTurnEnd", "Default status duration retains its clock and typed unit.");
            effect.StatusDurationOverride = 2;
            Check(DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, "").Parameters["binding.damage"].Value == 2, "Explicit status duration overrides the recipe default.");
            effect.StatusRecipe.DurationClock = StatusDurationClock.Permanent;
            bool permanent = false; try { DescriptionAuthoring.Context(contextDoc, contextDoc.LegacyCharacter, "en", 1, 1, 0, ""); } catch (InvalidOperationException) { permanent = true; }
            Check(permanent, "Permanent duration requires authored permanent wording, not zero turns.");
            Console.WriteLine("PASS: " + count + " M4 description authoring checks."); return count;
        }
    }
}
