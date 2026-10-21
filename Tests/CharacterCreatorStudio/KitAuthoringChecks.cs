using System;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class KitAuthoringChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var count = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Kit authoring: " + message); count++; }
            var doc = StudioDocumentService.ImportCharacter(baseline);
            var frozen = SnapshotJson.Serialize(doc.LegacyCharacter);
            var c = StudioDocumentService.BeginKitEditing(doc);
            Check(!ReferenceEquals(c, doc.LegacyCharacter), "The editable kit is separate from the frozen import.");
            Check(ReferenceEquals(c, StudioDocumentService.BeginKitEditing(doc)), "Repeated edits retain the same authoring payload.");
            c.DisplayName = "Edited kit"; c.BaseStats.Attack += 17; c.StatGrowth.AttackBp += 13;
            c.Skills[0].Ranks[0].Effect.CoefficientBp += 321;
            c.Skills[1].Ranks[2].Description = "Authored rank wording";
            c.UltimateTiers[5].Effect.CoefficientBp += 765;
            c.PassiveSource.Description = "Authored passive wording";
            var compiled = StudioCompiler.Compile(doc);
            Check(compiled.Success, string.Join("; ", compiled.Diagnostics));
            Check(compiled.Character.DisplayName == "Edited kit" && compiled.Character.BaseStats.Attack == baseline.BaseStats.Attack + 17, "Identity and base stats compile.");
            Check(compiled.Character.StatGrowth.AttackBp == baseline.StatGrowth.AttackBp + 13, "Growth compiles.");
            Check(compiled.Character.Skills[0].Ranks[0].Effect.CoefficientBp == baseline.Skills[0].Ranks[0].Effect.CoefficientBp + 321, "Base rank effects compile.");
            Check(compiled.Character.Skills[1].Ranks[2].Description == "Authored rank wording", "Independent rank wording compiles.");
            Check(compiled.Character.UltimateTiers[5].Effect.CoefficientBp == baseline.UltimateTiers[5].Effect.CoefficientBp + 765, "Ultimate tier edits compile.");
            Check(compiled.Character.PassiveSource.Description == "Authored passive wording", "Native passive wording compiles.");
            Check(SnapshotJson.Serialize(doc.LegacyCharacter) == frozen, "All kit edits preserve the frozen import.");
            var restored = StudioDocumentService.Clone(doc);
            Check(StudioCompiler.Compile(restored).Hashes.Bundle == compiled.Hashes.Bundle, "Authoring payload roundtrips deterministically.");
            var textOnly = StudioDocumentService.Clone(doc); textOnly.AuthoredCharacter.Skills[0].Ranks[0].Description = "New wording only";
            var textResult = StudioCompiler.Compile(textOnly);
            Check(textResult.Hashes.Text != compiled.Hashes.Text && textResult.Hashes.Bundle != compiled.Hashes.Bundle && textResult.Hashes.Gameplay == compiled.Hashes.Gameplay, "Native wording changes text/bundle identity without changing gameplay identity.");
            c.BaseStats.Attack = -1;
            Check(!StudioCompiler.Compile(doc).Success, "Invalid authored stats cannot hide behind a valid import.");
            c.BaseStats.Attack = baseline.BaseStats.Attack; c.Id = "fighter.wrong";
            Check(StudioCompiler.Compile(doc).Diagnostics.Any(d => d.Code == "authoring.identity"), "Identity changes must use reviewed native reidentification.");
            var old = StudioDocumentService.ImportCharacter(baseline); old.SchemaVersion = 3;
            Check(StudioDocumentService.TryMigrate(old, out var migrated, out _) && migrated.SchemaVersion == StudioDocument.CurrentSchemaVersion && migrated.AuthoredCharacter == null, "Schema 3 migrates without inventing edits.");
            Console.WriteLine("PASS: " + count + " Studio kit authoring assertions."); return count;
        }
    }
}
