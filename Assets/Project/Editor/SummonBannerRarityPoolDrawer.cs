using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    [CustomPropertyDrawer(typeof(SummonBannerRarityPool))]
    public sealed class SummonBannerRarityPoolDrawer : PropertyDrawer
    {
        private static bool IsShared(SerializedProperty property)
        {
            var rarity = (FighterRarity)property.FindPropertyRelative("rarity").intValue;
            return rarity == FighterRarity.R || rarity == FighterRarity.SR;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            return (line + spacing) * 3 + (IsShared(property)
                ? line * 2
                : EditorGUI.GetPropertyHeight(property.FindPropertyRelative("characters"), true));
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var row = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                row.y += line + spacing;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("rarity"));
                row.y += line + spacing;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("rateBasisPoints"));
                row.y += line + spacing;
                if (IsShared(property))
                {
                    row.height = line * 2;
                    EditorGUI.HelpBox(EditorGUI.IndentedRect(row),
                        "Automatically includes all characters of this rarity across all series.", MessageType.Info);
                }
                else
                {
                    var characters = property.FindPropertyRelative("characters");
                    row.height = EditorGUI.GetPropertyHeight(characters, true);
                    EditorGUI.PropertyField(row, characters, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
