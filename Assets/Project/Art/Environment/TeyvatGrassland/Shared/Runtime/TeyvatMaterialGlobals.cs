using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EnvironmentArt
{
    [ExecuteAlways]
    public sealed class TeyvatMaterialGlobals : MonoBehaviour
    {
        // Both transform matrices remain available to support nonuniform normals.
        // Stay below Unity's documented 511-instance default and the shader's
        // 500-entry fallback array. Validate actual GPU coverage when changing this.
        public const int InstancesPerSubmission = 500;
        public Texture2D colorField;
        public Vector4 colorBounds = new Vector4(-28,-18,1f/56,1f/66);
        public Vector2 windDirection = new Vector2(.93f,.36f);
        public float windStrength = .4f;
        public Color rockTint = new Color(.64f,.72f,.76f,1);
        public float rockPaintStrength = .35f;
        public float rockWorldScale = .11f;
        public bool fixedTime;
        public float fixedSeconds;
        public Mesh grassMesh;
        public Material grassMaterial;
        public Matrix4x4[] grassInstances = Array.Empty<Matrix4x4>();
        public Bounds grassBounds = new Bounds(new Vector3(0,1,6),new Vector3(54,7,52));
        [NonSerialized] public int submittedInstances;
        [NonSerialized] public int submittedBatches;
        [NonSerialized] public int submissionFrames;

        void OnEnable(){RenderPipelineManager.beginCameraRendering+=Draw;Apply();}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=Draw;}
        void Update(){Apply();}
        public void Apply()
        {
            var d=windDirection.sqrMagnitude>.001f?windDirection.normalized:Vector2.right;
            Shader.SetGlobalTexture("_TG_ColorField",colorField);
            Shader.SetGlobalVector("_TG_ColorBounds",colorBounds);
            Shader.SetGlobalFloat("_TG_ColorFieldEnabled",colorField!=null?1:0);
            Shader.SetGlobalVector("_TG_Wind",new Vector4(d.x,d.y,windStrength,fixedTime?fixedSeconds:Time.realtimeSinceStartup));
            var tint=rockTint.linear;Shader.SetGlobalVector("_TG_RockStyle",new Vector4(tint.r,tint.g,tint.b,rockPaintStrength));
            Shader.SetGlobalFloat("_TG_RockWorldScale",rockWorldScale);
        }
        void Draw(ScriptableRenderContext context,Camera camera)
        {
            if(camera.gameObject.scene!=gameObject.scene)return;
            // Ground/rock/water share these globals even before WP04 supplies grass.
            Apply();
            if(grassMesh==null||grassMaterial==null||grassInstances==null)return;
            submittedInstances=0;submittedBatches=0;submissionFrames++;
            var parameters=new RenderParams(grassMaterial){camera=camera,worldBounds=grassBounds,shadowCastingMode=ShadowCastingMode.On,receiveShadows=true};
            for(var first=0;first<grassInstances.Length;first+=InstancesPerSubmission)
            {
                var count=Mathf.Min(InstancesPerSubmission,grassInstances.Length-first);
                Graphics.RenderMeshInstanced(parameters,grassMesh,0,grassInstances,count,first);
                submittedInstances+=count;submittedBatches++;
            }
        }
        public static Vector3 Displacement(Vector3 world,Color mask,Vector4 wind,float response)
        {
            var phase=world.x*.37f+world.z*.23f+mask.b*6.283185f;
            var bend=mask.r*mask.r*(.65f*Mathf.Sin(wind.w*1.3f+phase)+.15f*Mathf.Sin(wind.w*3.7f+phase*2.1f))
                +mask.g*.35f*Mathf.Sin(wind.w*.85f+phase*.7f);
            var distance=bend*wind.z*response;return new Vector3(wind.x*distance,Mathf.Abs(distance)*.08f*mask.r,wind.y*distance);
        }
    }
}
