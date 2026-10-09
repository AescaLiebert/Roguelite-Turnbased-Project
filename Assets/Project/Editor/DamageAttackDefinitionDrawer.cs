using FightingAllstar.Core.Content;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DamageAttackDefinition))]
public sealed class DamageAttackDefinitionDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Presets = BuildLabels();
    private static GUIContent[] BuildLabels()
    {
        var labels = new GUIContent[DamageAnimationTemplates.All.Count];
        for (var i = 0; i < labels.Length; i++)
        {
            var template = DamageAnimationTemplates.All[i];
            labels[i] = new GUIContent($"{(template.Hits > 3 ? "Barrage" : template.Hits + " Hit")}/{template.Range} / {(template.Area ? "AOE" : "Single")} — {template.Name}");
        }
        return labels;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        (EditorGUIUtility.singleLineHeight + 2) * 6 + 58;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(row, "Damage Animation", EditorStyles.boldLabel);
        row.y += row.height + 2;
        var hits = property.FindPropertyRelative("HitCount");
        var range = property.FindPropertyRelative("Range");
        var parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
        var scope = property.serializedObject.FindProperty(parentPath + ".Target");
        var area = scope != null && scope.intValue == (int)EffectTargetScope.AllEnemies;
        var index = DamageAnimationTemplates.Index(hits.intValue, (AttackRange)range.intValue, area);
        EditorGUI.BeginChangeCheck();
        var selected = EditorGUI.Popup(row, new GUIContent("Template", "Selecting a preset sets hit count, range and target scope."), index, Presets);
        if (EditorGUI.EndChangeCheck())
        {
            var template = DamageAnimationTemplates.All[selected];
            hits.intValue = template.Hits;
            range.intValue = (int)template.Range;
            if (scope != null) scope.intValue = (int)(template.Area ? EffectTargetScope.AllEnemies : EffectTargetScope.SelectedEnemy);
        }
        row.y += row.height + 2;
        EditorGUI.IntSlider(row, hits, 1, 10, new GUIContent("Hit Count"));
        row.y += row.height + 2;
        EditorGUI.PropertyField(row, range);
        row.y += row.height + 2;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("Reaction"), new GUIContent("Target Reaction"));
        row.y += row.height + 2;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("ReactionTiming"), new GUIContent("Reaction On"));
        row.y += row.height + 2;
        row.height = 56;
        EditorGUI.HelpBox(row, "Damage is split across hits, each rolling crit/block. Damaging = first; After Damage = last. Stance resists reactions; cancelling a stance forces knockback.", MessageType.Info);
        EditorGUI.EndProperty();
    }
}
