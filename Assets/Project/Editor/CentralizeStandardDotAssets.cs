using System;
using FightingAllstar.Core.Content;
using UnityEditor;

/// <summary>One-time repair of inline legacy recipes; runtime normalization remains authoritative for saves/imports.</summary>
public static class CentralizeStandardDotAssets
{
    public static string Run()
    {
        var recipes = 0;
        var changed = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Project/Data" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) continue;
            var serialized = new SerializedObject(asset);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.Generic) continue;
                var id = property.FindPropertyRelative("Id");
                var periodic = property.FindPropertyRelative("PeriodicDamage");
                var behavior = property.FindPropertyRelative("Behavior");
                if (id?.propertyType != SerializedPropertyType.String || periodic == null || behavior == null) continue;
                var coefficient = StandardDotRules.CoefficientFor(id.stringValue);
                if (coefficient == 0) continue;
                recipes++;
                periodic.FindPropertyRelative("CoefficientBp").intValue = coefficient;
                periodic.FindPropertyRelative("Scaling").intValue = (int)StatusSnapshotScaling.TriggeringHealthDamage;
                periodic.FindPropertyRelative("Family").intValue = (int)DamageFamily.DamageOverTime;
                behavior.intValue |= (int)StatusBehavior.DamageOverTime;
            }
            if (!serialized.ApplyModifiedPropertiesWithoutUndo()) continue;
            changed++;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
        return $"Central DOT audit: {recipes} inline recipes; {changed} assets repaired. Shock/Poison 45%, Bleed 70%.";
    }
}
