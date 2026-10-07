using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CardRankData))]
public sealed class CardRankDataPropertyDrawer : PropertyDrawer
{
    private static readonly string[] Fields = { "description", "skillType", "runtimeEffect" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, RankLabel(property, label), Fields);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, RankLabel(property, label), Fields);

    private static GUIContent RankLabel(SerializedProperty property, GUIContent fallback)
    {
        var path = property.propertyPath;
        var marker = path.LastIndexOf("data[", System.StringComparison.Ordinal);
        if (marker < 0) return fallback;

        marker += 5;
        var end = path.IndexOf(']', marker);
        return end > marker && int.TryParse(path.Substring(marker, end - marker), out var index)
            ? new GUIContent("Rank " + (index + 1))
            : fallback;
    }
}
