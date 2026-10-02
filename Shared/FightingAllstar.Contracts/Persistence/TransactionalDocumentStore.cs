using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FightingAllstar.Contracts.Persistence
{
    /// <summary>Opaque document data keeps provider SDK types outside Core, Contracts, and the Unity client.</summary>
    [Serializable]
    public sealed class DocumentSnapshot
    {
        public string Key;
        public string Version;
        public byte[] Data;
    }

    [Serializable]
    public sealed class ExpectedDocumentVersion
    {
        public string Key;
        public string Version;
    }

    [Serializable]
    public sealed class DocumentWrite
    {
        public string Key;
        public byte[] Data;
        public bool Delete;
    }

    [Serializable]
    public sealed class TransactionCommitResult
    {
        public bool Committed;
        public string ConflictKey;
        public Dictionary<string, string> NewVersions = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Provider adapter must atomically compare all expected versions and apply all writes. It must not call remote
    /// services from inside a transaction callback; callers prepare deterministic outcomes before committing.
    /// </summary>
    public interface ITransactionalDocumentStore
    {
        Task<IReadOnlyList<DocumentSnapshot>> ReadAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken);
        Task<TransactionCommitResult> TryCommitAsync(IReadOnlyList<ExpectedDocumentVersion> expectedVersions,
            IReadOnlyList<DocumentWrite> writes, CancellationToken cancellationToken);
    }
}
