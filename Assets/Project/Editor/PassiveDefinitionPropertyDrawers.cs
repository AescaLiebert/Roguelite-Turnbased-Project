using System.Collections.Generic;
using FightingAllstar.Core.Content;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PassiveDefinition))]
public sealed class PassiveDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, "Id", "Auras", "Reactions");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        PassiveDrawerFields.Draw(position, property, label, "Id", "Auras", "Reactions");
    }
}

[CustomPropertyDrawer(typeof(PassiveAuraDefinition))]
public sealed class PassiveAuraDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var scaling = PassiveDrawerFields.EnumValue<PassiveScaling>(property, "Scaling");
        var fields = new List<string> { "Id", "Gate", "Targets", "Scaling" };
        if (scaling == PassiveScaling.FieldStatusStacks || scaling == PassiveScaling.OwnerCounter)
            fields.Add("ScalingKey");
        if (scaling == PassiveScaling.FieldStatusStacks) fields.Add("ScalingRelation");
        if (scaling == PassiveScaling.OwnerStat) fields.Add("SourceStat");
        if (scaling != PassiveScaling.Constant) fields.Add("MaximumUnits");
        fields.Add("Modifiers");
        return PassiveDrawerFields.Height(property, label, fields.ToArray());
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var scaling = PassiveDrawerFields.EnumValue<PassiveScaling>(property, "Scaling");
        var fields = new List<string> { "Id", "Gate", "Targets", "Scaling" };
        if (scaling == PassiveScaling.FieldStatusStacks || scaling == PassiveScaling.OwnerCounter)
            fields.Add("ScalingKey");
        if (scaling == PassiveScaling.FieldStatusStacks) fields.Add("ScalingRelation");
        if (scaling == PassiveScaling.OwnerStat) fields.Add("SourceStat");
        if (scaling != PassiveScaling.Constant) fields.Add("MaximumUnits");
        fields.Add("Modifiers");
        PassiveDrawerFields.Draw(position, property, label, fields.ToArray());
    }
}

[CustomPropertyDrawer(typeof(StatModifierDefinition))]
public sealed class StatModifierDefinitionPropertyDrawer : PropertyDrawer
{
    // Draw enum mirrors when Unity omits deeply nested enum SerializedProperties.
    private const float Spacing = 2f;
    private static readonly string[] ScalarFields = { "Amount", "ScaleByStatusPotency", "PotencyCoefficientBp" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
        var lines = 5; // Target, Stat, Bundle, Family, Operation.
        foreach (var name in ScalarFields)
        {
            if (name == "PotencyCoefficientBp" && !PassiveDrawerFields.BoolValue(property, "ScaleByStatusPotency")) continue;
            if (property.FindPropertyRelative(name) != null) lines++;
        }
        return EditorGUIUtility.singleLineHeight + lines * (EditorGUIUtility.singleLineHeight + Spacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var y = position.y;
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);
        y += EditorGUIUtility.singleLineHeight;

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawEnum<ModifierTarget>(position, ref y, property, "Target", "SerializedTarget", "Target");
            DrawEnum<StatId>(position, ref y, property, "Stat", "SerializedStat", "Stat");
            DrawEnum<StatBundleKind>(position, ref y, property, "Bundle", "SerializedBundle", "Stat Bundle");
            DrawEnum<DamageFamily>(position, ref y, property, "Family", "SerializedFamily", "Damage Family");
            DrawEnum<ModifierOperation>(position, ref y, property, "Operation", "SerializedOperation", "Operation");

            foreach (var name in ScalarFields)
            {
                if (name == "PotencyCoefficientBp" && !PassiveDrawerFields.BoolValue(property, "ScaleByStatusPotency")) continue;
                var child = property.FindPropertyRelative(name);
                if (child == null) continue;
                y += Spacing;
                var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                var content = name == "Amount"
                    ? new GUIContent("Amount", "For Percent Of Base, -2,500 means -25% of the base stat.")
                    : new GUIContent(child.displayName);
                EditorGUI.PropertyField(rect, child, content, true);
                y += EditorGUIUtility.singleLineHeight;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    private static void DrawEnum<T>(Rect position, ref float y, SerializedProperty property,
        string enumName, string fallbackName, string displayName) where T : System.Enum
    {
        var enumProperty = property.FindPropertyRelative(enumName);
        var fallbackProperty = property.FindPropertyRelative(fallbackName);
        var value = fallbackProperty != null && fallbackProperty.intValue >= 0
            ? fallbackProperty.intValue
            : enumProperty != null ? enumProperty.intValue : 0;

        y += Spacing;
        var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.BeginChangeCheck();
        var selected = EditorGUI.EnumPopup(rect, displayName, (T)System.Enum.ToObject(typeof(T), value));
        if (EditorGUI.EndChangeCheck())
        {
            var selectedValue = System.Convert.ToInt32(selected);
            if (fallbackProperty != null) fallbackProperty.intValue = selectedValue;
            else if (enumProperty != null) enumProperty.intValue = selectedValue;
        }
        y += EditorGUIUtility.singleLineHeight;
    }
}

[CustomPropertyDrawer(typeof(PassiveReactionDefinition))]
public sealed class PassiveReactionDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var trigger = PassiveDrawerFields.EnumValue<PassiveEventKind>(property, "Trigger");
        var fields = new List<string> { "Id", "Gate", "Trigger" };
        if (trigger == PassiveEventKind.TeamTurnStarted || trigger == PassiveEventKind.TeamTurnEnded)
            fields.Add("OwnTeamTurnOnly");
        fields.Add("ActorRelation");
        fields.Add("TargetRelation");

        if (trigger == PassiveEventKind.GaugeChanged)
        {
            fields.Add("CardOriginOnly");
            fields.Add("ExcludeUltimate");
            fields.Add("RequireGaugeLoss");
        }
        if (trigger == PassiveEventKind.BeforeAction || trigger == PassiveEventKind.AfterAction ||
            trigger == PassiveEventKind.BeforeDamage || trigger == PassiveEventKind.DamageResolved)
        {
            fields.Add("CardOriginOnly");
            fields.Add("ExcludeUltimate");
            if (trigger != PassiveEventKind.BeforeDamage)
            {
                fields.Add("RequireCritical");
                fields.Add("RequireBlocked");
            }
            fields.Add("FilterDamageFamily");
            if (PassiveDrawerFields.BoolValue(property, "FilterDamageFamily")) fields.Add("DamageFamily");
            fields.Add("FilterCategory");
            if (PassiveDrawerFields.BoolValue(property, "FilterCategory")) fields.Add("Category");
            if (PassiveDrawerFields.BoolValue(property, "FilterCategory")) fields.Add("MinimumRank");
        }
        fields.Add("AllowReactionOrigin");
        fields.Add("LimitScope");
        fields.Add("MaximumActivations");
        fields.Add("CooldownOwnerTurns");
        fields.Add("Conditions");
        fields.Add("Commands");
        fields.Add("ElseCommands");
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(PassiveCommandDefinition))]
public sealed class PassiveCommandDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var kind = PassiveDrawerFields.EnumValue<PassiveCommandKind>(property, "Kind");
        var fields = new List<string> { "Kind" };
        if (kind == PassiveCommandKind.IncrementCounter || kind == PassiveCommandKind.SetCounter)
        {
            fields.Add("CounterKey");
            fields.Add("CounterCap");
            fields.Add("Amount");
        }
        if (kind == PassiveCommandKind.ChangePowerGauge)
        {
            fields.Add("ValueSource");
            fields.Add("Amount");
        }
        if (kind == PassiveCommandKind.IncreaseCurrentAttackPercent ||
            kind == PassiveCommandKind.IncreaseCurrentDamageDealtPercent)
            fields.Add("Amount");
        if (kind == PassiveCommandKind.ExecuteEffect)
        {
            fields.Add("Effect");
            fields.Add("DelayOwnerTurns");
        }
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(EffectDefinition))]
public sealed class PassiveEffectDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var kind = PassiveDrawerFields.EnumValue<EffectKind>(property, "Kind");
        var fields = new List<string> { "Id", "Kind", "Target", "Conditions" };
        if (kind == EffectKind.Damage)
        {
            fields.Add("Attack");
            fields.Add("Family");
            fields.Add("Scaling");
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.SpecificStat)
                fields.Add("ScalingStat");
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.Fixed)
                fields.Add("Magnitude");
            fields.Add("CoefficientBp");
            fields.Add("KeywordFactorBp");
            fields.Add("KeywordId");
            fields.Add("Tags");
        }
        else if (kind == EffectKind.ApplyStatus)
        {
            fields.Add("StatusRecipe");
            fields.Add("StatusApplyChanceBp");
            fields.Add("StatusDurationOverride");
            fields.Add("StatusStackCount");
            fields.Add("StatusPotencyBp");
        }
        else if (kind == EffectKind.Heal)
        {
            fields.Add("HealValue");
            fields.Add("HealScalingStat");
            fields.Add("HealCoefficientBp");
        }
        else if (kind == EffectKind.ChangePowerGauge)
        {
            fields.Add("PowerGaugeAmount");
        }
        else if (kind == EffectKind.ModifyCardRank)
        {
            fields.Add("Magnitude");
        }
        fields.Add("Sequence");
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(StatusRecipeDefinition))]
public sealed class StatusRecipeDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var behaviorProperty = property.FindPropertyRelative("Behavior");
        var behavior = behaviorProperty == null ? StatusBehavior.None : (StatusBehavior)behaviorProperty.intValue;
        var stacking = PassiveDrawerFields.EnumValue<StatusStackingPolicy>(property, "Stacking");
        var fields = new List<string>
        {
            "Id", "NameKey", "Polarity", "Color", "Behavior", "Stacking", "Identity",
            "DurationClock", "DefaultDuration",
            "BypassDebuffImmunity", "BreakRule", "Tags"
        };

        if (stacking == StatusStackingPolicy.AddStacks || stacking == StatusStackingPolicy.IndependentStacks)
            fields.Add("MaxStacks");
        if ((behavior & (StatusBehavior.Stat | StatusBehavior.DamageOverTime)) != 0)
            fields.Add("DefaultPotencyBp");
        if ((behavior & StatusBehavior.Stat) != 0) fields.Add("Modifiers");
        if ((behavior & StatusBehavior.DamageOverTime) != 0) fields.Add("PeriodicDamage");
        if ((behavior & StatusBehavior.Heal) != 0) fields.Add("PeriodicHealing");
        if ((behavior & StatusBehavior.Disable) != 0) fields.Add("DisableMask");
        if ((behavior & StatusBehavior.Stance) != 0)
        {
            fields.AddRange(new[]
            {
                "DebuffImmunity", "AdditionalDamageImmunity", "EvadeAttacks",
                "StanceChildren", "RecoverDamageTakenBp"
            });
            fields.AddRange(new[] { "SurviveLethalCharges", "IgnoreCritResistanceBp", "IgnoreCritDefenseBp", "ImmuneStatusTags" });
        }
        if ((behavior & StatusBehavior.Triggered) != 0)
        {
            fields.AddRange(new[] { "Reactions", "SurviveLethalCharges", "IgnoreCritResistanceBp", "IgnoreCritDefenseBp", "ImmuneStatusTags" });
        }
        if ((behavior & (StatusBehavior.Stance | StatusBehavior.Triggered)) != 0)
        {
            fields.Add("CounterEnabled");
            if (PassiveDrawerFields.BoolValue(property, "CounterEnabled"))
                fields.AddRange(new[] { "CounterCategory", "CounterTarget", "CounterConditions", "CounterEffect" });
        }

        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(StatusApplicationRecipe))]
public sealed class StatusApplicationRecipePropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var fields = new List<string> { "StatusRecipeId", "DurationOverride", "StackCount", "PotencySource" };
        if (PassiveDrawerFields.EnumValue<StatusPotencySource>(property, "PotencySource") == StatusPotencySource.Authored)
            fields.Add("PotencyBp");
        else
        {
            fields.Add("PotencySourceRecipeId");
            fields.Add("PotencyBp");
        }
        fields.Add("ProcChanceBp");
        fields.Add("InlineRecipe");
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(CardEffectStep))]
public sealed class CardEffectStepPropertyDrawer : PropertyDrawer
{
    private static readonly string[] Fields = { "Timing", "Effect" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, Fields);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, Fields);
}

[CustomPropertyDrawer(typeof(CounterEffectDefinition))]
public sealed class CounterEffectDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property, includeSequence: true));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property, includeSequence: true));

    internal static string[] GetFields(SerializedProperty property, bool includeSequence)
    {
        var kind = PassiveDrawerFields.EnumValue<EffectKind>(property, "Kind");
        var fields = new List<string> { "Kind", "Target" };
        if (kind == EffectKind.Damage)
        {
            fields.Add("Attack");
            fields.AddRange(new[] { "Family", "Scaling" });
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.SpecificStat)
                fields.Add("ScalingStat");
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.Fixed)
                fields.Add("Magnitude");
            fields.Add("CoefficientBp");
            fields.Add("KeywordId");
        }
        else if (kind == EffectKind.ApplyStatus)
        {
            fields.AddRange(new[] { "StatusRecipeId", "StatusPolarity", "StatusApplyChanceBp", "StatusDurationOverride", "StatusStackCount", "StatusPotencyBp" });
        }
        else if (kind == EffectKind.Heal)
        {
            fields.AddRange(new[] { "HealSource", "HealAmount", "HealCoefficientBp" });
        }
        else if (kind == EffectKind.ChangePowerGauge)
        {
            fields.Add("PowerGaugeAmount");
        }
        if (includeSequence) fields.Add("Sequence");
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(EffectConditionDefinition))]
public sealed class EffectConditionDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        // Unity-authored condition lists are flat implicit-AND groups. Recursive condition
        // groups remain available to runtime/JSON without entering Unity's serialized type tree.
        var fields = new List<string>();
        var kind = PassiveDrawerFields.EnumValue<EffectConditionKind>(property, "Kind");
        fields.Add("Kind");
        fields.Add("Subject");
        switch (kind)
        {
            case EffectConditionKind.SourceHasStatusTag:
            case EffectConditionKind.TargetHasStatusTag:
                fields.Add("Tag");
                fields.Add("Threshold");
                break;
            case EffectConditionKind.TargetHasBuff:
            case EffectConditionKind.TargetHasDebuff:
            case EffectConditionKind.SourceGaugeAtLeast:
            case EffectConditionKind.TargetGaugeAtLeast:
            case EffectConditionKind.SourceHealthAtMost:
            case EffectConditionKind.TargetHealthAtMost:
                fields.Add("Threshold");
                break;
            case EffectConditionKind.TargetHasRecipe:
            case EffectConditionKind.TargetHasRecipeFromEffectOwner:
                fields.Add("RecipeId");
                fields.Add("Threshold");
                break;
            case EffectConditionKind.TargetAttributeIs:
            case EffectConditionKind.TargetTraitIs:
            case EffectConditionKind.TargetSeriesIs:
            case EffectConditionKind.CounterAtLeast:
                fields.Add("StringValue");
                if (kind == EffectConditionKind.CounterAtLeast) fields.Add("Threshold");
                break;
            case EffectConditionKind.RosterCountAtLeast:
                fields.Add("RosterFilter");
                fields.Add("InitialRoster");
                fields.Add("Threshold");
                break;
        }
        fields.Add("Negate");
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(StanceChildDefinition))]
public sealed class StanceChildDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var fields = new List<string>
        {
            "Id", "NameKey", "DebuffImmunity", "AdditionalDamageImmunity", "EvadeAttacks", "Taunt",
            "RecoverDamageTakenBp", "Modifiers", "CounterEnabled"
        };
        if (PassiveDrawerFields.BoolValue(property, "CounterEnabled"))
            fields.AddRange(new[] { "CounterCategory", "CounterTarget", "CounterConditions", "CounterEffect" });
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(CounterOperationDefinition))]
public sealed class CounterOperationDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, CounterEffectDefinitionPropertyDrawer.GetFields(property, includeSequence: false));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, CounterEffectDefinitionPropertyDrawer.GetFields(property, includeSequence: false));
}

[CustomPropertyDrawer(typeof(EffectValueDefinition))]
public sealed class EffectValueDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var source = PassiveDrawerFields.EnumValue<EffectValueSource>(property, "Source");
        var fields = new List<string> { "Source" };
        if (source == EffectValueSource.Fixed) fields.Add("FixedAmount");
        else fields.Add("CoefficientBp");
        fields.Add("MaximumAmount");
        return fields.ToArray();
    }
}

internal static class PassiveDrawerFields
{
    private const float Spacing = 2f;

    public static T EnumValue<T>(SerializedProperty property, string name) where T : struct
    {
        var child = property?.FindPropertyRelative(name);
        if (child == null || child.propertyType != SerializedPropertyType.Enum)
            return default;

        // intValue preserves flags and explicit enum values; enumValueIndex is only the
        // popup index and can differ from the value stored in the serialized asset.
        return (T)System.Enum.ToObject(typeof(T), child.intValue);
    }

    public static bool BoolValue(SerializedProperty property, string name)
    {
        var child = property.FindPropertyRelative(name);
        return child != null && child.boolValue;
    }

    public static float Height(SerializedProperty property, GUIContent label, params string[] names)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
        var height = EditorGUIUtility.singleLineHeight;
        foreach (var name in names)
        {
            var child = property.FindPropertyRelative(name);
            if (child == null) continue;
            height += Spacing + EditorGUI.GetPropertyHeight(child, true);
        }
        return height;
    }

    public static void Draw(Rect position, SerializedProperty property, GUIContent label, params string[] names)
    {
        EditorGUI.BeginProperty(position, label, property);
        var y = position.y;
        var foldout = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);
        y += EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (var name in names)
            {
                var child = property.FindPropertyRelative(name);
                if (child == null) continue;
                y += Spacing;
                var childHeight = EditorGUI.GetPropertyHeight(child, true);
                var childRect = new Rect(position.x, y, position.width, childHeight);
                if (name == "StatusApplyChanceBp")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Apply Chance (basis points)", "10,000 = 100%. The resolved chance is also affected by Control and Avoidance."), true);
                else if (name == "Modifiers")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Modifiers", "Requires Behavior to include Stat. Add a modifier with Target = Stat to change any stat, including Defense."), true);
                else if (name == "Behavior" && property.type == "StatusRecipeDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Behavior", "Enable Stat to expose the Modifiers list for stat changes."), true);
                else if (name == "Amount" && property.type == "StatModifierDefinition")
                {
                    var operation = PassiveDrawerFields.EnumValue<ModifierOperation>(property, "Operation");
                    var tooltip = operation == ModifierOperation.PercentOfBase
                        ? "Basis points of the stat's base value; -2,500 = -25%."
                        : operation == ModifierOperation.PercentagePoints
                            ? "Basis points; 10,000 = 100 percentage points."
                            : "Amount in the stat's native units unless the selected operation says otherwise.";
                    EditorGUI.PropertyField(childRect, child, new GUIContent("Amount", tooltip), true);
                }
                else if (name == "StringValue")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Condition Value", "For traits, attributes, and series you may enter either the short value (women) or the full content ID (trait.women)."), true);
                else if (name == "Conditions")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Conditions (all required)", "This effect only resolves when every condition passes for its resolved target."), true);
                else if (name == "CounterConditions")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Counter Conditions (all required)", "The reactive status counter only fires when every condition passes. Operation Target is the attacker."), true);
                else if (name == "Bundle")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Stat Bundle", "One modifier applies to every member. ATK/DEF/HP use percent-of-base; sub-stats use percentage points."), true);
                else
                    EditorGUI.PropertyField(childRect, child, true);
                y += childHeight;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
