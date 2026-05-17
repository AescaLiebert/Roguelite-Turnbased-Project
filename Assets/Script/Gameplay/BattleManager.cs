using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum BattleState
{
    Setup,
    IntroSequence,
    CombatDecision,
    BattleStart,
    TurnLoop,
    End
}

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Settings")]
    public BattleState CurrentState;
    public bool IsPlayerFirst;
    
    [Header("References")]
    [SerializeField] private TeamDataManager localTeamManager; // Drag in inspector or find
    [SerializeField] private BattleUIController uiController;
    private IBattleDataProvider _dataProvider;

    private float _playerTotalCC;
    private float _enemyTotalCC;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        // Default to local/test provider if not set externally
        if (localTeamManager == null) 
            localTeamManager = FindObjectOfType<TeamDataManager>();
            
        // Explicit cast/assignment
        if (localTeamManager is IBattleDataProvider provider)
        {
            _dataProvider = provider;
        }
    }

    private void Start()
    {
        // For now, auto-start if we are in the battle scene
        // In a full flow, this might be triggered by a transition
        StartCoroutine(InitializeBattleSequence());
    }

    private IEnumerator InitializeBattleSequence()
    {
        CurrentState = BattleState.Setup;
        Debug.Log("Battle State: Setup");

        // 1. Get Data
        if (_dataProvider == null)
        {
            Debug.LogError("No Battle Data Provider found!");
            yield break;
        }

        var playerTeam = _dataProvider.GetLocalPlayerTeam();
        var enemyTeam = _dataProvider.GetOpponentTeam();

        // 2. Spawn Units
        if (localTeamManager != null)
        {
             // Assume we have spawn points setup. 
             // In a real scenario, BattleManager should hold references to P1/P2 spawn points.
             // For now we rely on TeamDataManager having them or we find them.
             
             // Spawn Player
             // localTeamManager.SpawnSpecificTeam(playerTeam, playerSpawnPoints, "Hero"); 
             // Just triggering the default for now as fallback if specific points aren't passed
             localTeamManager.SpawnSpecificTeam(playerTeam, localTeamManager.GetCharacterSpawnPos(), "Hero"); // Accessing public getter
             
             // Spawn Enemy (Need points)
             if (localTeamManager.enemySpawnPos != null && localTeamManager.enemySpawnPos.Length > 0)
             {
                 localTeamManager.SpawnSpecificTeam(enemyTeam, localTeamManager.enemySpawnPos, "Enemy");
             }
             else
             {
                 Debug.LogWarning("Enemy Spawn Positions are not assigned in TeamDataManager!");
             }
        }
        
        // Wait for spawns to complete (simple delay or event wait)
        yield return new WaitForSeconds(0.5f); 

        // 3. Register Units logic check
        // (Assuming TeamDataManager or Unit.Start registers them)
        
        // 4. Calculate CC
        CalculateCombatPower();

        // 4. Calculate CC
        CalculateCombatPower();

        // 4b. Initialize Deck (New Deck System)
        if (CardDeckManager.Instance != null)
        {
            // We need the player units list again. CalculateCombatPower gets it from Registry.
            // Let's grab it here or Cache it.
            var playerUnits = FighterRegistry.Instance.GetTeam("Hero");
            CardDeckManager.Instance.InitializeDeck(playerUnits);
        }
        else
        {
            Debug.LogWarning("CardDeckManager instance not found!");
        }

        // 5. Decision
        CurrentState = BattleState.CombatDecision;
        DecideTurnOrder();
        
        // 6. Intro UI
        CurrentState = BattleState.IntroSequence;
        if (uiController != null)
        {
            // Get Player 1 Icon
            Sprite p1Icon = null;
            // playerTeam is already defined above in scope
            var p1Data = playerTeam.FirstOrDefault(x => x.slotPosition == 0);
            if (p1Data != null && p1Data.character != null)
            {
                p1Icon = p1Data.character.FighterIcon;
            }

            // Get Enemy 1 Icon
            Sprite e1Icon = null;
            // enemyTeam is already defined above in scope
            var e1Data = enemyTeam.FirstOrDefault(x => x.slotPosition == 0);
            if (e1Data != null && e1Data.character != null)
            {
                e1Icon = e1Data.character.FighterIcon;
            }

            uiController.ShowStartSequence(_playerTotalCC, _enemyTotalCC, IsPlayerFirst, p1Icon, e1Icon);
            yield return new WaitForSeconds(3.5f); // Wait for UI animation (approx count + delay)
        }
        else
        {
             yield return new WaitForSeconds(1.0f);
        }

        // 7. Start Battle
        CurrentState = BattleState.BattleStart;
        Debug.Log($"Battle Star! Player First: {IsPlayerFirst}");
        
        // Show Hand UI now that battle has started
        if (uiController != null) uiController.RefreshHandUI();
        
        // Handover to Turn Manager (GameManager for now)
    }

    private void CalculateCombatPower()
    {
        _playerTotalCC = 0;
        _enemyTotalCC = 0;

        if (FighterRegistry.Instance == null)
        {
            Debug.LogError("FighterRegistry.Instance is NULL! Make sure FighterRegistry script is in the scene.");
            return;
        }

        var playerUnits = FighterRegistry.Instance.GetTeam("Hero");
        var enemyUnits = FighterRegistry.Instance.GetTeam("Enemy");

        foreach (var unit in playerUnits)
        {
            _playerTotalCC += unit.Stats.classPower;
            Debug.Log($"[BattleManager] Found Player Unit {unit.name} with CC: {unit.Stats.classPower}");
        }

        foreach (var unit in enemyUnits)
        {
            _enemyTotalCC += unit.Stats.classPower;
            Debug.Log($"[BattleManager] Found Enemy Unit {unit.name} with CC: {unit.Stats.classPower}");
        }

        Debug.Log($"Player CC: {_playerTotalCC} | Enemy CC: {_enemyTotalCC}");
    }

    private void DecideTurnOrder()
    {
        if (_playerTotalCC >= _enemyTotalCC)
            IsPlayerFirst = true;
        else
            IsPlayerFirst = false;
            
        Debug.Log($"Turn Decision: {(IsPlayerFirst ? "Player" : "Enemy")} goes first.");
    }
}
