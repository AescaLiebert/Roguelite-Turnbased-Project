using System;
using UnityEngine;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>
    /// Binds authored world-space meadow data to a translated stage. Native
    /// Terrain limits this stage to world rotation identity and world scale one.
    /// The canonical placement asset and its mesh/material dependencies stay intact.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TeyvatStageBinding : MonoBehaviour
    {
        public Vector3 authoredWorldOrigin;
        public TeyvatMeadowRenderer meadow;
        public TeyvatMeadowData canonicalMeadowData;
        public TeyvatMaterialGlobals globals;
        public Vector4 authoredColorBounds;

        [NonSerialized] public int boundCellCount;
        [NonSerialized] public int boundPlacementCount;
        [NonSerialized] public int cloneCreationCount;
        [NonSerialized] public int rebaseCount;

        TeyvatMeadowData ownedData;
        TeyvatMeadowData clonedSource;
        TeyvatMeadowRenderer boundMeadow;
        TeyvatMaterialGlobals boundGlobals;
        Vector3 appliedTranslation;
        Vector4 appliedColorBounds;
        bool hasAppliedTranslation;
        bool unsupportedWarningLogged;
        bool bindingDirty = true;

        /// <summary>The temporary, unsaved data instance; never edit its source asset.</summary>
        public TeyvatMeadowData RuntimeMeadowData { get { return ownedData; } }

        public Vector3 WorldTranslation { get { return transform.position - authoredWorldOrigin; } }

        public bool TransformSupported
        {
            get
            {
                var position = transform.position;
                var scale = transform.lossyScale;
                var rotation = transform.rotation;
                return Finite(position) && Finite(authoredWorldOrigin) && Finite(scale)
                    && Finite(rotation.x) && Finite(rotation.y) && Finite(rotation.z) && Finite(rotation.w)
                    && Quaternion.Angle(rotation, Quaternion.identity) <= .01f
                    && (scale - Vector3.one).sqrMagnitude <= .00000001f;
            }
        }

        void OnEnable() { bindingDirty = true; ApplyBinding(); }
        void Update() { ApplyBinding(); }

        // OnValidate can run during asset loading. Defer Unity object creation to
        // the next regular update or the builder's explicit ApplyBinding call.
        void OnValidate() { bindingDirty = true; }

        void OnDisable() { ReleaseOwnedData(); }
        void OnDestroy() { ReleaseOwnedData(); }

        public void ApplyBinding()
        {
            if (!TransformSupported)
            {
                if (!unsupportedWarningLogged)
                {
                    Debug.LogWarning("Teyvat stage binding supports translation only. Keep the stage and its parents at world rotation identity and world scale one; the current transform was left unchanged.", this);
                    unsupportedWarningLogged = true;
                }
                return;
            }
            unsupportedWarningLogged = false;

            if (ownedData != null && clonedSource != canonicalMeadowData)
                ReleaseOwnedData();

            if (boundMeadow != meadow)
            {
                RestoreBoundMeadow();
                boundMeadow = meadow;
                bindingDirty = true;
            }

            // Builders assign these explicit references after AddComponent has
            // enabled the behaviour. Wait without creating placeholder data.
            if (canonicalMeadowData == null || meadow == null)
                return;

            if (ownedData == null)
                CreateOwnedData();

            var translation = WorldTranslation;
            var colorBounds = new Vector4(authoredColorBounds.x + translation.x,
                authoredColorBounds.y + translation.z, authoredColorBounds.z, authoredColorBounds.w);
            bool translationChanged = !hasAppliedTranslation || !translation.Equals(appliedTranslation);
            if (translationChanged)
            {
                RebaseFromCanonical(translation);
                appliedTranslation = translation;
                hasAppliedTranslation = true;
                rebaseCount++;
            }

            if (meadow.data != ownedData)
            {
                meadow.data = ownedData;
                bindingDirty = true;
            }
            if (meadow.globals != globals)
                meadow.globals = globals;

            if (translationChanged || bindingDirty)
                meadow.InvalidatePlacementCache();

            if (globals != null && (boundGlobals != globals || bindingDirty
                || !colorBounds.Equals(appliedColorBounds) || !globals.colorBounds.Equals(colorBounds)))
            {
                globals.colorBounds = colorBounds;
                globals.Apply();
                appliedColorBounds = colorBounds;
            }
            boundGlobals = globals;
            bindingDirty = false;
        }

        void CreateOwnedData()
        {
            clonedSource = canonicalMeadowData;
            ownedData = Instantiate(clonedSource);
            ownedData.name = clonedSource.name + " (stage runtime copy)";
            ownedData.hideFlags = HideFlags.DontSave;

            // Explicit copies make ownership independent of Unity's nested-array
            // clone behaviour. Meshes and materials remain shared asset references.
            var sourceSpecies = clonedSource.species ?? Array.Empty<TeyvatMeadowData.Species>();
            ownedData.species = new TeyvatMeadowData.Species[sourceSpecies.Length];
            for (int i = 0; i < sourceSpecies.Length; i++)
            {
                var source = sourceSpecies[i];
                if (source == null) continue;
                ownedData.species[i] = new TeyvatMeadowData.Species
                {
                    name = source.name,
                    meshLOD0 = source.meshLOD0,
                    meshLOD1 = source.meshLOD1,
                    materialsLOD0 = source.materialsLOD0 == null ? Array.Empty<Material>() : (Material[])source.materialsLOD0.Clone(),
                    materialsLOD1 = source.materialsLOD1 == null ? Array.Empty<Material>() : (Material[])source.materialsLOD1.Clone()
                };
            }

            var sourceCells = clonedSource.cells ?? Array.Empty<TeyvatMeadowData.Cell>();
            ownedData.cells = new TeyvatMeadowData.Cell[sourceCells.Length];
            boundCellCount = 0;
            boundPlacementCount = 0;
            for (int i = 0; i < sourceCells.Length; i++)
            {
                var source = sourceCells[i];
                if (source == null) continue;
                var placements = source.placements == null ? Array.Empty<TeyvatMeadowData.Placement>()
                    : (TeyvatMeadowData.Placement[])source.placements.Clone();
                ownedData.cells[i] = new TeyvatMeadowData.Cell
                {
                    name = source.name,
                    worldBounds = source.worldBounds,
                    placements = placements
                };
                boundCellCount++;
                boundPlacementCount += placements.Length;
            }
            ownedData.placementCount = boundPlacementCount;
            hasAppliedTranslation = false;
            bindingDirty = true;
            cloneCreationCount++;
        }

        void RebaseFromCanonical(Vector3 translation)
        {
            var bounds = clonedSource.worldBounds;
            bounds.center += translation;
            ownedData.worldBounds = bounds;
            for (int cellIndex = 0; cellIndex < ownedData.cells.Length; cellIndex++)
            {
                var target = ownedData.cells[cellIndex];
                var source = clonedSource.cells[cellIndex];
                if (target == null || source == null) continue;
                bounds = source.worldBounds;
                bounds.center += translation;
                target.worldBounds = bounds;
                for (int placementIndex = 0; placementIndex < target.placements.Length; placementIndex++)
                {
                    var placement = source.placements[placementIndex];
                    var matrix = placement.matrix;
                    matrix.m03 += translation.x;
                    matrix.m13 += translation.y;
                    matrix.m23 += translation.z;
                    placement.matrix = matrix;
                    target.placements[placementIndex] = placement;
                }
            }
        }

        void RestoreBoundMeadow()
        {
            if (boundMeadow != null && ownedData != null && boundMeadow.data == ownedData)
            {
                boundMeadow.data = clonedSource;
                boundMeadow.InvalidatePlacementCache();
            }
        }

        void ReleaseOwnedData()
        {
            RestoreBoundMeadow();
            var released = ownedData;
            ownedData = null;
            clonedSource = null;
            boundMeadow = null;
            boundGlobals = null;
            hasAppliedTranslation = false;
            bindingDirty = true;
            boundCellCount = 0;
            boundPlacementCount = 0;
            if (released != null)
            {
                if (Application.isPlaying) Destroy(released);
                else DestroyImmediate(released);
            }
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    }
}
