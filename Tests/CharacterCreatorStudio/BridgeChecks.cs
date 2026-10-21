using System;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class BridgeChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var count=0;void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Bridge: "+message);count++;}
            var generic=baseline.Clone();generic.Passive.Support.OpeningTeamGauge=0;
            var document=StudioDocumentService.ImportCharacter(generic);
            Check(document.LegacyCharacter.Id==generic.Id && !document.OwnsOpeningGauge,"General import preserves source identity without requiring opening gauge.");
            Check(document.Rules.Count==0,"General import does not invent a character-specific passive.");
            Check(StudioCompiler.Compile(document).Success,"General source imports compile.");
            var original=SnapshotJson.Serialize(generic);document.LegacyCharacter.BaseStats.Attack++;
            Check(SnapshotJson.Serialize(generic)==original,"Generic imports own a frozen source clone.");
            Check(StudioDocumentService.ImportCharacter(baseline).OwnsOpeningGauge,"Existing opening-gauge migration remains compatible.");
            var malformed=StudioDocumentService.ImportCharacter(baseline);malformed.LegacyCharacter.BaseStats.Attack=-1;
            Check(StudioCompiler.Compile(malformed).Diagnostics.Any(d=>d.Code=="character.validation"),"Negative native stats cannot compile green.");
            malformed=StudioDocumentService.ImportCharacter(baseline);malformed.LegacyCharacter.Skills[0].Ranks.Clear();
            Check(!StudioCompiler.Compile(malformed).Success,"Missing skill ranks block compilation.");
            malformed=StudioDocumentService.ImportCharacter(baseline);malformed.LegacyCharacter.UltimateTiers.Clear();
            Check(!StudioCompiler.Compile(malformed).Success,"Missing ultimate tiers block compilation.");
            malformed=StudioDocumentService.ImportCharacter(baseline);malformed.LegacyCharacter.Skills.Add(malformed.LegacyCharacter.Skills[0].Clone());
            Check(!StudioCompiler.Compile(malformed).Success,"A malformed skill count blocks compilation.");
            var raw=SnapshotJson.Serialize(baseline);StudioCompiler.Compile(StudioDocumentService.ImportCharacter(baseline));
            Check(SnapshotJson.Serialize(baseline)==raw,"Full runtime validation does not mutate source content.");
            Console.WriteLine("PASS: "+count+" Studio production bridge core assertions.");return count;
        }
    }
}
