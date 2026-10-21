using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class Program
{
    private static CharacterDefinition Kit(string name)
    {
        var character = Character("fighter.pre-release." + name);
        GenshinSummonKits.Upgrade(character);
        return character;
    }

    private static void KitChecks()
    {
        foreach (var name in new[] { "fischl", "xiangling", "ningguang" })
        {
            var kit = Kit(name); var initial = Json(kit); GenshinSummonKits.Upgrade(kit);
            Check(Json(kit) == initial, "Kit upgrade must be repeatable without adding duplicate rules: " + name);
            Check(PassiveRuleValidator.Validate(kit.Passive).Count == 0, "Migrated passive must validate: " + name);
            Check(kit.Skills.Count == 2 && kit.Skills.All(s => s.Ranks.Count == 3) && kit.UltimateTiers.Count == 6,
                "Kit retains complete rank/constellation tables: " + name);
            foreach (var rank in kit.Skills.Single(s => s.Slot == 1).Ranks)
                Check(rank.Effect.CoefficientBp == (name == "xiangling" ? 50000 : 45000),
                    "Source first skill coefficient at every rank: " + name + " " + rank.Rank);
        }
        var oz = GenshinSummonKits.CreateOz();
        Check(oz.Duration == 2 && oz.TurnEndAttack.Element == ElementId.Electro && oz.TurnEndAttack.AttackCoefficientBp == 25000 &&
            oz.ActionTarget == EffectTargetScope.AllEnemies && oz.ReactionFollowUp.AttackCoefficientBp == 8000,
            "Oz must include two owner turns, 250% Electro and 80% reaction follow-up with approved AoE targeting.");
        var guoba = GenshinSummonKits.CreateGuoba();
        Check(guoba.Duration == 2 && guoba.TurnEndAttack.Element == ElementId.Pyro && guoba.TurnEndAttack.AttackCoefficientBp == 30000 &&
            guoba.AttackCoefficientPerDotDealtBp == 8000 && guoba.TurnEndAttack.StatusStackCount == 2 &&
            guoba.TurnEndAttack.StatusDuration == 3 && guoba.ActionTarget == EffectTargetScope.AllEnemies,
            "Guoba source values must include AoE Pyro, DOT scaling and two Ignite stacks.");
        Check(guoba.OnHitStatuses.Any(s => s.DefaultDuration == 2 && s.ElementalModifiers.Any(m =>
            m.Element == ElementId.Pyro && m.ResistanceBp == -2000)), "Guoba must retain Pyro resistance rider.");
        var screen = GenshinSummonKits.CreateJadeScreen();
        Check(screen.Category == BattleEntityCategory.GeoConstruct && screen.CapacityCoefficientBp == 2000 &&
            screen.CapacitySourceStat == StatId.MaxHealth && screen.Duration == 2 && screen.DepletedStatus.DefaultDuration == 2 &&
            screen.DepletedStatus.ElementalModifiers.Any(m => m.Element == ElementId.Geo && m.DamageDealtBp == 1200),
            "Jade Screen source values must define 20% owner max HP capacity and two-turn 12% Geo depletion reward.");
        Check(GenshinSummonKits.CreateStarJade().MaxStacks == 10 && GenshinSummonKits.CreateStarJade().ElementalModifiers.Any(m =>
            m.Element == ElementId.Geo && m.DamageDealtBp == 300), "Star Jade stores at most ten 3% Geo layers.");

        for (var rank = 1; rank <= 3; rank++)
        {
            var fischl = Play(Start(Kit("fischl")), 2, rank);
            Check(fischl.Entities.Single().Definition.Id == GenshinSummonKits.OzId &&
                fischl.Opponent.Fighters.All(f => f.Health < f.Stats.MaxHealth),
                "Fischl skill 2 must summon autonomous AoE Oz at rank " + rank);
            Check(fischl.Player.Fighters[0].Statuses.Instances.Any(s => s.Recipe.EvadeAttacks),
                "Fischl skill 2 must retain Evasion after its applying turn at rank " + rank);
            var xiangling = Play(Start(Kit("xiangling"), allies: 2), 2, rank);
            var owner = xiangling.Player.Fighters[0];
            Check(xiangling.Entities.Single().Definition.Id == GenshinSummonKits.GuobaId &&
                xiangling.Opponent.Fighters.All(f => f.Health < f.Stats.MaxHealth),
                "Xiangling skill 2 must summon AoE Guoba at rank " + rank);
            Check(StatusSystem.GetEffectiveStats(xiangling.Player.Fighters[1]).Attack == 112 &&
                StatusSystem.GetEffectiveStats(owner).DamageOverTimeDealtBp == 2000,
                "Living Guoba provides source team ATK and owner DOT auras.");
            Check(xiangling.Opponent.Fighters.All(f => f.Statuses.Instances.Count(s => s.RecipeId == "status.debuff.ignite") == 2 &&
                f.Statuses.Instances.Any(s => s.Recipe.ElementalModifiers.Any(m => m.Element == ElementId.Pyro && m.ResistanceBp == -2000))),
                "Guoba attacks must apply Ignite and Pyro resistance reduction to every hit enemy.");
            var ended = Pass(Pass(xiangling));
            Check(ended.Entities.Count == 0 && ended.Player.Fighters.All(f => f.PassiveContributions.All(c =>
                !c.RuleId.Contains(":entity:"))) &&
                StatusSystem.GetEffectiveStats(ended.Player.Fighters[0]).DamageOverTimeDealtBp == 0,
                "Guoba entity auras leave no persistent contribution after expiry; independent cast buff retains its own lifetime.");
        }
        {
            var state = Start(Kit("ningguang"), allies: 2); var owner = state.Player.Fighters[0];
            owner.Definition.TraitIds.Add("trait.genshin-geo");
            state.Player.Fighters[1].Definition.TraitIds.Add("trait.genshin-pyro");
            Check(state.Entities.Count == 1 && state.Entities[0].Definition.Id == GenshinSummonKits.JadeScreenId,
                "Ningguang creates Jade Screen on battle start without a card. Entities=" + state.Entities.Count +
                "; facts=" + string.Join("|", state.Events.Select(e => e.Kind + ":" + e.Message)));
            var initialCapacity = state.Entities[0].Capacity;
            Check(initialCapacity == StatusSystem.GetEffectiveStats(owner).MaxHealth * 2000 / 10000,
                "Opening screen snapshots its owner's effective max HP.");
            Contact(state, state.Opponent.Fighters[0], state.Player.Fighters[1], initialCapacity);
            Check(state.Entities.Count == 0 && owner.Statuses.Instances.Any(s => s.RecipeId == screen.DepletedStatus.Id) &&
                !state.Player.Fighters[1].Statuses.Instances.Any(s => s.RecipeId == screen.DepletedStatus.Id),
                "Depletion rewards living Geo allies only, once; non-Geo allies still receive protection.");
            var expiry = Start(Kit("ningguang")); expiry = Pass(Pass(Pass(expiry)));
            Check(expiry.Entities.Count == 0 && !expiry.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == screen.DepletedStatus.Id),
                "Natural screen expiry must not award capacity-depletion reward.");
            var removed = Start(Kit("ningguang"));
            BattleEntitySystem.Remove(removed, removed.Entities[0].Id, BattleEntityRemovalCause.Replaced);
            Check(!removed.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == screen.DepletedStatus.Id),
                "Replacement must not award capacity-depletion reward.");
        }
        {
            var character = Kit("ningguang"); character.BaseStats.DamageOverTimeDealtBp = 10000;
            var state = Start(character); var owner = state.Player.Fighters[0];
            StatusSystem.Apply(owner, owner.Id, owner.Side, GenshinSummonKits.CreateStarJade(), "star-jade",
                "fixture", 1, stackCount: 3);
            var result = Play(state, 2);
            Check(!result.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == GenshinSummonKits.StarJadeRecipeId),
                "Ningguang skill 2 consumes its snapshots and newly earned Geo layer after action.");
            var another = Play(Start(Kit("ningguang")), 1);
            Check(another.Player.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == GenshinSummonKits.StarJadeRecipeId && s.StackCount == 1),
                "Only resolved Geo damage grants Star Jade.");
        }
        for (var tier = 0; tier <= 5; tier++)
        {
            foreach (var name in new[] { "fischl", "xiangling", "ningguang" })
            {
                var kit = Kit(name); var ultimate = kit.UltimateTiers[tier].Effect;
                Check(ultimate.CoefficientBp == (name == "fischl" ? 40000 : name == "xiangling" ? 20000 : 50000),
                    "Ultimate source base coefficient " + name + " C" + tier);
                var state = Start(kit); var owner = state.Player.Fighters[0];
                owner.ConstellationTier = tier; owner.PowerGauge = CardRules.UltimateGaugeCost;
                var card = new CardState { Id = "ultimate", OwnerFighterId = owner.Id, Kind = CardKind.Ultimate,
                    Rank = 1, UltimateTier = tier, Category = CardCategory.Ultimate,
                    EffectCategory = CardCategory.Attack, TargetScope = EffectTargetScope.SelectedEnemy };
                state.Player.Hand.Clear(); state.Player.Hand.Add(card);
                Check(BattleEngine.TryResolvePlan(state, new TurnPlan { ExpectedRevision = state.Revision, RequestId = "ultimate",
                    Actions = new List<PlannedAction> { new PlannedAction { CardId = card.Id,
                        TargetFighterId = state.Opponent.Fighters[0].Id } } }, out var next, out var error), error ?? "Ultimate rejected.");
                if (name == "fischl") Check(next.Entities.Single().Definition.Id == GenshinSummonKits.OzId,
                    "Fischl Ultimate creates Oz at C" + tier);
                else if (name == "xiangling")
                {
                    var pyronado = next.Entities.Single();
                    Check(pyronado.Definition.Id == GenshinSummonKits.PyronadoId && pyronado.Definition.ActOnBothSidesTurnEnd &&
                        pyronado.Definition.Duration == (tier == 5 ? 3 : 2) &&
                        pyronado.Definition.TurnEndAttack.AttackCoefficientBp == (tier == 5 ? 25000 : 15000),
                        "Xiangling Ultimate retains maximum-level Pyronado duration/damage at C" + tier);
                }
                else
                {
                    var jade = next.Player.Fighters[0].Statuses.Instances.Single(s => s.RecipeId == GenshinSummonKits.StarJadeRecipeId);
                    Check(jade.StackCount == (tier == 5 ? 10 : 2), "Ningguang grants pre-action Star Jade plus Geo-hit layer at C" + tier);
                    var applied = next.Events.FindIndex(e => e.Kind == BattleEventKind.StatusApplied && e.StatusRecipeId == GenshinSummonKits.StarJadeRecipeId);
                    var damage = next.Events.FindIndex(e => e.Kind == BattleEventKind.DamageApplied && e.SourceId == owner.Id);
                    Check(applied >= 0 && damage > applied, "Ningguang Ultimate layers apply before damage at C" + tier);
                }
            }
        }
        ReactionFollowUp();
    }

    private static void ReactionFollowUp()
    {
        var state = Start(Kit("fischl"));
        var owner = state.Player.Fighters[0]; var target = state.Opponent.Fighters[0];
        var entity = BattleEntitySystem.CreateOrReplace(state, owner, GenshinSummonKits.CreateOz());
        target.ElementStatuses.Apply(ElementId.Pyro, owner.Id, "fixture-pyro");
        var next = Play(state, 1);
        Check(next.Events.Any(e => e.Kind == BattleEventKind.ElementalReactionTriggered), "Fixture must produce a real elemental reaction.");
        Check(next.Events.Any(e => e.Kind == BattleEventKind.EntityActionStarted && e.EntityId == entity.Id &&
            e.RootActionId.Contains(":followup:")), "Oz reacts to ally elemental reaction through a separate bounded action.");
        Check(next.Events.Count(e => e.Kind == BattleEventKind.EntityActionStarted && e.EntityId == entity.Id &&
            e.RootActionId.Contains(":followup:")) <= next.Events.Count(e => e.Kind == BattleEventKind.ElementalReactionTriggered),
            "Oz cannot recursively multiply its own reaction follow-ups.");
    }
}
