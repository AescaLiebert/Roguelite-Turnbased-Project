using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>One scene lighting owner; does not change project pipeline settings.</summary>
    [ExecuteAlways,DisallowMultipleComponent]
    public sealed class TeyvatLightingRig:MonoBehaviour
    {
        public TeyvatLightingProfile profile;public Light sun;public Volume volume;public TeyvatMaterialGlobals globals;
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;Apply();}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeforeCamera;Shader.SetGlobalFloat("_TG_StyleEnabled",0);}
        void OnValidate(){Apply();}
        void BeforeCamera(ScriptableRenderContext context,Camera camera){if(camera.gameObject.scene==gameObject.scene||camera.cameraType==CameraType.SceneView)Apply();}
        public void Apply()
        {
            if(!profile)return;
            if(sun){sun.transform.rotation=Quaternion.Euler(profile.sunEuler);sun.color=profile.sunColor;sun.intensity=profile.sunIntensity;RenderSettings.sun=sun;Shader.SetGlobalVector("_TG_SunDirection",-sun.transform.forward);}
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=profile.ambientSky;RenderSettings.ambientEquatorColor=profile.ambientEquator;RenderSettings.ambientGroundColor=profile.ambientGround;
            // Explicit analytic SH for deterministic ambient fallback; baked
            // legacy actor probes are independent per-renderer inputs.
            var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(profile.ambientEquator.linear*.55f);sh.AddDirectionalLight(Vector3.up,profile.ambientSky.linear,.55f);sh.AddDirectionalLight(Vector3.down,profile.ambientGround.linear,.22f);RenderSettings.ambientProbe=sh;
            RenderSettings.skybox=profile.skybox;RenderSettings.fog=profile.fog;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=profile.fogColor;RenderSettings.fogStartDistance=profile.fogStart;RenderSettings.fogEndDistance=profile.fogEnd;RenderSettings.reflectionIntensity=profile.reflectionIntensity;
            Shader.SetGlobalFloat("_TG_StyleEnabled",1);Shader.SetGlobalVector("_TG_LightStyle",profile.lightStyle);Shader.SetGlobalVector("_TG_ShadowTint",new Vector4(profile.shadowTint.r,profile.shadowTint.g,profile.shadowTint.b,profile.shadowTint.a));
            Shader.SetGlobalVector("_TG_CloudShadow",profile.cloudShadow);
            var floor=profile.ambientFloor;Shader.SetGlobalVector("_TG_AmbientFloor",new Vector4(floor.r,floor.g,floor.b,1));
            if(globals){globals.rockTint=profile.rockTint;globals.Apply();}
            if(volume&&volume.sharedProfile!=profile.volume)volume.sharedProfile=profile.volume;
        }
    }
}
