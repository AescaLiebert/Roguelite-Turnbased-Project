using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Fighting Allstar/Actor Visual Effect Settings")]
    public sealed class ActorVisualEffectSettings : ScriptableObject
    {
        [Header("Starting values — validate at the planning camera and during area grants")]
        [Range(.5f, 1.2f)] public float GrantDuration = .95f;
        [Range(.3f, .8f)] public float StunFlinchDuration = .45f;
        [Range(.4f, 1.5f)] public float IconSize = .9f;
        public Color AttackUp = new Color(1f, .58f, .13f);
        public Color DefenseUp = new Color(.18f, .8f, 1f);
        public Color HealthUp = new Color(.25f, 1f, .58f);
        public Color StatDown = new Color(.8f, .35f, .92f);
        public Color Stun = new Color(1f, .85f, .18f);

        [Header("Persistent debuffs — starting values; test simultaneous status readability")]
        [Range(.3f, 1f)] public float DebuffEnterDuration = .7f;
        [Range(.25f, 1.5f)] public float DebuffParticleDensity = 1f;
        public Color Paralyze = new Color(1f, .8f, .12f);
        public Color Bleed = new Color(1f, .12f, .25f);
        public Color Poison = new Color(.4f, 1f, .2f);
        public Color Shock = new Color(.25f, .8f, 1f);
        public Color Ignite = new Color(1f, .37f, .06f);
    }
}
