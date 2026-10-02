using System;

namespace FightingAllstar.Adapters
{
    /// <summary>Identity seam for the offline profile. An auth adapter binds the provider's stable subject before loading app scenes.</summary>
    public static class LocalPlayerAccountContext
    {
        public const string GuestSubjectId = "guest:local-development";
        public static string SubjectId { get; private set; } = GuestSubjectId;

        public static void BindAuthenticatedSubject(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId)) throw new ArgumentException("Authenticated subject ID is required.", nameof(subjectId));
            SubjectId = subjectId.Trim();
        }
    }
}
