using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using System.Linq;

internal static partial class BattlePlaybackChecks
{
    private static void InspectorPassiveChecks()
    {
        var goroAsset = System.IO.File.ReadAllText("Assets/Project/Data/Character/goro94_Passive.asset");
        int GoroValue(string field) => int.Parse(System.Text.RegularExpressions.Regex.Match(
            goroAsset, @"(?m)^\s*(?:- )?" + field + @":\s*(-?\d+)").Groups[1].Value);
        foreach (var mode in new[] { BattleModeMask.PvE, BattleModeMask.PvP })
        {
            var goro = Fighter("fighter.goro94", 10000);
            goro.Passive = new PassiveDefinition { Id = "goro.passive" };
            goro.Passive.Auras.Add(new PassiveAuraDefinition {
                Id = "team-reduction", MaximumUnits = GoroValue("MaximumUnits"),
                Gate = new PassiveGate { Modes = (BattleModeMask)GoroValue("Modes") },
                Modifiers = new System.Collections.Generic.List<StatModifierDefinition> {
                    new StatModifierDefinition { Target = (ModifierTarget)GoroValue("Target"),
                        Operation = (ModifierOperation)GoroValue("Operation"), Amount = GoroValue("Amount") }
                }
            });
            Check(PassiveRuleValidator.Validate(goro.Passive).Count == 0, "Team reduction aura must validate.");
            var reductionBattle = BattleEngine.Create("goro-reduction", new[] {
                Fighter("a",10000), Fighter("b",10000), Fighter("c",10000), goro }, null,
                new[] { Fighter("enemy",10000) }, null, 73, TeamSide.Player, mode: mode);
            var ally = reductionBattle.Player.Fighters[0];
            var enemy = reductionBattle.Opponent.Fighters[0];
            Check(StatusSystem.BuildDamagePolicy(enemy, ally, DamageFamily.Normal).FinalReductionBp ==
                (mode == BattleModeMask.PvP ? 1000 : 0), "Goro SUB reduction must apply only in PvP.");
            Check(StatusSystem.BuildDamagePolicy(ally, enemy, DamageFamily.Normal).FinalReductionBp == 0,
                "Goro reduction must not protect enemies.");
            var reserve = reductionBattle.Player.Fighters[3];
            reserve.Health = 0; reserve.IsAlive = false;
            CharacterPassiveRuntime.Refresh(reductionBattle);
            Check(StatusSystem.BuildDamagePolicy(enemy, ally, DamageFamily.Normal).FinalReductionBp == 0,
                "Goro reduction must clear when its owner is defeated.");
        }
        var path = System.IO.File.Exists("Assets/Project/Data/Character/king94_Passive.asset")
            ? "Assets/Project/Data/Character/king94_Passive.asset"
            : "Assets/Resources/Character_WIP-Phase/king94_Passive.asset";
        var kingAsset = System.IO.File.ReadAllText(path);
        Check(kingAsset.Contains("Trigger: 2"), "Authored King must stack at team turn end, matching its source description.");
        Check(!kingAsset.Contains("Presence: 1"), "Authored King must remain available from SUB.");
        // Also exercise start-turn authoring so both supported passive clocks remain valid.
        foreach (var timing in new[] { PassiveEventKind.TeamTurnStarted, PassiveEventKind.TeamTurnEnded })
        {
            var king = Fighter("fighter.king94", 10000);
            king.Passive = StandardCharacterPassives.Create(king.Id);
            king.Passive.Reactions[0].Trigger = timing;
            var state = BattleEngine.Create("inspector-king", new[] {
                Fighter("a",10000), Fighter("b",10000), Fighter("c",10000), king }, null,
                new[] { Fighter("enemy",10000) }, null, 73, TeamSide.Player);
            Check(state.Player.Fighters[3].IsReserve, "King must start in SUB.");
            for (var turn = 0; turn < 12; turn++)
            {
                var owner = state.Player.Fighters[3];
                int stacks = CharacterPassiveRuntime.Counter(owner, "turn-stacks");
                foreach (var ally in state.Player.LivingActive())
                    Check(StatusSystem.GetEffectiveStats(ally).PierceBp == ally.Stats.PierceBp + stacks * 800,
                        "King SUB pierce must match the live counter on every ally.");
                Check(BattleEngine.TryResolvePlan(state, new TurnPlan { RequestId = "pass-" + turn,
                    ExpectedRevision = state.Revision }, out var next, out var error), error);
                state = next;
            }
            var reserve = state.Player.Fighters[3];
            Check(reserve.OwnerTurnsCompleted == 6, "SUB must count its own completed turns only.");
            Check(CharacterPassiveRuntime.Counter(reserve, "turn-stacks") == 5, "King stacks must cap at five.");
            reserve.Health = 0; reserve.IsAlive = false;
            CharacterPassiveRuntime.Refresh(state);
            foreach (var ally in state.Player.LivingActive())
            {
                Check(StatusSystem.GetEffectiveStats(ally).PierceBp == ally.Stats.PierceBp,
                    "Dead passive owner must immediately lose all ally contributions.");
                Check(!ally.PassiveContributions.Any(c => c.OwnerId == reserve.Id), "Dead owner remains in inspector contributions.");
            }
            var active = state.Player.Fighters[0];
            active.Definition.Passive = StandardCharacterPassives.Create("fighter.kensou94");
            CharacterPassiveRuntime.Refresh(state);
            Check(state.Player.Fighters[1].PassiveContributions.Any(c => c.OwnerId == active.Id), "Active owner aura missing.");
            active.Health = 0; active.IsAlive = false;
            CharacterPassiveRuntime.Refresh(state);
            Check(!state.Player.Fighters[1].PassiveContributions.Any(c => c.OwnerId == active.Id), "Defeated active owner aura persisted.");
        }
    }
}
