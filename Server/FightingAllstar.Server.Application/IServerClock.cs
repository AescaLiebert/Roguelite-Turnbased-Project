namespace FightingAllstar.Server.Application
{
    /// <summary>Injectable UTC clock used to enforce verified identity expiry at the application boundary.</summary>
    public interface IServerClock
    {
        long UtcNowUnixSeconds { get; }
    }
}
