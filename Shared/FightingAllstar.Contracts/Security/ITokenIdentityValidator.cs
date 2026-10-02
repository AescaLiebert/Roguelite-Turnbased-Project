using System.Threading;
using System.Threading.Tasks;

namespace FightingAllstar.Contracts.Security
{
    /// <summary>Server adapter validates issuer, audience, signature, and lifetime; invalid tokens return null.</summary>
    public interface ITokenIdentityValidator
    {
        Task<VerifiedIdentity> ValidateAsync(string bearerToken, CancellationToken cancellationToken);
    }
}
