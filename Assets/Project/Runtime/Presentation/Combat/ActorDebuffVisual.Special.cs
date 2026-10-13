using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    internal sealed partial class ActorDebuffVisual
    {
        private bool IsAura => Kind == ActorVisualEffectKind.ExtortBuff || Kind == ActorVisualEffectKind.ExtortDebuff ||
            Kind == ActorVisualEffectKind.Rejuvenation;
        private int SpecialShape => IsAura ? 5 : Kind == ActorVisualEffectKind.CardDisable ? 6 :
            Kind == ActorVisualEffectKind.Explosion ? 8 : Kind == ActorVisualEffectKind.ExplosionDetonation ? 9 : 4;

        private static Color StatusColor(ActorVisualEffectKind kind, ActorVisualEffectSettings settings)
        {
            switch (kind)
            {
                case ActorVisualEffectKind.Paralyze: return settings.Paralyze;
                case ActorVisualEffectKind.Bleed: return settings.Bleed;
                case ActorVisualEffectKind.Poison: return settings.Poison;
                case ActorVisualEffectKind.Shock: return settings.Shock;
                case ActorVisualEffectKind.CardDisable: return settings.CardDisable;
                case ActorVisualEffectKind.ExtortBuff: return settings.ExtortBuff;
                case ActorVisualEffectKind.ExtortDebuff: return settings.ExtortDebuff;
                case ActorVisualEffectKind.Explosion:
                case ActorVisualEffectKind.ExplosionDetonation: return settings.Explosion;
                case ActorVisualEffectKind.Rejuvenation: return settings.Rejuvenation;
                default: return settings.Ignite;
            }
        }

        private float SpecialRate(float fallback) => IsAura ? 8 :
            Kind == ActorVisualEffectKind.CardDisable || Kind == ActorVisualEffectKind.Explosion ? 1f : fallback;

        private bool IsMarker(Emitter emitter) => (Kind==ActorVisualEffectKind.CardDisable || Kind==ActorVisualEffectKind.Explosion) &&
            emitter.System==_idle[0].System;

        private void StartIdle()
        {
            foreach(var emitter in _idle)
            {
                emitter.System.Play(false);
                if(IsMarker(emitter)) emitter.System.Emit(1);
            }
        }

        private short EntranceCount() => Kind == ActorVisualEffectKind.CardDisable || Kind == ActorVisualEffectKind.Explosion ? (short)1 :
            (short)Mathf.Max(6, Mathf.RoundToInt((Kind == ActorVisualEffectKind.ExplosionDetonation ? 28 : 18) * _density));

        private void AddSpecialEmitters()
        {
            if (IsAura)
            {
                var shape = Kind == ActorVisualEffectKind.Rejuvenation ? 7 : 10;
                _idle.Add(new Emitter { System = CreateEmitter(Kind == ActorVisualEffectKind.Rejuvenation ? "Healing Crosses" : "Power Direction",
                    shape, false, 3, false), Rate = 3 });
            }
            if (Kind == ActorVisualEffectKind.Explosion)
                _idle.Add(new Emitter { System = CreateEmitter("Fuse Sparks", 4, false, 4, false), Rate = 4 });
            if (Kind == ActorVisualEffectKind.ExplosionDetonation)
            {
                var material = new Material(Resources.Load<Shader>("ActorVisualEffects")) { name="AVE Shockwave", hideFlags=HideFlags.DontSave };
                _materials.Add(material);
                AddLine(material, "Shockwave Glow", .14f);
                AddLine(material, "Shockwave Core", .035f);
            }
        }

        private void ConfigureSpecialEmitter(ParticleSystem system, bool enter, bool primary, int shapeStyle)
        {
            if (Kind < ActorVisualEffectKind.CardDisable) return;
            var main=system.main; var shape=system.shape; var velocity=system.velocityOverLifetime;
            var size=system.sizeOverLifetime;
            if (IsAura)
            {
                var down=Kind==ActorVisualEffectKind.ExtortDebuff;
                var ribbon=shapeStyle==5;
                var color=_color; color.a=ribbon ? enter ? .65f : .4f : .85f;
                if (!primary && Kind==ActorVisualEffectKind.ExtortBuff) color=new Color(1f,.88f,.35f,.85f);
                main.startColor=color;
                main.startRotation=down && !primary ? Mathf.PI : 0;
                main.startLifetime=enter ? new ParticleSystem.MinMaxCurve(_enterDuration*.8f,_enterDuration) : new ParticleSystem.MinMaxCurve(1.1f,1.6f);
                main.startSizeX=ribbon ? new ParticleSystem.MinMaxCurve(.23f,.42f) : new ParticleSystem.MinMaxCurve(enter ? .3f : .18f,enter ? .42f : .25f);
                main.startSizeY=ribbon ? new ParticleSystem.MinMaxCurve(.7f,1.25f) : main.startSizeX;
                shape.scale=new Vector3(_radius*2.2f,enter ? _height*.6f : _height*.12f,.08f);
                system.transform.localPosition=Vector3.up*_height*(enter ? .5f : down ? .8f : .18f);
                velocity.x=new ParticleSystem.MinMaxCurve(-.08f,.08f);
                velocity.y=new ParticleSystem.MinMaxCurve(down ? -1.5f : .9f,down ? -.9f : 1.5f);
                size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.2f,1),new Keyframe(1,.4f)));
            }
            else if (Kind==ActorVisualEffectKind.CardDisable || Kind==ActorVisualEffectKind.Explosion)
            {
                var symbol=primary;
                var symbolSize=Kind==ActorVisualEffectKind.CardDisable ? enter ? .95f : .55f : enter ? .9f : .52f;
                main.startSizeX=main.startSizeY=symbol ? symbolSize : .13f;
                main.startRotation=0;
                main.startLifetime=enter ? new ParticleSystem.MinMaxCurve(_enterDuration) : new ParticleSystem.MinMaxCurve(1.6f);
                shape.scale=new Vector3(.03f,.03f,.03f);
                system.transform.localPosition=Vector3.up*_height*(enter ? .68f : .85f) + Vector3.up*(symbol ? 0 : .28f);
                velocity.x=0; velocity.z=0;
                velocity.y=enter && Kind==ActorVisualEffectKind.Explosion ? -.25f : symbol ? .025f : .3f;
                size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.18f,1.08f),new Keyframe(.4f,1),new Keyframe(1,.9f)));
            }
            else if (Kind==ActorVisualEffectKind.ExplosionDetonation)
            {
                main.startSizeX=main.startSizeY=new ParticleSystem.MinMaxCurve(.2f,.55f);
                main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,_enterDuration);
                main.startSpeed=new ParticleSystem.MinMaxCurve(2.5f,4.5f);
                main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.12f;
                system.transform.localPosition=Vector3.up*_height*.55f;
                velocity.enabled=false;
                size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.15f,1),new Keyframe(1,.15f)));
            }
        }

        private bool EvaluateSpecial(float now, Camera camera, Vector3 right, Vector3 toward, float entrance)
        {
            if (Kind==ActorVisualEffectKind.CardDisable || Kind==ActorVisualEffectKind.Explosion)
            {
                // Place idle symbols beside the shoulder to preserve the actor's face.
                var side=Kind==ActorVisualEffectKind.CardDisable ? 1 : -1;
                foreach(var emitter in _idle) emitter.System.transform.localPosition+=right*_radius*.95f*side;
                _enter.transform.localPosition+=right*_radius*.55f*side;
            }
            if (Kind!=ActorVisualEffectKind.ExplosionDetonation) return Kind>=ActorVisualEffectKind.CardDisable;
            var t=Mathf.Clamp01((now-_entered)/_enterDuration);
            if (t>=1) { SetActive(false); return true; }
            var radius=Mathf.Lerp(.12f,_height*.85f,1-Mathf.Pow(1-t,3));
            var up=camera!=null ? camera.transform.up : Vector3.up;
            for (var i=0;i<_lines.Count;i++)
            {
                var line=_lines[i]; line.enabled=!_hidden; line.loop=true; line.positionCount=48;
                var color=i==0 ? _color : new Color(1f,.95f,.65f);
                color.a=(1-t)*(i==0 ? .25f : .9f); line.startColor=line.endColor=color;
                line.widthMultiplier=(i==0 ? .14f : .035f)*(1-t*.7f);
                for(var p=0;p<48;p++)
                {
                    var angle=p*Mathf.PI*2/48;
                    line.SetPosition(p,Vector3.up*_height*.55f+toward*_radius*1.2f+
                        (right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius);
                }
            }
            return true;
        }
    }
}
