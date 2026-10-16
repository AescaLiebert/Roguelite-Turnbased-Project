using System;

namespace FightingAllstar.Core.Content
{
    /// <summary>Reusable combat capabilities; the resolver never switches on character identity.</summary>
    [Serializable]
    public sealed class CombatSupportDefinition
    {
        public PassivePresence Presence = PassivePresence.LivingActive;
        public int ExplosionMultiplierBp = 10000;
        public int SwirlStrengthBp;
        public PassivePresence SwirlPresence = PassivePresence.LivingReserve;
        public int OpeningTeamGauge;
        public int DamageHealLowestAllyBp;
        public string BonusAgainstRecipeId;
        public int BonusCritChanceBp;
        public int BonusDamageBp;
        public string AttackerGaugeAgainstRecipeId;
        public int AttackerGaugeGainBp;
        public string AfterAttackStatusRecipeId;
        public bool RequiresElementalTeam;
        public int AttackCadence;
        public int CadenceDamageBp;
        public int CadenceDotBp;
        public bool IsConfigured => ExplosionMultiplierBp > 10000 || SwirlStrengthBp != 0 || OpeningTeamGauge != 0 ||
            DamageHealLowestAllyBp != 0 || !string.IsNullOrEmpty(BonusAgainstRecipeId) ||
            !string.IsNullOrEmpty(AttackerGaugeAgainstRecipeId) || !string.IsNullOrEmpty(AfterAttackStatusRecipeId) || AttackCadence != 0;
        public CombatSupportDefinition Clone() => (CombatSupportDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class TriggeredElementalAttackDefinition
    {
        public ElementId Element;
        public int AttackCoefficientBp;
        public int ReceivedDamageCoefficientBp;
        public bool Explosion;
        public string StatusRecipeId;
        public int StatusDuration;
        public int StatusStackCount = 1;
        public int SelfHealDamageBp;
        public bool IsConfigured => Element != ElementId.None || AttackCoefficientBp != 0 || ReceivedDamageCoefficientBp != 0 ||
            Explosion || !string.IsNullOrEmpty(StatusRecipeId) || StatusDuration != 0 || StatusStackCount != 1 || SelfHealDamageBp != 0;
        public TriggeredElementalAttackDefinition Clone() => (TriggeredElementalAttackDefinition)MemberwiseClone();
    }
}
