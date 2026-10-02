using System;
using System.Collections.Generic;

namespace FightingAllstar.Contracts.Protocol
{
    /// <summary>Transport metadata. Subject identity is deliberately supplied by verified authentication, never the body.</summary>
    [Serializable]
    public sealed class CommandEnvelope<TPayload>
    {
        public int SchemaVersion = 1;
        public string RequestId;
        public long ExpectedRevision;
        public TPayload Payload;
    }

    [Serializable]
    public sealed class SubmitBattlePlan
    {
        public List<PlannedActionDto> Actions = new List<PlannedActionDto>();
    }

    [Serializable]
    public sealed class PlannedActionDto
    {
        public bool IsMove;
        public string CardId;
        public string TargetFighterId;
        public int DestinationIndex = -1;
    }

    [Serializable]
    public sealed class ChooseRouteNode
    {
        public string NodeId;
    }

    [Serializable]
    public sealed class ChooseRestAction
    {
        public string Choice;
        public string FighterId;
    }

    [Serializable]
    public sealed class ChooseBoon
    {
        public string OfferId;
        public string BoonId;
    }

    [Serializable]
    public sealed class Summon
    {
        public string BannerId;
        public string BannerRevision;
        public int Count;
    }

    /// <summary>Stored with a successful command so a retry returns the original result without applying it again.</summary>
    [Serializable]
    public sealed class CommandReceipt<TProjection>
    {
        public string RequestId;
        public string PayloadHash;
        public long ResultRevision;
        public TProjection Result;
    }

    [Serializable]
    public sealed class CommandError
    {
        public string Code;
        public string Message;
        public long CurrentRevision;
    }
}
