namespace MarketProHunter.Models;

public sealed record FilterDecision(bool Accepted, string Reason)
{
    public static FilterDecision Accept(string reason = "Accepted") => new(true, reason);
    public static FilterDecision Reject(string reason) => new(false, reason);
}
