using System;
using System.Collections.Generic;
using FightingAllstar.Core.Run;
using UnityEngine;

namespace FightingAllstar.Presentation.Route
{
    [Serializable]
    public sealed class EnemyTemplateFighterEntry
    {
        public CharacterObject Character;
        public bool OverrideConstellationTier;
        [Range(0, 5)] public int ConstellationTier;
    }

    [Serializable]
    public sealed class EnemyFormationTemplateEntry
    {
        public string Id;
        [Min(0)] public int Score;
        public bool UseForBattle = true;
        public bool UseForElite = true;
        public bool UseForBoss = true;
        public List<EnemyTemplateFighterEntry> Fighters = new List<EnemyTemplateFighterEntry>();

        public EnemyFormationTemplate ToCoreTemplate()
        {
            var result = new EnemyFormationTemplate { Id = Id, Score = Score, UseForBattle = UseForBattle,
                UseForElite = UseForElite, UseForBoss = UseForBoss };
            if (Fighters != null)
                foreach (var fighter in Fighters)
                    result.Fighters.Add(fighter == null ? null : new EnemyFormationMember
                    {
                        FighterId = fighter.Character == null ? null : fighter.Character.DefinitionId,
                        ConstellationTier = fighter.OverrideConstellationTier ? fighter.ConstellationTier : -1
                    });
            return result;
        }
    }

    [Serializable]
    public sealed class EnemyTemplatePolicyEntry
    {
        [Tooltip("Exact DungeonProfile.Id, for example dungeon.open-circuit or dungeon.filter:series.kof:1:0.")]
        public string PolicyId;
        [Range(0, 100)] public int PresetTeamChancePercent = 50;
        [Range(0, 100)] public int DifficultyIncreasePerRowPercent = 3;
        [Range(0, 100)] public int EliteDifficultyBonusPercent = 10;
        [Range(0, 100)] public int BossDifficultyBonusPercent = 20;
        public List<EnemyFormationTemplateEntry> Templates = new List<EnemyFormationTemplateEntry>();
    }

    [CreateAssetMenu(fileName = "EnemyTemplateLibrary", menuName = "Fighting Allstar/Enemy Template Library")]
    public sealed class EnemyTemplateLibrarySO : ScriptableObject
    {
        private const string ResourcesAssetName = "EnemyTemplateLibrary";

        [SerializeField] private List<EnemyTemplatePolicyEntry> policies = new List<EnemyTemplatePolicyEntry>();

        public static DungeonProfile ApplyResourcesTo(DungeonProfile profile)
        {
            if (profile == null) return null;
            var library = Resources.Load<EnemyTemplateLibrarySO>(ResourcesAssetName);
            if (library != null) library.ApplyTo(profile);
            return profile;
        }

        public void ApplyTo(DungeonProfile profile)
        {
            if (profile == null || policies == null) return;
            foreach (var policy in policies)
            {
                if (policy == null || !string.Equals(policy.PolicyId, profile.Id, StringComparison.Ordinal)) continue;
                profile.PresetTeamChancePercent = policy.PresetTeamChancePercent;
                profile.DifficultyIncreasePerRowPercent = policy.DifficultyIncreasePerRowPercent;
                profile.EliteDifficultyBonusPercent = policy.EliteDifficultyBonusPercent;
                profile.BossDifficultyBonusPercent = policy.BossDifficultyBonusPercent;
                profile.EnemyFormationTemplates.Clear();
                if (policy.Templates != null)
                    foreach (var template in policy.Templates)
                        profile.EnemyFormationTemplates.Add(template?.ToCoreTemplate());
                return;
            }
        }
    }
}
