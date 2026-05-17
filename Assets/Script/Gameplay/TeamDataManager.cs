using System.Collections.Generic;
using UnityEngine;

public enum CharacterBattleState
{
    Active,
    Sub,
    Dead
}

[System.Serializable]
    public class TransferTeamData
    {
        public int slotPosition;
        public int characterId;
        public CharacterObject character;

        public TransferTeamData(CharacterObject characterdata, int slot, int charId)
        {
            slotPosition = slot;
            characterId = charId;
            character = characterdata;
        }
    }

[System.Serializable]
    public class InGameCharacterData
    {
        public int slotPosition;
        public CharacterObject character;
        public bool isAlive;
        public CharacterBattleState currentBattleState;
        public GameObject characterInstance;
        //public SlotPositonInBattle SpawnPos;

        public InGameCharacterData(CharacterObject characterInGame,GameObject instance, int slot)
        {
            slotPosition = slot;
            character = characterInGame;
            characterInstance = instance;
            isAlive = true;
            // Default state set later or based on slot?
            // Initialize simple default
            currentBattleState = CharacterBattleState.Active; 
        }
    }


    // Manager class that persists between scenes
    public class TeamDataManager : MonoBehaviour, IBattleDataProvider
    {
        private static TeamDataManager _instance;
        public static TeamDataManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TeamDataManager();
                }
                return _instance;
            }
        }

        [SerializeField] private Transform[] characterSpawnPos;
        public Transform[] enemySpawnPos; // Add enemy spawn positions
        [SerializeField] private GameObject universalUnitPrefab; // The container with Unit.cs
        [SerializeField] private float spawnHeightOffset = 0.0f;

        // Store the selected team data
        private static List<TransferTeamData> _selectedTeam = new List<TransferTeamData>();

        [SerializeField] private List<InGameCharacterData> totalTeamList = new List<InGameCharacterData>(); // Stores EVERYONE
        [SerializeField] private List<InGameCharacterData> inGameActiveTeam = new List<InGameCharacterData>(); // Stores only ON FIELD alive/dead
        
        public List<InGameCharacterData> TotalTeam => totalTeamList;
        public List<InGameCharacterData> ActiveTeam => inGameActiveTeam;
        
        // Reference to character database/inventory
        [SerializeField] private InventoryObject characterDatabase;
        


        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            
            // Invoke(nameof(InitializeTeam), 0.1f); // REMOVED: Legacy auto-spawn causing duplicates
        }

        // Save current team setup
        public void SaveTeamSetup(List<CharacterSlot> characterSlots)
        {
            _selectedTeam.Clear();
            
            for (int slotdata = 0; slotdata < characterSlots.Count; slotdata++)
            {
                var slot = characterSlots[slotdata];
                if (slot.AssignedCharacter != null)
                {
                    _selectedTeam.Add(new TransferTeamData(slot.AssignedCharacter, slotdata, slot.AssignedCharacter.ID));
                    Debug.Log($"Saved character {slot.AssignedCharacter.FighterName} at position {slotdata}"); // Add this
                }
            }
            
            Debug.Log($"Saved team with {_selectedTeam.Count} characters");
        }
        
        private void SpawnAndAddCharacter(InGameCharacterData slotData, Transform spawnPoint, string teamTag, bool isSub)
        {
            // 1. Determine spawn position
            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = Quaternion.identity;

            if (spawnPoint != null)
            {
                 spawnPosition = spawnPoint.position + new Vector3(0, spawnHeightOffset, 0);
                 spawnRotation = spawnPoint.rotation;
            }
            else
            {
                // Fallback for Sub unit with no point (Spawn far away hidden)
                spawnPosition = new Vector3(0, -500, 0); 
            }

            if (universalUnitPrefab == null)
            {
                Debug.LogError("Universal Unit Prefab is NOT assigned in TeamDataManager! Please assign it in the Inspector.");
                return;
            }
            
            // 2. Instantiate the Universal Unit Container (Holds Unit.cs, HUD, etc.)
            GameObject unitContainer = Instantiate(universalUnitPrefab, spawnPosition, spawnRotation);
            
            // SET TAG
            unitContainer.tag = teamTag;

            // 3. Instantiate the Visual Model (Character Mesh)
            GameObject visualModel = Instantiate(slotData.character.Fighter3DPrefab, unitContainer.transform);
            // Optional: Reset local position/rotation of model inside container
            visualModel.transform.localPosition = Vector3.zero;
            visualModel.transform.localRotation = Quaternion.identity;

            // 4. Register with FighterRegistry
            Unit unitComponent = unitContainer.GetComponent<Unit>();
            if (unitComponent != null)
            {
                // Initialize Card Data
                CharacterObject charData = slotData.character;
                unitComponent.InitializeCardData(charData.Skill1, charData.Skill2, charData.Ultimate);
                
                // Assign Slot Position
                unitComponent.SlotPosition = slotData.slotPosition;

                if (FighterRegistry.Instance != null)
                {
                    FighterRegistry.Instance.RegisterFighter(unitComponent);
                }
                
                // 5. INJECT STATS & BIND ANIMATOR
                unitComponent.Initialize(slotData.character, visualModel);
                
                // 6. Set SUB Status
                unitComponent.IsSubUnit = isSub;

                // Naming for clarity
                unitContainer.name = $"Unit_{slotData.character.FighterName}";
            }
            else
            {
                Debug.LogError("Universal Unit Prefab is missing Unit script!");
            }

            // 7. Handle Sub Unit Visibility (Hidden but Registered)
            if (isSub)
            {
                unitContainer.SetActive(false);
                Debug.Log($"[TeamDataManager] Sub Unit {slotData.character.FighterName} registered but deactivated.");
            }
            
            // 8. Update Data Reference
            slotData.characterInstance = unitContainer;
        }
        

        public List<TransferTeamData> GetLocalPlayerTeam()
        {
            return _selectedTeam;
        }

        public List<TransferTeamData> GetOpponentTeam()
        {
            List<TransferTeamData> enemyTeam = new List<TransferTeamData>();
            
            if (characterDatabase == null)
            {
                Debug.LogError("GetOpponentTeam: Character Database (InventoryObject) is NULL!");
                return enemyTeam;
            }
            
            if (characterDatabase.CharacterAmount == 0)
            {
                Debug.LogError("GetOpponentTeam: Character Database is EMPTY! Add characters to the Codex.");
                return enemyTeam;
            }

            // Create a pool of available indices
            List<int> availableIndices = new List<int>();
            for (int i = 0; i < characterDatabase.CharacterAmount; i++)
            {
                availableIndices.Add(i);
            }

            // Pick up to 4 characters (Pos 0, 1, 2 = Main, 3 = Sub)
            for (int slot = 0; slot < 4; slot++)
            {
                if (availableIndices.Count == 0) break;

                int randomIndex = UnityEngine.Random.Range(0, availableIndices.Count);
                int pickedIndex = availableIndices[randomIndex];
                availableIndices.RemoveAt(randomIndex); // Ensure uniqueness

                CharacterObject pickedChar = characterDatabase.characterContainer[pickedIndex];
                enemyTeam.Add(new TransferTeamData(pickedChar, slot, pickedChar.ID));
            }
            
            Debug.Log($"Generated Enemy Team with {enemyTeam.Count} units.");
            return enemyTeam; 
        }

        
        // Get the saved team data
        public List<TransferTeamData> GetSavedTeam()
        {
            return _selectedTeam;
        }

        // Spawn characters in the battle scene
        public void SpawnTeamInBattle(Transform[] spawnPoints)
        {
            SpawnSpecificTeam(_selectedTeam, spawnPoints, "Hero");
        }

        public void SpawnSpecificTeam(List<TransferTeamData> teamData, Transform[] spawnPoints, string teamTag)
        {
             if (teamData == _selectedTeam)
             {
                 // Reset Player State
                 totalTeamList.Clear();
                 inGameActiveTeam.Clear();
             }

             // Note: using local lists to track for now, but really this should be stateless or tracked per team
             // For this refactor, we just focus on spawning logic
             
            foreach (var selectedData in teamData)
            {
                //Debug.Log(selectedData.slotPosition);
                GameObject characterInstance = selectedData.character.Fighter3DPrefab;
                InGameCharacterData inGameCharacter = new InGameCharacterData(
                        selectedData.character, 
                        characterInstance, 
                        selectedData.slotPosition
                    );

                // Add to Total Team
                if (teamData == _selectedTeam)
                {
                    totalTeamList.Add(inGameCharacter);
                }

                if (selectedData.slotPosition < 3)
                {
                    // Active Team
                    inGameCharacter.currentBattleState = CharacterBattleState.Active;

                    if (teamData == _selectedTeam)
                    {
                        inGameActiveTeam.Add(inGameCharacter);
                    }
                    
                     if (selectedData.slotPosition < spawnPoints.Length)
                     {
                         SpawnAndAddCharacter(inGameCharacter, spawnPoints[selectedData.slotPosition], teamTag, false);
                     }
                }
                else
                {
                    // Sub Team
                    inGameCharacter.currentBattleState = CharacterBattleState.Sub;

                    // Just spawn hidden
                    Transform subPoint = (selectedData.slotPosition < spawnPoints.Length) ? spawnPoints[selectedData.slotPosition] : null;
                    SpawnAndAddCharacter(inGameCharacter, subPoint, teamTag, true); // true = isSub
                }
            }

            Debug.Log($"[TeamDataManager] Spawned {totalTeamList.Count} Total Units, {inGameActiveTeam.Count} Active Units.");
        }

        public void OnUnitDead(Unit deadUnit)
        {
            Debug.Log($"[TeamDataManager] Unit Died: {deadUnit.name}");

            // 1. Find Data for the dead unit
            var deadData = inGameActiveTeam.Find(x => x.characterInstance == deadUnit.gameObject);
            
            if (deadData != null)
            {
                deadData.isAlive = false;
                deadData.currentBattleState = CharacterBattleState.Dead;

                inGameActiveTeam.Remove(deadData); 
                
                // Let's Find a SUB unit
                var subData = totalTeamList.Find(x => x.currentBattleState == CharacterBattleState.Sub && x.isAlive == true);
                
                if (subData != null)
                {
                     Debug.Log($"[TeamDataManager] Found Sub Unit to swap: {subData.character.FighterName}");
                     StartCoroutine(SwapSubToActive(deadUnit, subData));
                }
                else
                {
                    Debug.Log("[TeamDataManager] No available SUB unit to swap.");
                }
            }
        }

        private System.Collections.IEnumerator SwapSubToActive(Unit deadUnit, InGameCharacterData subData)
        {
             yield return new WaitForSeconds(1.5f); // Wait for death animation/effect
             
             // 1. Hide Dead Unit (or verify it destroys itself? Plan said to NOT destroy immediately)
             if (deadUnit != null) deadUnit.gameObject.SetActive(false);

             // 2. Get Spawn Position from Dead Unit
             Vector3 targetPos = deadUnit.transform.position;
             Quaternion targetRot = deadUnit.transform.rotation;

             // 3. Move Sub Unit to Field
             if (subData.characterInstance != null)
             {
                 subData.characterInstance.transform.position = targetPos;
                 subData.characterInstance.transform.rotation = targetRot;
                 subData.characterInstance.SetActive(true);
                 
                 // Update Logic
                 Unit subUnitScript = subData.characterInstance.GetComponent<Unit>();
                 if (subUnitScript != null)
                 {
                     subUnitScript.IsSubUnit = false; 
                     // IMPORTANT: Assign the DEAD UNIT's Slot Position? Or keep its own? 
                     // Usually it takes the slot aesthetically, but logic-wise it might keep 3?
                     // User said: "Current Active Team (not Sub slot anymore) in replace slot that dead unit"
                     // We should probably update the slot position to match the dead one so card sorting works nicely?
                     // Let's keep it simple: It joins ActiveTeam.
                     subUnitScript.SlotPosition = deadUnit.SlotPosition;
                 }
                 
                 inGameActiveTeam.Add(subData);
                 subData.currentBattleState = CharacterBattleState.Active;
                 Debug.Log($"[TeamDataManager] Sub Unit {subData.character.FighterName} Joined Active Team!");
             }
        }

        public List<Unit> GetActiveAliveUnits()
        {
            List<Unit> aliveUnits = new List<Unit>();
            if (FighterRegistry.Instance == null) return aliveUnits;

            var playerUnits = FighterRegistry.Instance.GetTeam("Hero");
            foreach(var unit in playerUnits)
            {
                 // Check if in Active Team (Slot < 3 normally, but dynamic now)
                 // Simplest: Check if not IsSubUnit and not Dead
                 if (!unit.IsDead && !unit.IsSubUnit)
                 {
                     aliveUnits.Add(unit);
                 }
            }
            return aliveUnits;
        }

        public int GetTotalTeamCount()
        {
             // return TotalTeam.Count; // This relies on Create logic
             // Or better, just ask Registry for all Hero units
             return totalTeamList.Count;
        }

        
        // Legacy method - used by old GameManager testing flow
        public void InitializeTeam()
        {
            if (Instance != null)
            {
                // Instance.SpawnTeamInBattle(characterSpawnPos);
                Debug.LogWarning("InitializeTeam called (Legacy). Spawning via this method is disabled to prevent duplicates with BattleManager.");
            }
            else
            {
                Debug.LogError("TeamDataManager instance is null!");
            }
        }

        public Transform[] GetCharacterSpawnPos() => characterSpawnPos;
    }