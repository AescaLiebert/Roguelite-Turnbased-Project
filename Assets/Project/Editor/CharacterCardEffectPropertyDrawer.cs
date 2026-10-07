using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterCardEffect))]
public sealed class CharacterCardEffectPropertyDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        var height = EditorGUIUtility.singleLineHeight;
        foreach (var name in GetVisibleFields(property))
        {
            var child = property.FindPropertyRelative(name);
            if (child == null) continue;
            height += Spacing + EditorGUI.GetPropertyHeight(child, true);
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var y = position.y;
        var foldout = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);
        y += EditorGUIUtility.singleLineHeight;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (var name in GetVisibleFields(property))
            {
                var child = property.FindPropertyRelative(name);
                if (child == null) continue;

                y += Spacing;
                var childHeight = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, childHeight), child, true);
                y += childHeight;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static IEnumerable<string> GetVisibleFields(SerializedProperty property)
    {
        yield return "kind";
        yield return "statusVisual";

        var kindProperty = property.FindPropertyRelative("kind");
        if (kindProperty == null) yield break;

        var kind = (CharacterCardEffectKind)kindProperty.enumValueIndex;
        switch (kind)
        {
            case CharacterCardEffectKind.ApplyStatus:
                yield return "statusId";
                yield return "targetType";
                yield return "applyChancePercent";
                yield return "durationTurns";
                yield return "stackCount";
                yield return "stackCap";
                break;

            case CharacterCardEffectKind.HealAttackMultiplier:
            case CharacterCardEffectKind.HealMissingHealthPercent:
            case CharacterCardEffectKind.HealMaxHealthPercent:
            case CharacterCardEffectKind.DrainPowerGauge:
            case CharacterCardEffectKind.IncreaseCardRank:
                yield return "targetType";
                yield return "magnitude";
                break;

            case CharacterCardEffectKind.CleanseDebuffs:
            case CharacterCardEffectKind.RemoveStance:
            case CharacterCardEffectKind.RemoveBuffs:
                yield return "targetType";
                break;

            case CharacterCardEffectKind.DisableCardType:
                yield return "disabledCardType";
                yield return "targetType";
                break;

            case CharacterCardEffectKind.ModifyStat:
                yield return "statId";
                yield return "targetType";
                yield return "magnitude";
                yield return "durationTurns";
                break;
        }
    }
}
