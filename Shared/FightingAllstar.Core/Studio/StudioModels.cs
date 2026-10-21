using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    public enum StudioParameterUnit { Integer, BasisPoints }
    public enum StudioDiagnosticSeverity { Info, Warning, Error }
    public enum StudioCapabilityStatus { Enabled, RuntimeOnly, Proposed, Unsupported }
    public enum StudioActionKind { Sequence, Branch, ForEachTarget, Command, SpendResource, TransferResource, ConsumeStatusLayers, Module }

    [Serializable]
    public sealed class StudioParameter
    {
        public string Id;
        public string Name;
        public StudioParameterUnit Unit;
        public int Value;
    }

    // Flat references are deliberate: no recursively serialized authoring objects.
    [Serializable]
    public sealed class StudioConditionNode
    {
        public string Id;
        public ConditionLogic Logic;
        public EffectConditionKind Kind;
        public ConditionSubject Subject;
        public int Threshold;
        public string ThresholdParameterId;
        public string StringValue;
        public string RecipeId;
        public string Tag;
        public PassiveTargetFilter RosterFilter = new PassiveTargetFilter();
        public bool InitialRoster;
        public bool Negate;
        public List<string> Children = new List<string>();
    }

    [Serializable]
    public sealed class StudioRule
    {
        public string Id;
        public string Name;
        public bool Enabled = true;
        public PassiveEventKind Trigger = PassiveEventKind.BattleStarted;
        public PassiveGate Gate = new PassiveGate();
        public string ConditionRootId;
        public EffectTargetScope Target = EffectTargetScope.AllAllies;
        public string AmountParameterId;
        // An empty root retains the original, version-zero opening-gauge slice.
        public string ActionRootId;
        public string ElseActionRootId;
        public int RuntimeVersion = 1;
        public bool Imported;
        // Empty means a character rule; otherwise owned by the referenced status instance.
        public string StatusId;
        public PassiveReactionDefinition Settings = new PassiveReactionDefinition();
    }

    /// <summary>Flat action storage. Modules are expanded only after references and budgets pass validation.</summary>
    [Serializable]
    public sealed class StudioActionNode
    {
        public string Id;
        public string Name;
        public StudioActionKind Kind;
        public string ConditionRootId;
        public List<string> Children = new List<string>();
        public List<string> ElseChildren = new List<string>();
        public string ModuleId;
        public PassiveCommandDefinition Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
            Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.ChangePowerGauge, Target = EffectTargetScope.Self, Magnitude = 1 } };
        public string AmountParameterId;
        public string CoefficientParameterId;
        public string MultiplierParameterId;
        public EffectTargetScope Target = EffectTargetScope.Self;
        public int MaximumTargets = 16;
        public TargetQueryDefinition Query;
        public string ResourceKey;
        public int Amount = 1;
        public int ResourceCap = int.MaxValue;
        public string StatusRecipeId;
        public bool StatusOwnerOnly = true;
        public bool RequireFullAmount = true;
        public StatusRemovalReason RemovalReason = StatusRemovalReason.Consumed;
        public string ResultBinding;
    }

    [Serializable]
    public sealed class StudioModule
    {
        public string Id;
        public string Name;
        public string RootActionId;
    }

    [Serializable]
    public sealed class StudioResource
    {
        public string Id;
        public string Name;
        public int Cap = 6;
    }

    [Serializable]
    public sealed class StudioStatus
    {
        public string Id;
        public string Name;
        public StatusRecipeDefinition Definition = new StatusRecipeDefinition();
    }

    [Serializable]
    public sealed class StudioPolicy
    {
        public string Id;
        public string Name;
        public string ConditionRootId;
        public CombatPolicyDefinition Definition = new CombatPolicyDefinition();
        public string CapParameterId;
        public string CostParameterId;
        public string HealParameterId;
    }

    [Serializable]
    public sealed class StudioAbilityVariant
    {
        public string SkillId;
        public AbilityVariantDefinition Definition = new AbilityVariantDefinition();
    }

    [Serializable]
    public sealed class StudioAbilityOverride
    {
        public string SkillId;
        // Empty means the base skill; otherwise references a Studio-authored variant.
        public string VariantId;
        public AbilityValueOverride Definition = new AbilityValueOverride();
        public string ValueParameterId;
    }

    [Serializable]
    public sealed class StudioPresentationProfile
    {
        public string Id;
        public string ActorAssetGuid;
        public string ActionClipGuid;
        public string ReactionClipGuid;
        public int DurationMilliseconds = 1000;
    }

    [Serializable]
    public sealed class StudioDocument
    {
        public const int CurrentSchemaVersion = 5;
        public int SchemaVersion = CurrentSchemaVersion;
        public string Id;
        public string Name;
        public string SourceCharacterId;
        // Frozen import source; compilation always clones and never changes this snapshot.
        public CharacterDefinition LegacyCharacter;
        // Copy-on-write authoring payload. The import remains immutable for comparison/conflict checks.
        public CharacterDefinition AuthoredCharacter;
        [Newtonsoft.Json.JsonIgnore] public CharacterDefinition EditingCharacter => AuthoredCharacter ?? LegacyCharacter;
        public bool OwnsOpeningGauge;
        public List<string> ReplacedNativeReactionIds = new List<string>();
        public List<string> NativeReactionOrder = new List<string>();
        public Dictionary<string, string> CompatibilityReasons = new Dictionary<string, string>();
        public List<StudioParameter> Parameters = new List<StudioParameter>();
        public List<StudioRule> Rules = new List<StudioRule>();
        public List<StudioConditionNode> Conditions = new List<StudioConditionNode>();
        public List<StudioActionNode> Actions = new List<StudioActionNode>();
        public List<StudioModule> Modules = new List<StudioModule>();
        public List<StudioResource> Resources = new List<StudioResource>();
        public List<StudioStatus> Statuses = new List<StudioStatus>();
        public List<StudioPolicy> Policies = new List<StudioPolicy>();
        public List<StudioAbilityVariant> AbilityVariants = new List<StudioAbilityVariant>();
        public List<StudioAbilityOverride> AbilityOverrides = new List<StudioAbilityOverride>();
        public DescriptionDocument Description = new DescriptionDocument();
        public StudioPresentationProfile Presentation = new StudioPresentationProfile();
    }

    [Serializable]
    public sealed class StudioDiagnostic
    {
        public StudioDiagnosticSeverity Severity;
        public string Code;
        public string SourceId;
        public string Message;
        public override string ToString() => Severity + " " + Code + " [" + SourceId + "] " + Message;
    }

    [Serializable]
    public sealed class StudioHashes
    {
        public int SchemaVersion = 1;
        public string Gameplay;
        public string Text;
        public string Presentation;
        public string Bundle;
    }

    public sealed class StudioCompileResult
    {
        public bool Success;
        public CharacterDefinition Character;
        public StudioHashes Hashes;
        public DescriptionCompileResult Description;
        public List<StudioDiagnostic> Diagnostics = new List<StudioDiagnostic>();
    }

    [Serializable]
    public sealed class StudioCapability
    {
        public string Id;
        public string Label;
        public StudioCapabilityStatus Status;
        public string Evidence;
    }

    /// <summary>Authoring capabilities share the compiler's allow-list; imported legacy behavior remains frozen.</summary>
    public static class StudioCapabilities
    {
        public static IReadOnlyList<StudioCapability> All => new List<StudioCapability> {
            Entry("trigger.battle-started", "Battle started", StudioCapabilityStatus.Enabled, "BattleEngine.Create → CharacterPassiveRuntime.Notify"),
            Entry("condition.all", "All conditions", StudioCapabilityStatus.Enabled, "CardEffectSystem.ConditionsPass"),
            Entry("condition.any", "Any condition", StudioCapabilityStatus.Enabled, "CardEffectSystem.ConditionsPass"),
            Entry("condition.not", "Not condition", StudioCapabilityStatus.Enabled, "CardEffectSystem.ConditionsPass"),
            Entry("condition.always", "Always", StudioCapabilityStatus.Enabled, "EffectConditionKind.Always"),
            Entry("condition.source-gauge", "Owner gauge at least", StudioCapabilityStatus.Enabled, "EffectConditionKind.SourceGaugeAtLeast"),
            Entry("condition.source-health", "Owner health at most", StudioCapabilityStatus.Enabled, "EffectConditionKind.SourceHealthAtMost; basis points"),
            Entry("condition.roster-count", "Matching roster count", StudioCapabilityStatus.Enabled, "EffectConditionKind.RosterCountAtLeast"),
            Entry("action.team-gauge", "Change active ally gauge", StudioCapabilityStatus.Enabled, "ExecuteEffect → ChangePowerGauge; AllAllies"),
            Entry("action.composition", "Sequence, branch and reusable modules", StudioCapabilityStatus.Enabled, "M3 flat action compiler → bounded PassiveExecution trees; cycle and expansion validation"),
            Entry("action.target-loop", "Bounded target loops and remembered selections", StudioCapabilityStatus.Enabled, "TargetQuerySystem; formation, health/layer ordering, sampling, exclusion and bindings"),
            Entry("limits.cooldown", "Owner-turn cooldown", StudioCapabilityStatus.Enabled, "ExecutionVersion 1 activation receipts; owner-turn eligibility"),
            Entry("limits.maximum-activations", "Activation scope and limits", StudioCapabilityStatus.Enabled, "Battle, owner turn, root, root/target and event receipts"),
            Entry("origin.allow-reactions", "Reaction-origin policy", StudioCapabilityStatus.Enabled, "ExecutionVersion 1 checks reaction origin before dispatch; root fact/step budgets"),
            Entry("execution.order", "Stable phase and priority", StudioCapabilityStatus.Enabled, "Before/Normal/After → descending priority → side/formation/source/rule"),
            Entry("trigger.before-defeat", "Before defeat interception", StudioCapabilityStatus.Enabled, "Declarative BeforeDefeat policy; atomic cost and heal before defeat is committed"),
            Entry("action.atomic-spend", "Atomic resource spend and transfer", StudioCapabilityStatus.Enabled, "TrySpendResource/TryTransferResource; success/failure action branches"),
            Entry("action.consume-layers", "Owned partial-layer consumption", StudioCapabilityStatus.Enabled, "Frozen removal snapshot, exact/partial amount and explicit reason"),
            Entry("trigger.lifecycle", "Action, turn, healing, status and resource events", StudioCapabilityStatus.Enabled, "StudioAuthoringCatalog.Triggers → production fact dispatcher"),
            Entry("policy.damage-cap", "Incoming cap and gauge-loss guard", StudioCapabilityStatus.Enabled, "CombatPolicySystem; priority, conditions, presence and explicit bypass precedence"),
            Entry("damage.true", "Explicit true damage and bypass profile", StudioCapabilityStatus.Enabled, "Authored profile with explicit bonus; legacy null profile retained"),
            Entry("ability.variants", "Skill variants and independent upgrade axes", StudioCapabilityStatus.Enabled, "AbilityVariantSystem; stable card identity, readiness, unlock/relock, level/rank/tier overrides"),
            Entry("authoring.shared-definitions", "Shared resource and status libraries", StudioCapabilityStatus.Enabled, "Schema3 canonical caps, status lifetime/source break policies and status-owned action rules"),
            Entry("authoring.scoped-bindings", "Scoped value and target references", StudioCapabilityStatus.Enabled, "StudioBindingAnalysis definite assignment across branches, loops and module calls; explicit runtime rejection"),
            Entry("authoring.variant-payloads", "Complete variant sequence editing and validation", StudioCapabilityStatus.Enabled, "Add/remove/reorder effects, status/heal payload controls and rank payload validation"),
            Entry("trace.observed-context", "Observed branch, targets and resource payment", StudioCapabilityStatus.Enabled, "Read-only context snapshots with nested condition observations; integrated evidence dock"),
            Entry("trigger.before-damage", "Current-hit calculation modifiers", StudioCapabilityStatus.Enabled, "Fixed local Attack, Crit Chance, damage and Crit Defense modifiers; state-changing programs and action-start snapshots remain unsupported"),
            Entry("legacy.support", "Other imported support fields", StudioCapabilityStatus.RuntimeOnly, "Preserved in import snapshot; only opening gauge is migrated.")
        }.AsReadOnly();

        private static StudioCapability Entry(string id, string label, StudioCapabilityStatus status, string evidence) =>
            new StudioCapability { Id = id, Label = label, Status = status, Evidence = evidence };
    }
}
