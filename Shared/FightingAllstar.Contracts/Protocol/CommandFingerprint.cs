using System;
using System.Security.Cryptography;
using System.Text;

namespace FightingAllstar.Contracts.Protocol
{
    /// <summary>Canonical request binding used by durable idempotency records.</summary>
    public static class CommandFingerprint
    {
        public static string Compute(string operation, string requestId, long expectedRevision, string canonicalPayload)
        {
            if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("An operation name is required.", nameof(operation));
            if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("A request ID is required.", nameof(requestId));
            var canonical = new StringBuilder();
            Append(canonical, operation);
            Append(canonical, requestId);
            canonical.Append(expectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            Append(canonical, canonicalPayload ?? string.Empty);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(':').Append(value).Append('|');
        }
    }
}
