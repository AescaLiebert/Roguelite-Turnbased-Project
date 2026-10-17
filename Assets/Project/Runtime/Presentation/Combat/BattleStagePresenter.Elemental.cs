using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        // Starting presentation values: the arc must visibly descend, cross wet ground, then climb the next target.
        [Header("Electro-Charged Spread")]
        [SerializeField, Min(.2f)] private float electrifyTravelDuration = .5f;
        [SerializeField, Min(.1f)] private float electrifyFadeDuration = .35f;
        private readonly List<GameObject> _electrifySpreads = new List<GameObject>();
        private Material _electrifyWetMaterial;
        private Mesh _electrifyWetMesh;

        /// <summary>World-space lightning and particles. Recipients come exclusively from committed reaction events.</summary>
        public IEnumerator ElectrifySpread(string originId, string targetId)
        {
            if (!TryView(originId, out var origin) || !TryView(targetId, out var target) ||
                !origin.gameObject.activeInHierarchy || !target.gameObject.activeInHierarchy) yield break;
            EnsureEnergyMaterial();
            if (_energyMaterial == null) yield break;
            EnsureElectrifyWetSurface();
            var from = FighterBounds(origin).center;
            var to = FighterBounds(target).center;
            var fromGround = ElectrifyGround(origin);
            var toGround = ElectrifyGround(target);
            // Put the contact on the visible side of the body, so opaque actor meshes do not hide the discharge.
            var fromFront=Vector3.ProjectOnPlane((_camera!=null?_camera.transform.position:origin.position-Vector3.forward)-origin.position,Vector3.up).normalized;
            var toFront=Vector3.ProjectOnPlane((_camera!=null?_camera.transform.position:target.position-Vector3.forward)-target.position,Vector3.up).normalized;
            from+=fromFront*(FighterRadius(origin)+.12f);to+=toFront*(FighterRadius(target)+.12f);
            fromGround+=fromFront*.4f;toGround+=toFront*.4f;
            var root = new GameObject("Electrify Spread 3D");
            root.transform.SetParent(transform, false);
            _electrifySpreads.Add(root);
            var color = GenshinCombatTextStyle.ElementColor(FightingAllstar.Core.Content.ElementId.Electro);
            var glow = ElectrifyLine(root.transform, "Lightning glow", color, .13f);
            var core = ElectrifyLine(root.transform, "Lightning core", new Color(.92f,.83f,1f), .035f);
            var patches = new[] {
                ElectrifyWetPatch(root.transform, fromGround, .9f),
                ElectrifyWetPatch(root.transform, Vector3.Lerp(fromGround,toGround,.5f), .65f),
                ElectrifyWetPatch(root.transform, toGround, .9f) };
            var rings = new[] { ElectrifyRipple(root.transform,fromGround), ElectrifyRipple(root.transform,toGround) };
            var particles = ElectrifyParticles(root.transform, color);
            var tint = new MaterialPropertyBlock();
            var elapsed = 0f;
            var travel = Mathf.Max(.2f,electrifyTravelDuration);
            var duration = travel + Mathf.Max(.1f,electrifyFadeDuration);
            PlayCue(19);
            try
            {
                while (root != null && root.activeSelf && elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var progress = Mathf.Clamp01(elapsed / travel);
                    var fade = 1f - Mathf.Clamp01((elapsed-travel) / Mathf.Max(.1f,electrifyFadeDuration));
                    var positions = new Vector3[37];
                    for (var i=0;i<positions.Length;i++)
                    {
                        var p = progress * i / (positions.Length-1);
                        var point = ElectrifyPath(from,fromGround,toGround,to,p);
                        var envelope = Mathf.Sin(i*Mathf.PI/(positions.Length-1));
                        var flicker = Mathf.Floor(elapsed*35);
                        var jitter = new Vector3(Mathf.Sin(i*17.3f+flicker*2.1f),0,Mathf.Cos(i*11.9f+flicker*1.7f));
                        point += jitter * (.07f*envelope);
                        if (p>.22f && p<.78f) point.y += Mathf.Abs(Mathf.Sin(i*2.3f+flicker))*.025f;
                        positions[i] = point;
                    }
                    glow.SetPositions(positions); core.SetPositions(positions);
                    glow.startColor=glow.endColor=new Color(color.r,color.g,color.b,.55f*fade);
                    core.startColor=core.endColor=new Color(.94f,.86f,1f,fade);
                    for(var i=0;i<patches.Length;i++)
                    {
                        var revealed = Mathf.Clamp01((progress-(i==0?.1f:i==1?.4f:.7f))*8);
                        tint.SetColor("_Color",new Color(.08f,.4f,.7f,.24f*revealed*fade));
                        tint.SetColor("_BaseColor",new Color(.08f,.4f,.7f,.24f*revealed*fade));
                        patches[i].SetPropertyBlock(tint);
                    }
                    for(var i=0;i<rings.Length;i++)
                    {
                        var scale=Mathf.Clamp01((progress-(i==0?.15f:.75f))*5);
                        rings[i].transform.localScale=Vector3.one*(.45f+.8f*scale);
                        rings[i].startColor=rings[i].endColor=new Color(.35f,.8f,1f,.48f*scale*fade);
                    }
                    if (progress<1)
                    {
                        var head=ElectrifyPath(from,fromGround,toGround,to,progress);
                        for(var i=0;i<3;i++) particles.Emit(new ParticleSystem.EmitParams {
                            position=head, velocity=new Vector3(Mathf.Sin(elapsed*67+i)*.8f,.5f,Mathf.Cos(elapsed*53+i)*.8f),
                            startColor=i==0?new Color(.85f,.95f,1f):color, startSize=.045f+i*.015f, startLifetime=.22f },1);
                    }
                    yield return null;
                }
            }
            finally
            {
                _electrifySpreads.Remove(root);
                if(root!=null) { root.SetActive(false); ActorVisualEffectGeometry.Release(root); }
            }
        }

        private static Vector3 ElectrifyPath(Vector3 from,Vector3 fromGround,Vector3 toGround,Vector3 to,float progress)
        {
            if(progress<.22f)return Vector3.Lerp(from,fromGround,progress/.22f);
            if(progress<.78f)return Vector3.Lerp(fromGround,toGround,(progress-.22f)/.56f);
            return Vector3.Lerp(toGround,to,(progress-.78f)/.22f);
        }

        private Vector3 ElectrifyGround(Transform fighter)
        {
            var ground=FighterGroundPosition(fighter);
            var hits=Physics.RaycastAll(ground+Vector3.up*3,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
            var nearest=float.MaxValue;
            foreach(var hit in hits)
            {
                var actor=false;
                foreach(var view in _views.Values)
                    if(view!=null && hit.transform.IsChildOf(view.transform)) { actor=true;break; }
                if(actor || hit.distance>=nearest)continue;
                nearest=hit.distance;ground=hit.point;
            }
            return ground+Vector3.up*.045f;
        }

        private LineRenderer ElectrifyLine(Transform parent,string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.sharedMaterial=_energyMaterial;line.useWorldSpace=true;line.positionCount=37;line.widthMultiplier=width;
            line.startColor=line.endColor=color;line.numCornerVertices=2;line.numCapVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            return line;
        }

        private ParticleSystem ElectrifyParticles(Transform parent,Color color)
        {
            var particles=new GameObject("Electrify sparks").AddComponent<ParticleSystem>();particles.transform.SetParent(parent,false);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=false;main.duration=1;main.startLifetime=.22f;main.startSpeed=0;main.startSize=.06f;
            main.startColor=color;main.maxParticles=160;main.simulationSpace=ParticleSystemSimulationSpace.World;main.useUnscaledTime=true;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var fade=particles.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(color,1) },
                new[] { new GradientAlphaKey(1,0),new GradientAlphaKey(0,1) });fade.color=gradient;
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_energyMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.08f;
            particles.Play();return particles;
        }

        private void EnsureElectrifyWetSurface()
        {
            if(_electrifyWetMaterial==null)
            {
                _electrifyWetMaterial=new Material(_energyMaterial) { name="Electrify Wet Ground",hideFlags=HideFlags.DontSave };
                ConfigureTransparentMaterial(_electrifyWetMaterial);
            }
            if(_electrifyWetMesh!=null)return;
            var vertices=new Vector3[25];var triangles=new int[72];var colors=new Color[25];colors[0]=Color.white;
            for(var i=0;i<24;i++)
            {
                var angle=i*Mathf.PI*2/24;var radius=1+.1f*Mathf.Sin(angle*7);
                vertices[i+1]=new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius*.7f);colors[i+1]=new Color(1,1,1,0);
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%24+1;triangles[i*3+2]=i+1;
            }
            _electrifyWetMesh=new Mesh { name="Electrify wet patch",hideFlags=HideFlags.DontSave };
            _electrifyWetMesh.vertices=vertices;_electrifyWetMesh.triangles=triangles;_electrifyWetMesh.colors=colors;
            _electrifyWetMesh.RecalculateNormals();_electrifyWetMesh.RecalculateBounds();
        }

        private MeshRenderer ElectrifyWetPatch(Transform parent,Vector3 position,float radius)
        {
            var patch=new GameObject("Wet ground sheen");patch.transform.SetParent(parent,false);patch.transform.position=position;
            patch.transform.localScale=Vector3.one*radius;patch.AddComponent<MeshFilter>().sharedMesh=_electrifyWetMesh;
            var renderer=patch.AddComponent<MeshRenderer>();renderer.sharedMaterial=_electrifyWetMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }

        private LineRenderer ElectrifyRipple(Transform parent,Vector3 position)
        {
            var ring=ElectrifyLine(parent,"Wet ground ripple",new Color(.35f,.8f,1f,0),.025f);
            ring.transform.position=position;ring.useWorldSpace=false;ring.loop=true;ring.positionCount=32;
            for(var i=0;i<32;i++) { var angle=i*Mathf.PI*2/32;ring.SetPosition(i,new Vector3(Mathf.Cos(angle),.005f,Mathf.Sin(angle))); }
            return ring;
        }

        private void ClearElectrifySpreads()
        {
            foreach(var root in _electrifySpreads)if(root!=null) { root.SetActive(false);ActorVisualEffectGeometry.Release(root); }
            _electrifySpreads.Clear();
        }
    }
}
