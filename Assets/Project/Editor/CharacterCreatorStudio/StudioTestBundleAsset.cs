#if UNITY_EDITOR
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Reversible editor-only test activation; never replaces the production roster.</summary>
    public sealed class StudioTestBundleAsset : ScriptableObject
    {
        public StudioDraftAsset SourceDraft;
        public bool UseStudioDefinition;
        [HideInInspector] public string LegacyDefinitionJson;
        [HideInInspector] public string CompiledDefinitionJson;
        [HideInInspector] public string DescriptionJson;
        [HideInInspector] public string HashesJson;
        public CharacterDefinition ResolveDefinition() => SnapshotJson.Deserialize<CharacterDefinition>(
            UseStudioDefinition ? CompiledDefinitionJson : LegacyDefinitionJson);
    }
}
#endif
