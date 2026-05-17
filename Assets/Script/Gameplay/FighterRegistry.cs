using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class FighterRegistry : MonoBehaviour
{
    public static FighterRegistry Instance { get; private set; }

    private List<Unit> _allFighters = new List<Unit>();

    public IReadOnlyList<Unit> AllFighters => _allFighters;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void RegisterFighter(Unit fighter)
    {
        if (!_allFighters.Contains(fighter))
        {
            _allFighters.Add(fighter);
            Debug.Log($"[FighterRegistry] Registered: {fighter.name}");
        }
    }

    public void UnregisterFighter(Unit fighter)
    {
        if (_allFighters.Contains(fighter))
        {
            _allFighters.Remove(fighter);
        }
    }

    public List<Unit> GetTeam(string tag)
    {
        return _allFighters.Where(f => f.CompareTag(tag)).ToList();
    }
    
    public void ClearRegistry()
    {
        _allFighters.Clear();
    }
}
