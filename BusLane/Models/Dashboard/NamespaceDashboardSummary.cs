namespace BusLane.Models.Dashboard;

public record NamespaceDashboardSummary(
    long TotalActiveMessages,
    long TotalDeadLetterMessages,
    long TotalScheduledMessages,
    long TotalSizeInBytes,
    double ActiveMessagesGrowthPercentage,
    double DeadLetterGrowthPercentage,
    double ScheduledGrowthPercentage,
    double SizeGrowthPercentage,
    DateTimeOffset Timestamp
);
