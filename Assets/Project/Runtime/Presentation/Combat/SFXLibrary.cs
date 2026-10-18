using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Central editable collection of combat sound effects.</summary>
    [CreateAssetMenu(fileName = "SFXLibrary", menuName = "Fighting Allstar/Audio/SFX Library")]
    public sealed class SFXLibrary : ScriptableObject
    {
        [SerializeField] private AudioClip hit;
        [SerializeField] private AudioClip criticalHit;
        [SerializeField, Range(0f, 1f)] private float hitVolume = .55f;
        [SerializeField, Range(0f, 1f)] private float criticalVolume = .7f;

        public AudioClip Hit => hit;
        public AudioClip CriticalHit => criticalHit;
        public float HitVolume => hitVolume;
        public float CriticalVolume => criticalVolume;
    }
}
