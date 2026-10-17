using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum ElementId { None, Pyro, Hydro, Electro, Cryo, Dendro, Anemo, Geo }
    public enum ElementalReactionId { None, Melt, Vaporize, ReverseMelt, ReverseVaporize, Overload, Swirl, Crystallize, Superconduct, ElectroCharged, Freeze, Burning, Shatter }
    public enum ElementalDamageOrigin { Unspecified, Card, Reaction, DamageOverTime, Reflection, TriggeredAttack }

    [Serializable]
    public sealed class ElementalModifierDefinition
    {
        public ElementId Element;
        public int DamageDealtBp;
        public int ResistanceBp;
        public ElementalModifierDefinition Clone() => (ElementalModifierDefinition)MemberwiseClone();
    }

    /// <summary>Legacy serialized contact metadata. Only Element is used as a fallback; new content uses DamageElement.</summary>
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
        public static void Upgrade(EffectDefinition effect)
        {
            var legacy = effect?.ElementalApplication;
            if (legacy == null) return;
            if (effect.DamageElement == ElementId.None) effect.DamageElement = legacy.Element;
            var rider = effect.Sequence?.Find(step => step?.Effect?.Id == legacy.StatusEffectId)?.Effect;
            if (legacy.RequiresCritical && rider != null && !rider.Conditions.Exists(c => c?.Kind == EffectConditionKind.WasCritical))
                rider.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.WasCritical });
            effect.ElementalApplication = null;
        }
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
        public const int ShatterCoefficientBp = 30000;
        public const int CurrentSchemaVersion = 2;
        public int SchemaVersion = CurrentSchemaVersion;
        public string ContentVersion = "elemental-auras-v6";
        // Reaction base damage is character level * 500 before reaction multipliers.
        public int ReactionDamagePerLevel = 500;
        public int ReactionDamageCap = int.MaxValue;
        public List<ElementalReactionDefinition> Reactions = new List<ElementalReactionDefinition>();

        public ElementalRuleSet Clone()
        {
            var copy = (ElementalRuleSet)MemberwiseClone();
            copy.Reactions = Reactions?.ConvertAll(r => r?.Clone());
            return copy;
        }

        public bool NeedsDefaultUpgrade() => ContentVersion == "elemental-auras-v5" ||
            ContentVersion == "elemental-auras-v4" ||
            ContentVersion == "elemental-auras-v3" ||
            ContentVersion == "elemental-auras-v2";

        // Migrate shipped reaction balances, preserving the authored reaction damage cap.
        public void UpgradeLegacyDefaults()
        {
            if (!NeedsDefaultUpgrade()) return;
            ReactionDamagePerLevel = 500;
            if (Reactions != null)
            {
                foreach (var reaction in Reactions)
                {
                    if (reaction == null) continue;
                    if (reaction.Id == ElementalReactionId.Overload && reaction.AttackCoefficientBp == 15000)
                        reaction.AttackCoefficientBp = 20000;
                    else if (reaction.Id == ElementalReactionId.Superconduct && reaction.AttackCoefficientBp == 10000)
                        reaction.AttackCoefficientBp = 15000;
                }
                var shatter = Reactions.Find(r => r?.Id == ElementalReactionId.Shatter);
                if (shatter == null)
                    Reactions.Add(CreateDefault().Reactions.Find(r => r.Id == ElementalReactionId.Shatter));
                else
                    shatter.AttackCoefficientBp = ShatterCoefficientBp;
            }
            ContentVersion = "elemental-auras-v6";
        }

        public static ElementalRuleSet CreateSliceAC() => new ElementalRuleSet {
            Reactions = new List<ElementalReactionDefinition> {
                Amplify(ElementalReactionId.Melt, ElementId.Cryo, ElementId.Pyro, 10, 20000),
                Amplify(ElementalReactionId.Vaporize, ElementId.Pyro, ElementId.Hydro, 20, 20000),
                Amplify(ElementalReactionId.ReverseMelt, ElementId.Pyro, ElementId.Cryo, 30, 15000),
                Amplify(ElementalReactionId.ReverseVaporize, ElementId.Hydro, ElementId.Pyro, 40, 15000),
                new ElementalReactionDefinition { Id = ElementalReactionId.Overload, ExistingElement = ElementId.Pyro,
                    IncomingElement = ElementId.Electro, Priority = 50, AttackCoefficientBp = 20000, OutputElement = ElementId.Pyro },
                new ElementalReactionDefinition { Id = ElementalReactionId.Overload, ExistingElement = ElementId.Electro,
                    IncomingElement = ElementId.Pyro, Priority = 50, AttackCoefficientBp = 20000, OutputElement = ElementId.Pyro } } };

        private static ElementalReactionDefinition Amplify(ElementalReactionId id, ElementId existing,
            ElementId incoming, int priority, int factor) => new ElementalReactionDefinition {
                Id = id, ExistingElement = existing, IncomingElement = incoming,
                Priority = priority, AmplificationBp = factor };

        public static ElementalRuleSet CreateDefault()
        {
            var rules = CreateSliceAC();
            rules.ContentVersion = "elemental-auras-v6";
            foreach (var aura in new[] { ElementId.Pyro, ElementId.Hydro, ElementId.Electro, ElementId.Cryo })
            {
                // Starting spread coefficient; compare single-target and AOE total output in balance fixtures.
                rules.Reactions.Add(new ElementalReactionDefinition { Id = ElementalReactionId.Swirl,
                    ExistingElement = aura, IncomingElement = ElementId.Anemo, Priority = 100,
                    AttackCoefficientBp = 10000, OutputElement = aura, PreserveFuel = true });
                rules.Reactions.Add(new ElementalReactionDefinition { Id = ElementalReactionId.Crystallize,
                    ExistingElement = aura, IncomingElement = ElementId.Geo, Priority = 110, PreserveFuel = true });
            }
            Pair(rules, ElementalReactionId.Superconduct, ElementId.Cryo, ElementId.Electro, 60, ElementId.Cryo, 15000);
            Pair(rules, ElementalReactionId.ElectroCharged, ElementId.Hydro, ElementId.Electro, 70, ElementId.Electro);
            Pair(rules, ElementalReactionId.Freeze, ElementId.Hydro, ElementId.Cryo, 80, ElementId.None);
            Pair(rules, ElementalReactionId.Burning, ElementId.Pyro, ElementId.Dendro, 90, ElementId.Pyro);
            rules.Reactions.Add(new ElementalReactionDefinition { Id = ElementalReactionId.Shatter,
                AttackCoefficientBp = ShatterCoefficientBp, OutputElement = ElementId.None });
            return rules;
        }

        private static void Pair(ElementalRuleSet rules, ElementalReactionId id, ElementId a, ElementId b,
            int priority, ElementId output, int coefficient = 0)
        {
            rules.Reactions.Add(new ElementalReactionDefinition { Id = id, ExistingElement = a, IncomingElement = b,
                Priority = priority, OutputElement = output, AttackCoefficientBp = coefficient, PreserveFuel = id == ElementalReactionId.ElectroCharged });
            rules.Reactions.Add(new ElementalReactionDefinition { Id = id, ExistingElement = b, IncomingElement = a,
                Priority = priority, OutputElement = output, AttackCoefficientBp = coefficient, PreserveFuel = id == ElementalReactionId.ElectroCharged });
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
            if (rules.Reactions == null) { errors.Add("Elemental rules require reactions."); return errors; }
            if (rules.ReactionDamagePerLevel <= 0) errors.Add("Level reaction damage must be positive.");
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
                    (reaction.Id == ElementalReactionId.Overload || reaction.Id == ElementalReactionId.Swirl || reaction.Id == ElementalReactionId.Superconduct || reaction.Id == ElementalReactionId.Shatter ? reaction.AmplificationBp != 10000 || reaction.AttackCoefficientBp <= 0
                        : reaction.AttackCoefficientBp != 0)) errors.Add("Unsupported, duplicate or malformed elemental reaction: " + reaction.Id);
            }
            return errors;
        }

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
            // Legacy application metadata may identify the element; debuff delivery stays an ordinary sequence step.
            // Auras are automatic on contact, without hit-index, critical, recipe or status-roll gates.
            if (app != null && (app.Element < ElementId.Pyro || app.Element > ElementId.Geo ||
                effect.DamageElement != ElementId.None && effect.DamageElement != app.Element))
                errors.Add("Unsupported or conflicting elemental application.");
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
