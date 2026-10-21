using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class M6PresentationChecks
    {
        public static int Run()
        {
            var count=0;
            void Check(bool ok,string message) { if(!ok)throw new InvalidOperationException("M6: "+message);count++; }
            var events=new List<BattleEvent>{new BattleEvent{Id=1,Kind=BattleEventKind.CardPlayed,SourceId="a",Card=new CardState{SkillId="skill.1"}},
                new BattleEvent{Id=2,Kind=BattleEventKind.DamageApplied,SourceId="a",TargetId="b",HitIndex=1,HitCount=2,Amount=15,HealthAfter=85},
                new BattleEvent{Id=3,Kind=BattleEventKind.DamageApplied,SourceId="a",TargetId="c",HitIndex=1,HitCount=2,Amount=20,HealthAfter=80},
                new BattleEvent{Id=4,Kind=BattleEventKind.DamageApplied,SourceId="a",TargetId="b",HitIndex=2,HitCount=2,Amount=25,HealthAfter=60},
                new BattleEvent{Id=5,Kind=BattleEventKind.HealApplied,TargetId="b",Amount=10,HealthAfter=70},
                new BattleEvent{Id=6,Kind=BattleEventKind.ActionCompleted,SourceId="a"}};
            var before=SnapshotJson.Serialize(events);var timeline=PresentationTimeline.Build(events);
            Check(timeline.Entries.Count==events.Count,"Every committed fact is retained.");
            Check(timeline.Entries.Select(e=>e.Fact.Id).SequenceEqual(events.Select(e=>e.Id)),"Committed ordering preserved.");
            Check(timeline.Entries[1].ContactId=="contact:1" && timeline.Entries[2].ContactId=="contact:1","Area recipients share logical contact identity.");
            Check(timeline.Entries[1].Time==timeline.Entries[2].Time,"Area impacts are simultaneous.");
            Check(timeline.Entries[3].ContactId=="contact:2","Later ordinal retains its stable identity.");
            Check(timeline.HealthAt(0).Count==0,"Future HP facts are not projected early.");
            Check(timeline.HealthAt(timeline.Entries[1].Time)["b"]==85,"First contact projects its exact committed HP.");
            Check(timeline.HealthAt(timeline.Duration)["b"]==70,"Heal after damage projects final committed HP.");
            Check(timeline.HealthAt(timeline.Duration)["c"]==80,"Other target HP retained independently.");
            var altered=PresentationTimeline.Build(events,.4f,2,.5f,new Dictionary<int,float>{{1,.2f},{2,.8f},{9,.9f}});
            Check(altered.Entries.Count==timeline.Entries.Count,"An unmatched authored marker cannot create a hit.");
            Check(SnapshotJson.Serialize(altered.Entries.Select(e=>e.Fact).ToList())==before,"Visual timing edits cannot mutate any combat fact.");
            Check(altered.Duration!=timeline.Duration,"Timing edits change only the presentation clock.");
            var backwards=PresentationTimeline.Build(events,contacts:new Dictionary<int,float>{{1,.9f},{2,.1f}});
            Check(backwards.Entries.Zip(backwards.Entries.Skip(1),(a,b)=>a.Time<=b.Time).All(x=>x),"Malformed markers cannot reverse authoritative order.");
            timeline.Entries[1].Fact.Amount=999;
            Check(timeline.Entries[1].Fact.Amount==15,"Fact reads are immutable copies.");
            events[1].Amount=888;
            Check(timeline.Entries[1].Fact.Amount==15,"Source edits cannot change frozen playback.");
            var copy=timeline.HealthAt(timeline.Duration);copy["b"]=999;
            Check(timeline.HealthAt(timeline.Duration)["b"]==70,"Projected dictionaries are independent.");
            Check(PresentationTimeline.Build(null).Duration>0,"Empty playback has a bounded duration.");
            var rejected=false;try{PresentationTimeline.Build(events,action:float.NaN);}catch(ArgumentException){rejected=true;}
            Check(rejected,"NaN clocks are rejected.");
            rejected=false;try{PresentationTimeline.Build(events,contacts:new Dictionary<int,float>{{1,2}});}catch(ArgumentException){rejected=true;}
            Check(rejected,"Out-of-range marker offsets rejected.");
            var multi=PresentationTimeline.Build(events.Concat(events.Select(e=>{var c=e.Clone();c.Id+=10;return c;})));
            Check(multi.Entries[6].Time>multi.Entries[5].Time,"Consecutive actions retain recovery separation.");
            Console.WriteLine("PASS: "+count+" M6 immutable presentation timeline assertions.");return count;
        }
    }
}
