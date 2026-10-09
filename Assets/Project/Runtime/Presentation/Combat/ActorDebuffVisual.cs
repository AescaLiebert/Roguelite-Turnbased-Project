using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>A pooled status entrance and low-density idle loop, attached to the actor model.</summary>
    internal sealed class ActorDebuffVisual : IDisposable
    {
        private sealed class Emitter
        {
            public ParticleSystem System;
            public float Rate;
        }

        public readonly ActorVisualEffectKind Kind;
        public readonly Transform Root;
        public bool Active { get; private set; }
        public bool Entering { get; private set; }
        private readonly List<Emitter> _idle = new List<Emitter>();
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly ParticleSystem _enter;
        private readonly float _height, _radius, _enterDuration;
        private readonly Color _color;
        private float _started, _entered, _density = 1;
        private bool _hidden;

        public ActorDebuffVisual(Transform actor, ActorVisualEffectKind kind, float height, float radius,
            float groundOffset, ActorVisualEffectSettings settings)
        {
            Kind = kind; _height = height; _radius = radius; _enterDuration = settings.DebuffEnterDuration;
            _color = kind == ActorVisualEffectKind.Paralyze ? settings.Paralyze : kind == ActorVisualEffectKind.Bleed ? settings.Bleed :
                kind == ActorVisualEffectKind.Poison ? settings.Poison : kind == ActorVisualEffectKind.Shock ? settings.Shock : settings.Ignite;
            Root = new GameObject("AVE " + kind + " Particles").transform;
            Root.SetParent(actor,false); Root.localPosition = Vector3.up * groundOffset;
            // Upright gravity and billboards remain readable when the actor turns or flinches.
            Root.rotation = Quaternion.identity;
            _enter = CreateEmitter("Enter", PrimaryShape, true, 0, true);
            var rate = kind == ActorVisualEffectKind.Bleed ? 10 : kind == ActorVisualEffectKind.Poison ? 5 :
                kind == ActorVisualEffectKind.Ignite ? 14 : 3;
            _idle.Add(new Emitter { System = CreateEmitter("Idle",PrimaryShape,false,rate,true), Rate = rate });
            if (kind == ActorVisualEffectKind.Poison || kind == ActorVisualEffectKind.Ignite)
                _idle.Add(new Emitter { System = CreateEmitter(kind == ActorVisualEffectKind.Poison ? "Bubbles" : "Embers",
                    kind == ActorVisualEffectKind.Poison ? 1 : 4, false, 5, false), Rate = 5 });
            if (kind == ActorVisualEffectKind.Paralyze || kind == ActorVisualEffectKind.Shock)
            {
                var material = new Material(Resources.Load<Shader>("ActorVisualEffects")) { name="AVE Electric Arcs",hideFlags=HideFlags.DontSave };
                _materials.Add(material);
                var count = kind == ActorVisualEffectKind.Paralyze ? 5 : 3;
                for (var i=0;i<count;i++)
                {
                    AddLine(material,"Arc Glow",.085f);
                    AddLine(material,"Arc Core",.025f);
                }
            }
            Root.gameObject.SetActive(false);
        }

        private int PrimaryShape => Kind == ActorVisualEffectKind.Bleed ? 0 : Kind == ActorVisualEffectKind.Poison ? 2 :
            Kind == ActorVisualEffectKind.Ignite ? 3 : 4;

        private ParticleSystem CreateEmitter(string name,int shapeStyle,bool enter,float rate,bool primary)
        {
            var go = new GameObject(name); go.transform.SetParent(Root,false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed=false; ps.randomSeed=(uint)((int)Kind*137 + _materials.Count*53 + 1);
            var main = ps.main;
            main.playOnAwake=false; main.loop=!enter; main.duration=enter ? _enterDuration : 1.6f;
            main.useUnscaledTime=true; main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Shape; main.maxParticles=enter ? 32 : primary ? 28 : 18;
            main.startLifetime=new ParticleSystem.MinMaxCurve(enter ? .3f : .65f,enter ? _enterDuration : 1.2f);
            main.startSpeed=0;
            main.startSize3D=true;
            var size = primary ? Kind == ActorVisualEffectKind.Poison ? .8f : Kind == ActorVisualEffectKind.Ignite ? .5f :
                Kind == ActorVisualEffectKind.Bleed ? .24f : .12f : .18f;
            if (enter) size*=1.35f;
            main.startSizeX=new ParticleSystem.MinMaxCurve(size*.65f,size);
            main.startSizeY=new ParticleSystem.MinMaxCurve(size, size*(shapeStyle == 0 ? 1.5f : shapeStyle == 3 ? 2 : 1));
            main.startSizeZ=.1f;
            main.startRotation = shapeStyle == 0 || shapeStyle == 3 ? 0 : new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
            var color=_color;
            if (shapeStyle==2) color.a=enter ? .5f : .42f;
            if (!primary && Kind==ActorVisualEffectKind.Poison) color=new Color(.65f,.4f,1f,.8f);
            main.startColor=color;
            main.gravityModifier=shapeStyle==0 ? .16f : 0;
            var emission=ps.emission; emission.rateOverTime=rate;
            if (enter) emission.SetBursts(new[] { new ParticleSystem.Burst(0,(short)18) });
            var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box;
            shape.scale=new Vector3(_radius*1.8f,_height*(Kind==ActorVisualEffectKind.Bleed ? .45f :
                Kind==ActorVisualEffectKind.Poison || Kind==ActorVisualEffectKind.Ignite ? enter ? .4f : .15f : .8f),.12f);
            go.transform.localPosition=Vector3.up*_height*(Kind==ActorVisualEffectKind.Bleed ? .7f :
                Kind==ActorVisualEffectKind.Poison ? .25f : Kind==ActorVisualEffectKind.Ignite ? enter ? .35f : .13f : .5f);
            var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.x=new ParticleSystem.MinMaxCurve(enter ? -.55f : -.14f,enter ? .55f : .14f);
            velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);
            var speed=shapeStyle==0 ? -.6f : shapeStyle==2 ? .45f : shapeStyle==3 ? 1.4f : .7f;
            velocity.y=new ParticleSystem.MinMaxCurve(Mathf.Min(speed*.65f,speed),Mathf.Max(speed*.65f,speed));
            var fade=ps.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.75f,.55f),new GradientAlphaKey(0,1) });
            fade.color=gradient;
            var sizeModule=ps.sizeOverLifetime; sizeModule.enabled=true;
            sizeModule.size=new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.2f,1),new Keyframe(1,shapeStyle==2 ? 1.8f : .35f)));
            var material=new Material(Resources.Load<Shader>("ActorStatusParticles")) { name="AVE " + Kind + " " + name,hideFlags=HideFlags.DontSave };
            material.SetFloat("_Shape",shapeStyle); _materials.Add(material);
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard; renderer.maxParticleSize=.2f;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return ps;
        }

        private void AddLine(Material material,string name,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(Root,false);
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=material; line.useWorldSpace=false;
            line.widthMultiplier=width; line.numCornerVertices=2; line.numCapVertices=2;
            line.shadowCastingMode=ShadowCastingMode.Off; _lines.Add(line);
        }

        public void SetActive(bool active)
        {
            if (Active==active) return;
            Active=active;
            if (active)
            {
                _started=Time.unscaledTime; Root.gameObject.SetActive(!_hidden);
                if (!_hidden) foreach(var emitter in _idle) emitter.System.Play(false);
            }
            else
            {
                ClearEnter(); foreach(var emitter in _idle) if (emitter.System!=null) emitter.System.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                if (Root!=null) Root.gameObject.SetActive(false);
            }
        }

        public void Enter()
        {
            SetActive(true); _entered=Time.unscaledTime; Entering=true;
            _enter.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission=_enter.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0,(short)Mathf.Max(6,Mathf.RoundToInt(18*_density))) });
            if (!_hidden) _enter.Play(false);
        }

        public void SetDensity(float density)
        {
            _density=density;
            foreach(var emitter in _idle) { var emission=emitter.System.emission; emission.rateOverTime=emitter.Rate*density; }
        }

        public void SetHidden(bool hidden)
        {
            if (_hidden==hidden) return;
            _hidden=hidden; Root.gameObject.SetActive(Active && !hidden);
            if (Active && !hidden) foreach(var emitter in _idle) emitter.System.Play(false);
        }

        public void Evaluate(float now,Camera camera)
        {
            if (!Active) return;
            Root.rotation=Quaternion.identity;
            if (Entering && now-_entered>=_enterDuration) ClearEnter();
            var age=now-_started;
            var enter=Entering ? Mathf.Sin(Mathf.Clamp01((now-_entered)/_enterDuration)*Mathf.PI) : 0;
            var right=camera!=null ? camera.transform.right : Vector3.right;
            var toward=camera!=null ? Vector3.ProjectOnPlane(-camera.transform.forward,Vector3.up).normalized : Vector3.back;
            // Keep a thin emitter band on the visible surface instead of losing droplets/flames inside the model.
            PositionEmitter(_enter,toward);
            foreach(var emitter in _idle) PositionEmitter(emitter.System,toward);
            for(var i=0;i<_lines.Count;i++)
            {
                var line=_lines[i]; var index=i/2; var glow=(i&1)==0;
                var band=Kind==ActorVisualEffectKind.Paralyze && index<3;
                var cycle=Mathf.Repeat(age*1.7f+index*.27f,1);
                var alpha=Kind==ActorVisualEffectKind.Paralyze ? .65f+.25f*Mathf.Sin(age*5+index) : cycle<.5f ? Mathf.Sin(cycle*Mathf.PI*2) : 0;
                alpha=Mathf.Max(alpha,enter); alpha*=glow ? .13f : .9f;
                line.enabled=!_hidden && alpha>.015f;
                var color=_color; color.a=alpha; line.startColor=line.endColor=color;
                line.loop=band; line.positionCount=band ? 32 : 7;
                var y=_height*(band ? .2f+index*.28f : .55f);
                for(var p=0;p<line.positionCount;p++)
                {
                    Vector3 point;
                    if (band)
                    {
                        var angle=p*Mathf.PI*2/line.positionCount+age*.4f;
                        var radius=_radius*(1.22f+.18f*enter);
                        point=new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
                    }
                    else
                    {
                        var t=p/6f;
                        var side=Kind==ActorVisualEffectKind.Paralyze ? index==3 ? -1 : 1 : index-1;
                        var jitter=Mathf.Sin(p*2.7f+Mathf.Floor(age*12)*1.8f)*.17f;
                        point=right*(side*_radius*.95f+jitter)+toward*(_radius*1.05f)+
                            Vector3.up*(_height*(.18f+t*.65f));
                    }
                    line.SetPosition(p,point);
                }
            }
        }

        private void PositionEmitter(ParticleSystem system,Vector3 toward)
        {
            var position=system.transform.localPosition;
            system.transform.localPosition=toward*(_radius*1.12f)+Vector3.up*position.y;
            if (toward.sqrMagnitude>.01f) system.transform.rotation=Quaternion.LookRotation(toward,Vector3.up);
        }

#if UNITY_EDITOR
        public void SamplePreview(float age)
        {
            if (!Active) return;
            foreach(var emitter in _idle) emitter.System.Simulate(age,false,true,false);
            if (Entering) _enter.Simulate(age,false,true,false);
        }
#endif

        public void ClearEnter()
        {
            Entering=false; if (_enter!=null) _enter.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Dispose()
        {
            if (Root!=null) ActorVisualEffectGeometry.Release(Root.gameObject);
            foreach(var material in _materials) ActorVisualEffectGeometry.Release(material);
        }
    }
}
