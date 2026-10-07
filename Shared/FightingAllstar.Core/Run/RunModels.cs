using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Run
{
    public enum RouteNodeType { Start, Battle, Elite, Rest, Boon, Boss }
    public enum RouteNodeProgress { Locked, Reachable, Selected, Completed, Bypassed }
    public enum RunStatus { InProgress, InBattle, Completed, Defeated, Abandoned }
    public enum RestChoice { HealLiving, ReviveOne, Continue }
    public enum RunBoonEffectKind { StatusDamageIncrease, FirstCleanseShield, ReflectFirstRootAction, StartPowerGauge }

    [Serializable]
    public sealed class RunBoonDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public RunBoonEffectKind EffectKind;
        public int MagnitudeBp;
        public int MagnitudePoints;
        public RunBoonDefinition Clone() => (RunBoonDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class RosterRestriction
    {
        public string AttributeId;
        public string TraitId;
        public int MinimumCount;
        public string Description;
        public RosterRestriction Clone() => (RosterRestriction)MemberwiseClone();
    }

    [Serializable]
    public sealed class EnemyFormationMember
    {
        public string FighterId;
        // -1 uses the owning dungeon policy's default tier.
        public int ConstellationTier = -1;

        public EnemyFormationMember Clone() => (EnemyFormationMember)MemberwiseClone();
    }

    /// <summary>A complete, authored enemy formation owned by one dungeon policy.</summary>
    [Serializable]
    public sealed class EnemyFormationTemplate
    {
        public string Id;
        public int Score;
        public bool UseForBattle = true;
        public bool UseForElite = true;
        public bool UseForBoss = true;
        public List<EnemyFormationMember> Fighters = new List<EnemyFormationMember>();

        public EnemyFormationTemplate Clone()
        {
            var copy = new EnemyFormationTemplate { Id = Id, Score = Score, UseForBattle = UseForBattle,
                UseForElite = UseForElite, UseForBoss = UseForBoss };
            if (Fighters != null) foreach (var fighter in Fighters) copy.Fighters.Add(fighter?.Clone());
            return copy;
        }
    }

    [Serializable]
    public sealed class RouteRowDefinition
    {
        public int Row;
        public RouteNodeType[] Choices = Array.Empty<RouteNodeType>();
        public RouteRowDefinition Clone() => new RouteRowDefinition { Row = Row, Choices = (RouteNodeType[])Choices.Clone() };
    }

    [Serializable]
    public sealed class DungeonProfile
    {
        public string SeriesId;
        public int Phase;
        public int SubPhase;
        public static readonly string[] PhaseNames = { "WIP-Phase", "Pre-Release", "Official-Release", "Season2", "Season3", "Season4", "MidGame", "LateGame", "EndGame" };
        public static readonly string[] SubPhaseNames = { "Launch", "Begin", "Middle", "Late", "End" };

        public bool Accepts(CharacterDefinition character) => character != null &&
            (string.IsNullOrEmpty(SeriesId) || string.Equals(SeriesId, character.SeriesId, StringComparison.OrdinalIgnoreCase)) &&
            (Phase == 0 || character.CategoryId >= 1000 && character.CategoryId <= 9499 &&
                character.CategoryId / 1000 == Phase && character.CategoryId / 100 % 10 == SubPhase) &&
            (EligibleCharacterIds == null || EligibleCharacterIds.Count == 0 || EligibleCharacterIds.Contains(character.Id));

        public static DungeonProfile Filtered(string seriesId, int phase, int subPhase)
        {
            if (string.IsNullOrWhiteSpace(seriesId) || phase < 1 || phase > 9 || subPhase < 0 || subPhase > 4)
                throw new ArgumentException("Choose a series, phase 1-9 and sub-phase 0-4.");
            var profile = CreateBase("dungeon.filter:" + seriesId + ":" + phase + ":" + subPhase,
                seriesId.Replace("series.", "").ToUpperInvariant() + " · " + PhaseNames[phase - 1] + " / " + SubPhaseNames[subPhase]);
            profile.SeriesId = seriesId; profile.Phase = phase; profile.SubPhase = subPhase;
            profile.Rows[2].Choices = new[] { RouteNodeType.Battle, RouteNodeType.Battle, RouteNodeType.Elite };
            profile.Rows[4].Choices = new[] { RouteNodeType.Boon, RouteNodeType.Battle, RouteNodeType.Boon };
            profile.Rows[6].Choices = new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Elite };
            return profile;
        }
        public string Id;
        public string DisplayName;
        public int GeneratorVersion = RouteGenerator.CurrentGeneratorVersion;
        public int EnemyConstellationTier;
        public int PresetTeamChancePercent = 50;
        public int DifficultyIncreasePerRowPercent = 3;
        public int EliteDifficultyBonusPercent = 10;
        public int BossDifficultyBonusPercent = 20;
        public List<string> EligibleCharacterIds = new List<string>();
        public List<RosterRestriction> Restrictions = new List<RosterRestriction>();
        public List<EnemyFormationTemplate> EnemyFormationTemplates = new List<EnemyFormationTemplate>();
        public List<string> BoonIds = new List<string>();
        public List<RunBoonDefinition> Boons = new List<RunBoonDefinition>();
        public List<RouteRowDefinition> Rows = new List<RouteRowDefinition>();

        public DungeonProfile Clone()
        {
            var copy = new DungeonProfile { Id = Id, DisplayName = DisplayName, GeneratorVersion = GeneratorVersion,
                EnemyConstellationTier = EnemyConstellationTier, PresetTeamChancePercent = PresetTeamChancePercent,
                DifficultyIncreasePerRowPercent = DifficultyIncreasePerRowPercent,
                EliteDifficultyBonusPercent = EliteDifficultyBonusPercent, BossDifficultyBonusPercent = BossDifficultyBonusPercent,
                SeriesId = SeriesId, Phase = Phase, SubPhase = SubPhase };
            copy.EligibleCharacterIds.AddRange(EligibleCharacterIds);
            foreach (var restriction in Restrictions) copy.Restrictions.Add(restriction.Clone());
            if (EnemyFormationTemplates != null)
                foreach (var template in EnemyFormationTemplates) copy.EnemyFormationTemplates.Add(template?.Clone());
            copy.BoonIds.AddRange(BoonIds);
            if (Boons != null) foreach (var boon in Boons) copy.Boons.Add(boon.Clone());
            foreach (var row in Rows) copy.Rows.Add(row.Clone());
            return copy;
        }

        public static DungeonProfile OpenCircuit() => CreateBase("dungeon.open-circuit", "Open Circuit");
        public static DungeonProfile GreenAccord()
        {
            var profile = CreateBase("dungeon.green-accord", "Green Accord");
            profile.Restrictions.Add(new RosterRestriction { AttributeId = "attribute.green", MinimumCount = 2,
                Description = "At least two Green fighters in the selected formation." });
            return profile;
        }
        public static DungeonProfile WomenExhibition()
        {
            var profile = CreateBase("dungeon.women-exhibition", "Women Exhibition");
            profile.Restrictions.Add(new RosterRestriction { TraitId = "trait.women", MinimumCount = 2,
                Description = "At least two Women trait fighters in the selected formation." });
            return profile;
        }

        private static DungeonProfile CreateBase(string id, string name)
        {
            var profile = new DungeonProfile { Id = id, DisplayName = name, GeneratorVersion = RouteGenerator.CurrentGeneratorVersion,
                Rows = new List<RouteRowDefinition>
                {
                    Row(0, RouteNodeType.Start),
                    Row(1, RouteNodeType.Battle, RouteNodeType.Battle),
                    Row(2, RouteNodeType.Battle, RouteNodeType.Battle),
                    Row(3, RouteNodeType.Rest, RouteNodeType.Elite),
                    Row(4, RouteNodeType.Boon),
                    Row(5, RouteNodeType.Battle, RouteNodeType.Battle),
                    Row(6, RouteNodeType.Rest, RouteNodeType.Battle),
                    Row(7, RouteNodeType.Elite, RouteNodeType.Elite),
                    Row(8, RouteNodeType.Boss)
                }, Boons = DefaultBoons() };
            foreach (var boon in profile.Boons) profile.BoonIds.Add(boon.Id);
            return profile;
        }

        private static RouteRowDefinition Row(int row, params RouteNodeType[] choices) => new RouteRowDefinition { Row = row, Choices = choices };

        private static List<RunBoonDefinition> DefaultBoons() => new List<RunBoonDefinition>
        {
            new RunBoonDefinition { Id = "boon.lingering-venom", Name = "Lingering Venom", Description = "+25% Poison, Bleed, and Shock damage.", EffectKind = RunBoonEffectKind.StatusDamageIncrease, MagnitudeBp = 2500 },
            new RunBoonDefinition { Id = "boon.restorative-rhythm", Name = "Restorative Rhythm", Description = "After the first cleanse each owner turn, grant a shield equal to 60% of caster ATK.", EffectKind = RunBoonEffectKind.FirstCleanseShield, MagnitudeBp = 6000 },
            new RunBoonDefinition { Id = "boon.mirror-sigil", Name = "Mirror Sigil", Description = "Reflect 15% of direct HP loss from the first enemy root action after owner TurnStart.", EffectKind = RunBoonEffectKind.ReflectFirstRootAction, MagnitudeBp = 1500 },
            new RunBoonDefinition { Id = "boon.opening-plan", Name = "Opening Plan", Description = "If the enemy takes the first turn, your lowest-slot active fighter starts with +1 PG.", EffectKind = RunBoonEffectKind.StartPowerGauge, MagnitudePoints = 1 }
        };
    }

    [Serializable]
    public sealed class RunFighterSeed
    {
        public string OwnedFighterId;
        public CharacterDefinition Definition;
        public StatBlock ResolvedStats;
        public int ConstellationTier;
        public int FormationSlot;
        public bool IsReserve;
    }

    [Serializable]
    public sealed class RunFighterState
    {
        public string RunFighterId;
        public string DefinitionId;
        public StatBlock Stats;
        public int ConstellationTier;
        public int OriginalFormationIndex;
        public int CurrentHealth;
        public bool IsDefeated;

        public RunFighterState Clone() => new RunFighterState { RunFighterId = RunFighterId, DefinitionId = DefinitionId,
            Stats = Stats?.Clone(), ConstellationTier = ConstellationTier,
            OriginalFormationIndex = OriginalFormationIndex, CurrentHealth = CurrentHealth, IsDefeated = IsDefeated };
    }

    [Serializable]
    public sealed class EncounterFighterSnapshot
    {
        public string FighterId;
        public string DefinitionId;
        public CharacterDefinition Definition;
        public StatBlock Stats;
        public int CurrentHealth;
        public int ConstellationTier;
        public int FormationSlot;
        public bool IsReserve;

        public EncounterFighterSnapshot Clone() => new EncounterFighterSnapshot { FighterId = FighterId, DefinitionId = DefinitionId,
            Definition = Definition?.Clone(), Stats = Stats?.Clone(), CurrentHealth = CurrentHealth,
            ConstellationTier = ConstellationTier, FormationSlot = FormationSlot, IsReserve = IsReserve };
    }

    [Serializable]
    public sealed class RouteNodeState
    {
        public string Id;
        public int Row;
        public int Column;
        public RouteNodeType Type;
        public RouteNodeProgress Progress;
        public List<string> OutgoingNodeIds = new List<string>();
        public List<EncounterFighterSnapshot> EnemyTeamSnapshot = new List<EncounterFighterSnapshot>();
        public List<string> BoonOfferIds = new List<string>();
        public string TacticalTag;

        public RouteNodeState Clone()
        {
            var copy = new RouteNodeState { Id = Id, Row = Row, Column = Column, Type = Type, Progress = Progress, TacticalTag = TacticalTag };
            copy.OutgoingNodeIds.AddRange(OutgoingNodeIds);
            foreach (var fighter in EnemyTeamSnapshot) copy.EnemyTeamSnapshot.Add(fighter.Clone());
            copy.BoonOfferIds.AddRange(BoonOfferIds);
            return copy;
        }
    }

    [Serializable]
    public sealed class RunCommandReceipt
    {
        public string RequestId;
        public string PayloadHash;
        public long ResultRevision;
        public RunCommandReceipt Clone() => (RunCommandReceipt)MemberwiseClone();
    }

    [Serializable]
    public sealed class RunState
    {
        public string RunId;
        public string UserId;
        public string ProfileId;
        public string ContentVersion;
        public string ContentHash;
        public string CurrentNodeId;
        public string PendingBattleId;
        public int GeneratorVersion;
        public int DifficultyBonusPercent;
        public int BaseCompletionDiamonds = 320;
        public int RewardQuoteDiamonds;
        public ulong Seed;
        public long Revision;
        public RunStatus Status;
        public List<RunFighterState> Roster = new List<RunFighterState>();
        public List<RouteNodeState> Nodes = new List<RouteNodeState>();
        public List<string> SelectedPath = new List<string>();
        public List<string> ChosenBoons = new List<string>();
        public List<RunBoonDefinition> BoonDefinitionSnapshot = new List<RunBoonDefinition>();
        public List<string> CompletedBattleReceiptIds = new List<string>();
        public List<RunCommandReceipt> CommandReceipts = new List<RunCommandReceipt>();
        public bool RewardClaimed;
        public List<string> PreRunFormation = new List<string>();
        public List<string> Formation = new List<string> { "", "", "", "" };

        public RunState Clone()
        {
            var copy = new RunState { RunId = RunId, UserId = UserId, ProfileId = ProfileId, ContentVersion = ContentVersion,
                ContentHash = ContentHash, CurrentNodeId = CurrentNodeId, PendingBattleId = PendingBattleId,
                GeneratorVersion = GeneratorVersion, DifficultyBonusPercent = DifficultyBonusPercent,
                BaseCompletionDiamonds = BaseCompletionDiamonds, RewardQuoteDiamonds = RewardQuoteDiamonds, Seed = Seed,
                Revision = Revision, Status = Status, RewardClaimed = RewardClaimed };
            foreach (var fighter in Roster) copy.Roster.Add(fighter.Clone());
            foreach (var node in Nodes) copy.Nodes.Add(node.Clone());
            copy.SelectedPath.AddRange(SelectedPath);
            copy.ChosenBoons.AddRange(ChosenBoons);
            foreach (var boon in BoonDefinitionSnapshot) copy.BoonDefinitionSnapshot.Add(boon.Clone());
            copy.CompletedBattleReceiptIds.AddRange(CompletedBattleReceiptIds);
            foreach (var receipt in CommandReceipts) copy.CommandReceipts.Add(receipt.Clone());
            if (PreRunFormation != null) copy.PreRunFormation.AddRange(PreRunFormation);
            copy.Formation = Formation == null ? new List<string> { "", "", "", "" } : new List<string>(Formation);
            return copy;
        }

        public RouteNodeState FindNode(string id) => Nodes.Find(node => node.Id == id);
        public RunFighterState FindFighter(string id) => Roster.Find(fighter => fighter.RunFighterId == id);
    }

    [Serializable]
    public sealed class BattleFighterResult
    {
        public string RunFighterId;
        public int CurrentHealth;
        public bool IsDefeated;
    }

    [Serializable]
    public sealed class EncounterProjection
    {
        public string BattleId;
        public long RunRevision;
        public int DifficultyBonusPercent;
        public List<EncounterFighterSnapshot> PlayerTeam = new List<EncounterFighterSnapshot>();
        public List<EncounterFighterSnapshot> EnemyTeam = new List<EncounterFighterSnapshot>();
        public List<RunBoonDefinition> ChosenBoons = new List<RunBoonDefinition>();
    }
}
