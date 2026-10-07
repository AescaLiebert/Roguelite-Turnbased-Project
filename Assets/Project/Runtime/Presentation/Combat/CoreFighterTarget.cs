using System;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Identifies models for the battle controller's tap/hold arbitration.</summary>
    public sealed class CoreFighterTarget : MonoBehaviour
    {
        private string _fighterId;
        private Action<string> _onSelected;

        public void Initialize(string fighterId, Action<string> onSelected)
        {
            _fighterId = fighterId;
            _onSelected = onSelected;
        }

        public string FighterId => _fighterId;

        public void Select()
        {
            if (enabled && gameObject.activeInHierarchy && !string.IsNullOrEmpty(_fighterId))
                _onSelected?.Invoke(_fighterId);
        }

    }
}
