using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Rendering
{
    [CreateAssetMenu(menuName="Fighting Allstar/Rendering/Map Optimization Policy")]
    public sealed class MapOptimizationPolicy : ScriptableObject
    {
        [Tooltip("Starting budget; validate in a Windows player on target hardware.")]
        public int drawCallBudget=2000;
        public float preferredCpuMilliseconds=16.67f,maximumCpuMilliseconds=50;
        public bool useBakedOcclusion=true,combineStaticMeshes=true;
        [Tooltip("A batching partition, not a draw-distance or quality reduction.")]
        public float spatialCellMetres=128;
        public float smallestOccluder=5,smallestHole=.25f,backfaceThreshold=100;
        public Vector3 cameraVolumeSize=new Vector3(90,45,90);
    }

}

