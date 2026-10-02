using System.Collections.Generic;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Adapters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Connects the local dungeon entry, route map, and combat presenters.</summary>
    public sealed class DungeonFlowController : MonoBehaviour
    {
        [SerializeField] private DungeonEntryController entry;
        [SerializeField] private DungeonRouteMapController routeMap;
        [SerializeField] private string battleScene = "Battle";

        private void Awake()
        {
            if (entry != null) entry.RunStarted += OnRunStarted;
            if (routeMap != null) routeMap.EncounterReady += OnEncounterReady;
        }

        private void OnDestroy()
        {
            if (entry != null) entry.RunStarted -= OnRunStarted;
            if (routeMap != null) routeMap.EncounterReady -= OnEncounterReady;
        }

        private void Start()
        {
            if (LocalEncounterContext.TryConsumeResult(out var battle))
            {
                if (entry != null) entry.gameObject.SetActive(false);
                if (routeMap != null) routeMap.gameObject.SetActive(true);
                if (routeMap == null)
                    Debug.LogError("Could not apply local battle result to the run: Route map is missing.", this);
                else if (!routeMap.ApplyBattleResult(battle, battle.MatchId + ":result", out var error))
                    Debug.LogError("Could not apply local battle result to the run: " + error, this);
                ClaimCompletedRunRewardIfNeeded();
                return;
            }
            ClaimCompletedRunRewardIfNeeded();
            ResumeSavedRun();
        }

        /// <summary>Supply snapshots from the owning app composition, then show dungeon entry.</summary>
        public void OpenDungeon(string userId, string contentVersion, string contentHash, ulong seed,
            IReadOnlyList<CharacterDefinition> catalog, IReadOnlyList<RunFighterSeed> roster)
        {
            if (entry == null || routeMap == null)
            {
                Debug.LogError("Dungeon flow requires Entry and Route Map controllers.", this);
                return;
            }
            entry.gameObject.SetActive(true);
            routeMap.gameObject.SetActive(false);
            entry.Bind(userId, contentVersion, contentHash, seed, catalog, roster);
        }

        /// <summary>Keep the player on entry and explain why this local build cannot start a dungeon.</summary>
        public void ShowEntryUnavailable(string message)
        {
            if (entry != null)
            {
                entry.gameObject.SetActive(true);
                entry.ShowUnavailable(message);
            }
            if (routeMap != null) routeMap.gameObject.SetActive(false);
        }

        /// <summary>Show the persisted local run map when resuming from the app menu.</summary>
        public bool ResumeSavedRun()
        {
            if (routeMap == null || entry == null) return false;
            var run = new LocalRunStateStore().TryLoad(out var saved) ? saved : null;
            if (run == null || run.Status == RunStatus.Completed || run.Status == RunStatus.Defeated || run.Status == RunStatus.Abandoned)
                return false;
            entry.gameObject.SetActive(false);
            routeMap.gameObject.SetActive(true);
            routeMap.SetRun(run);
            return true;
        }

        private void OnRunStarted(RunState run)
        {
            routeMap.gameObject.SetActive(true);
            routeMap.SetRun(run);
            entry.gameObject.SetActive(false);
        }

        private void OnEncounterReady(EncounterProjection encounter)
        {
            var run = routeMap.GetRunSnapshot();
            if (run == null) return;
            LocalEncounterContext.Begin(encounter, run.Seed ^ (ulong)run.Revision);
            SceneManager.LoadScene(battleScene);
        }

        private void ClaimCompletedRunRewardIfNeeded()
        {
            var store = new LocalRunStateStore();
            if (!store.TryLoad(out var run) || run == null || run.Status != RunStatus.Completed || run.RewardClaimed) return;
            var inventory = PlayerInventoryService.Instance;
            if (inventory == null)
            {
                Debug.LogError("Could not grant the completed run reward: player inventory is not initialized.", this);
                return;
            }
            var granted = inventory.GrantRunReward(run.RunId, run.RewardQuoteDiamonds);
            run.RewardClaimed = true;
            store.Save(run);
            Debug.Log("Account run reward " + run.RunId + (granted ? " granted " + run.RewardQuoteDiamonds + " Diamonds." : " was already claimed."), this);
        }
    }
}
