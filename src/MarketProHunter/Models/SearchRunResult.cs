namespace MarketProHunter.Models;

public sealed record SearchRunResult(
    int ScannedCount,
    int AcceptedCount,
    int SkippedCount,
    string OutputPath);
