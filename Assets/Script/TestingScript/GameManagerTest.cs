using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManagerTest : MonoBehaviour
{
    public Transform[] spawnPoints;
    private void Awake()
    {
        Invoke(nameof(InitializeTeam), 0.1f);
    }
    
    private void InitializeTeam()
    {
        if (TeamDataManager.Instance != null)
        {
            TeamDataManager.Instance.SpawnTeamInBattle(spawnPoints);
        }
        else
        {
            Debug.LogError("TeamDataManager instance is null!");
        }
    }
}
