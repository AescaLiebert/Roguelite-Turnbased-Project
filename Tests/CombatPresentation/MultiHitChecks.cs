using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void MultiHitChecks()
    {
        // Full-formula partitioning must conserve totals even with non-divisible damage,
        // defense, pierce, flat reduction, caps, shields and each supported damage family.
        var attacker = new StatBlock { Attack = 137, PierceBp = 1700, CritChanceBp = 5000, CritDamageBp = 17500 };
        var defender = new StatBlock { Defense = 23, ResistanceBp = 1300, BlockChanceBp = 8000, BlockPowerBp = 3500 };
        foreach (DamageFamily family in Enum.GetValues(typeof(DamageFamily)))
        foreach (var roll in new[] { 0, 9999 })
        foreach (var cap in new[] { 77, int.MaxValue })
        for (var hits = 1; hits <= 10; hits++)
        {
            var packet = new DamagePacket { BaseAmount = 137, CoefficientBp = 17321,
                Policy = new DamagePolicy { Family = family, FlatReduction = 7, DamageCap = cap } };
            var single = DamageResolver.Resolve(packet, attacker, defender, 5000, 31, roll, 0);
            var hp = 5000; var shield = 31; var total = 0;
            packet.HitCount = hits;
            for (var hit = 1; hit <= hits; hit++)
            {
                packet.HitIndex = hit;
                var slice = DamageResolver.Resolve(packet, attacker, defender, hp, shield, roll, 0);
                total += slice.CalculatedDamage; hp = slice.RemainingHealth; shield = slice.RemainingShield;
            }
            Check(total == single.CalculatedDamage && hp == single.RemainingHealth && shield == single.RemainingShield,
                $"{family}: {hits} hits changed the total formula or shield accounting.");
        }

        for (var count = 1; count <= 10; count++)
        foreach (var area in new[] { false, true })
        {
            var state = Create((ulong)(4100 + count));
            var effect = new EffectDefinition { CoefficientBp = 12345,
                Attack = new DamageAttackDefinition { HitCount = count, Range = AttackRange.Long } };
            var start = state.Events.Count;
            var next = PlayKind(state, CardCategory.Attack, area ? EffectTargetScope.AllEnemies : EffectTargetScope.SelectedEnemy, effect);
            var events = next.Events.Skip(start).ToList();
            var played = events.First(e => e.Kind == BattleEventKind.CardPlayed);
            var damage = events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == played.CardId).ToList();
            Check(played.HitCount == count && played.AttackRange == AttackRange.Long, "Action lost its animation definition.");
            Check(damage.Count == count * (area ? 3 : 1), "Wrong number of independently resolved hits.");
            Check(events.Count(e => e.Kind == BattleEventKind.HitStarted) == count, "Missing animation boundaries.");
            Check(damage.Select(e => e.HitIndex).SequenceEqual(damage.Select(e => e.HitIndex).OrderBy(i => i)), "AOE must resolve hit-major.");
            foreach (var group in damage.GroupBy(e => e.TargetId))
                Check(group.Sum(e => e.Amount) == 104, "Multi-hit changed a card's unmodified total damage.");
            var before = events.FindIndex(e => e.Kind == BattleEventKind.ActionTiming && e.Timing == CardEffectTiming.Damaging);
            var after = events.FindIndex(e => e.Kind == BattleEventKind.ActionTiming && e.Timing == CardEffectTiming.AfterDamage);
            Check(before < events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied) &&
                after > events.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == played.CardId),
                "Timing windows must surround the entire hit sequence.");
            var display = state.Clone(); foreach (var item in events) BattlePlaybackState.Apply(display, item);
            EqualDisplay(display, next);
            var projected = BattleProjectionBuilder.Build(next, TeamSide.Player, "multi-hit-test");
            Check(projected.Events.Count(e => e.Kind == "HitStarted" && e.HitCount == count) == count, "Projection lost hit facts.");
        }

        var mixed = Create(4501);
        mixed.Player.Fighters[0].Stats.CritChanceBp = 5000;
        mixed.Player.Fighters[0].Stats.CritDamageBp = 20000;
        mixed.Opponent.Fighters[0].Stats.BlockChanceBp = 10000;
        mixed.Opponent.Fighters[0].Stats.BlockPowerBp = 5000;
        var multi = new EffectDefinition { Attack = new DamageAttackDefinition { HitCount = 10 } };
        var mixedNext = PlayKind(mixed, CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi);
        var rolls = mixedNext.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 10).ToList();
        Check(rolls.Count == 10 && rolls.Any(e => e.WasCritical) && rolls.Any(e => e.WasBlocked), "Crit/block was reused across the combo.");
        Check(mixedNext.RngDrawCount - mixed.RngDrawCount == (ulong)(10 + rolls.Count(e => !e.WasCritical)), "Unexpected per-hit RNG consumption.");
        var repeat = PlayKind(mixed.Clone(), CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi.Clone());
        Check(SnapshotJson.Serialize(mixedNext) == SnapshotJson.Serialize(repeat), "Multi-hit replay is nondeterministic.");

        // A death ends this target's packets without retargeting the remainder to a reserve.
        var lethal = Create(4502);
        lethal.Opponent.Fighters[0].Health = 1;
        var lethalNext = PlayKind(lethal, CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi);
        Check(lethalNext.Events.Count(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 10) == 1, "Dead target received extra damage packets.");
        Check(lethalNext.Events.Count(e => e.Kind == BattleEventKind.FighterDefeated) == 1, "Combo duplicated defeat handling.");
        var lethalDisplay = lethal.Clone();
        foreach (var item in lethalNext.Events.Skip(lethal.Events.Count)) BattlePlaybackState.Apply(lethalDisplay, item);
        EqualDisplay(lethalDisplay, lethalNext);

        // Non-damage definitions ignore attack settings, including their animation metadata.
        var support = Create(4503);
        var heal = new EffectDefinition { Kind = EffectKind.Heal, Target = EffectTargetScope.Self,
            HealCoefficientBp = 10000, Attack = new DamageAttackDefinition { HitCount = 10 } };
        support.Player.Fighters[0].Health = 50;
        var healed = PlayKind(support, CardCategory.Recovery, EffectTargetScope.Self, heal, support.Player.Fighters[0].Id);
        Check(!healed.Events.Any(e => e.Kind == BattleEventKind.HitStarted), "Utility card gained a damage sequence.");
        Check(healed.Events.Last(e => e.Kind == BattleEventKind.CardPlayed).HitCount == 0, "Utility card gained attack animation metadata.");

        var timed = Create(4504);
        var timedEffect = multi.Clone();
        foreach (CardEffectTiming timing in Enum.GetValues(typeof(CardEffectTiming)))
            timedEffect.Sequence.Add(new CardEffectStep { Timing = timing, Effect = new EffectDefinition {
                Kind = EffectKind.ApplyStatus, Target = EffectTargetScope.SelectedEnemy,
                StatusRecipe = new StatusRecipeDefinition { Id = "multihit." + timing, DefaultDuration = 5,
                    Polarity = StatusPolarity.Debuff } } });
        var timedNext = PlayKind(timed, CardCategory.AttackDebuff, EffectTargetScope.SelectedEnemy, timedEffect);
        var timedEvents = timedNext.Events.Skip(timed.Events.Count).ToList();
        foreach (CardEffectTiming timing in Enum.GetValues(typeof(CardEffectTiming)))
            Check(timedEvents.Count(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == "multihit." + timing) == 1,
                timing + " effect repeated per hit.");
        Check(timedEvents.FindIndex(e => e.StatusRecipeId == "multihit.Damaging") < timedEvents.FindIndex(e => e.Kind == BattleEventKind.DamageApplied), "First-hit status ran late.");
        Check(timedEvents.FindIndex(e => e.StatusRecipeId == "multihit.AfterDamage") > timedEvents.FindLastIndex(e => e.Kind == BattleEventKind.DamageApplied), "Last-hit status ran early.");

        var counterState = Create(4505);
        var counterOwner = counterState.Opponent.Fighters[0];
        var counterRecipe = new StatusRecipeDefinition { Id = "multihit.counter", Behavior = StatusBehavior.Stance,
            Polarity = StatusPolarity.Buff, DefaultDuration = 5, CounterEnabled = true,
            CounterEffect = new CounterEffectDefinition { Attack = new DamageAttackDefinition { HitCount = 3, Range = AttackRange.Long } } };
        Check(StatusSystem.Apply(counterOwner, counterOwner.Id, counterOwner.Side, counterRecipe, "multi-counter", "fixture", 1).Accepted, "Counter fixture rejected.");
        var counterNext = PlayKind(counterState, CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi);
        var counterStart = counterNext.Events.Single(e => e.Kind == BattleEventKind.CounterStarted);
        Check(counterStart.HitCount == 3 && counterStart.AttackRange == AttackRange.Long, "Counter lost its sequence definition.");
        Check(counterNext.Events.Count(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == counterStart.CardId) == 3,
            "Counter must trigger once per action and resolve its own three hits.");
        var counterDisplay = counterState.Clone();
        foreach (var item in counterNext.Events.Skip(counterState.Events.Count)) BattlePlaybackState.Apply(counterDisplay, item);
        EqualDisplay(counterDisplay, counterNext);

        var areaDeath = Create(4506);
        areaDeath.Opponent.Fighters[0].Health = 1;
        var areaNext = PlayKind(areaDeath, CardCategory.Attack, EffectTargetScope.AllEnemies, multi);
        Check(areaNext.Events.Count(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 10) == 21,
            "AOE death stopped surviving recipients or kept hitting the dead recipient.");

        var evade = Create(4507);
        var evader = evade.Opponent.Fighters[0];
        Check(StatusSystem.Apply(evader, evader.Id, evader.Side, new StatusRecipeDefinition {
            Id = "multihit.evade", EvadeAttacks = true, DefaultDuration = 5, Polarity = StatusPolarity.Buff },
            "evade", "fixture", 1).Accepted, "Evade fixture rejected.");
        var evaded = PlayKind(evade, CardCategory.Attack, EffectTargetScope.SelectedEnemy, multi);
        Check(evaded.Events.Count(e => e.Kind == BattleEventKind.AttackEvaded && e.HitCount == 10) == 10 &&
            !evaded.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 10), "Evade was not resolved per hit.");

        foreach (var value in new[] { -1, 0, 1, 10, 11, int.MaxValue })
            Check(new EffectDefinition { Attack = new DamageAttackDefinition { HitCount = value } }.DamageHitCount == Math.Max(1, Math.Min(10, value)), "Hit count clamp failed.");
        Check(new EffectDefinition { Attack = null }.DamageHitCount == 1, "Legacy/null effect did not default to one hit.");
        var clone = multi.Clone(); clone.Attack.HitCount = 2;
        Check(multi.Attack.HitCount == 10, "Effect clone shares attack data.");
        var roundTrip = SnapshotJson.Deserialize<EffectDefinition>(SnapshotJson.Serialize(multi));
        Check(roundTrip.DamageHitCount == 10, "Snapshot lost hit count.");
        Check(DamageAnimationTemplates.All.Count == 16, "Animation bundle must contain sixteen presets.");
        for (var i = 0; i < 16; i++)
        {
            var preset = DamageAnimationTemplates.All[i];
            Check(DamageAnimationTemplates.Index(preset.Hits, preset.Range, preset.Area) == i, "Preset is not selectable.");
        }
        Console.WriteLine("PASS: multi-hit formula conservation, 1–10 hits, AOE ordering, independent crit/block, replay, death, support isolation and 16 presets.");
    }
}
