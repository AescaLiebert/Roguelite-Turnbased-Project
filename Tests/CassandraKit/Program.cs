using System;
using System.IO;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Combat;

var cassandra = SnapshotJson.Deserialize<CharacterDefinition>(File.ReadAllText(args[0]));
void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
Check(PassiveRuleValidator.Validate(cassandra.Passive).Count == 0, "Passive validation");
Check(cassandra.Skills.Count == 2 && cassandra.Skills.All(s => s.Ranks.Count == 3), "Skill ranks");
Check(cassandra.UltimateTiers.Count == 6, "Ultimate tiers");
foreach (var effect in cassandra.Skills.SelectMany(s => s.Ranks.Select(r => r.Effect)).Concat(cassandra.UltimateTiers.Select(t => t.Effect)))
    Check(effect.CoefficientBp == 30000, "300% coefficient at every tier");

CharacterDefinition Enemy(string id) { var e = cassandra.Clone(); e.Id = id; e.Passive = null;
    e.BaseStats.MaxHealth = 1000000; e.BaseStats.Defense = 0; e.BaseStats.CritResistanceBp = 0;
    e.BaseStats.BlockChanceBp = 0; return e; }
BattleState Start() { var c = cassandra.Clone(); c.BaseStats.CritChanceBp = 0; c.BaseStats.PierceBp = 0;
    c.BaseStats.LifeStealBp = 0; return BattleEngine.Create("cassandra", new[]{c}, null,
        new[]{Enemy("e1"),Enemy("e2"),Enemy("e3")}, null, 75, TeamSide.Player); }
BattleState Play(BattleState state, int slot) {
    var card = state.Player.Hand.First(c => c.SkillId == cassandra.Skills[slot - 1].Id);
    var plan = new TurnPlan { RequestId = Guid.NewGuid().ToString(), ExpectedRevision = state.Revision };
    plan.Actions.Add(new PlannedAction { CardId = card.Id, TargetFighterId = state.Opponent.Fighters[0].Id });
    Check(BattleEngine.TryResolvePlan(state, plan, out var next, out var error), error); return next;
}
var initial = Start(); var owner = initial.Player.Fighters[0];
Check(StatusSystem.GetEffectiveStats(owner).MaxHealth == cassandra.BaseStats.MaxHealth * 110 / 100, "HP aura");
Check(StatusSystem.GetEffectiveStats(owner).CritResistanceBp == cassandra.BaseStats.CritResistanceBp + 1000, "Crit resistance aura");
var first = Play(initial, 1); owner = first.Player.Fighters[0];
var calculation = new AttackEffectCalculation { Attacker = StatusSystem.GetEffectiveStats(initial.Player.Fighters[0]), Policy = new DamagePolicy() };
CharacterPassiveRuntime.ApplyBeforeDamageReactions(initial, initial.Player.Fighters[0], initial.Opponent.Fighters[0], DamageFamily.Normal, initial.Player.Hand[0], calculation);
Check(calculation.Attacker.Attack == cassandra.BaseStats.Attack * 112 / 100, "12% ATK applies during attack calculation");
Check(StatusSystem.GetEffectiveStats(initial.Player.Fighters[0]).Attack == cassandra.BaseStats.Attack, "12% does not mutate fighter ATK");
var buff = owner.Statuses.Instances.Single(s => s.RecipeId == "status.buff.cassandra.defense-attack");
Check(buff.PotencyBp == cassandra.BaseStats.Defense, "Current DEF snapshot");
Check(StatusSystem.GetEffectiveStats(owner).Attack == cassandra.BaseStats.Attack + cassandra.BaseStats.Defense / 2, "DEF-to-ATK; no lasting 12%");
Check(first.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.cassandra.electrified"), "Electrified target");
Check(first.Opponent.Fighters.Skip(1).All(f => f.Health == f.Stats.MaxHealth), "Single-target isolation");
Check(StatusSystem.BuildDamagePolicy(owner, first.Opponent.Fighters[0], DamageFamily.Normal).IncomingIncreaseBp == 4000, "40% vulnerability");
var second = Play(Start(), 2); owner = second.Player.Fighters[0];
Check(second.Opponent.Fighters.All(f => f.Health < f.Stats.MaxHealth), "AOE hits all enemies");
Check(StatusSystem.GetEffectiveStats(owner).Defense == cassandra.BaseStats.Defense * 160 / 100, "60% DEF");
Check(owner.Statuses.Instances.Single(s => s.RecipeId == "status.buff.cassandra.defense-attack").PotencyBp == cassandra.BaseStats.Defense * 160 / 100, "Passive snapshots boosted DEF after skill 2");
var repeated = Start();
var repeatPlan = new TurnPlan { RequestId = "repeat", ExpectedRevision = repeated.Revision };
foreach (int slot in new[]{2,1}) repeatPlan.Actions.Add(new PlannedAction {
    CardId = repeated.Player.Hand.First(c => c.SkillId == cassandra.Skills[slot - 1].Id).Id,
    TargetFighterId = repeated.Opponent.Fighters[0].Id });
Check(BattleEngine.TryResolvePlan(repeated, repeatPlan, out repeated, out var repeatError), repeatError);
Check(repeated.Player.Fighters[0].Statuses.Instances.Count(s => s.RecipeId == "status.buff.cassandra.defense-attack") == 1, "Buff refreshes without stacking");
Check(StatusSystem.GetEffectiveStats(repeated.Player.Fighters[0]).Attack == cassandra.BaseStats.Attack + cassandra.BaseStats.Defense * 160 / 100 / 2, "Repeated attacks do not accumulate 12% ATK");
for (int turn = 0; turn < 3; turn++) {
    var pass = new TurnPlan { RequestId = "pass-" + turn, ExpectedRevision = repeated.Revision };
    Check(BattleEngine.TryResolvePlan(repeated, pass, out repeated, out var passError), passError);
}
Check(!repeated.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.buff.cassandra.defense-attack"), "DEF conversion expires");
for (int tier = 0; tier <= 5; tier++) {
    var ult = Start(); var u = ult.Player.Fighters[0]; u.ConstellationTier = tier; u.PowerGauge = 5;
    ult.Player.Hand.Clear(); ult.Player.Hand.Add(new CardState { Id = "ult", OwnerFighterId = u.Id,
        Kind = CardKind.Ultimate, UltimateTier = tier, Category = CardCategory.Ultimate,
        EffectCategory = CardCategory.AttackDebuff, TargetScope = EffectTargetScope.AllEnemies, Rank = 1 });
    var plan = new TurnPlan { RequestId = "ult-" + tier, ExpectedRevision = ult.Revision };
    plan.Actions.Add(new PlannedAction { CardId = "ult", TargetFighterId = ult.Opponent.Fighters[0].Id });
    Check(BattleEngine.TryResolvePlan(ult, plan, out ult, out var error), error);
    var buffIndex = ult.Events.FindIndex(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == "status.buff.cassandra.ultimate");
    var damageIndex = ult.Events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied);
    Check(buffIndex >= 0 && damageIndex > buffIndex, "Ultimate buff applies before damage at C" + tier);
    Check(StatusSystem.BuildDamagePolicy(ult.Player.Fighters[0], ult.Opponent.Fighters[0], DamageFamily.Normal).OutgoingIncreaseBp == 5000, "50% outgoing bonus");
    Check(ult.Opponent.Fighters.All(f => f.Statuses.Instances.Any(s => s.Recipe.DisableMask == CardCategoryMask.Debuff)), "AOE Debuff seal at C" + tier);
    Check(StatusSystem.BuildDamagePolicy(ult.Opponent.Fighters[0], ult.Player.Fighters[0], DamageFamily.Normal).IncomingDecreaseBp == 5000, "50% incoming reduction");
}
Console.WriteLine("PASS: Cassandra ranks, C0-C5 Ultimates, attack-only 12%, DEF snapshot and refresh, expiration, targeting, vulnerability, buff timing, reduction and Debuff seal.");
