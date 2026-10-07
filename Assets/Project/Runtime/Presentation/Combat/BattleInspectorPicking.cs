using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Finds inspectable models on either side, independent of enemy targeting rules.</summary>
    public static class BattleInspectorPicking
    {
        public static string Pick(Camera camera, Vector2 point, IReadOnlyDictionary<string,GameObject> views)
        {
            if(camera==null || views==null)return null;
            string nearest=null;float depth=float.MaxValue;
            foreach(var hit in Physics.RaycastAll(camera.ScreenPointToRay(point),1000f,~0,QueryTriggerInteraction.Collide))
                foreach(var pair in views)
                    if(pair.Value!=null && pair.Value.activeInHierarchy &&
                        (hit.transform==pair.Value.transform || hit.transform.IsChildOf(pair.Value.transform)) && hit.distance<depth)
                    {nearest=pair.Key;depth=hit.distance;}
            if(nearest!=null)return nearest;
            // Comfortable fallback for animated models with narrow or absent colliders.
            float distance=64f*Mathf.Max(.75f,Screen.height/900f);
            foreach(var pair in views)
            {
                if(pair.Value==null || !pair.Value.activeInHierarchy)continue;
                var screen=camera.WorldToScreenPoint(pair.Value.transform.position+Vector3.up);
                if(screen.z<=0)continue;
                float d=Vector2.Distance(point,screen);
                if(d<distance){distance=d;nearest=pair.Key;}
            }
            return nearest;
        }
    }
}
