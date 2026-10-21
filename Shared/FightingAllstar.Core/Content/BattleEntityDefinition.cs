using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum BattleEntityCategory { Summon, GeoConstruct }
    public enum BattleEntityRemovalCause { Expired, CapacityDepleted, OwnerDefeated, Replaced, BattleEnded, ScriptedLifecycle }

    /// <summary>Authoritative battle-local object content. Presentation never supplies combat behavior.</summary>
    [Serializable]
    public sealed class BattleEntityDefinition
    {
        public int SchemaVersion = 1;
        public string Id;
        public string NameKey;
        public string PresentationKey;
        public BattleEntityCategory Category;
        public int Duration = 2;
        public StatusRecipeDefinition MarkerRecipe;
        public TriggeredElementalAttackDefinition TurnEndAttack;
        public EffectTargetScope ActionTarget = EffectTargetScope.AllEnemies;
        public bool ActOnBothSidesTurnEnd;
        public int AttackCoefficientPerDotDealtBp;
        public List<StatusRecipeDefinition> OnHitStatuses = new List<StatusRecipeDefinition>();
        public List<PassiveAuraDefinition> Auras = new List<PassiveAuraDefinition>();
        // Zero capacity means this entity does not intercept, regardless of category.
        public int CapacityCoefficientBp;
        public StatId CapacitySourceStat = StatId.MaxHealth;
        public int ProtectionPriority;
        public StatusRecipeDefinition DepletedStatus;
        public PassiveTargetFilter DepletedTargets = new PassiveTargetFilter { IncludeReserve = false };
        public TriggeredElementalAttackDefinition ReactionFollowUp;

        public BattleEntityDefinition Clone()
        {
            var copy = (BattleEntityDefinition)MemberwiseClone();
            copy.MarkerRecipe = MarkerRecipe?.Clone();
            copy.TurnEndAttack = TurnEndAttack?.Clone();
            copy.ReactionFollowUp = ReactionFollowUp?.Clone();
            copy.OnHitStatuses = OnHitStatuses?.ConvertAll(s => s?.Clone()) ?? new List<StatusRecipeDefinition>();
            copy.Auras = Auras?.ConvertAll(a => a?.Clone()) ?? new List<PassiveAuraDefinition>();
            copy.DepletedStatus = DepletedStatus?.Clone();
            copy.DepletedTargets = DepletedTargets?.Clone();
            return copy;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (SchemaVersion != 1) errors.Add("Unsupported summon definition schema version.");
            if (string.IsNullOrWhiteSpace(Id)) errors.Add("A summon requires a stable definition id.");
            if (!Enum.IsDefined(typeof(BattleEntityCategory), Category)) errors.Add("Invalid entity category.");
            if (Duration <= 0) errors.Add("A summon requires a positive owner-turn lifetime.");
            if (CapacityCoefficientBp < 0 || !Enum.IsDefined(typeof(StatId), CapacitySourceStat)) errors.Add("Invalid construct capacity formula.");
            if (MarkerRecipe != null && string.IsNullOrWhiteSpace(MarkerRecipe.Id)) errors.Add("A marker requires a stable recipe id.");
            if (TurnEndAttack != null && ActionTarget != EffectTargetScope.AllEnemies && ActionTarget != EffectTargetScope.SelectedEnemy)
                errors.Add("Entity attacks support enemy targets only.");
            if (AttackCoefficientPerDotDealtBp < 0) errors.Add("Entity DOT coefficient scaling cannot be negative.");
            ValidateAttack(TurnEndAttack, "turn-end", errors);
            ValidateAttack(ReactionFollowUp, "reaction follow-up", errors);
            if ((OnHitStatuses?.Count ?? 0) > 32 || (Auras?.Count ?? 0) > 32) errors.Add("Entity payload exceeds its bounded status/aura budget.");
            if (OnHitStatuses != null) foreach (var status in OnHitStatuses)
            {
                if (status == null || status.StructuralEntityMarker) errors.Add("Entity hit statuses must be ordinary authored statuses.");
                else errors.AddRange(EffectRecipeValidator.Validate(new[] { status }, null));
            }
            if (DepletedStatus != null)
            {
                if (DepletedStatus.StructuralEntityMarker) errors.Add("A depletion reward cannot create an entity marker.");
                errors.AddRange(EffectRecipeValidator.Validate(new[] { DepletedStatus }, null));
                if (DepletedTargets == null || !Enum.IsDefined(typeof(PassiveRelation), DepletedTargets.Relation)) errors.Add("A depletion reward requires a valid target filter.");
            }
            errors.AddRange(PassiveRuleValidator.Validate(new PassiveDefinition { Id = Id + ".entity-auras", Auras = Auras }));
            return errors;
        }

        private static void ValidateAttack(TriggeredElementalAttackDefinition attack, string purpose, List<string> errors)
        {
            if (attack == null) return;
            if (!Enum.IsDefined(typeof(ElementId), attack.Element) || attack.AttackCoefficientBp <= 0 ||
                attack.ReceivedDamageCoefficientBp != 0 || attack.DamageBonusBp < 0 || attack.SelfHealDamageBp < 0 ||
                attack.SelfHealDamageBp > 10000 || attack.StatusDuration < 0 || attack.StatusStackCount <= 0)
                errors.Add("Invalid entity " + purpose + " attack.");
            if (!string.IsNullOrEmpty(attack.StatusRecipeId) && !StandardEffectDatabase.CreateStatusRecipes().Exists(r => r.Id == attack.StatusRecipeId))
                errors.Add("Unknown entity attack status recipe: " + attack.StatusRecipeId);
        }
    }
}
