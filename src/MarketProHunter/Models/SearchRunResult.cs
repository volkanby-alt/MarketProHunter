namespace MarketProHunter.Models;

public sealed record SearchRunResult(
    int ScannedCount,
    int AcceptedCount,
    int SkippedCount,
    string OutputPath,
    string SmartQueuePath = "",
    string SummaryPath = "",
    int SmartQueueCount = 0,
    decimal SmartQueueExpectedNetProfit = 0,
    decimal SmartQueueAverageUploadScore = 0,
    decimal SmartQueueAverageConfidenceScore = 0,
    int FailedPageCount = 0);
