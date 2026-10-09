using FightingAllstar.Core.Combat;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Formation-facing actor root, with imported model orientation underneath it.</summary>
    public static class BattleFighterView
    {
        public static Quaternion FormationRotation(TeamSide side)
        {
            var player = GameObject.Find("CharHeroPosition2");
            var enemy = GameObject.Find("EnemyHeroPosition2");
            // The no-anchor spawn positions use the X axis.
            var direction = player != null && enemy != null
                ? enemy.transform.position - player.transform.position : Vector3.right;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) direction = Vector3.right;
            if (side != TeamSide.Player) direction = -direction;
            return Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public static GameObject Create(GameObject prefab, Vector3 position, Quaternion formationRotation,
            float modelYawOffset, Mesh mesh = null, Material material = null)
        {
            var actor = new GameObject("BattleFighter");
            actor.transform.SetPositionAndRotation(position, formationRotation);
            // The correction must sit above the Animator's binding root. Imported idle
            // clips can key their own root rotation back to zero on every frame.
            var visual = new GameObject("Model");
            visual.transform.SetParent(actor.transform, false);
            visual.transform.localRotation = Quaternion.Euler(0, modelYawOffset, 0);
            var model = Object.Instantiate(prefab, visual.transform, false);
            model.name = "Rig";
            model.transform.localPosition = Vector3.zero;
            // Keep imported scale/Animator on the model. Only the actor root moves/turns
            // during card execution, so authored model animation cannot flip camera facing.
            var filter = model.GetComponent<MeshFilter>();
            if (filter != null && mesh != null) filter.sharedMesh = mesh;
            var renderer = model.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return actor;
        }
    }
}
