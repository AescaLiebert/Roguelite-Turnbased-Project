using System;

namespace FightingAllstar.Contracts.Security
{
    /// <summary>Identity output produced only after a trusted server adapter validates a provider token.</summary>
    [Serializable]
    public sealed class VerifiedIdentity
    {
        public string SubjectId;
        public string Provider;
        public long IssuedAtUnixSeconds;
        public long ExpiresAtUnixSeconds;
    }
}
