using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    public static class StudioDocumentService
    {
        public static CharacterDefinition BeginKitEditing(StudioDocument document)
        {
            if (document?.LegacyCharacter == null) throw new InvalidOperationException("Import a character before editing its kit.");
            if (document.AuthoredCharacter == null)
            {
                document.AuthoredCharacter = document.LegacyCharacter.Clone();
            }
            // Passive editing may create AuthoredCharacter before the kit editor opens.
            // Root card settings must be initialized in either entry path so editing one
            // root field cannot silently reset another field to its default.
            InitializeAbilityRootSettings(document.AuthoredCharacter);
            return document.AuthoredCharacter;
        }

        private static void InitializeAbilityRootSettings(CharacterDefinition character)
        {
            foreach (var skill in character?.Skills ?? new List<SkillDefinition>())
            {
                if (skill == null || skill.DamageAnimation != null) continue;
                foreach (var rank in skill.Ranks ?? new List<SkillRankDefinition>())
                {
                    var attack = FirstDamageAnimation(rank?.Effect);
                    if (attack == null) continue;
                    skill.DamageAnimation = attack;
                    break;
                }
            }
            if (character == null || character.HasUltimateCardSettings) return;
            var tier = character.UltimateTiers?.Find(item => item?.Tier == 0) ?? character.UltimateTiers?.FirstOrDefault(item => item != null);
            if (tier == null) return;
            character.HasUltimateCardSettings = true;
            character.UltimateCategory = tier.Category;
            character.UltimateTargetScope = tier.Effect?.Target ?? EffectTargetScope.SelectedEnemy;
            character.UltimateDamageAnimation = FirstDamageAnimation(tier.Effect);
        }

        private static DamageAttackDefinition FirstDamageAnimation(EffectDefinition root)
        {
            if (root?.Kind == EffectKind.Damage && root.Attack != null) return root.Attack.Clone();
            if (root?.Sequence != null)
                foreach (var step in root.Sequence)
                    if (step?.Effect?.Kind == EffectKind.Damage && step.Effect.Attack != null)
                        return step.Effect.Attack.Clone();
            return null;
        }
        public static StudioDocument Clone(StudioDocument source) => source == null ? null :
            SnapshotJson.Deserialize<StudioDocument>(SnapshotJson.Serialize(source));

        public static StudioDocument ImportCharacter(CharacterDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Passive?.Support?.OpeningTeamGauge > 0) return ImportOpeningGauge(source);
            var document = new StudioDocument { Id = "studio." + Guid.NewGuid().ToString("N"), Name = source.DisplayName,
                SourceCharacterId = source.Id, LegacyCharacter = source.Clone(),
                Description = DescriptionDocument.FromSource(source.PassiveSource?.Description ?? ""),
                Presentation = new StudioPresentationProfile { Id = "presentation.default" } };
            document.Description.OwnerId = document.Id; return document;
        }

        public static StudioDocument ImportOpeningGauge(CharacterDefinition source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Passive?.Support == null || source.Passive.Support.OpeningTeamGauge <= 0)
                throw new ArgumentException("The source has no executable opening team gauge capability.", nameof(source));
            var document = new StudioDocument { Id = "studio." + Guid.NewGuid().ToString("N"),
                Name = source.DisplayName + " · Opening gauge", SourceCharacterId = source.Id,
                LegacyCharacter = source.Clone(), OwnsOpeningGauge = true,
                Description = DescriptionDocument.FromSource(DescriptionLibrary.OpeningGaugeSource),
                Presentation = new StudioPresentationProfile { Id = "presentation.default" } };
            document.Description.OwnerId = document.Id;
            document.Description.Id = "description.opening-gauge";
            document.Parameters.Add(new StudioParameter { Id = "param.opening-gauge", Name = "Opening team gauge",
                Unit = StudioParameterUnit.Integer, Value = source.Passive.Support.OpeningTeamGauge });
            document.Parameters.Add(new StudioParameter { Id = "param.minimum-gauge", Name = "Minimum owner gauge",
                Unit = StudioParameterUnit.Integer, Value = 0 });
            // A genuinely nested, default-true eligibility expression, editable without changing the runtime source.
            document.Conditions.Add(new StudioConditionNode { Id = "condition.eligible", Logic = ConditionLogic.All,
                Children = new List<string> { "condition.gauge", "condition.alternative" } });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.gauge", Kind = EffectConditionKind.SourceGaugeAtLeast,
                ThresholdParameterId = "param.minimum-gauge" });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.alternative", Logic = ConditionLogic.Any,
                Children = new List<string> { "condition.always", "condition.not" } });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.always", Kind = EffectConditionKind.Always });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.not", Logic = ConditionLogic.Not,
                Children = new List<string> { "condition.never" } });
            document.Conditions.Add(new StudioConditionNode { Id = "condition.never", Kind = EffectConditionKind.SourceGaugeAtLeast, Threshold = 5 });
            document.Rules.Add(new StudioRule { Id = "studio.opening-team-gauge", Name = "Opening team gauge",
                ConditionRootId = "condition.eligible", AmountParameterId = "param.opening-gauge",
                Gate = new PassiveGate { Presence = source.Passive.Support.Presence }, Target = EffectTargetScope.AllAllies });
            document.Description.Locales[0].Blocks[0].MechanicIds.Add(document.Rules[0].Id);
            return document;
        }

        /// <summary>Copies local identities while retaining external keyword, snippet, style, and imported source IDs.</summary>
        public static StudioDocument Duplicate(StudioDocument source)
            => Duplicate(source, out _);

        public static StudioDocument Duplicate(StudioDocument source, out Dictionary<string, string> remappedIds)
        {
            var copy = Clone(source) ?? throw new ArgumentNullException(nameof(source));
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            remappedIds = ids;
            void Add(string id) { if (!string.IsNullOrEmpty(id) && !ids.ContainsKey(id)) ids.Add(id, id + "." + Guid.NewGuid().ToString("N")); }
            Add(copy.Id);
            foreach (var binding in copy.Description.Bindings) Add(binding.Id);
            foreach (var p in copy.Parameters ?? new List<StudioParameter>()) Add(p?.Id);
            foreach (var n in copy.Conditions ?? new List<StudioConditionNode>()) Add(n?.Id);
            foreach (var r in copy.Rules ?? new List<StudioRule>()) Add(r?.Id);
            foreach (var a in copy.Actions ?? new List<StudioActionNode>()) Add(a?.Id);
            foreach (var m in copy.Modules ?? new List<StudioModule>()) Add(m?.Id);
            foreach (var r in copy.Resources ?? new List<StudioResource>()) Add(r?.Id);
            foreach (var s in copy.Statuses ?? new List<StudioStatus>()) Add(s?.Id);
            foreach (var p in copy.Policies ?? new List<StudioPolicy>()) Add(p?.Id);
            foreach (var v in copy.AbilityVariants ?? new List<StudioAbilityVariant>())
            { Add(v?.Definition?.Id); foreach (var item in v?.Definition?.Overrides ?? new List<AbilityValueOverride>()) Add(item?.Id); }
            foreach (var v in copy.AbilityOverrides ?? new List<StudioAbilityOverride>()) Add(v?.Definition?.Id);
            string Map(string id) => id != null && ids.TryGetValue(id, out var mapped) ? mapped : id;
            copy.NativeReactionOrder = copy.NativeReactionOrder.Select(Map).ToList();
            foreach (var resource in copy.Resources ?? new List<StudioResource>()) resource.Id = Map(resource.Id);
            foreach (var status in copy.Statuses ?? new List<StudioStatus>()) { status.Id = Map(status.Id); status.Definition.Id = status.Id; }
            foreach (var rule in copy.Rules)
            {
                rule.StatusId = Map(rule.StatusId);
                rule.Settings.EventResourceKey = Map(rule.Settings.EventResourceKey);
                rule.Settings.EventStatusRecipeId = Map(rule.Settings.EventStatusRecipeId);
            }
            foreach (var condition in copy.Conditions) { condition.StringValue = Map(condition.StringValue); condition.RecipeId = Map(condition.RecipeId); }
            foreach (var action in copy.Actions)
            {
                action.ResourceKey = Map(action.ResourceKey); action.StatusRecipeId = Map(action.StatusRecipeId);
                if (action.Query != null) action.Query.RecipeId = Map(action.Query.RecipeId);
                if (action.Command == null) continue;
                action.Command.CounterKey = Map(action.Command.CounterKey);
                var op = action.Command.Effect;
                if (op == null) continue;
                op.RemoveRecipeId = Map(op.RemoveRecipeId);
                if (op.TargetQuery != null) op.TargetQuery.RecipeId = Map(op.TargetQuery.RecipeId);
                if (op.Status != null) { op.Status.StatusRecipeId = Map(op.Status.StatusRecipeId); if (op.Status.InlineRecipe != null) op.Status.InlineRecipe.Id = Map(op.Status.InlineRecipe.Id); }
            }
            foreach (var policy in copy.Policies) policy.Definition.ResourceKey = Map(policy.Definition.ResourceKey);
            foreach (var variant in copy.AbilityVariants)
            {
                variant.Definition.RequiredResourceKey = Map(variant.Definition.RequiredResourceKey);
                foreach (var rank in variant.Definition.Ranks)
                {
                    if (rank.Effect.StatusRecipe != null) rank.Effect.StatusRecipe.Id = Map(rank.Effect.StatusRecipe.Id);
                    foreach (var step in rank.Effect.Sequence) if (step.Effect?.StatusRecipe != null) step.Effect.StatusRecipe.Id = Map(step.Effect.StatusRecipe.Id);
                }
            }
            copy.Id = Map(copy.Id); copy.Name = (copy.Name ?? "Draft") + " (copy)";
            foreach (var p in copy.Parameters ?? new List<StudioParameter>()) if (p != null) p.Id = Map(p.Id);
            foreach (var n in copy.Conditions ?? new List<StudioConditionNode>()) if (n != null)
            { n.Id = Map(n.Id); n.ThresholdParameterId = Map(n.ThresholdParameterId); n.Children = n.Children?.Select(Map).ToList(); }
            foreach (var r in copy.Rules ?? new List<StudioRule>()) if (r != null)
            { r.Id = Map(r.Id); r.ConditionRootId = Map(r.ConditionRootId); r.AmountParameterId = Map(r.AmountParameterId); r.ActionRootId = Map(r.ActionRootId); r.ElseActionRootId = Map(r.ElseActionRootId); }
            foreach (var a in copy.Actions ?? new List<StudioActionNode>()) if (a != null)
            {
                a.Id = Map(a.Id); a.ConditionRootId = Map(a.ConditionRootId); a.ModuleId = Map(a.ModuleId);
                a.AmountParameterId = Map(a.AmountParameterId); a.CoefficientParameterId = Map(a.CoefficientParameterId);
                a.MultiplierParameterId = Map(a.MultiplierParameterId);
                a.Children = a.Children?.Select(Map).ToList(); a.ElseChildren = a.ElseChildren?.Select(Map).ToList();
            }
            foreach (var m in copy.Modules ?? new List<StudioModule>()) if (m != null)
            { m.Id = Map(m.Id); m.RootActionId = Map(m.RootActionId); }
            foreach (var p in copy.Policies ?? new List<StudioPolicy>()) if (p != null)
            { p.Id = Map(p.Id); p.ConditionRootId = Map(p.ConditionRootId); p.CapParameterId = Map(p.CapParameterId); p.CostParameterId = Map(p.CostParameterId); p.HealParameterId = Map(p.HealParameterId); }
            foreach (var v in copy.AbilityVariants ?? new List<StudioAbilityVariant>()) if (v?.Definition != null)
            { v.Definition.Id = Map(v.Definition.Id); foreach (var item in v.Definition.Overrides ?? new List<AbilityValueOverride>()) if (item != null) item.Id = Map(item.Id); }
            foreach (var v in copy.AbilityOverrides ?? new List<StudioAbilityOverride>()) if (v?.Definition != null)
            { v.Definition.Id = Map(v.Definition.Id); v.VariantId = Map(v.VariantId); v.ValueParameterId = Map(v.ValueParameterId); }
            foreach (var a in copy.Actions ?? new List<StudioActionNode>()) if (a?.Command != null && a.Command.Kind == PassiveCommandKind.SetAbilityVariant)
                a.Command.VariantId = Map(a.Command.VariantId);
            copy.Description = DescriptionCompiler.RemapLocalReferences(copy.Description, ids);
            return copy;
        }

        /// <summary>Version one upgrades add empty M3 tables while retaining its exact version-zero gameplay.</summary>
        public static bool TryMigrate(StudioDocument source, out StudioDocument migrated, out List<StudioDiagnostic> diagnostics)
        {
            migrated = null; diagnostics = new List<StudioDiagnostic>();
            if (source == null || source.SchemaVersion < 1 || source.SchemaVersion > StudioDocument.CurrentSchemaVersion)
            {
                diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Error, Code = "migration.unsupported",
                    SourceId = source?.Id, Message = "No migration exists for this schema. Original source and last compiled bundle must be retained." });
                return false;
            }
            migrated = Clone(source);
            if (migrated.SchemaVersion == 1)
            {
                migrated.Actions = migrated.Actions ?? new List<StudioActionNode>();
                migrated.Modules = migrated.Modules ?? new List<StudioModule>();
                migrated.Policies = migrated.Policies ?? new List<StudioPolicy>();
                migrated.AbilityVariants = migrated.AbilityVariants ?? new List<StudioAbilityVariant>();
                migrated.AbilityOverrides = migrated.AbilityOverrides ?? new List<StudioAbilityOverride>();
                migrated.SchemaVersion = 2;
                diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Info, Code = "migration.v1-v2",
                    SourceId = migrated.Id, Message = "Added M3 authoring tables. Existing opening-gauge rules retain their original execution semantics; the saved source changes only on an explicit edit/save." });
            }
            if (migrated.SchemaVersion == 2)
            {
                migrated.Resources = migrated.Resources ?? new List<StudioResource>();
                migrated.Statuses = migrated.Statuses ?? new List<StudioStatus>();
                migrated.SchemaVersion = 3;
                diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Info, Code = "migration.v2-v3",
                    SourceId = migrated.Id, Message = "Added shared resource/status libraries and status-owned rules. Existing rule behavior is retained." });
            }
            if (migrated.SchemaVersion == 3)
            {
                migrated.SchemaVersion = 4;
                diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Info, Code = "migration.v3-v4",
                    SourceId = migrated.Id, Message = "Added separate editable base kit ownership. The frozen import and existing behavior are preserved; the authoring copy is created on first kit edit." });
            }
            if (migrated.SchemaVersion == 4)
            {
                migrated.SchemaVersion = 5;
                diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Info, Code = "migration.v4-v5", SourceId = migrated.Id,
                    Message = "Added typed passive ownership and unified event-rule import. Native data remains unchanged until Save." });
            }
            return true;
        }
    }
}
