using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleHUDManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnitHUD unitHUDPrefab;
    [SerializeField] private Transform hudContainer; // Canvas Transform

    [Header("Settings")]
    [SerializeField] private bool autoScanOnStart = true;

    private List<UnitHUD> activeHUDs = new List<UnitHUD>();

    private void Start()
    {
        if (hudContainer == null) hudContainer = transform;
        
        // Ensure this container is drawn behind other UI (First sibling = first rendered)
        hudContainer.SetAsFirstSibling(); 
        
        // Or if the container is the object itself
        transform.SetAsFirstSibling();

        if (autoScanOnStart)
        {
            // Wait a frame or two for units to spawn/initialize
            StartCoroutine(ScanAndCreateHUDs());
        }
    }

    private void LateUpdate()
    {
        // Toggle visibility based on Battle State
        if (BattleManager.Instance != null && hudContainer != null)
        {
            // Changed from TurnLoop to BattleStart as requested
            bool shouldShow = BattleManager.Instance.CurrentState == BattleState.BattleStart || BattleManager.Instance.CurrentState == BattleState.TurnLoop;
            
            // Optimization: Only set active if state changes to avoid dirty layout flags
            if (hudContainer.gameObject.activeSelf != shouldShow)
            {
                 hudContainer.gameObject.SetActive(shouldShow);
            }
        }
    }

    public void RefreshHUDs()
    {
        StartCoroutine(ScanAndCreateHUDs());
    }

    private IEnumerator ScanAndCreateHUDs()
    {
        // Wait for units to be fully initialized
        yield return new WaitForSeconds(0.5f);

        // Clear existing
        foreach (var hud in activeHUDs)
        {
            if (hud != null) Destroy(hud.gameObject);
        }
        activeHUDs.Clear();

        // Find all Units
        Unit[] units = FindObjectsOfType<Unit>();
        
        foreach (var unit in units)
        {
            if (unit.GetDead()) continue;

            CreateHUD(unit);
        }
    }

    private void CreateHUD(Unit unit)
    {
        if (unitHUDPrefab == null)
        {
            Debug.LogError("UnitHUD Prefab not assigned in BattleHUDManager!");
            return;
        }

        UnitHUD newHUD = Instantiate(unitHUDPrefab, hudContainer);
        newHUD.Initialize(unit);
        activeHUDs.Add(newHUD);
        
        newHUD.gameObject.name = $"HUD_{unit.gameObject.name}";
    }
}
