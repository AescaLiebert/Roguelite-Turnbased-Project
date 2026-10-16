using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum ElementId { None, Pyro, Hydro, Electro, Cryo, Dendro, Anemo, Geo }
    public enum ElementalReactionId { None, Melt, Vaporize, ReverseMelt, ReverseVaporize, Overload, Swirl, Crystallize, Superconduct, ElectroCharged, Freeze, Burning }
    public enum ElementalDamageOrigin { Unspecified, Card, Reaction, DamageOverTime, Reflection, TriggeredAttack }

    [Serializable]
    public sealed class ElementalModifierDefinition
    {
        public ElementId Element;
        public int DamageDealtBp;
        public int ResistanceBp;
        public ElementalModifierDefinition Clone() => (ElementalModifierDefinition)MemberwiseClone();
    }

    /// <summary>Optional contact delivery; a referenced sequence status is managed at this hit, once per target/action.</summary>
    [Serializable]
    public sealed class ElementalApplicationDefinition
    {
        public ElementId Element;
        public int HitIndex = 1;
        public string StatusEffectId;
        // Conditions only the status delivery; a direct elemental hit remains a trigger when its rider fails.
        public bool RequiresCritical;
        public bool IsConfigured => Element != ElementId.None || !string.IsNullOrWhiteSpace(StatusEffectId) || RequiresCritical;
        public ElementalApplicationDefinition Clone() => (ElementalApplicationDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class ElementalFuelDefinition
    {
        public string RecipeId;
        public ElementId Element;
        public ElementalFuelDefinition Clone() => (ElementalFuelDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class ElementalReactionDefinition
    {
        public ElementalReactionId Id;
        public ElementId ExistingElement;
        public ElementId IncomingElement;
        public int Priority;
        public int AmplificationBp = 10000;
        public int AttackCoefficientBp;
        public ElementId OutputElement;
        public bool PreserveFuel;
        public ElementalReactionDefinition Clone() => (ElementalReactionDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class ElementalRuleSet
    {
        public const string GenshinTrait = "trait.genshin-impact";
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public string ContentVersion = "elemental-ac-v1";
        public bool BridgeDotDealt = true;
        public int ReactionDamageCap = int.MaxValue;
        public List<ElementalFuelDefinition> Fuel = new List<ElementalFuelDefinition>();
        public List<ElementalReactionDefinition> Reactions = new List<ElementalReactionDefinition>();

        public ElementId ElementFor(string recipeId)
        {
            if (Fuel != null) foreach (var mapping in Fuel)
                if (mapping != null && mapping.RecipeId == recipeId) return mapping.Element;
            return ElementId.None;
        }

        public ElementalRuleSet Clone()
        {
            var copy = (ElementalRuleSet)MemberwiseClone();
            copy.Fuel = Fuel?.ConvertAll(f => f?.Clone());
            copy.Reactions = Reactions?.ConvertAll(r => r?.Clone());
            return copy;
        }

        public static ElementalRuleSet CreateSliceAC() => new ElementalRuleSet {
            // Missing aliases/Frost Bite/Wet recipes are not manufactured into production content.
            Fuel = new List<ElementalFuelDefinition> {
                new ElementalFuelDefinition { RecipeId = "status.debuff.ignite", Element = ElementId.Pyro },
                new ElementalFuelDefinition { RecipeId = "status.debuff.shock", Element = ElementId.Electro } },
            Reactions = new List<ElementalReactionDefinition> {
                Amplify(ElementalReactionId.Melt, ElementId.Cryo, ElementId.Pyro, 10, 20000),
                Amplify(ElementalReactionId.Vaporize, ElementId.Pyro, ElementId.Hydro, 20, 20000),
                Amplify(ElementalReactionId.ReverseMelt, ElementId.Pyro, ElementId.Cryo, 30, 15000),
                Amplify(ElementalReactionId.ReverseVaporize, ElementId.Hydro, ElementId.Pyro, 40, 15000),
                new ElementalReactionDefinition { Id = ElementalReactionId.Overload, ExistingElement = ElementId.Pyro,
                    IncomingElement = ElementId.Electro, Priority = 50, AttackCoefficientBp = 15000, OutputElement = ElementId.Pyro },
                new ElementalReactionDefinition { Id = ElementalReactionId.Overload, ExistingElement = ElementId.Electro,
                    IncomingElement = ElementId.Pyro, Priority = 50, AttackCoefficientBp = 15000, OutputElement = ElementId.Pyro } } };

        private static ElementalReactionDefinition Amplify(ElementalReactionId id, ElementId existing,
            ElementId incoming, int priority, int factor) => new ElementalReactionDefinition {
                Id = id, ExistingElement = existing, IncomingElement = incoming,
                Priority = priority, AmplificationBp = factor };

        public static ElementalRuleSet CreateDefault()
        {
            var rules = CreateSliceAC();
            rules.ContentVersion = "elemental-df-v1";
            rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "status.debuff.frost-bite", Element = ElementId.Cryo });
            rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "status.debuff.wet", Element = ElementId.Hydro });
            rules.Fuel.Add(new ElementalFuelDefinition { RecipeId = "status.debuff.nature", Element = ElementId.Dendro });
            foreach (var aura in new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo })
            {
                // Starting spread coefficient; compare single-target and AOE total output in balance fixtures.
                rules.Reactions.Add(new ElementalReactionDefinition { Id = ElementalReactionId.Swirl,
                    ExistingElement = aura, IncomingElement = ElementId.Anemo, Priority = 100,
                    AttackCoefficientBp = 10000, OutputElement = aura, PreserveFuel = true });
                rules.Reactions.Add(new ElementalReactionDefinition { Id = ElementalReactionId.Crystallize,
                    ExistingElement = aura, IncomingElement = ElementId.Geo, Priority = 110, PreserveFuel = true });
            }
            Pair(rules, ElementalReactionId.Superconduct, ElementId.Cryo, ElementId.Electro, 60, ElementId.Cryo, 10000);
            Pair(rules, ElementalReactionId.ElectroCharged, ElementId.Hydro, ElementId.Electro, 70, ElementId.Electro);
            Pair(rules, ElementalReactionId.Freeze, ElementId.Hydro, ElementId.Cryo, 80, ElementId.None);
            Pair(rules, ElementalReactionId.Burning, ElementId.Pyro, ElementId.Dendro, 90, ElementId.Pyro);
            return rules;
        }

        private static void Pair(ElementalRuleSet rules, ElementalReactionId id, ElementId a, ElementId b,
            int priority, ElementId output, int coefficient = 0)
        {
            rules.Reactions.Add(new ElementalReactionDefinition { Id = id, ExistingElement = a, IncomingElement = b,
                Priority = priority, OutputElement = output, AttackCoefficientBp = coefficient });
            rules.Reactions.Add(new ElementalReactionDefinition { Id = id, ExistingElement = b, IncomingElement = a,
                Priority = priority, OutputElement = output, AttackCoefficientBp = coefficient });
        }
    }

    public static class ElementalContentValidator
    {
        public static List<string> Validate(ElementalRuleSet rules, IReadOnlyList<StatusRecipeDefinition> statuses = null)
        {
            var errors = new List<string>();
            if (rules == null) return errors;
            if (rules.SchemaVersion != ElementalRuleSet.CurrentSchemaVersion) errors.Add("Unsupported elemental rules schema.");
            if (string.IsNullOrWhiteSpace(rules.ContentVersion)) errors.Add("Elemental rules require a content version.");
            if (rules.ReactionDamageCap < 0) errors.Add("Elemental damage cap cannot be negative.");
            if (rules.Fuel == null || rules.Reactions == null) { errors.Add("Elemental rules require fuel and reactions."); return errors; }
            var fuelIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fuel in rules.Fuel)
            {
                if (fuel == null || string.IsNullOrWhiteSpace(fuel.RecipeId) || !fuelIds.Add(fuel.RecipeId) ||
                    fuel.Element < ElementId.Pyro || fuel.Element > ElementId.Dendro)
                { errors.Add("Elemental fuel has a missing/duplicate recipe or invalid aura element."); continue; }
                if (statuses == null) continue;
                StatusRecipeDefinition recipe = null;
                foreach (var item in statuses) if (item?.Id == fuel.RecipeId) recipe = item;
                if (recipe == null || recipe.Polarity != StatusPolarity.Debuff || recipe.Color == StatusColor.Grey ||
                    recipe.ParentUnsafeForFuel()) errors.Add("Elemental fuel must reference an ordinary removable debuff: " + fuel.RecipeId);
            }
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var reaction in rules.Reactions)
            {
                if (reaction == null) { errors.Add("Null elemental reaction."); continue; }
                var expected = ElementalRuleSet.CreateDefault().Reactions.Find(r => r.Id == reaction.Id &&
                    r.ExistingElement == reaction.ExistingElement && r.IncomingElement == reaction.IncomingElement);
                if (expected == null || !pairs.Add(reaction.ExistingElement + ":" + reaction.IncomingElement) ||
                    reaction.Priority < 0 || reaction.AmplificationBp < 10000 || reaction.AttackCoefficientBp < 0 ||
                    reaction.OutputElement != expected.OutputElement ||
                    reaction.PreserveFuel != expected.PreserveFuel ||
                    reaction.Id >= ElementalReactionId.Overload && reaction.AmplificationBp != 10000 ||
                    (reaction.Id == ElementalReactionId.Overload || reaction.Id == ElementalReactionId.Swirl || reaction.Id == ElementalReactionId.Superconduct ? reaction.AmplificationBp != 10000 || reaction.AttackCoefficientBp <= 0
                        : reaction.AttackCoefficientBp != 0)) errors.Add("Unsupported, duplicate or malformed elemental reaction: " + reaction.Id);
            }
            return errors;
        }

        // Complex attached state needs a consumption contract before it can become fuel.
        private static bool ParentUnsafeForFuel(this StatusRecipeDefinition recipe) =>
            recipe.StanceChildren?.Count > 0 || (recipe.Behavior & (StatusBehavior.Barrier | StatusBehavior.Stance)) != 0 ||
            recipe.NaturalExpiryAttack?.IsConfigured == true || recipe.Retaliation?.IsConfigured == true ||
            recipe.PeriodicDamage?.Family == DamageFamily.ElementalReaction;

        public static List<string> Validate(CharacterDefinition character, ElementalRuleSet rules)
        {
            var errors = new List<string>();
            if (character == null) return errors;
            ValidateModifiers(character.BaseStats?.ElementalModifiers, errors);
            if (character.Skills != null) foreach (var skill in character.Skills)
                if (skill?.Ranks != null) foreach (var rank in skill.Ranks) Validate(rank?.Effect, rules, errors);
            if (character.UltimateTiers != null) foreach (var tier in character.UltimateTiers) Validate(tier?.Effect, rules, errors);
            return errors;
        }

        public static void Validate(EffectDefinition effect, ElementalRuleSet rules, List<string> errors)
        {
            if (effect == null) return;
            ValidateRecipe(effect.StatusRecipe, errors);
            ValidateModifiers(effect.StatusRecipe?.ElementalModifiers, errors);
            if (!Enum.IsDefined(typeof(ElementId), effect.DamageElement)) errors.Add("Invalid damage element.");
            if (effect.Family == DamageFamily.ElementalReaction) errors.Add("Reaction damage is a generated packet, not an authored card family.");
            if (effect.Sequence != null) foreach (var child in effect.Sequence)
            {
                if (child?.Effect == null) continue;
                ValidateRecipe(child.Effect.StatusRecipe, errors);
                ValidateModifiers(child.Effect.StatusRecipe?.ElementalModifiers, errors);
                if (child.Effect.DamageElement != ElementId.None || child.Effect.ElementalApplication != null)
                    errors.Add("Elemental application belongs on the root contact, not a deferred sequence step.");
            }
            var app = effect.ElementalApplication;
            if ((app != null || effect.DamageElement != ElementId.None) &&
                (rules == null || effect.Kind != EffectKind.Damage || effect.Family != DamageFamily.Normal ||
                    (effect.Target != EffectTargetScope.SelectedEnemy && effect.Target != EffectTargetScope.AllEnemies)))
                errors.Add("Elemental card contacts require enemy-targeted Normal damage and versioned rules.");
            if (app == null)
            {
                if (effect.DamageElement != ElementId.None && effect.Sequence?.Exists(s =>
                    s?.Effect?.Kind == EffectKind.ApplyStatus && rules?.ElementFor(s.Effect.StatusRecipe?.Id) == effect.DamageElement) == true)
                    errors.Add("A matching elemental rider must be linked to its contact to prevent reapplication.");
                return;
            }
            if (app.Element < ElementId.Pyro || app.Element > ElementId.Geo || app.HitIndex < 1 || app.HitIndex > effect.DamageHitCount ||
                effect.DamageElement != ElementId.None && effect.DamageElement != app.Element)
                errors.Add("Unsupported or conflicting elemental application point.");
            if (string.IsNullOrWhiteSpace(app.StatusEffectId))
            {
                if (effect.DamageElement == ElementId.None || app.RequiresCritical)
                    errors.Add("A status-dependent/critical application requires a delivery step.");
                return;
            }
            var matches = effect.Sequence?.FindAll(s => s?.Effect?.Id == app.StatusEffectId);
            var step = matches?.Count == 1 ? matches[0] : null;
            var rider = step?.Effect;
            if (rider == null || rider.Kind != EffectKind.ApplyStatus ||
                rider.StatusRecipe == null || rider.StatusRecipe.Polarity != StatusPolarity.Debuff ||
                rider.StatusRecipe.Color == StatusColor.Grey || rules?.ElementFor(rider.StatusRecipe.Id) != app.Element ||
                step.Timing != CardEffectTiming.AfterDamage || !string.IsNullOrEmpty(step.BatchId) ||
                rider.Conditions?.Count > 0 || (rider.Target != EffectTargetScope.SelectedEnemy && rider.Target != EffectTargetScope.AllEnemies) ||
                rider.Target == EffectTargetScope.AllEnemies && effect.Target != EffectTargetScope.AllEnemies ||
                rider.StatusRecipe.ParentUnsafeForFuel())
                errors.Add("Contact delivery must reference one unbatched AfterDamage enemy fuel-status step; conditions use RequiresCritical.");
            if (effect.Sequence != null) foreach (var item in effect.Sequence)
                if (item?.Effect?.Kind == EffectKind.ApplyStatus && item.Effect.Id != app.StatusEffectId &&
                    rules?.ElementFor(item.Effect.StatusRecipe?.Id) == app.Element)
                    errors.Add("A second same-element rider needs a separately authored future application contract.");
        }

        public static void ValidateModifiers(List<ElementalModifierDefinition> modifiers, List<string> errors)
        {
            if (modifiers == null) return;
            foreach (var modifier in modifiers)
                if (modifier == null || modifier.Element < ElementId.Pyro || modifier.Element > ElementId.Geo)
                    errors.Add("Invalid element-specific modifier.");
        }

        private static void ValidateRecipe(StatusRecipeDefinition recipe, List<string> errors)
        {
            if (recipe == null) return;
            if (recipe.BarrierMaxHealthCapBp < 0 || recipe.EndTurnPowerGaugeLoss < 0 || recipe.PhysicalDamageReceivedBp < 0 ||
                recipe.SourceOnlyDamageReductionBp < 0 || recipe.SourceOnlyDamageReductionBp > 10000)
                errors.Add("Invalid status output budget: " + recipe.Id);
            foreach (var attack in new[] { recipe.NaturalExpiryAttack, recipe.Retaliation })
            {
                if (attack?.IsConfigured != true) continue;
                if (attack.Element < ElementId.Pyro || attack.Element > ElementId.Geo || attack.AttackCoefficientBp < 0 ||
                    attack.ReceivedDamageCoefficientBp < 0 || (attack.AttackCoefficientBp > 0) == (attack.ReceivedDamageCoefficientBp > 0) ||
                    attack.SelfHealDamageBp < 0 || attack.SelfHealDamageBp > 10000 || attack.StatusDuration < 0 ||
                    !string.IsNullOrEmpty(attack.StatusRecipeId) && !StandardEffectDatabase.CreateStatusRecipes().Exists(r => r.Id == attack.StatusRecipeId))
                    errors.Add("Invalid triggered elemental attack: " + recipe.Id);
            }
        }
    }
}
