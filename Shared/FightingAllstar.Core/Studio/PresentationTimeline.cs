using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;

namespace FightingAllstar.Core.Studio
{
    /// <summary>Pure presentation clock. Committed event order and contact identities are immutable.</summary>
    public sealed class PresentationTimeline
    {
        public sealed class Entry
        {
            public float Time { get; internal set; }
            public string ContactId { get; internal set; }
            public BattleEvent Fact => fact.Clone();
            internal BattleEvent fact;
        }
        private readonly List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries.AsReadOnly();
        public float Duration { get; private set; }
        public static string ContactId(int ordinal) => ordinal > 0 ? "contact:" + ordinal : null;

        // Starting presentation timings, in seconds; they never enter combat resolution.
        public static PresentationTimeline Build(IEnumerable<BattleEvent> committed, float windup = .2f,
            float action = 1.2f, float recovery = .3f, IReadOnlyDictionary<int, float> contacts = null)
        {
            if (!Finite(windup) || !Finite(action) || !Finite(recovery) || windup < 0 || action <= 0 || recovery < 0)
                throw new ArgumentException("Presentation durations must be finite and nonnegative, with a positive action duration.");
            var result = new PresentationTimeline();
            var start = 0f; var currentStart = 0f; var previous = 0f; var haveAction = false;
            foreach (var original in committed ?? Enumerable.Empty<BattleEvent>())
            {
                if (original == null) continue;
                var fact = original.Clone();
                if (fact.Kind == BattleEventKind.CardPlayed)
                {
                    if (haveAction) start = Math.Max(start, currentStart + windup + action + recovery);
                    currentStart = start; previous = start; haveAction = true;
                }
                var time = previous;
                var contact = ContactId(fact.HitIndex);
                if (fact.Kind == BattleEventKind.DamageApplied && fact.HitIndex > 0 && fact.HitCount > 0)
                {
                    var fraction = (float)fact.HitIndex / fact.HitCount;
                    if (contacts != null && contacts.TryGetValue(fact.HitIndex, out var authored))
                    {
                        if (!Finite(authored) || authored < 0 || authored > 1) throw new ArgumentException("Contact offsets must be in 0..1.");
                        fraction = authored;
                    }
                    // A visual marker cannot reorder an authoritative contact or an effect preceding it.
                    time = Math.Max(previous, currentStart + windup + action * fraction);
                }
                else if (fact.Kind == BattleEventKind.ActionCompleted)
                    time = Math.Max(previous, currentStart + windup + action);
                else if (!haveAction && fact.Kind != BattleEventKind.BattleStarted) time = previous + .1f;
                result.entries.Add(new Entry { Time = time, ContactId = contact, fact = fact }); previous = time;
                start = Math.Max(start, time);
            }
            var lastImpact=result.entries.Where(e=>e.fact.Kind==BattleEventKind.DamageApplied || e.fact.Kind==BattleEventKind.FighterDefeated).Select(e=>e.Time).DefaultIfEmpty(-.5f).Max();
            result.Duration = Math.Max(lastImpact+.5f,Math.Max(.1f, Math.Max(previous, haveAction ? currentStart + windup + action : 0) + recovery));
            return result;
        }
        public Dictionary<string, int> HealthAt(float time)
        {
            var health = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
                if (entry.Time <= time && !string.IsNullOrEmpty(entry.fact.TargetId) &&
                    (entry.fact.Kind == BattleEventKind.DamageApplied || entry.fact.Kind == BattleEventKind.HealApplied))
                    health[entry.fact.TargetId] = entry.fact.HealthAfter;
            return health;
        }
        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    }
}
