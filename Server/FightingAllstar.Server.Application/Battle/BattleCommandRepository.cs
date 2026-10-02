using System.Threading;
using System.Threading.Tasks;
using FightingAllstar.Contracts.Protocol;
using FightingAllstar.Contracts.Projections;
using FightingAllstar.Core.Combat;

namespace FightingAllstar.Server.Application.Battle
{
    /// <summary>Server-owned match record; subject membership is loaded from trusted persistence, never command JSON.</summary>
    public sealed class BattleCommandSnapshot
    {
        public string StorageVersion;
        public string PlayerSubjectId;
        public string OpponentSubjectId;
        public BattleState State;
        public CommandReceipt<BattleView> ExistingReceipt;
    }

    /// <summary>
    /// Persistence adapter must read match and request receipt consistently and atomically compare the match version
    /// while writing both the new state and receipt. Receipt keys are scoped by match and authenticated subject.
    /// </summary>
    public interface IBattleCommandRepository
    {
        Task<BattleCommandSnapshot> ReadAsync(string matchId, string authenticatedSubjectId, string requestId,
            CancellationToken cancellationToken);

        Task<bool> TryCommitAsync(string matchId, string authenticatedSubjectId, string expectedStorageVersion,
            BattleState nextState, CommandReceipt<BattleView> receipt, CancellationToken cancellationToken);
    }
}
