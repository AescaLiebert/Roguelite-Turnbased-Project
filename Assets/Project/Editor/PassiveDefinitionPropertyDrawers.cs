using System.Collections.Generic;
using FightingAllstar.Core.Content;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PassiveDefinition))]
public sealed class PassiveDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, "Id", "Support", "Auras", "Reactions", "Triggers");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        PassiveDrawerFields.Draw(position, property, label, "Id", "Support", "Auras", "Reactions", "Triggers");
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
        if (trigger == PassiveEventKind.DamageResolved) fields.Add("DamageTiming");
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
        if (kind == PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent)
        {
            fields.Add("ScalingStat");
            fields.Add("ScalingCoefficientBp");
        }
        if (kind == PassiveCommandKind.ExecuteEffect)
        {
            fields.Add("Effect");
        }
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(EffectDefinition))]
public sealed class PassiveEffectDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, EffectDrawerFields.GetFields(property, includeSequence: true));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, EffectDrawerFields.GetFields(property, includeSequence: true));
}

[CustomPropertyDrawer(typeof(EffectOperationDefinition))]
public sealed class EffectOperationDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, EffectDrawerFields.GetFields(property, includeSequence: false));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, EffectDrawerFields.GetFields(property, includeSequence: false));
}

internal static class EffectDrawerFields
{
    public static string[] GetFields(SerializedProperty property, bool includeSequence)
    {
        var kind = PassiveDrawerFields.EnumValue<EffectKind>(property, "Kind");
        var fields = new List<string> { "Id", "Kind" };
        if (kind == EffectKind.None) return fields.ToArray();

        fields.Add("Target");
        fields.Add("Conditions");
        if (kind == EffectKind.Damage)
        {
            fields.Add("Attack");
            fields.Add("Family");
            fields.Add("DamageElement");
            fields.Add("Scaling");
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.SpecificStat)
                fields.Add("ScalingStat");
            if (PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling") == StatScaling.Fixed)
                fields.Add("Magnitude");
            fields.Add("CoefficientBp");
            fields.Add("KeywordFactorBp");
            fields.Add("KeywordId");
            if (includeSequence)
            {
                fields.Add("ConsumeTargetRecipeId");
                fields.Add("DamagePerConsumedStackBp");
                fields.Add("ElementalApplication");
            }
            if (includeSequence) fields.Add("AttributeAdvantageDamageBonusBp");
        }
        else if (kind == EffectKind.ApplyStatus)
        {
            fields.Add("StatusRecipe");
            var stepPath = property.propertyPath.EndsWith(".Effect")
                ? property.propertyPath.Substring(0, property.propertyPath.Length - ".Effect".Length) : null;
            var batchId = stepPath == null ? null : property.serializedObject.FindProperty(stepPath + ".BatchId");
            if (batchId == null || string.IsNullOrWhiteSpace(batchId.stringValue)) fields.Add("StatusApplyChanceBp");
            fields.Add("StatusDurationOverride");
            fields.Add("StatusStackCount");
            fields.Add("StatusPotencyBp");
        }
        else if (kind == EffectKind.Heal)
        {
            fields.Add("HealValue");
        }
        else if (kind == EffectKind.ChangePowerGauge)
        {
            fields.Add("PowerGaugeAmount");
        }
        else if (kind == EffectKind.ModifyCardRank)
        {
            fields.Add("Magnitude");
        }
        if (includeSequence)
        {
            fields.Add("Batches");
            fields.Add("Sequence");
        }
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(DamageEffectRecipe))]
public sealed class DamageEffectRecipePropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var family = PassiveDrawerFields.EnumValue<DamageFamily>(property, "Family");
        var scaling = PassiveDrawerFields.EnumValue<StatScaling>(property, "Scaling");
        var fields = new List<string> { "Family", "Scaling" };
        if (scaling == StatScaling.Fixed) fields.Add("FixedAmount");
        else fields.Add("StatSource");
        if (scaling == StatScaling.SpecificStat) fields.Add("ScalingStat");
        fields.Add("CoefficientBp");
        if (family == DamageFamily.Normal || family == DamageFamily.True || family == DamageFamily.Destructive)
        {
            fields.Add("KeywordFactorBp");
            fields.Add("AttackEffectId");
            if (family != DamageFamily.Destructive) fields.AddRange(new[] { "CannotCrit", "CannotBlock" });
        }
        return fields.ToArray();
    }
}

[CustomPropertyDrawer(typeof(StatTransferRecipeDefinition))]
public sealed class StatTransferRecipeDefinitionPropertyDrawer : PropertyDrawer
{
    private static readonly string[] Fields = { "CoefficientBp", "Stats", "SourceStatus", "TargetStatus" };
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, Fields);
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, Fields);
}

[CustomPropertyDrawer(typeof(CardEffectOperationDefinition))]
public sealed class CardEffectOperationDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, GetFields(property));

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, GetFields(property));

    private static string[] GetFields(SerializedProperty property)
    {
        var kind = PassiveDrawerFields.EnumValue<CardEffectOperationKind>(property, "Kind");
        var fields = new List<string> { "Id", "Window", "Kind", "Target", "Conditions" };
        switch (kind)
        {
            case CardEffectOperationKind.Damage:
                fields.Add("Damage");
                break;
            case CardEffectOperationKind.ApplyStatus:
                fields.Add("Status");
                break;
            case CardEffectOperationKind.RemoveStatus:
                fields.Add("RemovePolarity");
                break;
            case CardEffectOperationKind.Heal:
                fields.Add("Value");
                break;
            case CardEffectOperationKind.ChangePowerGauge:
            case CardEffectOperationKind.ModifyCardRank:
                fields.Add("Magnitude");
                break;
            case CardEffectOperationKind.TransferStats:
                fields.Add("StatTransfer");
                break;
        }
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
        var transferTemplate = property.name == "SourceStatus" || property.name == "TargetStatus";
        var fields = new List<string>
        {
            "Id", "NameKey", "Polarity", "Color", "Behavior", "Stacking", "Identity",
            "DurationClock", "DefaultDuration",
            "BypassDebuffImmunity", "BreakRule", "Tags"
        };

        if ((behavior & StatusBehavior.Barrier) != 0) fields.Remove("Stacking");
        if ((behavior & StatusBehavior.Barrier) == 0 &&
            (stacking == StatusStackingPolicy.AddStacks || stacking == StatusStackingPolicy.IndependentStacks))
            fields.Add("MaxStacks");
        if (transferTemplate) fields.Remove("Behavior");
        if ((behavior & (StatusBehavior.Stat | StatusBehavior.DamageOverTime)) != 0 && !transferTemplate)
            fields.Add("DefaultPotencyBp");
        // Transfer modifiers are snapshots computed from the selected Stats and coefficient.
        if ((behavior & StatusBehavior.Stat) != 0 && !transferTemplate) fields.Add("Modifiers");
        if ((behavior & StatusBehavior.DamageOverTime) != 0) fields.Add("PeriodicDamage");
        if ((behavior & StatusBehavior.Barrier) != 0)
            fields.AddRange(new[] { "BarrierSourceStat", "BarrierCoefficientBp", "BarrierDurationPolicy", "BarrierAmountPolicy" });
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
            fields.AddRange(new[]
            {
                "NaturalExpiryAttack", "Retaliation", "ElementalModifiers",
                "SurviveLethalCharges", "IgnoreCritResistanceBp", "IgnoreCritDefenseBp", "ImmuneStatusTags"
            });
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

[CustomPropertyDrawer(typeof(TriggeredElementalAttackDefinition))]
public sealed class TriggeredElementalAttackDefinitionPropertyDrawer : PropertyDrawer
{
    private static readonly string[] Fields =
    {
        "Element", "AttackCoefficientBp", "ReceivedDamageCoefficientBp", "Explosion",
        "StatusRecipeId", "StatusDuration", "StatusStackCount", "SelfHealDamageBp"
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, Fields);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, Fields);
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
    private static readonly string[] Fields = { "Timing", "BatchId", "Effect" };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, Fields);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, Fields);
}

[CustomPropertyDrawer(typeof(CardEffectBatchDefinition))]
public sealed class CardEffectBatchDefinitionPropertyDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Height(property, label, "Id", "ApplyChanceBp");
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) =>
        PassiveDrawerFields.Draw(position, property, label, "Id", "ApplyChanceBp");
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
                if (name == "StatusApplyChanceBp" || name == "ProcChanceBp" || name == "ApplyChanceBp")
                {
                    var percent = child.intValue / 100f;
                    EditorGUI.BeginChangeCheck();
                    percent = EditorGUI.Slider(childRect,
                        new GUIContent("Apply Chance (%)", "100 = 100% before Control and Avoidance modifiers."),
                        percent, 0f, 100f);
                    if (EditorGUI.EndChangeCheck())
                        child.intValue = Mathf.RoundToInt(percent * 100f);
                }
                else if (name == "AttributeAdvantageDamageBonusBp")
                {
                    EditorGUI.BeginChangeCheck();
                    var percent = EditorGUI.FloatField(childRect,
                        new GUIContent("Advantage Damage Bonus (%)", "Additional damage dealt by this card when its target has attribute advantage. 30 = +30%."),
                        child.intValue / 100f);
                    if (EditorGUI.EndChangeCheck()) child.intValue = Mathf.RoundToInt(Mathf.Max(0f, percent) * 100f);
                }
                else if (name == "ConsumeTargetRecipeId")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Per-Stack Bonus Status ID", "Status recipe whose stacks are consumed after damage; each stack adds the configured bonus."), true);
                else if (name == "DamagePerConsumedStackBp")
                {
                    EditorGUI.BeginChangeCheck();
                    var percent = EditorGUI.FloatField(childRect,
                        new GUIContent("Bonus Damage per Stack (%)", "Basis points: 2,000 = +20% damage for each consumed stack."),
                        child.intValue / 100f);
                    if (EditorGUI.EndChangeCheck()) child.intValue = Mathf.RoundToInt(Mathf.Max(0f, percent) * 100f);
                }
                else if (name == "ElementalApplication")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Elemental Contact / Status Rider", "Configures the elemental contact and, optionally, which sequence status step it delivers. Requires Critical gates the status rider."), true);
                else if (name == "BarrierCoefficientBp")
                {
                    var sourceStat = PassiveDrawerFields.EnumValue<BarrierSourceStat>(property, "BarrierSourceStat");
                    var sourceStatName = sourceStat switch
                    {
                        BarrierSourceStat.Defense => "Source DEF",
                        BarrierSourceStat.MaxHealth => "Source Max HP",
                        _ => "Source ATK"
                    };
                    EditorGUI.BeginChangeCheck();
                    var percent = EditorGUI.FloatField(childRect,
                        new GUIContent("Barrier (% of " + sourceStatName + ")", "Shield capacity based on the source's current selected stat when applied. 375 = 375%."),
                        child.intValue / 100f);
                    if (EditorGUI.EndChangeCheck()) child.intValue = Mathf.RoundToInt(Mathf.Max(0f, percent) * 100f);
                }
                else if (name == "ScalingStat" && property.type == "PassiveCommandDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Base Stat", "Its unmodified value is added as a damage dealt increase for non-critical hits. For Crit Chance, 3,500 adds +35% damage. Status buffs and debuffs do not change this value."), true);
                else if (name == "ScalingCoefficientBp" && property.type == "PassiveCommandDefinition")
                {
                    EditorGUI.BeginChangeCheck();
                    var percent = EditorGUI.FloatField(childRect,
                        new GUIContent("Stat Modifier (%)", "Multiplier applied to the selected base stat. 100% adds the stat value as a damage dealt increase."),
                        child.intValue / 100f);
                    if (EditorGUI.EndChangeCheck()) child.intValue = Mathf.RoundToInt(Mathf.Max(0f, percent) * 100f);
                }
                else if (name == "BarrierSourceStat")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Barrier Source Stat", "Current effective Attack, Defense, or Max HP is snapshotted when this barrier is applied."), true);
                else if (name == "BarrierDurationPolicy")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Duration on Reapply", "Refresh keeps the longer remaining duration; Replace uses the newly applied duration."), true);
                else if (name == "BarrierAmountPolicy")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Barrier Amount on Reapply", "Replace resets this barrier to the newly calculated amount; Accumulate adds it to the current remaining barrier."), true);
                else if (name == "Stacking" && property.type == "StatusRecipeDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Stacking", "Refresh Stronger replaces a stronger effect, or an equally strong effect with longer remaining duration. Weaker effects cannot replace stronger ones."), true);
                else if (name == "CoefficientBp" && property.type == "StatTransferRecipeDefinition")
                {
                    EditorGUI.BeginChangeCheck();
                    var percent = EditorGUI.Slider(childRect,
                        new GUIContent("Transfer (%)", "Percentage of each selected target stat to snapshot and transfer to the source."),
                        child.intValue / 100f, .01f, 100f);
                    if (EditorGUI.EndChangeCheck()) child.intValue = Mathf.RoundToInt(percent * 100f);
                }
                else if ((name == "SourceStatus" || name == "TargetStatus") && property.type == "StatTransferRecipeDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent(name == "SourceStatus" ? "Source Buff Recipe" : "Target Debuff Recipe",
                            "Standard status recipe metadata. Flat stat modifiers are computed from the transfer snapshot at runtime."), true);
                else if (name == "KeywordFactorBp")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Keyword Factor Bp", "10,000 = normal keyword scaling."), true);
                else if (name == "StatSource" && property.type == "DamageEffectRecipe")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Stat Source", "Whose current effective stat supplies the formula: Effect Owner or resolved Target. Stat buffs/debuffs affect this input. Additional uses only Additional-family damage modifiers."), true);
                else if (name == "DamageTiming")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Damage Timing", "Each Hit reacts to every damage hit. Final Hit reacts once per attacked target on the final hit. On-attack Additional damage always resolves on the final hit."), true);
                else if (name == "BatchId")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Chance Batch ID", "Optional ID from this card's Batches list. All steps in that batch share one chance roll per target; individual status chances are ignored."), true);
                else if (name == "Batches")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Chance Batches", "Shared chance gates for sequence steps. A failed roll skips every effect in that batch, including removal and gauge changes."), true);
                else if (name == "Modifiers")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Modifiers", "Requires Behavior to include Stat. Add a modifier with Target = Stat to change any stat, including Defense."), true);
                else if (name == "Behavior" && property.type == "StatusRecipeDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Behavior", "Enable Stat to expose Modifiers. Enable Barrier to configure its source stat, capacity, and reapplication behavior."), true);
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
                        new GUIContent("Conditions (all required)", "Optional runtime gates. The effect resolves only when every condition passes for its resolved target."), true);
                else if (name == "Timing")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Timing", "Selects when this sequence step runs. This is the single timing authority for sequence effects."), true);
                else if (name == "StatusRecipe")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Status Recipe", "Defines the status being applied. Tags inside the recipe classify that status for conditions and immunity checks."), true);
                else if (name == "Tags" && property.type == "StatusRecipeDefinition")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Status Tags", "Stable status classifications used by conditions, passives, AI, and immunity checks."), true);
                else if (name == "Target")
                    EditorGUI.PropertyField(childRect, child,
                        new GUIContent("Target", "The target for this operation. Sequence steps may target a different fighter or team than the root effect."), true);
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
