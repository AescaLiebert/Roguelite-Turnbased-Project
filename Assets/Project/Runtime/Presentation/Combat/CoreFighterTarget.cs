using System;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Forwards clicks on an opponent model to the Core battle planner.</summary>
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

        private void OnMouseDown()
        {
            Select();
        }
    }
}
