using FightingAllstar.Core.Combat;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        public BattleEntityPresenter Entities { get; private set; }
        private void InitializeEntities()
        {
            Entities = GetComponent<BattleEntityPresenter>();
            if (Entities == null) Entities = gameObject.AddComponent<BattleEntityPresenter>();
            Entities.Initialize(this);
        }
        public bool TryFighterPosition(string id, out Vector3 position)
        { position = Vector3.zero; if (!TryView(id,out var view)) return false; position=view.position;return true; }
        public bool TryEntityPlacement(FighterState owner, BattleEntityPresentationProfile profile, out Pose placement)
        {
            placement = default;
            if (owner == null || !_poses.TryGetValue(owner.Id, out var home)) return false;
            var direction = Vector3.ProjectOnPlane(_arenaCenter-home.position,Vector3.up).normalized;
            if (direction.sqrMagnitude < .01f) direction = owner.Side == TeamSide.Player ? Vector3.forward : Vector3.back;
            var rotation = Quaternion.LookRotation(direction,Vector3.up);
            var anchor = home.position;
            if (profile != null && profile.teamFront)
            {
                var center=Vector3.zero;var count=0;
                foreach (var pair in _poses) if (_teams.TryGetValue(pair.Key,out var side) && side == owner.Side)
                { center+=pair.Value.position;count++; }
                if (count>0) anchor = center/count;
            }
            placement = new Pose(anchor+rotation*(profile != null ? profile.placementOffset : new Vector3(1,0,1)),rotation);
            return true;
        }
    }
}
