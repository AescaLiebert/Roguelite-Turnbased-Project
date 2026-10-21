using System;
using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Fighting Allstar/Battle Entity Presentation")]
    public sealed class BattleEntityPresentationProfile : ScriptableObject
    {
        public string definitionId;
        public GameObject prefab;
        public Sprite portrait;
        public bool teamFront;
        public bool followFormation;
        public Vector3 placementOffset;
        public float modelScale = 1;
        public float modelYaw = 180;
        public Bounds presentationBounds = new Bounds(Vector3.up, Vector3.one * 3);
        public AnimationClip spawn, idle, attack, absorbHit, capacityBreak, despawn;
        public float revealTime = .28f;
        public float emitTime = .48f;
        public Color effectColor = Color.white;
        public AudioClip spawnSound, attackSound, absorbSound, breakSound, despawnSound;
    }

}
