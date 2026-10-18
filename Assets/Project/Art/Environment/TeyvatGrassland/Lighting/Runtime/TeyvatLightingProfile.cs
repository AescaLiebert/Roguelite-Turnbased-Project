using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EnvironmentArt
{
    [CreateAssetMenu(menuName="Teyvat/Lighting profile",fileName="TG_LightingProfile")]
    public sealed class TeyvatLightingProfile:ScriptableObject
    {
        public Vector3 sunEuler=new Vector3(52,145,0);
        public Color sunColor=new Color(1,1,.99f);public float sunIntensity=1.7f;
        public Color ambientSky=new Color(.41f,.64f,.84f),ambientEquator=new Color(.27f,.44f,.51f),ambientGround=new Color(.19f,.29f,.25f);
        public Color fogColor=new Color(.48f,.72f,.86f);public float fogStart=140,fogEnd=650;
        public bool fog=true;public Material skybox;public VolumeProfile volume;
        public Vector4 lightStyle=new Vector4(.85f,1.10f,.16f,1.15f);
        public Color shadowTint=new Color(.63f,.82f,1,.64f);
        public Color ambientFloor=new Color(.08f,.105f,.13f);
        public Color rockTint=new Color(.64f,.72f,.78f);
        public Vector4 cloudShadow=new Vector4(.021f,.018f,.16f,1);
        public float reflectionIntensity=.65f;
    }
}
