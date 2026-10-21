using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public enum VisualRootMotion { Locked, ClipLocal }
    [Serializable] public sealed class ActorSocket
    {
        public string Id = "impact", Path = "";
        public Vector3 Offset = Vector3.up;
    }
    [Serializable] public sealed class ActorAnimationBinding
    {
        public string Role = "Idle", AnimatorClipName = "";
        public AnimationClip Clip;
    }
    [Serializable] public sealed class ActorReactionBinding
    {
        public HitReaction Reaction = HitReaction.Hit;
        public AnimationClip Clip;
        public bool ProceduralFallback = true;
    }
    [CreateAssetMenu(menuName = "Character Studio/Actor Presentation Profile")]
    public sealed class ActorPresentationProfile : ScriptableObject
    {
        public GameObject Prefab;
        public RuntimeAnimatorController AnimatorController;
        public string AnimatorPath = "";
        [Min(.01f)] public float Scale = 1;
        public float Yaw;
        public VisualRootMotion RootMotion;
        public Bounds Bounds = new Bounds(Vector3.up, new Vector3(1,2,1));
        [Min(0)] public float Approach = 1;
        public List<ActorSocket> Sockets = new List<ActorSocket> { new ActorSocket() };
        public List<ActorAnimationBinding> Animations = new List<ActorAnimationBinding>();
        public List<ActorReactionBinding> Reactions = new List<ActorReactionBinding>();
        public List<AbilityPresentationProfile> Abilities = new List<AbilityPresentationProfile>();
        public bool HasValidTransform => Finite(Scale) && Scale>0 && Finite(Yaw) && Finite(Approach) && Approach>=0;
        public AnimationClip Resolve(string role, Animator animator)
        {
            var binding = Animations.FirstOrDefault(b => b != null && b.Role == role);
            if (binding?.Clip != null) return binding.Clip;
            var controller = AnimatorController != null ? AnimatorController : animator?.runtimeAnimatorController;
            return controller?.animationClips.FirstOrDefault(c => c != null && c.name == binding?.AnimatorClipName);
        }
        public AbilityPresentationProfile Ability(string skillId,string variantId=null) =>
            (!string.IsNullOrEmpty(variantId) ? Abilities.FirstOrDefault(a=>a!=null && a.SkillId==skillId && a.VariantId==variantId) : null) ??
            Abilities.FirstOrDefault(a => a != null && a.SkillId == skillId && string.IsNullOrEmpty(a.VariantId));
        public IEnumerable<string> Validate()
        {
            if (Prefab == null) yield return "Missing model prefab: deterministic primitive fallback will be used.";
            if (!Finite(Scale) || Scale <= 0 || !Finite(Yaw) || !Finite(Approach) || Approach < 0) yield return "Invalid scale, yaw or approach.";
            if (Bounds.size.x <= 0 || Bounds.size.y <= 0 || Bounds.size.z <= 0) yield return "Bounds must have positive dimensions.";
            foreach (var group in Sockets.Where(s => s != null).GroupBy(s => s.Id))
                if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1) yield return "Socket IDs must be nonempty and unique.";
            foreach (var a in Abilities.Where(a => a != null).GroupBy(a => new {a.SkillId,a.VariantId}))
                if (string.IsNullOrWhiteSpace(a.Key.SkillId) || a.Count() > 1) yield return "Ability skill/variant IDs must be nonempty and unique.";
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
