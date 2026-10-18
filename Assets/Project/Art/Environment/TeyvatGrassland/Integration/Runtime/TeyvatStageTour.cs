using System;
using UnityEngine;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>Review-only camera playback; excluded from the environment prefab.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TeyvatStageTour:MonoBehaviour
    {
        [Serializable] public struct Shot{public string name;public Vector3 position;public Quaternion rotation;public float fieldOfView;}
        public Shot[] shots=Array.Empty<Shot>();public Vector3 arenaOrigin;
        [Min(.1f)] public float secondsPerShot=2.5f;
        Camera view;
        void OnEnable(){view=GetComponent<Camera>();if(Application.isPlaying)view.enabled=true;}
        void Update(){if(Application.isPlaying)Evaluate(Time.time);}
        public void Evaluate(float seconds)
        {
            if(shots==null||shots.Length==0)return;if(view==null)view=GetComponent<Camera>();var phase=seconds/secondsPerShot;var i=Mathf.FloorToInt(phase)%shots.Length;var j=(i+1)%shots.Length;var t=Mathf.SmoothStep(0,1,phase-Mathf.Floor(phase));
            var a=shots[i];var b=shots[j];transform.SetPositionAndRotation(Orbit(a.position,b.position,arenaOrigin,t),Quaternion.Slerp(a.rotation,b.rotation,t));view.fieldOfView=Mathf.Lerp(a.fieldOfView,b.fieldOfView,t);
        }
        public static Vector3 Orbit(Vector3 from,Vector3 to,Vector3 center,float t)
        {
            var a=from-center;var b=to-center;var aa=Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg;var bb=Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg;var yaw=(aa+Mathf.DeltaAngle(aa,bb)*t)*Mathf.Deg2Rad;var pitch=Mathf.Lerp(Mathf.Asin(a.y/a.magnitude),Mathf.Asin(b.y/b.magnitude),t);var radius=Mathf.Lerp(a.magnitude,b.magnitude,t);
            return center+new Vector3(Mathf.Sin(yaw)*Mathf.Cos(pitch),Mathf.Sin(pitch),Mathf.Cos(yaw)*Mathf.Cos(pitch))*radius;
        }
    }
}
