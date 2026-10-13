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
        public DungeonMode Mode;
        public string SeriesId;
        public int Phase;
        public int SubPhase;
        public static readonly string[] PhaseNames = { "WIP-Phase", "Pre-Release", "Official-Release", "Season2", "Season3", "Season4", "MidGame", "LateGame", "META" };
        public static readonly string[] SubPhaseNames = { "Launch", "Begin", "Middle", "Late", "META" };

        public bool Accepts(CharacterDefinition character) => character != null &&
            (string.IsNullOrEmpty(SeriesId) || string.Equals(NormalizeSeriesId(SeriesId), NormalizeSeriesId(character.SeriesId), StringComparison.OrdinalIgnoreCase)) &&
            (Phase == 0 || IncludesCategory(character.CategoryId)) &&
            (EligibleCharacterIds == null || EligibleCharacterIds.Count == 0 || EligibleCharacterIds.Contains(character.Id));

        // IDs encode phase / sub-phase / character index. The chosen stage is an inclusive
        // release cutoff, so every earlier phase and sub-phase remains eligible.
        private bool IncludesCategory(int categoryId)
        {
            if (categoryId < 1000 || categoryId > 9499) return false;
            var phase = categoryId / 1000;
            var subPhase = categoryId / 100 % 10;
            return subPhase <= 4 && (phase < Phase || phase == Phase && subPhase <= SubPhase);
        }

        public static string SeriesDisplayName(string seriesId)
        {
            switch (NormalizeSeriesId(seriesId))
            {
                case null: case "": case "*": return "Open Circuit";
                case "series.kof": return "KOF";
                case "series.tekken": return "Tekken";
                case "series.soulcalibur": return "Soul Calibur";
                case "series.genshin-impact": return "Genshin Impact";
                default: return seriesId.Replace("series.", "");
            }
        }

        // Existing Soul Calibur assets use series.sc; entry policies use the full stable name.
        public static string NormalizeSeriesId(string seriesId) => string.Equals(seriesId?.Trim(), "series.sc", StringComparison.OrdinalIgnoreCase)
            ? "series.soulcalibur" : seriesId?.Trim().ToLowerInvariant();

        public static DungeonProfile Filtered(string seriesId, int phase, int subPhase)
        {
            if (phase < 1 || phase > 9 || subPhase < 0 || subPhase > 4)
                throw new ArgumentException("Choose phase 1-9 and sub-phase 0-4.");
            seriesId = string.IsNullOrWhiteSpace(seriesId) || seriesId == "*" ? null : seriesId.Trim();
            var profile = CreateBase("dungeon.filter:" + (seriesId ?? "*") + ":" + phase + ":" + subPhase,
                SeriesDisplayName(seriesId) + " · " + PhaseNames[phase - 1] + " / " + SubPhaseNames[subPhase]);
            profile.SeriesId = seriesId; profile.Phase = phase; profile.SubPhase = subPhase;
            profile.Rows[2].Choices = new[] { RouteNodeType.Battle, RouteNodeType.Battle, RouteNodeType.Elite };
            profile.Rows[4].Choices = new[] { RouteNodeType.Boon, RouteNodeType.Battle, RouteNodeType.Boon };
            profile.Rows[6].Choices = new[] { RouteNodeType.Rest, RouteNodeType.Battle, RouteNodeType.Elite };
            return profile;
        }
        public static DungeonProfile SeriesChallengeBoss(string seriesId, int phase, int subPhase)
        {
            var profile = Filtered(seriesId, phase, subPhase);
            profile.Mode = DungeonMode.SeriesChallengeBoss;
            profile.Id += ":series-boss";
            profile.DisplayName += " · SERIES CHALLENGE BOSS";
            profile.Rows = new List<RouteRowDefinition> { Row(0, RouteNodeType.Start), Row(1, RouteNodeType.Boss) };
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
                SeriesId = SeriesId, Phase = Phase, SubPhase = SubPhase, Mode = Mode };
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
                } };
            foreach (var boon in profile.Boons) profile.BoonIds.Add(boon.Id);
            return profile;
        }

        private static RouteRowDefinition Row(int row, params RouteNodeType[] choices) => new RouteRowDefinition { Row = row, Choices = choices };

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
        public List<ArtifactInstance> ArtifactLoadout = new List<ArtifactInstance>();
        public int ConstellationTier;
        public int OriginalFormationIndex;
        public int CurrentHealth;
        public bool IsDefeated;

        public RunFighterState Clone() => new RunFighterState { RunFighterId = RunFighterId, DefinitionId = DefinitionId,
            Stats = Stats?.Clone(), ArtifactLoadout = ArtifactLoadout?.ConvertAll(a => a?.Clone()) ?? new List<ArtifactInstance>(), ConstellationTier = ConstellationTier,
            OriginalFormationIndex = OriginalFormationIndex, CurrentHealth = CurrentHealth, IsDefeated = IsDefeated };
    }

    [Serializable]
    public sealed class EncounterFighterSnapshot
    {
        public int Level = 1;
        public DungeonEnemyKind EnemyKind;
        public string FighterId;
        public string DefinitionId;
        public CharacterDefinition Definition;
        public StatBlock Stats;
        public int CurrentHealth;
        public int ConstellationTier;
        public int FormationSlot;
        public bool IsReserve;

        public EncounterFighterSnapshot Clone() => new EncounterFighterSnapshot { FighterId = FighterId, DefinitionId = DefinitionId,
            Level = Level, EnemyKind = EnemyKind,
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
        public bool ShouldSerializeBoonOfferIds() => false;
        public string TacticalTag;
        public DungeonStatBoost StatBoost;

        public RouteNodeState Clone()
        {
            var copy = new RouteNodeState { Id = Id, Row = Row, Column = Column, Type = Type, Progress = Progress, TacticalTag = TacticalTag,
                StatBoost = StatBoost?.Clone() };
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
    public sealed class DungeonGenerationData
    {
        // Only route content belongs to dungeon generation. Player/account state and battle RNG do not.
        public ulong Seed;
        public int GeneratorVersion;
        public List<RouteNodeState> Nodes = new List<RouteNodeState>();
    }

    [Serializable]
    public sealed class RunState
    {
        public DungeonMode Mode;
        public int RewardPhase = 1, RewardSubPhase;
        public List<DungeonRewardReceipt> Rewards = new List<DungeonRewardReceipt>();
        public string RunId;
        public string UserId;
        public string ProfileId;
        public string ContentVersion;
        public string ContentHash;
        public string CurrentNodeId;
        public string PendingBattleId;
        public int DataVersion = 2;
        public DungeonGenerationData Generation = new DungeonGenerationData();
        // Compatibility accessors also accept old flat saves; new saves write Generation only.
        public int GeneratorVersion { get => Generation.GeneratorVersion; set => Generation.GeneratorVersion = value; }
        public bool ShouldSerializeGeneratorVersion() => false;
        public int DifficultyBonusPercent;
        public int BaseCompletionDiamonds = 320;
        public int RewardQuoteDiamonds;
        public ulong Seed { get => Generation.Seed; set => Generation.Seed = value; }
        public bool ShouldSerializeSeed() => false;
        public long Revision;
        public RunStatus Status;
        public List<RunFighterState> Roster = new List<RunFighterState>();
        public List<RouteNodeState> Nodes { get => Generation.Nodes; set => Generation.Nodes = value; }
        public bool ShouldSerializeNodes() => false;
        public List<DungeonStatBoost> StatBoosts = new List<DungeonStatBoost>();
        public List<string> PresentedRewardIds = new List<string>();
        public List<string> SelectedPath = new List<string>();
        public List<string> ChosenBoons = new List<string>();
        public List<RunBoonDefinition> BoonDefinitionSnapshot = new List<RunBoonDefinition>();
        public bool ShouldSerializeChosenBoons() => false;
        public bool ShouldSerializeBoonDefinitionSnapshot() => false;
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
                Revision = Revision, Status = Status, RewardClaimed = RewardClaimed,
                Mode = Mode, RewardPhase = RewardPhase, RewardSubPhase = RewardSubPhase, DataVersion = DataVersion };
            foreach (var boost in StatBoosts) copy.StatBoosts.Add(boost.Clone());
            copy.PresentedRewardIds.AddRange(PresentedRewardIds);
            if (Rewards != null) foreach (var reward in Rewards) copy.Rewards.Add(reward.Clone());
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
