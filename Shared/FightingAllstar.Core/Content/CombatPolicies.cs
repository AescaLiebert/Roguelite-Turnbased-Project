using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum CombatPolicyKind { IncomingDamageCap, GaugeLossGuard, BeforeDefeat }

    /// <summary>Declarative interception owned by a passive. Caps limit HP-directed loss per committed contact.</summary>
    [Serializable]
    public sealed class CombatPolicyDefinition
    {
        public string Id;
        public int Priority;
        public CombatPolicyKind Kind;
        public PassiveGate Gate = new PassiveGate();
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public int MaximumHealthDamageBp = 4900;
        public string ResourceKey;
        public int ResourceCost;
        public int HealMaxHealthBp = 5000;
        public bool CleanseDebuffs;
        public bool Bypassable = true;

        public CombatPolicyDefinition Clone()
        {
            var copy = (CombatPolicyDefinition)MemberwiseClone();
            copy.Gate = Gate?.Clone();
            copy.Conditions = Conditions?.ConvertAll(c => c?.Clone()) ?? new List<EffectConditionDefinition>();
            return copy;
        }
    }

    /// <summary>Explicit damage semantics. A null profile preserves the legacy family behavior.</summary>
    [Serializable]
    public sealed class DamageProfileDefinition
    {
        public string Id;
        public bool ExplicitTrueDamage;
        public int TrueDamageBonusBp = 2000;
        public bool BypassDefense;
        public bool BypassResistance;
        public bool BypassGenericReduction;
        public bool BypassFinalReduction;
        public bool BypassFlatReduction;
        public bool BypassShield;
        public bool BypassConstruct;
        public bool BypassDamageCap;
        public bool BypassFatalSave;
        public DamageProfileDefinition Clone() => (DamageProfileDefinition)MemberwiseClone();
    }
}
