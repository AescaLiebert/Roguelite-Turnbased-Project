using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CardDeckManager : MonoBehaviour
{
    public static CardDeckManager Instance { get; private set; }

    [Header("Deck State")]
    [SerializeField] private List<RuntimeCard> deck = new List<RuntimeCard>();
    [SerializeField] private List<RuntimeCard> hand = new List<RuntimeCard>();
    [SerializeField] private List<RuntimeCard> actionQueue = new List<RuntimeCard>();

    [SerializeField] private int maxHandSize;
    [SerializeField] private int maxActionSlots = 3;

    public List<RuntimeCard> GetHand() => hand;
    public List<RuntimeCard> GetActionQueue() => actionQueue;

    public bool MoveCardToActionQueue(RuntimeCard card)
    {
        if (actionQueue.Count >= maxActionSlots) return false;
        if (!hand.Contains(card)) return false;

        hand.Remove(card);
        actionQueue.Add(card);
        return true;
    }

    public void RemoveCardFromActionQueue(RuntimeCard card)
    {
        if (actionQueue.Contains(card))
        {
            actionQueue.Remove(card);
            // Optionally return to hand? For now just remove (used/cancelled)
            // If cancelled, usually goes back to hand. 
            // Let's assume this is strictly for "Using" it unless specified otherwise.
        }
    }

    // Called by UI when drag-reorder happens
    public void ReorderHand(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= hand.Count || newIndex < 0 || newIndex >= hand.Count) return;

        RuntimeCard card = hand[oldIndex];
        hand.RemoveAt(oldIndex);
        hand.Insert(newIndex, card);
    }
    
    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void InitializeDeck(List<Unit> playerTeam)
    {
        deck.Clear();
        hand.Clear();

        // 1. Calculate Max Hand Size
        // We use the TOTAL active active + sub count from Manager
        // Rule: 1 char = 4, 3 char = 6, 4 char = 7 (Count + 3 generally, assuming full team)
        // If we want it based on Total Team Length:
        int totalTeamCount = TeamDataManager.Instance != null ? TeamDataManager.Instance.GetTotalTeamCount() : playerTeam.Count;
        maxHandSize = totalTeamCount + 3;
        
        Debug.Log($"[CardDeckManager] Initializing Deck. Total Team Size: {totalTeamCount}, Max Hand: {maxHandSize}");

        // 2. Identify Active Units 
        // Use the Manager's ActiveAlive List if available to ensure sync
        List<Unit> activeUnits = new List<Unit>();
        if (TeamDataManager.Instance != null)
        {
            activeUnits = TeamDataManager.Instance.GetActiveAliveUnits();
        }
        else
        {
            // Fallback
             activeUnits = playerTeam.Where(u => u.SlotPosition < 3 && !u.IsDead).ToList();
        }
        
        // 3. Initial Fixed Sorting: Pos3 -> Pos2 -> Pos1 (Skill 2, then Skill 1) (Reverse order usually)
        // Adjust logic if needed. Original: OrderByDescending SlotPosition
        var sortedUnits = activeUnits.OrderByDescending(u => u.SlotPosition).ToList();
        
        foreach (var unit in sortedUnits)
        {
            if (unit.GetSkill2() != null)
                hand.Add(new RuntimeCard(unit, unit.GetSkill2(), 1));
                
            if (unit.GetSkill1() != null)
                hand.Add(new RuntimeCard(unit, unit.GetSkill1(), 1));
        }
        
        // 4. Fill Hand randomly from Active Units
        int safetyCounter = 0;
        while (hand.Count < maxHandSize && safetyCounter < 100)
        {
            safetyCounter++;
            
            // Draw Dynamic Card
            RuntimeCard newCard = DrawRandomCard(activeUnits); 
            if (newCard != null)
            {
                hand.Add(newCard);
            }
            else
            {
               // Debug.LogWarning("[CardDeckManager] DrawRandomCard returned null. Stopping fill.");
                break; 
            }
        }
        
        Debug.Log($"[CardDeckManager] Deck Initialized. Hand Count: {hand.Count}");
    }

    public void CheckRefillHand(List<Unit> playerTeam)
    {
         // Call this at start of turn / end of turn
         // Use only currently active living units
         List<Unit> activeUnits = new List<Unit>();
         if (TeamDataManager.Instance != null)
         {
             activeUnits = TeamDataManager.Instance.GetActiveAliveUnits();
         }
         else
         {
             activeUnits = playerTeam.Where(u => u.SlotPosition < 3 && !u.IsDead).ToList();
         }

         int safetyCounter = 0;
         while (hand.Count < maxHandSize && safetyCounter < 100)
         {
             safetyCounter++;
             RuntimeCard newCard = DrawRandomCard(activeUnits);
             if (newCard != null) hand.Add(newCard);
             else break;
         }
    }

    private RuntimeCard DrawRandomCard(List<Unit> eligibleUnits)
    {
        // Filter for units that actually have skills and are not dead (Double check)
        var unitsWithSkills = eligibleUnits.Where(u => !u.IsDead && (u.GetSkill1() != null || u.GetSkill2() != null)).ToList();
        
        if (unitsWithSkills.Count == 0) return null;

        Unit randomUnit = unitsWithSkills[UnityEngine.Random.Range(0, unitsWithSkills.Count)];
        
        // Pick random skill
        List<SkillCardSO> availableSkills = new List<SkillCardSO>();
        if (randomUnit.GetSkill1() != null) availableSkills.Add(randomUnit.GetSkill1());
        if (randomUnit.GetSkill2() != null) availableSkills.Add(randomUnit.GetSkill2());
        
        if (availableSkills.Count > 0)
        {
             SkillCardSO pickedSkill = availableSkills[UnityEngine.Random.Range(0, availableSkills.Count)];
             return new RuntimeCard(randomUnit, pickedSkill, 1);
        }

        return null;
    }
}
