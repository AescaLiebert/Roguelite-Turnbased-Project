using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    [Serializable] public sealed class AbilityContactCue
    {
        [Range(1,10)] public int Ordinal = 1;
        [Range(0,1)] public float At = .5f;
        public string Socket = "impact";
        public GameObject Effect;
        public AudioClip Audio;
        [Range(0,1)] public float Volume = .7f;
        [Min(.01f)] public float Lifetime = .5f;
    }
    [CreateAssetMenu(menuName = "Character Studio/Ability Presentation Profile")]
    public sealed class AbilityPresentationProfile : ScriptableObject
    {
        public string SkillId;
        public string VariantId;
        public static string AbilityKey(FightingAllstar.Core.Combat.CardState card) => card?.Kind==FightingAllstar.Core.Combat.CardKind.Ultimate ? "$ultimate" : card?.SkillId;
        [Min(0)] public float Windup = .2f, Recovery = .3f;
        [Min(.01f)] public float Action = 1.2f;
        public AnimationClip WindupClip, ActionClip, RecoveryClip;
        public bool CameraCue;
        public Vector3 CameraOffset = new Vector3(0,2,-7);
        [Range(15,90)] public float CameraFov = 32;
        public List<AbilityContactCue> Contacts = new List<AbilityContactCue>();
        public bool HasValidClock => Finite(Windup) && Finite(Action) && Finite(Recovery) && Windup>=0 && Action>0 && Recovery>=0 && Windup+Action+Recovery<=30
            && Contacts.Where(c=>c!=null).All(c=>Finite(c.At) && c.At>=0 && c.At<=1 && Finite(c.Lifetime) && c.Lifetime>0);
        public Dictionary<int,float> ContactTimes() => Contacts.Where(c => c != null).GroupBy(c => c.Ordinal).ToDictionary(g => g.Key,g => g.First().At);
        public IEnumerable<string> Validate(int hitCount = 0)
        {
            if (string.IsNullOrWhiteSpace(SkillId)) yield return "Choose a stable skill ID.";
            if (!Finite(Windup) || !Finite(Action) || !Finite(Recovery) || Windup < 0 || Action <= 0 || Recovery < 0 || Windup + Action + Recovery > 30)
                yield return "Timeline must be finite, positive and bounded to 30 seconds.";
            if (ActionClip == null) yield return "No action clip: bounded procedural motion will be shown.";
            var last = -1f;
            foreach (var cue in Contacts.Where(c => c != null).OrderBy(c => c.Ordinal))
            {
                if (cue.Ordinal < 1 || cue.Ordinal > 10 || hitCount > 0 && cue.Ordinal > hitCount) yield return "Unmatched contact:" + cue.Ordinal + ". A marker never creates damage.";
                if (!Finite(cue.At) || cue.At < 0 || cue.At > 1 || cue.At < last) yield return "Contact offsets must be monotonic in 0..1.";
                if (!Finite(cue.Lifetime) || cue.Lifetime <= 0 || !Finite(cue.Volume)) yield return "Invalid cue lifetime or volume.";
                last = cue.At;
            }
            if (Contacts.Where(c => c != null).GroupBy(c => c.Ordinal).Any(g => g.Count()>1)) yield return "Duplicate contact ordinal.";
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
