#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public abstract class StudioOwnedSourceEditor : UnityEditor.Editor
    {
        private bool _rawCardFields, _statusVisuals;
        protected abstract bool Owned { get; }
        public override void OnInspectorGUI()
        {
            var character = target as CharacterObject ?? CharacterObjectRegistrySO.LoadAll().FirstOrDefault(c => c.Skill1 == target || c.Skill2 == target || c.Ultimate == target || c.PassiveDefinition == target);
            if(character != null && GUILayout.Button("Open in Character Creator Studio"))
            {
                var window = CharacterCreatorStudioWindow.Open(); window.LoadDraft(StudioProductionBridge.OpenSource(character));
                window.ShowWorkspace(target == character ? "identity" : target == character.Skill1 ? "skill1" : target == character.Skill2 ? "skill2" : target == character.Ultimate ? "ultimate" : "passive");
            }
            if (target is SkillCardSO || target is UltimateCardSO) { DrawCardAsset(); return; }
            if(Owned)EditorGUILayout.HelpBox("Native character source. The Studio edits and saves this asset set directly; visible Inspector fields are mirrors of its complete versioned payload.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Owned))DrawDefaultInspector();
        }

        private void DrawCardAsset()
        {
            var skill = target as SkillCardSO;
            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("CARD ASSET", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(skill != null ? "cardName" : "ultimateName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(skill != null ? "cardIcon" : "icon"), new GUIContent("Artwork"));
            _statusVisuals = EditorGUILayout.Foldout(_statusVisuals, "Status icons", true);
            if (_statusVisuals) EditorGUILayout.PropertyField(serializedObject.FindProperty("statusVisuals"), GUIContent.none, true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(skill != null ? "RANKS" : "ULTIMATE TIERS", EditorStyles.boldLabel);
            if (skill != null)
            {
                for (var i = 1; i <= 3; i++)
                {
                    var rank = skill.GetRankData(i);
                    EditorGUILayout.LabelField("R" + i, rank == null ? "Missing" : rank.skillType + " · " + CardActionSummary(rank.runtimeEffect));
                }
            }
            else
            {
                var ultimate = (UltimateCardSO)target;
                for (var i = 0; i <= 5; i++)
                {
                    var tier = ultimate.GetLevelData(i);
                    EditorGUILayout.LabelField("C" + i, tier == null ? "Missing" : tier.skillType + " · " + CardActionSummary(tier.runtimeEffect));
                }
            }
            EditorGUILayout.HelpBox("Edit ranks, action flow, damage animation and live game text in Character Creator Studio.", MessageType.None);
            _rawCardFields = EditorGUILayout.Foldout(_rawCardFields, "Advanced serialized fields", true);
            if (_rawCardFields) using (new EditorGUI.DisabledScope(Owned)) DrawDefaultInspector();
        }

        private static string CardActionSummary(FightingAllstar.Core.Content.EffectDefinition effect)
        {
            if (effect == null) return "No action";
            var actions = 1 + (effect.Sequence?.Count ?? 0);
            return effect.Kind + " · " + actions + (actions == 1 ? " action" : " actions") +
                (effect.Kind == FightingAllstar.Core.Content.EffectKind.Damage ? " · " + (effect.Attack?.ResolvedHitCount ?? 1) + " hits" : "");
        }
    }
    [CustomEditor(typeof(SkillCardSO))] public sealed class StudioSkillSourceEditor:StudioOwnedSourceEditor {protected override bool Owned=>((SkillCardSO)target).StudioOwned;}
    [CustomEditor(typeof(UltimateCardSO))] public sealed class StudioUltimateSourceEditor:StudioOwnedSourceEditor {protected override bool Owned=>((UltimateCardSO)target).StudioOwned;}
    [CustomEditor(typeof(PassiveDefinitionSO))] public sealed class StudioPassiveSourceEditor:StudioOwnedSourceEditor {protected override bool Owned=>((PassiveDefinitionSO)target).StudioOwned;}
    [CustomEditor(typeof(CharacterObject))] public sealed class StudioCharacterSourceEditor:StudioOwnedSourceEditor {protected override bool Owned=>((CharacterObject)target).StudioOwned;}
}
#endif
