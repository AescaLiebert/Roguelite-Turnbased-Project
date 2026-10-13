using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    /// <summary>Run progression and fighter history, independent of seeded route generation and battle entropy.</summary>
    public static class DungeonRunEngine
    {
        /// <summary>Read-only inspection of any platform, including locked, bypassed and completed nodes.</summary>
        public static RouteNodeState InspectNode(RunState run, string nodeId) => run?.FindNode(nodeId)?.Clone();

        public static bool CanEnterNode(RunState run, string nodeId)
        {
            var node = run?.FindNode(nodeId);
            var current = run?.FindNode(run.CurrentNodeId);
            return run != null && run.Status == RunStatus.InProgress && current?.Progress == RouteNodeProgress.Completed &&
                node?.Progress == RouteNodeProgress.Reachable && current.OutgoingNodeIds.Contains(nodeId);
        }
        public static RunState CreateRun(string runId, string userId, string requestId, DungeonProfile profile,
            int difficultyBonusPercent, ulong seed, string contentVersion, string contentHash,
            IReadOnlyList<RunFighterSeed> roster, IReadOnlyList<CharacterDefinition> catalog,
            int baseCompletionDiamonds = 320, bool deferFormation = false)
        {
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Run, user, and request ids are required.");
            if (profile == null || profile.Restrictions == null) throw new ArgumentException("A complete dungeon profile is required.", nameof(profile));
            if (string.IsNullOrWhiteSpace(contentVersion) || string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Runs must pin a content version and content hash.");
            if (baseCompletionDiamonds < 0) throw new ArgumentOutOfRangeException(nameof(baseCompletionDiamonds));
            if (!deferFormation || roster == null || roster.Count > 0) ValidateRoster(profile, roster, catalog);
            var nodes = RouteGenerator.Generate(profile, catalog, difficultyBonusPercent, seed);
            var state = new RunState { RunId = runId, UserId = userId, ProfileId = profile.Id,
                ContentVersion = contentVersion, ContentHash = contentHash, GeneratorVersion = profile.GeneratorVersion,
                DifficultyBonusPercent = difficultyBonusPercent, BaseCompletionDiamonds = baseCompletionDiamonds,
                RewardQuoteDiamonds = 0, Seed = seed,
                Revision = 1, Status = RunStatus.InProgress, Nodes = nodes,
                Mode = profile.Mode, RewardPhase = Math.Max(1, profile.Phase), RewardSubPhase = profile.SubPhase };
            foreach (var boon in profile.Boons) state.BoonDefinitionSnapshot.Add(boon.Clone());
            for (var i = 0; i < (roster?.Count ?? 0); i++)
            {
                var seedFighter = roster[i];
                var definition = FindCatalogDefinition(catalog, seedFighter.Definition.Id).Clone();
                var stats = (seedFighter.ResolvedStats ?? definition.BaseStats).Clone();
                state.Roster.Add(new RunFighterState { RunFighterId = seedFighter.OwnedFighterId,
                    DefinitionId = definition.Id, Stats = stats,
                    ArtifactLoadout = seedFighter.Definition.ArtifactLoadout?.ConvertAll(a => a?.Clone()) ?? new List<ArtifactInstance>(),
                    ConstellationTier = seedFighter.ConstellationTier, OriginalFormationIndex = seedFighter.FormationSlot,
                    CurrentHealth = stats.MaxHealth, IsDefeated = false });
                state.Formation[seedFighter.FormationSlot] = seedFighter.OwnedFighterId;
            }
            state.CurrentNodeId = "r0n0";
            state.SelectedPath.Add(state.CurrentNodeId);
            state.CommandReceipts.Add(new RunCommandReceipt { RequestId = requestId, PayloadHash = HashPayload(
                "start", requestId, runId, profile.Id, difficultyBonusPercent.ToString(), seed.ToString()), ResultRevision = state.Revision });
            return state;
        }

        public static bool TryChooseNode(RunState current, string nodeId, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashPayload("choose-node", nodeId, expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var state = current.Clone();
            if (state.Status != RunStatus.InProgress) { error = "Run is not accepting route choices."; return false; }
            var currentNode = state.FindNode(state.CurrentNodeId);
            if (currentNode == null || currentNode.Progress != RouteNodeProgress.Completed)
            { error = "The current route node is not complete."; return false; }
            if (!currentNode.OutgoingNodeIds.Contains(nodeId)) { error = "That node is not reachable from the current position."; return false; }
            var chosen = state.FindNode(nodeId);
            if (chosen == null || chosen.Progress != RouteNodeProgress.Reachable) { error = "That route node is locked or already bypassed."; return false; }
            foreach (var siblingId in currentNode.OutgoingNodeIds)
            {
                var sibling = state.FindNode(siblingId);
                if (sibling != null && sibling.Id != chosen.Id && sibling.Progress == RouteNodeProgress.Reachable)
                    sibling.Progress = RouteNodeProgress.Bypassed;
            }
            chosen.Progress = RouteNodeProgress.Selected;
            state.CurrentNodeId = chosen.Id;
            state.SelectedPath.Add(chosen.Id);
            if (IsEncounter(chosen.Type))
            {
                state.Status = RunStatus.InBattle;
                state.PendingBattleId = state.RunId + ":" + chosen.Id;
            }
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TrySetFormation(RunState current, IReadOnlyList<RunFighterSeed> selected,
            IReadOnlyList<CharacterDefinition> catalog, DungeonProfile profile, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashPayload("formation", SnapshotJson.Serialize(selected), expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            if (current.Status != RunStatus.InBattle) { error = "Select an encounter before preparing a team."; return false; }
            if (profile == null || profile.Id != current.ProfileId) { error = "Formation must use the run's dungeon policy."; return false; }
            try { ValidateRoster(profile, selected, catalog); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            var state = current.Clone();
            state.Formation = new List<string> { "", "", "", "" };
            foreach (var seed in selected)
            {
                var fighter = state.FindFighter(seed.OwnedFighterId);
                if (fighter != null && fighter.DefinitionId != seed.Definition.Id)
                { error = "Inventory id does not match this fighter's run history."; return false; }
                if (fighter == null && state.Roster.Exists(f => f.DefinitionId == seed.Definition.Id))
                { error = "This character already has run history under another inventory id."; return false; }
                if (fighter != null && (fighter.IsDefeated || fighter.CurrentHealth <= 0))
                { error = "Defeated fighters are unavailable for the remainder of this run."; return false; }
                if (fighter == null)
                {
                    // Run fighters retain their original equipment even if account gear moves or is consumed.
                    // A newly entering fighter must not reuse another fighter's frozen instance in this run.
                    foreach (var artifact in seed.Definition.ArtifactLoadout ?? new List<ArtifactInstance>())
                    {
                        if (state.Roster.Exists(other => other.ArtifactLoadout != null &&
                            other.ArtifactLoadout.Exists(locked => locked != null && locked.InstanceId == artifact.InstanceId)))
                        {
                            error = ArtifactRules.Name(artifact) + " is already assigned to another fighter in this run. Choose other equipment or start a new run.";
                            return false;
                        }
                    }
                    var definition = FindCatalogDefinition(catalog, seed.Definition.Id);
                    var stats = (seed.ResolvedStats ?? definition.BaseStats).Clone();
                    foreach (var boost in state.StatBoosts) boost.Apply(stats);
                    fighter = new RunFighterState { RunFighterId = seed.OwnedFighterId, DefinitionId = definition.Id,
                        Stats = stats, ArtifactLoadout = seed.Definition.ArtifactLoadout?.ConvertAll(a => a?.Clone()) ?? new List<ArtifactInstance>(),
                        CurrentHealth = stats.MaxHealth, ConstellationTier = seed.ConstellationTier };
                    state.Roster.Add(fighter);
                }
                fighter.OriginalFormationIndex = seed.FormationSlot;
                state.Formation[seed.FormationSlot] = fighter.RunFighterId;
            }
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TryRetryBattle(RunState current, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            var payload = HashPayload("retry", expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var node = current.FindNode(current.CurrentNodeId);
            if (current.Status != RunStatus.InProgress || node == null || !IsEncounter(node.Type) ||
                node.Progress != RouteNodeProgress.Selected || current.Rewards.Exists(r => r.NodeId == node.Id))
            { error = "This stage has no defeated encounter to retry."; return false; }
            var state = current.Clone();
            state.Status = RunStatus.InBattle;
            state.PendingBattleId = state.RunId + ":" + node.Id + ":attempt:" + (state.Revision + 1);
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TryBuildEncounter(RunState run, IReadOnlyList<CharacterDefinition> catalog,
            out EncounterProjection encounter, out string error)
        {
            encounter = null;
            error = null;
            if (run == null || run.Status != RunStatus.InBattle || string.IsNullOrEmpty(run.PendingBattleId))
            { error = "Run has no committed encounter to resume."; return false; }
            var node = run.FindNode(run.CurrentNodeId);
            if (node == null || !IsEncounter(node.Type) || node.Progress != RouteNodeProgress.Selected)
            { error = "Current route node is not a pending encounter."; return false; }
            var result = new EncounterProjection { BattleId = run.PendingBattleId, RunRevision = run.Revision,
                DifficultyBonusPercent = run.DifficultyBonusPercent };
            var living = new List<RunFighterState>();
            var seenSlots = new HashSet<int>();
            foreach (var fighter in run.Roster)
                if (fighter != null && !fighter.IsDefeated && fighter.CurrentHealth > 0 && run.Formation.Contains(fighter.RunFighterId)) living.Add(fighter);
            living.Sort((a, b) => a.OriginalFormationIndex.CompareTo(b.OriginalFormationIndex));
            foreach (var fighter in living)
            {
                if (result.PlayerTeam.Count >= 4) break;
                var slot = run.Formation.IndexOf(fighter.RunFighterId);
                if (!seenSlots.Add(slot)) { error = "Formation contains a duplicate position."; return false; }
                var definition = FindCatalogDefinition(catalog, fighter.DefinitionId);
                if (definition == null) { error = "Character definitions are missing the run fighter: " + fighter.DefinitionId; return false; }
                var encounterDefinition = definition.Clone();
                encounterDefinition.ArtifactLoadout = fighter.ArtifactLoadout?.ConvertAll(a => a?.Clone()) ?? new List<ArtifactInstance>();
                result.PlayerTeam.Add(new EncounterFighterSnapshot { FighterId = fighter.RunFighterId,
                    DefinitionId = fighter.DefinitionId, Definition = encounterDefinition, Stats = fighter.Stats.Clone(),
                    CurrentHealth = fighter.CurrentHealth, ConstellationTier = fighter.ConstellationTier,
                    FormationSlot = Math.Min(slot, 2), IsReserve = slot == 3 });
            }
            if (result.PlayerTeam.Count < 1 || !result.PlayerTeam.Exists(f => !f.IsReserve))
            {
                error = "Encounter needs one to four living player fighters.";
                return false;
            }
            foreach (var enemy in node.EnemyTeamSnapshot) result.EnemyTeam.Add(enemy.Clone());
            encounter = result;
            return true;
        }

        public static bool TryApplyBattleResult(RunState current, string battleId, string battleReceiptId,
            long expectedRevision, string requestId, bool playerWon, IReadOnlyList<BattleFighterResult> fighters,
            out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashBattleResult(battleId, battleReceiptId, playerWon, fighters, expectedRevision);
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var state = current.Clone();
            if (state.Status != RunStatus.InBattle || state.PendingBattleId != battleId)
            { error = "Battle result does not match the pending encounter."; return false; }
            if (string.IsNullOrWhiteSpace(battleReceiptId)) { error = "A stable battle result receipt id is required."; return false; }
            if (state.CompletedBattleReceiptIds.Contains(battleReceiptId)) { error = "Battle receipt was already applied by another request."; return false; }
            if (!ValidateFighterResults(state, fighters, playerWon, out error)) return false;
            foreach (var result in fighters)
            {
                var fighter = state.FindFighter(result.RunFighterId);
                fighter.CurrentHealth = result.CurrentHealth;
                fighter.IsDefeated = result.IsDefeated;
                if (fighter.IsDefeated)
                    for (var slot = 0; slot < state.Formation.Count; slot++)
                        if (state.Formation[slot] == fighter.RunFighterId) state.Formation[slot] = string.Empty;
            }
            state.CompletedBattleReceiptIds.Add(battleReceiptId);
            state.PendingBattleId = null;
            var node = state.FindNode(state.CurrentNodeId);
            var livingCount = state.Roster.FindAll(f => !f.IsDefeated && f.CurrentHealth > 0).Count;
            if (playerWon && livingCount > 0)
            {
                state.Rewards.Add(DungeonRewards.Victory(state, node));
            }
            if (playerWon && node.Type == RouteNodeType.Boss)
            {
                node.Progress = RouteNodeProgress.Completed;
                state.Status = RunStatus.Completed;
            }
            else if (playerWon && node.Type == RouteNodeType.Elite)
            {
                // Elite victory stays selected until its stat reward is claimed.
                state.Status = RunStatus.InProgress;
            }
            else if (playerWon)
            {
                CompleteNodeAndUnlock(state, node);
                state.Status = RunStatus.InProgress;
            }
            else
            {
                // Keep the stage retryable. A wipe never ends the run; the player can bring replacements or abandon.
                state.Status = RunStatus.InProgress;
            }
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TryApplyRest(RunState current, RestChoice choice, string fighterId,
            long expectedRevision, string requestId, out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashPayload("rest", choice.ToString(), fighterId, expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var state = current.Clone();
            if (state.Status != RunStatus.InProgress) { error = "Run is not accepting a Rest choice."; return false; }
            var node = state.FindNode(state.CurrentNodeId);
            if (node == null || node.Type != RouteNodeType.Rest || node.Progress != RouteNodeProgress.Selected)
            { error = "The selected route node is not a pending Rest."; return false; }
            if (choice == RestChoice.HealLiving)
            {
                var eligible = false;
                foreach (var fighter in state.Roster)
                {
                    if (fighter.IsDefeated || fighter.CurrentHealth <= 0) continue;
                    var amount = RestRecoveryAmount(fighter.Stats.MaxHealth, 40);
                    if (fighter.CurrentHealth < fighter.Stats.MaxHealth && amount > 0) eligible = true;
                    fighter.CurrentHealth = (int)Math.Min(fighter.Stats.MaxHealth, (long)fighter.CurrentHealth + amount);
                }
                if (!eligible) { error = "No living fighter can benefit from healing; choose Continue."; return false; }
            }
            else if (choice == RestChoice.ReviveOne)
            {
                error = "Defeated fighters are unavailable for the remainder of this run.";
                return false;
            }
            else if (choice == RestChoice.Continue)
            {
                foreach (var fighter in state.Roster)
                    if (!fighter.IsDefeated && fighter.CurrentHealth > 0 && fighter.CurrentHealth < fighter.Stats.MaxHealth)
                    { error = "Continue is available only when no fighter can benefit from Rest."; return false; }
            }
            else { error = "Unknown Rest choice."; return false; }
            CompleteNodeAndUnlock(state, node);
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TryChooseBoon(RunState current, string boonId, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashPayload("boon", boonId, expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var state = current.Clone();
            if (state.Status != RunStatus.InProgress) { error = "Run is not accepting a boon choice."; return false; }
            var node = state.FindNode(state.CurrentNodeId);
            if (node == null || (node.Type != RouteNodeType.Boon && node.Type != RouteNodeType.Elite) || node.Progress != RouteNodeProgress.Selected)
            { error = "The selected node has no pending boon choice."; return false; }
            if (boonId != "stat-boost:" + node.Id)
            { error = "That stat reward does not belong to the selected stage."; return false; }
            if (node.Type == RouteNodeType.Elite && !state.Rewards.Exists(r => r.NodeId == node.Id))
            { error = "Win the elite battle before claiming its stat reward."; return false; }
            var boost = node.StatBoost ?? DungeonStatBoost.Generate(state.Seed, node.Id);
            if (state.StatBoosts.Exists(b => b.NodeId == node.Id)) { error = "Stat reward already claimed."; return false; }
            state.StatBoosts.Add(boost.Clone());
            foreach (var fighter in state.Roster)
                if (!fighter.IsDefeated && fighter.CurrentHealth > 0)
                {
                    var maxHealth = fighter.Stats.MaxHealth;
                    boost.Apply(fighter.Stats);
                    if (boost.Stat == StatId.MaxHealth) fighter.CurrentHealth += fighter.Stats.MaxHealth - maxHealth;
                }
            CompleteNodeAndUnlock(state, node);
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static List<string> GetAvailableBoonOffers(RunState run, string nodeId)
        {
            var available = new List<string>();
            if (run == null) return available;
            var node = run.FindNode(nodeId);
            if (node == null || (node.Type != RouteNodeType.Boon && node.Type != RouteNodeType.Elite)) return available;
            if (!run.StatBoosts.Exists(b => b.NodeId == node.Id)) available.Add("stat-boost:" + node.Id);
            return available;
        }

        public static bool TryAcknowledgeReward(RunState current, string receiptId, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            var payload = HashPayload("acknowledge-reward", receiptId, expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            if (!current.Rewards.Exists(r => r.Id == receiptId && !r.CompletionBonus))
            { error = "Victory reward receipt is missing."; return false; }
            var state = current.Clone();
            if (!state.PresentedRewardIds.Contains(receiptId)) state.PresentedRewardIds.Add(receiptId);
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        public static bool TryAbandon(RunState current, long expectedRevision, string requestId,
            out RunState next, out string error)
        {
            next = null;
            error = null;
            var payload = HashPayload("abandon", expectedRevision.ToString());
            if (!PrepareCommand(current, expectedRevision, requestId, payload, out next, out error)) return false;
            if (next != null) return true;
            var state = current.Clone();
            if (state.Status == RunStatus.Completed || state.Status == RunStatus.Defeated || state.Status == RunStatus.Abandoned)
            { error = "Run has already ended."; return false; }
            state.Status = RunStatus.Abandoned;
            state.PendingBattleId = null;
            FinishCommand(state, requestId, payload);
            next = state;
            return true;
        }

        private static bool PrepareCommand(RunState current, long expectedRevision, string requestId, string payload,
            out RunState replay, out string error)
        {
            replay = null;
            error = null;
            if (current == null) { error = "Run state is missing."; return false; }
            if (string.IsNullOrWhiteSpace(requestId)) { error = "Command requires a stable request id."; return false; }
            var receipt = current.CommandReceipts.Find(item => item.RequestId == requestId);
            if (receipt != null)
            {
                if (receipt.PayloadHash != payload) { error = "Request id was already used with a different payload."; return false; }
                replay = current.Clone();
                return true;
            }
            if (current.Revision != expectedRevision) { error = "Run revision is stale."; return false; }
            return true;
        }

        private static void FinishCommand(RunState state, string requestId, string payload)
        {
            state.Revision++;
            state.CommandReceipts.Add(new RunCommandReceipt { RequestId = requestId, PayloadHash = payload, ResultRevision = state.Revision });
        }

        private static void CompleteNodeAndUnlock(RunState state, RouteNodeState node)
        {
            node.Progress = RouteNodeProgress.Completed;
            foreach (var id in node.OutgoingNodeIds)
            {
                var successor = state.FindNode(id);
                if (successor != null && successor.Progress == RouteNodeProgress.Locked)
                    successor.Progress = RouteNodeProgress.Reachable;
            }
        }

        private static bool ValidateRoster(DungeonProfile profile, IReadOnlyList<RunFighterSeed> roster,
            IReadOnlyList<CharacterDefinition> catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (roster == null || roster.Count < 1 || roster.Count > 4) throw new ArgumentException("A run roster must contain one to four fighters.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var definitions = new HashSet<string>(StringComparer.Ordinal);
            var formationSlots = new HashSet<int>();
            var artifactIds = new HashSet<string>(StringComparer.Ordinal);
            var hasActiveFighter = false;
            foreach (var fighter in roster)
            {
                if (fighter == null || string.IsNullOrWhiteSpace(fighter.OwnedFighterId) || fighter.Definition == null ||
                    string.IsNullOrWhiteSpace(fighter.Definition.Id))
                    throw new ArgumentException("Run roster contains an incomplete fighter snapshot.");
                if (!ids.Add(fighter.OwnedFighterId) || !definitions.Add(fighter.Definition.Id))
                    throw new ArgumentException("Run roster fighter ids and definitions must be unique.");
                if (fighter.FormationSlot < 0 || fighter.FormationSlot > 3 || !formationSlots.Add(fighter.FormationSlot))
                    throw new ArgumentException("Run roster formation positions must be unique and within the four saved slots.");
                if (fighter.IsReserve != (fighter.FormationSlot == 3))
                    throw new ArgumentException("Only formation position four can be the reserve.");
                if (!fighter.IsReserve) hasActiveFighter = true;
                if (fighter.ConstellationTier < 0 || fighter.ConstellationTier > 5) throw new ArgumentOutOfRangeException(nameof(roster), "Constellation tier must be 0-5.");
                var artifactSlots = new HashSet<ArtifactSlot>();
                foreach (var artifact in fighter.Definition.ArtifactLoadout ?? new List<ArtifactInstance>())
                {
                    if (artifact == null || string.IsNullOrEmpty(artifact.InstanceId) ||
                        !Enum.IsDefined(typeof(ArtifactSlot), artifact.Slot) || !Enum.IsDefined(typeof(ArtifactSet), artifact.Set) ||
                        !artifactSlots.Add(artifact.Slot) || !artifactIds.Add(artifact.InstanceId))
                        throw new ArgumentException("Run equipment must use unique artifact instances in valid fixed slots.");
                }
                var catalogDefinition = FindCatalogDefinition(catalog, fighter.Definition.Id);
                if (catalogDefinition == null)
                    throw new ArgumentException("Run roster definition is missing from the pinned content catalog: " + fighter.Definition.Id);
                if (!catalogDefinition.RuntimeReady)
                    throw new ArgumentException("Pinned catalog contains draft content for roster fighter: " + fighter.Definition.Id);
                if (!profile.Accepts(catalogDefinition))
                    throw new ArgumentException("Run fighter does not match the dungeon series and phase: " + fighter.Definition.Id);
                var resolvedStats = fighter.ResolvedStats ?? catalogDefinition.BaseStats;
                if (resolvedStats == null) throw new ArgumentException("Run roster has no resolved stats: " + fighter.Definition.Id);
                if (resolvedStats.Attack < 0 || resolvedStats.Defense < 0 || resolvedStats.MaxHealth <= 0)
                    throw new ArgumentException("Run roster has invalid resolved combat stats: " + fighter.Definition.Id);
                if (profile.EligibleCharacterIds != null && profile.EligibleCharacterIds.Count > 0 &&
                    !profile.EligibleCharacterIds.Contains(catalogDefinition.Id))
                    throw new ArgumentException("Run roster fighter is not allowed in this dungeon profile: " + fighter.Definition.Id);
            }
            if (!hasActiveFighter) throw new ArgumentException("A run needs at least one active fighter.");
            foreach (var restriction in profile.Restrictions)
            {
                var count = 0;
                foreach (var fighter in roster)
                {
                    var character = FindCatalogDefinition(catalog, fighter.Definition.Id);
                    if (!string.IsNullOrEmpty(restriction.AttributeId) && character.AttributeId != restriction.AttributeId) continue;
                    if (!string.IsNullOrEmpty(restriction.TraitId) && (character.TraitIds == null || !character.TraitIds.Contains(restriction.TraitId))) continue;
                    count++;
                }
                if (count < restriction.MinimumCount) throw new ArgumentException("Roster does not meet dungeon restriction: " + restriction.Description);
            }
            return true;
        }

        private static CharacterDefinition FindCatalogDefinition(IReadOnlyList<CharacterDefinition> catalog, string definitionId)
        {
            if (catalog == null) return null;
            foreach (var definition in catalog)
                if (definition != null && definition.Id == definitionId) return definition;
            return null;
        }

        private static bool ValidateFighterResults(RunState state, IReadOnlyList<BattleFighterResult> results, bool playerWon, out string error)
        {
            error = null;
            if (results == null || results.Count != state.Roster.Count) { error = "Battle result must include every frozen run fighter."; return false; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var living = 0;
            foreach (var result in results)
            {
                var fighter = result == null ? null : state.FindFighter(result.RunFighterId);
                if (fighter == null || !seen.Add(result.RunFighterId)) { error = "Battle result has an unknown or duplicate run fighter id."; return false; }
                if (result.CurrentHealth < 0 || result.CurrentHealth > fighter.Stats.MaxHealth || result.IsDefeated != (result.CurrentHealth == 0))
                { error = "Battle result HP is outside the frozen run HP range."; return false; }
                if (fighter.IsDefeated && !result.IsDefeated || !state.Formation.Contains(fighter.RunFighterId) &&
                    (result.CurrentHealth != fighter.CurrentHealth || result.IsDefeated != fighter.IsDefeated))
                { error = "Battle results cannot revive defeated fighters or change benched fighters."; return false; }
                if (!result.IsDefeated && state.Formation.Contains(fighter.RunFighterId)) living++;
            }
            if (playerWon && living == 0) { error = "A victory cannot leave every run fighter defeated."; return false; }
            return true;
        }

        private static string HashBattleResult(string battleId, string receiptId, bool won, IReadOnlyList<BattleFighterResult> results, long revision)
        {
            var text = new StringBuilder("battle-result|");
            text.Append(battleId).Append('|').Append(receiptId).Append('|').Append(won).Append('|').Append(revision).Append('|');
            if (results != null)
            {
                var ordered = new List<BattleFighterResult>(results);
                ordered.Sort((a, b) => string.CompareOrdinal(a?.RunFighterId, b?.RunFighterId));
                foreach (var result in ordered)
                    text.Append(result?.RunFighterId).Append(':').Append(result?.CurrentHealth).Append(':').Append(result?.IsDefeated).Append('|');
            }
            return Sha256(text.ToString());
        }

        private static string HashPayload(params string[] parts)
        {
            var text = new StringBuilder();
            foreach (var part in parts)
            {
                var value = part ?? string.Empty;
                text.Append(value.Length).Append(':').Append(value).Append('|');
            }
            return Sha256(text.ToString());
        }

        private static string Sha256(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static bool IsEncounter(RouteNodeType type) => type == RouteNodeType.Battle || type == RouteNodeType.Elite || type == RouteNodeType.Boss;
        private static int Scale(int value, int percent) => (int)((long)Math.Max(0, value) * percent / 100);
        private static int RestRecoveryAmount(int maxHealth, int percent) => maxHealth <= 0 ? 0 : Math.Max(1, Scale(maxHealth, percent));
    }
}
