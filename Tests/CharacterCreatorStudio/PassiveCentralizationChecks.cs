using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    internal static class PassiveCentralizationChecks
    {
        public static int Run(CharacterDefinition baseline)
        {
            var count = 0;
            void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("Passive centralization: " + message); count++; }
            var d = StudioDocumentService.ImportCharacter(baseline); StudioPassiveEditing.Normalize(d);
            var opening = d.Rules[0]; opening.Settings.ActorRelation = PassiveRelation.Enemies;
            opening.Settings.TargetRelation = PassiveRelation.Self; opening.Settings.LimitScope = PassiveLimitScope.Battle; opening.Settings.CooldownOwnerTurns = 3;
            var c = StudioCompiler.Compile(d);
            Check(c.Success, string.Join("; ", c.Diagnostics));
            var r = c.Character.Passive.Reactions.Single(x => x.Id == opening.Id);
            Check(r.ExecutionVersion == 1 && r.ActorRelation == PassiveRelation.Enemies && r.TargetRelation == PassiveRelation.Self, "Opening filters survive compilation.");
            Check(r.LimitScope == PassiveLimitScope.Battle && r.CooldownOwnerTurns == 3, "Opening activation controls survive compilation.");
            var view = StudioPassiveView.Rule(opening);
            Check(view.Who.Contains("Enemies") && view.Who.Contains("Self") && view.Execution.Contains("3 owner turns"), "View derives filters and execution from the same model.");
            opening.Settings.ActorRelation = PassiveRelation.Allies;
            Check(StudioPassiveView.Rule(opening).Who.Contains("Allies"), "Summary projection has no stale cached state.");

            d = StudioDocumentService.ImportCharacter(baseline);
            var authored = StudioDocumentService.BeginKitEditing(d);
            authored.Skills[0].Variants.Add(new AbilityVariantDefinition { Id = d.Rules[0].Id, Name = "Native collision", InitiallyUnlocked = true, Ranks = authored.Skills[0].Ranks.Select(x => x.Clone()).ToList() });
            c = StudioCompiler.Compile(d); Check(c.Success, "Cross-kind native identity fixture is valid.");
            var restored = StudioPassiveEditing.RemoveGenerated(c.Character, d);
            Check(restored.Skills[0].Variants.Count == 1 && restored.Skills[0].Variants[0].Id == d.Rules[0].Id, "Removing a generated passive rule preserves unrelated native variant with same ID.");
            Check(!restored.Passive.Reactions.Any(x => x.Id == d.Rules[0].Id), "Generated character reaction is removed from its exact collection.");

            var source = baseline.Clone(); source.Passive.Support.OpeningTeamGauge = 0;
            source.Passive.Reactions.Clear(); source.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "native.opening", Trigger = PassiveEventKind.BattleStarted,
                Commands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.ChangePowerGauge, Amount = 1 } },
                Conditions = { new EffectConditionDefinition { Kind = EffectConditionKind.Always, Negate = true } },
                ElseCommands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.ChangePowerGauge, Amount = 2 } } });
            d = StudioDocumentService.ImportCharacter(source); var frozen = SnapshotJson.Serialize(d.LegacyCharacter); StudioPassiveEditing.ImportReactions(d);
            Check(d.Rules.Count == 1 && d.Rules[0].Imported && d.Rules[0].RuntimeVersion == 0, "Imported native reaction uses the shared rule representation with original receipt version.");
            c = StudioCompiler.Compile(d); Check(c.Success, string.Join("; ", c.Diagnostics));
            r = c.Character.Passive.Reactions.Single();
            Check(r.Commands[0].Amount == 1 && r.ElseCommands[0].Amount == 2 && r.Actions.Count == 0, "Native command order and else behavior retain runtime representation.");
            Check(r.Conditions[0].Children[0].Negate, "Imported negated predicates survive.");
            Check(SnapshotJson.Serialize(d.LegacyCharacter) == frozen, "Import never edits the frozen native definition.");
            var beforeBattle = StudioSimulation.CreateFixture(source, 98765); var afterBattle = StudioSimulation.CreateFixture(c.Character, 98765);
            Check(beforeBattle.Player.Fighters[0].PowerGauge == afterBattle.Player.Fighters[0].PowerGauge, "Imported else program matches native opening execution.");
            d.Rules.Clear(); c = StudioCompiler.Compile(d);
            Check(c.Success && c.Character.Passive.Reactions.Count == 0, "Removing imported event rule does not resurrect its native copy.");

            d = StudioDocumentService.ImportCharacter(source); StudioPassiveEditing.ImportReactions(d);
            d.Rules[0].Trigger = PassiveEventKind.BeforeDamage; d.Rules[0].RuntimeVersion = 1;
            d.Rules[0].ElseActionRootId = null; d.Conditions.First(x => x.Id == d.Rules[0].ConditionRootId).Children.Clear();
            var rootCondition = d.Conditions.First(x => x.Id == d.Rules[0].ConditionRootId); rootCondition.Logic = ConditionLogic.Leaf; rootCondition.Kind = EffectConditionKind.Always;
            var command = d.Actions.First(a => a.Kind == StudioActionKind.Command).Command;
            command.Kind = PassiveCommandKind.IncreaseCurrentCritChancePercent; command.Amount = 1500;
            c = StudioCompiler.Compile(d); Check(c.Success, string.Join("; ", c.Diagnostics));
            r = c.Character.Passive.Reactions.Single();
            Check(r.Actions.Count == 0 && r.Commands.Single().Kind == PassiveCommandKind.IncreaseCurrentCritChancePercent, "Calculation rule lowers to synchronous modifiers, not side effects.");
            d.Rules[0].Settings.ActorRelation = PassiveRelation.Self; d.Rules[0].Settings.TargetRelation = PassiveRelation.Enemies;
            c = StudioCompiler.Compile(d);
            var battle = StudioSimulation.CreateFixture(c.Character, 12345);
            var owner = battle.Player.Fighters[0]; var target = battle.Opponent.Fighters[0];
            var card = battle.Player.Hand.First(x => x.OwnerFighterId == owner.Id && x.Kind == CardKind.Skill);
            var originalCrit = StatusSystem.GetEffectiveStats(owner).CritChanceBp;
            var hit = AttackEffectSystem.Prepare(null, battle, owner, target, DamageFamily.Normal, card);
            Check(hit.Attacker.CritChanceBp == originalCrit + 1500, "Production hit preparation applies the authored Crit modifier.");
            Check(AttackEffectSystem.Prepare(null, battle, owner, target, DamageFamily.Normal, card).Attacker.CritChanceBp == originalCrit + 1500,
                "Repeated hit calculations each receive one local bonus.");
            Check(AttackEffectSystem.Prepare(null, battle, owner, owner, DamageFamily.Normal, card).Attacker.CritChanceBp == originalCrit,
                "Target filtering excludes an unqualified hit.");
            Check(StatusSystem.GetEffectiveStats(owner).CritChanceBp == originalCrit, "Local Crit modifiers never leak into persistent actor stats.");
            d.Rules[0].Settings.RequireCritical = true;
            Check(!StudioCompiler.Compile(d).Success, "BeforeDamage rejects unresolved critical outcomes rather than silently disabling execution.");
            d.Rules[0].Settings.RequireCritical = false; d.Rules[0].ElseActionRootId = d.Rules[0].ActionRootId;
            Check(!StudioCompiler.Compile(d).Success, "BeforeDamage rejects an unsupported else calculation program.");
            d.Rules[0].ElseActionRootId = null;
            command.Kind = PassiveCommandKind.ChangePowerGauge;
            Check(!StudioCompiler.Compile(d).Success, "Calculation stage rejects authoritative gauge side effects.");
            Check(StudioPassiveView.Mechanics(d).Count(x => x.Kind == "Calculation rule") == 1, "Overview recognizes calculation rules.");
            var ordered = source.Clone(); ordered.Passive.Reactions.Clear();
            ordered.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "z.first", Trigger = PassiveEventKind.BattleStarted,
                Commands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.SetCounter, CounterKey = "order", Amount = 3 } } });
            ordered.Passive.Reactions.Add(new PassiveReactionDefinition { Id = "a.second", Trigger = PassiveEventKind.BattleStarted,
                Commands = { new PassiveCommandDefinition { Kind = PassiveCommandKind.IncrementCounter, CounterKey = "order", Amount = 2 } } });
            d = StudioDocumentService.ImportCharacter(ordered); StudioPassiveEditing.ImportReactions(d); c = StudioCompiler.Compile(d);
            Check(c.Success && c.Character.Passive.Reactions.Select(x => x.Id).SequenceEqual(ordered.Passive.Reactions.Select(x => x.Id)), "Native dispatch order survives import even when IDs sort differently.");
            Check(StudioSimulation.CreateFixture(c.Character, 555).Player.Fighters[0].PassiveCounters.Single(x => x.Key == "order").Value == 5,
                "Native ordered set/increment behavior remains equivalent.");
            d.Rules[0].Enabled = false; c = StudioCompiler.Compile(d);
            d.LegacyCharacter = c.Character.Clone(); d.AuthoredCharacter = StudioPassiveEditing.RemoveGenerated(c.Character, d);
            d.Rules[0].Enabled = true; c = StudioCompiler.Compile(d);
            Check(c.Character.Passive.Reactions.Select(x => x.Id).SequenceEqual(ordered.Passive.Reactions.Select(x => x.Id)), "Disable, native reconstruction and re-enable retain original dispatch positions.");
            d = StudioDocumentService.ImportCharacter(baseline);
            Check(!StudioPassiveView.AvailabilitySummary(d).Contains("Mixed"), "Equivalent support and event gates share one availability state.");
            var support = StudioDocumentService.BeginKitEditing(d).Passive.Support;
            support.SwirlStrengthBp = 1000; support.SwirlPresence = PassivePresence.LivingReserve;
            var inventory = StudioPassiveView.Mechanics(d);
            Check(inventory.Any(x => x.Id == "compatibility.support.target-bonus") && inventory.Any(x => x.Id == "compatibility.support.gauge"), "Kaeya's support clauses are individually discoverable.");
            Check(inventory.Single(x => x.Id == "compatibility.support.swirl").Availability.Contains("Off-Field"), "Swirl availability uses its own authoritative gate.");
            StudioPassiveEditing.RemoveSupportClause(d, "compatibility.support.target-bonus");
            Check(support.BonusAgainstRecipeId == null && !string.IsNullOrEmpty(support.AttackerGaugeAgainstRecipeId) && support.SwirlStrengthBp == 1000,
                "Removing one compatibility mechanic preserves unrelated clauses.");
            d = StudioDocumentService.ImportCharacter(source); StudioPassiveEditing.ImportReactions(d); d.Rules[0].Enabled = false;
            d.AuthoredCharacter = source.Clone(); d.AuthoredCharacter.Passive.Support = null;
            Check(StudioPassiveView.AvailabilitySummary(d) == "No executable mechanics", "Disabled owned rules do not create executable availability badges.");
            Check(!StudioPassiveView.Gates(d, true).Any(), "Disabled imported rules and their replaced native copies do not contribute preview badges.");
            var familyModifier = new StatModifierDefinition { Target = ModifierTarget.FamilyDamageDealt, Family = DamageFamily.DamageOverTime, Amount = 1250 };
            Check(StudioPassiveView.Modifier(familyModifier).Contains("Damage Over Time damage dealt") && StudioPassiveView.Modifier(familyModifier).Contains("12.5%"),
                "Family modifier summaries use the family and percentage semantics even when their ignored operation remains Flat.");
            var bundleModifier = new StatModifierDefinition { Target = ModifierTarget.StatBundle, Bundle = StatBundleKind.BasicStats, Amount = 1500 };
            Check(StudioPassiveView.Modifier(bundleModifier).Contains("Basic Stats bundle") && StudioPassiveView.Modifier(bundleModifier).Contains("15%"), "Bundle summaries never report the unrelated Stat field or flat units.");
            var flatModifier = new StatModifierDefinition { Target = ModifierTarget.FlatDamageReduction, Amount = 250 };
            Check(StudioPassiveView.Modifier(flatModifier).Contains("250") && !StudioPassiveView.Modifier(flatModifier).Contains("%"), "Flat reduction summaries retain flat units.");
            foreach (var targetKind in Enum.GetValues(typeof(ModifierTarget)).Cast<ModifierTarget>())
            {
                var modifier = new StatModifierDefinition { Target = targetKind, Stat = StatId.Attack, Amount = 2000 };
                var summary = StudioPassiveView.Modifier(modifier);
                Check(!string.IsNullOrWhiteSpace(summary) && (targetKind == ModifierTarget.Stat || !summary.StartsWith("Attack ·")), "Every modifier target has its own summary: " + targetKind);
            }
            d = StudioDocumentService.ImportCharacter(baseline);
            var snapshot = SnapshotJson.Serialize(d);
            var rejected = false;
            try { StudioPassiveEditing.SetAvailability(d, PassivePresence.LivingReserve, BattleModeMask.PvP); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && SnapshotJson.Serialize(d) == snapshot, "Unsupported support-mode changes reject atomically before altering presence or imported content.");
            StudioPassiveEditing.SetAvailability(d, PassivePresence.LivingReserve, null);
            Check(StudioPassiveView.Gates(d).All(g => g.Presence == PassivePresence.LivingReserve), "Position changes include preserved support availability.");
            var frozenSource = SnapshotJson.Serialize(d.LegacyCharacter);
            var kit = StudioDocumentService.BeginKitEditing(d); kit.Passive.Support = null;
            kit.Passive.Auras.Add(new PassiveAuraDefinition { Id = "availability.aura" });
            var localRule = new StudioRule { Id = "status.local", StatusId = "status.fixture" }; d.Rules.Add(localRule);
            StudioPassiveEditing.SetAvailability(d, PassivePresence.LivingActive, BattleModeMask.PvE);
            Check(StudioPassiveView.Gates(d).All(g => g.Presence == PassivePresence.LivingActive && g.Modes == BattleModeMask.PvE), "Availability edits consistently update character rules and continuous mechanics.");
            Check(localRule.Gate.Presence == PassivePresence.LivingRoster && localRule.Gate.Modes == BattleModeMask.All, "Character availability edits preserve status-local trigger gates.");
            Check(kit.PassiveSource.SourceType == "On-Field" && kit.PassiveSource.Restriction == "PVE", "Native metadata follows explicit availability edits.");
            Check(SnapshotJson.Serialize(d.LegacyCharacter) == frozenSource, "Availability editing preserves the frozen source definition.");
            Console.WriteLine("PASS: " + count + " passive centralization assertions."); return count;
        }
    }
}
