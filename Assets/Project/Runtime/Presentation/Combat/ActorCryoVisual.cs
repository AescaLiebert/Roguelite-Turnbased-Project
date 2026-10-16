using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Actor-sized procedural Cryo effects. Removal owns an exit phase; resets dispose immediately.</summary>
    internal sealed class ActorCryoVisual : IDisposable
    {
        private sealed class Crystal
        {
            public Transform Transform;
            public Vector3 Position, Scale, Scatter;
            public Quaternion Rotation;
        }

        public readonly ActorVisualEffectKind Kind;
        public readonly Transform Root;
        public bool Active { get; private set; }
        public bool Entering { get; private set; }
        public bool Exiting { get; private set; }
        private readonly ActorVisualEffectSettings _settings;
        private readonly float _height, _radius;
        private readonly Color _color;
        private readonly Material _ice, _mistMaterial, _snowMaterial, _lineMaterial;
        private readonly List<Crystal> _crystals = new List<Crystal>();
        private readonly List<LineRenderer> _ribbons = new List<LineRenderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly ParticleSystem _mist, _snow, _burst;
        private float _started, _entered, _exited;
        private bool _hidden;

        public ActorCryoVisual(Transform actor, ActorVisualEffectKind kind, float height, float radius,
            float groundOffset, ActorVisualEffectSettings settings)
        {
            Kind=kind; _height=height; _radius=radius; _settings=settings;
            _color=kind==ActorVisualEffectKind.Freeze ? settings.Freeze : kind==ActorVisualEffectKind.FrostBite ? settings.FrostBite : settings.GlacialWaltz;
            Root=new GameObject("AVE "+kind).transform;
            Root.SetParent(actor,false); Root.localPosition=Vector3.up*groundOffset;
            Root.rotation=Quaternion.identity;
            var shader=Resources.Load<Shader>("ActorCryo");
            _ice=Material(shader,0,"Ice facets"); _mistMaterial=Material(shader,1,"Cold vapor");
            _snowMaterial=Material(shader,2,"Ice flecks");
            _lineMaterial=new Material(Resources.Load<Shader>("ActorVisualEffects")) { name="AVE Cryo highlights",hideFlags=HideFlags.DontSave };
            var crystalMesh=CrystalMesh(); _meshes.Add(crystalMesh);
            if (kind==ActorVisualEffectKind.Freeze)
            {
                var shell=ShellMesh(); _meshes.Add(shell);
                AddCrystal("Ice prison",shell,new Vector3(0,0,0),new Vector3(radius*2.9f,height*1.04f,radius*2.8f),Quaternion.identity,Vector3.zero);
                for (var i=0;i<7;i++)
                {
                    var angle=i*Mathf.PI*2/7;
                    var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    AddCrystal("Foot ice "+i,crystalMesh,radial*radius*.94f,
                        new Vector3(radius*.75f,height*(.26f+.08f*(i%3)),radius*.6f),Quaternion.Euler(radial.z*22,i*53,-radial.x*22),radial);
                }
                for (var i=0;i<8;i++) _ribbons.Add(Line("Prison edge "+i,.014f));
            }
            else if (kind==ActorVisualEffectKind.GlacialWaltz)
            {
                for (var i=0;i<3;i++)
                {
                    AddCrystal("Orbit icicle "+i,crystalMesh,Vector3.zero,new Vector3(height*.19f,height*.78f,height*.16f),Quaternion.identity,Vector3.zero);
                    _ribbons.Add(Line("Icicle wake "+i,.085f));
                }
                _ribbons.Add(Line("Frost orbit",.018f));
            }
            else
            {
                for (var i=0;i<6;i++)
                {
                    var angle=i*Mathf.PI*2/6;
                    AddCrystal("Frost glaze "+i,crystalMesh,new Vector3(Mathf.Cos(angle)*radius*.96f,height*(.18f+(i%3)*.23f),Mathf.Sin(angle)*radius*.96f),
                        new Vector3(height*.06f,height*.17f,height*.035f),Quaternion.Euler(0,i*60,15),new Vector3(Mathf.Cos(angle),.2f,Mathf.Sin(angle)));
                }
            }
            _mist=Particles("Cold glaze smoke",_mistMaterial,false,false);
            _snow=Particles("Snow crystals",_snowMaterial,false,true);
            _burst=Particles("Ice burst",_snowMaterial,true,true);
            Root.gameObject.SetActive(false);
        }

        private Material Material(Shader shader,float mode,string name)
        {
            var material=new Material(shader) { name="AVE "+Kind+" "+name,hideFlags=HideFlags.DontSave };
            material.SetFloat("_Mode",mode);
            return material;
        }

        private ParticleSystem Particles(string name, Material material, bool burst, bool flecks)
        {
            var go=new GameObject(name); go.transform.SetParent(Root,false);
            var ps=go.AddComponent<ParticleSystem>(); ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed=false; ps.randomSeed=(uint)(413+(int)Kind*17+(flecks ? 5 : 0)+(burst ? 7 : 0));
            var main=ps.main;
            main.playOnAwake=false; main.loop=!burst; main.duration=burst ? _settings.CryoExitDuration : 2;
            main.useUnscaledTime=true; main.simulationSpace=ParticleSystemSimulationSpace.Local; main.maxParticles=burst ? 40 : 48;
            main.startLifetime=burst ? new ParticleSystem.MinMaxCurve(.25f,_settings.CryoExitDuration) : new ParticleSystem.MinMaxCurve(.9f,1.6f);
            main.startSpeed=burst ? new ParticleSystem.MinMaxCurve(.6f,1.7f) : .07f;
            main.startSize=flecks ? new ParticleSystem.MinMaxCurve(.035f,.085f) : new ParticleSystem.MinMaxCurve(_radius*.8f,_radius*1.7f);
            main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            main.startColor=flecks ? new Color(.65f,.94f,1,.9f) : new Color(.65f,.87f,1,Kind==ActorVisualEffectKind.FrostBite ? .44f : .2f);
            var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box;
            var spread=Kind==ActorVisualEffectKind.GlacialWaltz ? _radius*3.2f : _radius*2.1f;
            shape.scale=new Vector3(spread,burst ? _height*.7f : _height*.5f,spread*.8f);
            go.transform.localPosition=Vector3.up*_height*(burst ? .5f : .28f);
            var emission=ps.emission; emission.rateOverTime=burst ? 0 : flecks ? 8 : Kind==ActorVisualEffectKind.FrostBite ? 13 : 5;
            if(burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0,(short)30) });
            var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.x=new ParticleSystem.MinMaxCurve(-.12f,.12f); velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);
            velocity.y=new ParticleSystem.MinMaxCurve(flecks ? .12f : .15f,flecks ? .4f : .34f);
            var fade=ps.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(1,.18f),new GradientAlphaKey(.6f,.65f),new GradientAlphaKey(0,1) });
            fade.color=gradient;
            var size=ps.sizeOverLifetime; size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.25f,1),new Keyframe(1,flecks ? .2f : 1.8f)));
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard; renderer.maxParticleSize=.35f;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return ps;
        }

        private void AddCrystal(string name,Mesh mesh,Vector3 position,Vector3 scale,Quaternion rotation,Vector3 scatter)
        {
            var go=new GameObject(name); go.transform.SetParent(Root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=_ice;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            _crystals.Add(new Crystal { Transform=go.transform,Position=position,Scale=scale,Rotation=rotation,Scatter=scatter });
        }

        private LineRenderer Line(string name,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(Root,false);
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=_lineMaterial;
            line.useWorldSpace=false; line.widthMultiplier=width; line.positionCount=32;
            line.numCornerVertices=2; line.numCapVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            return line;
        }

        public void SetActive(bool active,bool immediate=false)
        {
            if(immediate) { Active=false; Exiting=false; ClearEnter(); Stop(true); Root.gameObject.SetActive(false); return; }
            if(Active==active) return;
            Active=active;
            if(active)
            {
                _started=Time.unscaledTime; Exiting=false; Entering=false;
                Stop(true); Root.gameObject.SetActive(!_hidden);
                if(!_hidden) { _mist.Play(false); _snow.Play(false); }
            }
            else
            {
                Exiting=true; _exited=Time.unscaledTime; ClearEnter();
                _mist.Stop(false,ParticleSystemStopBehavior.StopEmitting); _snow.Stop(false,ParticleSystemStopBehavior.StopEmitting);
                if(!_hidden) { _burst.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear); _burst.Play(false); }
            }
        }

        public void Enter()
        {
            SetActive(true); Entering=true; _entered=Time.unscaledTime;
            _burst.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(!_hidden) _burst.Play(false);
        }

        public void ClearEnter() { Entering=false; }

        public void SetHidden(bool hidden)
        {
            if(_hidden==hidden) return;
            _hidden=hidden;
            if(hidden) { ClearEnter(); Stop(true); }
            Root.gameObject.SetActive((Active || Exiting) && !hidden);
            if(Active && !hidden) { _mist.Play(false); _snow.Play(false); }
        }

        public void Evaluate(float now,Camera camera,float flinch)
        {
            if(!Active && !Exiting) return;
            Root.rotation=Quaternion.identity;
            var age=now-_started;
            var enter=Entering ? Mathf.Clamp01((now-_entered)/_settings.CryoEnterDuration) : 1;
            if(Entering && enter>=1) Entering=false;
            var exit=Exiting ? Mathf.Clamp01((now-_exited)/_settings.CryoExitDuration) : 0;
            if(Exiting && exit>=1) { SetActive(false,true); return; }
            var visibility=(1-Mathf.SmoothStep(0,1,exit))*Mathf.SmoothStep(0,1,enter);
            if(Kind==ActorVisualEffectKind.FrostBite && camera!=null)
            {
                var toward=Vector3.ProjectOnPlane(camera.transform.position-Root.position,Vector3.up).normalized;
                _mist.transform.localPosition=Vector3.up*_height*.3f+toward*(_radius*1.2f);
                _snow.transform.localPosition=Vector3.up*_height*.45f+toward*(_radius*1.15f);
                var shape=_mist.shape; shape.scale=new Vector3(_radius*2.3f,_height*.6f,.12f);
                if(toward.sqrMagnitude>.01f) _mist.transform.rotation=Quaternion.LookRotation(toward,Vector3.up);
            }
            var tint=_color; tint.a*=visibility; _ice.SetColor("_Tint",tint);
            _mistMaterial.SetColor("_Tint",new Color(1,1,1,1-exit)); _mistMaterial.SetFloat("_Phase",age);
            _snowMaterial.SetColor("_Tint",new Color(1,1,1,1-exit));
            for(var i=0;i<_crystals.Count;i++)
            {
                var crystal=_crystals[i];
                if(Kind==ActorVisualEffectKind.GlacialWaltz)
                {
                    var angle=age*_settings.GlacialOrbitDegreesPerSecond*Mathf.Deg2Rad+i*Mathf.PI*2/3;
                    var radius=(_radius+_height*.39f)*(.5f+.5f*enter+exit*.35f);
                    var center=Orbit(angle,radius);
                    var tangent=new Vector3(-Mathf.Sin(angle),.17f,Mathf.Cos(angle));
                    crystal.Transform.localPosition=center;
                    crystal.Transform.localRotation=Quaternion.FromToRotation(Vector3.up,tangent);
                    crystal.Transform.localScale=crystal.Scale*(.35f+.65f*enter)*(1-exit*.7f);
                    var ribbon=_ribbons[i]; ribbon.startColor=new Color(.6f,.94f,1,.65f*visibility); ribbon.endColor=new Color(.5f,.9f,1,0);
                    for(var p=0;p<ribbon.positionCount;p++) ribbon.SetPosition(p,Orbit(angle-p*.035f,radius)+Vector3.up*Mathf.Sin(p*.16f)*.05f);
                }
                else
                {
                    var shatter=exit*exit;
                    crystal.Transform.localPosition=crystal.Position+crystal.Scatter*(_height*.55f*shatter)+Vector3.down*(exit*exit*.16f);
                    crystal.Transform.localRotation=crystal.Rotation*Quaternion.Euler(0,0,(i%2==0 ? 1 : -1)*flinch*2+exit*i*8);
                    var growth=Kind==ActorVisualEffectKind.Freeze ? new Vector3(.85f+.15f*enter,Mathf.Max(.005f,enter),.85f+.15f*enter) : Vector3.one*(.5f+.5f*enter);
                    crystal.Transform.localScale=Vector3.Scale(crystal.Scale,growth)*(1-exit*.25f);
                }
            }
            if(Kind==ActorVisualEffectKind.Freeze)
            {
                for(var i=0;i<8;i++)
                {
                    var edge=_ribbons[i]; edge.positionCount=2;
                    var angle=i*Mathf.PI*2/8;
                    var x=Mathf.Cos(angle)*_radius*1.4f; var z=Mathf.Sin(angle)*_radius*1.36f;
                    edge.SetPosition(0,new Vector3(x,0,z)); edge.SetPosition(1,new Vector3(x*.88f,_height*1.02f*enter,z*.88f));
                    edge.startColor=edge.endColor=new Color(.68f,.95f,1,.45f*visibility);
                }
            }
            if(Kind==ActorVisualEffectKind.GlacialWaltz)
            {
                var ring=_ribbons[3]; ring.loop=true;
                for(var p=0;p<ring.positionCount;p++) ring.SetPosition(p,Orbit(p*Mathf.PI*2/ring.positionCount,_radius+_height*.39f));
                ring.startColor=ring.endColor=new Color(.5f,.9f,1,.18f*visibility);
            }
        }

        private Vector3 Orbit(float angle,float radius) => new Vector3(Mathf.Cos(angle)*radius,_height*.46f+Mathf.Sin(angle*2)*_height*.065f,Mathf.Sin(angle)*radius);

        private void Stop(bool clear)
        {
            var mode=clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting;
            _mist.Stop(false,mode); _snow.Stop(false,mode); _burst.Stop(false,mode);
        }

#if UNITY_EDITOR
        public void SamplePreview(float elapsed)
        {
            if(!Active && !Exiting) return;
            _mist.Simulate(elapsed,false,true,false); _snow.Simulate(elapsed,false,true,false);
            if(Entering || Exiting) _burst.Simulate(elapsed,false,true,false);
        }
#endif

        private static Mesh CrystalMesh()
        {
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            for(var i=0;i<6;i++)
            {
                var a=i*Mathf.PI*2/6; var b=(i+1)*Mathf.PI*2/6;
                var p=new Vector3(Mathf.Cos(a)*.5f,.36f,Mathf.Sin(a)*.5f);
                var q=new Vector3(Mathf.Cos(b)*.5f,.36f,Mathf.Sin(b)*.5f);
                Face(vertices,uv,triangles,Vector3.up,p,q);
                Face(vertices,uv,triangles,Vector3.zero,q,p);
            }
            return Mesh("AVE faceted icicle",vertices,uv,triangles);
        }

        private static Mesh ShellMesh()
        {
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var triangles=new List<int>();
            var corners=new[] { new Vector3(-.36f,0,-.5f),new Vector3(.36f,0,-.5f),new Vector3(.5f,0,-.34f),new Vector3(.5f,0,.34f),
                new Vector3(.36f,0,.5f),new Vector3(-.36f,0,.5f),new Vector3(-.5f,0,.34f),new Vector3(-.5f,0,-.34f) };
            for(var i=0;i<8;i++)
            {
                var a=corners[i]; var b=corners[(i+1)%8];
                var c=a*.9f+Vector3.up*(.94f+(i%3)*.035f); var d=b*.9f+Vector3.up*(.94f+((i+1)%3)*.035f);
                Face(vertices,uv,triangles,a,c,b); Face(vertices,uv,triangles,b,c,d);
                Face(vertices,uv,triangles,c,Vector3.up*1.09f,d);
            }
            return Mesh("AVE ice prison",vertices,uv,triangles);
        }

        private static void Face(List<Vector3> vertices,List<Vector2> uv,List<int> triangles,Vector3 a,Vector3 b,Vector3 c)
        {
            var index=vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uv.Add(new Vector2(0,0)); uv.Add(new Vector2(.5f,1)); uv.Add(new Vector2(1,0));
            triangles.Add(index); triangles.Add(index+1); triangles.Add(index+2);
        }

        private static Mesh Mesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> triangles)
        {
            var mesh=new Mesh { name=name,hideFlags=HideFlags.DontSave };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            var colors=new Color[vertices.Count]; for(var i=0;i<colors.Length;i++) colors[i]=Color.white;
            mesh.colors=colors; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        public void Dispose()
        {
            ActorVisualEffectGeometry.Release(Root.gameObject);
            foreach(var mesh in _meshes) ActorVisualEffectGeometry.Release(mesh);
            ActorVisualEffectGeometry.Release(_ice); ActorVisualEffectGeometry.Release(_mistMaterial);
            ActorVisualEffectGeometry.Release(_snowMaterial); ActorVisualEffectGeometry.Release(_lineMaterial);
        }
    }
}
