using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using Newtonsoft.Json;

internal static partial class Program
{
    private static void IntegrationChecks()
    {
        var state = Start(); var owner = state.Player.Fighters[0];
        var screen = BattleEntitySystem.CreateOrReplace(state, owner, Definition("cap-screen", true));
        owner.Shield = 1000;
        owner.Definition.Passive.Policies.Add(new CombatPolicyDefinition { Id = "fixture.cap",
            Kind = CombatPolicyKind.IncomingDamageCap, MaximumHealthDamageBp = 1000 });
        var damage = Contact(state, state.Opponent.Fighters[0], owner, 5000);
        Check(damage.FormulaDamage == 5000 && damage.ConstructAbsorbed == 2000 && damage.ShieldLost == 1000 &&
            damage.HealthLost == 1000 && damage.CalculatedDamage == 4000,
            "Incoming HP cap applies only to the construct/personal-shield remainder and preserves immutable formula damage.");

        state = Start(); owner = state.Player.Fighters[0];
        screen = BattleEntitySystem.CreateOrReplace(state, owner, Definition("practice-screen", true));
        owner.CannotDie = true;
        damage = Contact(state, state.Opponent.Fighters[0], owner, 100);
        Check(screen.Capacity == 1900 && damage.ConstructAbsorbed == 100 && owner.Health == owner.Stats.MaxHealth,
            "Practice invulnerability preserves HP while still exercising construct capacity.");

        state = Start(); owner = state.Player.Fighters[0];
        var aura = Definition("pending-aura");
        aura.Auras.Add(new PassiveAuraDefinition { Id = "fixture.aura", Targets = new PassiveTargetFilter {
            Relation = PassiveRelation.Self }, Modifiers = new List<StatModifierDefinition> {
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.Attack,
                    Operation = ModifierOperation.PercentOfBase, Amount = 2000 } } });
        BattleEntitySystem.CreateOrReplace(state, owner, aura);
        Check(StatusSystem.GetEffectiveStats(owner).Attack == 120, "Entity aura is active before lethal damage.");
        Contact(state, state.Opponent.Fighters[0], owner, owner.Health + 1);
        Check(owner.IsAlive && owner.Health == 0 && StatusSystem.GetEffectiveStats(owner).Attack == 100,
            "Pending defeat suppresses cached entity auras before final fighter defeat.");
        BattleEntitySystem.Reconcile(state);
        Check(state.Entities.Count == 1 && !BattleEntitySystem.IsAvailable(state, state.Entities[0]),
            "Pending defeat does not finalize ownership death early.");

        foreach (var family in new[] { DamageFamily.Normal, DamageFamily.True, DamageFamily.Additional,
            DamageFamily.DamageOverTime })
        {
            var attacker = Character("packet-" + family); var defender = Kit("ningguang");
            attacker.Skills[0].Ranks[0].Effect = new EffectDefinition { Id = "fixture.packet",
                Kind = EffectKind.Damage, Scaling = StatScaling.Fixed, Magnitude = 2500,
                CoefficientBp = 10000, Family = family };
            state = BattleEngine.Create("damage-origin-" + family, new[] { attacker }, null,
                new[] { defender }, null, 123, TeamSide.Player);
            screen = state.Entities.Single(); var capacity = screen.Capacity;
            var next = Play(state, 1);
            var hit = next.Events.First(e => e.Kind == BattleEventKind.DamageApplied);
            Check(hit.ConstructAbsorbed == Math.Min(capacity, hit.FormulaDamage),
                "Real card damage commit transports construct accounting for " + family);
        }

        var elementalAttacker = Character("electro-attacker");
        elementalAttacker.Skills[0].Ranks[0].Effect.DamageElement = ElementId.Electro;
        var reactionDefender = Kit("ningguang"); reactionDefender.BaseStats.MaxHealth = 100000;
        state = BattleEngine.Create("reaction-interception", new[] { elementalAttacker }, null,
            new[] { reactionDefender }, null, 123);
        state.Opponent.Fighters[0].ElementStatuses.Apply(ElementId.Pyro, state.Player.Fighters[0].Id, "reaction-pyro");
        var afterReaction = Play(state, 1);
        Check(afterReaction.Events.Any(e => e.Kind == BattleEventKind.DamageApplied &&
            e.DamageOrigin == ElementalDamageOrigin.Reaction && e.ConstructAbsorbed > 0),
            "Generated reaction output commits through construct interception.");

        var dotSource = Character("dot-source");
        var ningguang = Kit("ningguang");
        state = BattleEngine.Create("dot-interception", new[] { ningguang }, null, new[] { dotSource }, null, 15);
        owner = state.Player.Fighters[0]; var dot = new StatusRecipeDefinition { Id = "fixture.periodic-damage",
            Polarity = StatusPolarity.Debuff, Behavior = StatusBehavior.DamageOverTime, DefaultDuration = 3,
            PeriodicDamage = new PeriodicDamageDefinition { Timing = StatusTickTiming.TargetTurnEnd,
                Scaling = StatusSnapshotScaling.SourceAttack, CoefficientBp = 10000, Family = DamageFamily.DamageOverTime } };
        StatusSystem.Apply(owner, state.Opponent.Fighters[0].Id, TeamSide.Opponent, dot, "fixture.dot", "fixture", 1,
            snapshot: new StatusSnapshot { SourceAttack = 300, TriggeringHealthDamage = 300 }, duration: 3);
        var afterDot = Pass(state);
        Check(afterDot.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.DamageOverTime &&
            e.ConstructAbsorbed > 0), "Real turn-end DOT commit uses the same live construct pool.");

        var reflector = Character("reflector", 100000); reflector.BaseStats.ReflectDamageBp = 10000;
        state = BattleEngine.Create("reflection-interception", new[] { Kit("ningguang") }, null, new[] { reflector }, null, 91);
        var reflected = Play(state, 1);
        Check(reflected.Events.Any(e => e.Kind == BattleEventKind.DamageApplied &&
            e.DamageOrigin == ElementalDamageOrigin.Reflection && e.ConstructAbsorbed > 0 && e.HealthLost == 0),
            "Manual reflection commit preserves construct absorption and actual zero HP loss.");

        state = Start(Kit("xiangling")); owner = state.Player.Fighters[0];
        BattleEntitySystem.CreateOrReplace(state, owner, GenshinSummonKits.CreateGuoba());
        StatusSystem.Apply(owner, state.Opponent.Fighters[0].Id, TeamSide.Opponent, dot, "lethal-dot", "fixture", 1,
            snapshot: new StatusSnapshot { SourceAttack = 1000000 }, duration: 3);
        var defeated = Pass(state);
        Check(defeated.Entities.Count == 0 && !defeated.Events.Any(e => e.Kind == BattleEventKind.EntityActionStarted) &&
            defeated.Events.Any(e => e.Kind == BattleEventKind.EntityRemoved &&
                e.EntityRemovalCause == BattleEntityRemovalCause.OwnerDefeated),
            "Turn-end DOT owner defeat removes Guoba before any autonomous action can run.");

        state = Start(Kit("xiangling"));
        var first = Play(state, 2);
        Check(first.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId.EndsWith("skill2-team-attack")),
            "Guoba cast creates an independent team Attack buff.");
        var removedGuoba = first.Clone();
        BattleEntitySystem.Remove(removedGuoba, removedGuoba.Entities.Single().Id, BattleEntityRemovalCause.ScriptedLifecycle);
        Check(StatusSystem.GetEffectiveStats(removedGuoba.Player.Fighters[0]).Attack == 112 &&
            StatusSystem.GetEffectiveStats(removedGuoba.Player.Fighters[0]).DamageOverTimeDealtBp == 0,
            "Removing Guoba removes entity-owned buffs but preserves the separate skill Attack buff.");
        PersistedKits();
    }

    private static void PersistedKits()
    {
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "Assets/Project/Data/Character/Pre-Release_Phase/PR_Genshin-Impact");
        foreach (var slug in new[] { "fischl", "xiangling", "ningguang" })
        {
            var character = ReadAsset<CharacterDefinition>(Path.Combine(folder, slug + ".asset"), "studioMetadataJson");
            character.Skills = new List<SkillDefinition> {
                ReadAsset<SkillDefinition>(Path.Combine(folder, slug + "_skilldata1.asset"), "studioDefinitionJson"),
                ReadAsset<SkillDefinition>(Path.Combine(folder, slug + "_skilldata2.asset"), "studioDefinitionJson") };
            character.UltimateTiers = ReadAsset<List<UltimateTierDefinition>>(Path.Combine(folder, slug + "_ultdata1.asset"), "studioDefinitionJson");
            character.Passive = ReadAsset<PassiveDefinition>(Path.Combine(folder, slug + "_Passive.asset"), "studioDefinitionJson");
            var compiled = StudioCompiler.Compile(StudioDocumentService.ImportCharacter(character));
            Check(compiled.Success, "Persisted " + slug + " Studio package compiles: " + string.Join(";", compiled.Diagnostics));
            Check(character.Skills.Count == 2 && character.UltimateTiers.Count == 6 &&
                character.Skills.All(skill => skill.Ranks.Count == 3), "Persisted source kit retains rank/tier tables: " + slug);
            if (slug == "ningguang")
            {
                var started = Start(character);
                Check(started.Entities.Single().Definition.Id == GenshinSummonKits.JadeScreenId,
                    "Persisted Ningguang package actually spawns opening Jade Screen.");
            }
            else
            {
                var result = Play(Start(character), 2);
                Check(result.Entities.Single().Definition.Id == (slug == "fischl" ? GenshinSummonKits.OzId : GenshinSummonKits.GuobaId),
                    "Persisted " + slug + " skill resolves its entity rather than a legacy marker.");
            }
        }
    }

    private static T ReadAsset<T>(string path, string field)
    {
        var match = Regex.Match(File.ReadAllText(path), "(?m)^  " + Regex.Escape(field) + ": (.*)$");
        if (!match.Success) throw new InvalidDataException("Missing migrated scalar " + field + " in " + path);
        return SnapshotJson.Deserialize<T>(JsonConvert.DeserializeObject<string>(match.Groups[1].Value.Trim()));
    }
}
