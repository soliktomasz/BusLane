namespace BusLane.Tests.Services.Monitoring;

using BusLane.Models;
using BusLane.Services.Monitoring;
using FluentAssertions;

public class NamespaceInboxReviewStoreTests : IDisposable
{
    private readonly string _storePath = Path.Combine(Path.GetTempPath(), $"namespace-inbox-review-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_storePath))
        {
            try
            {
                File.Delete(_storePath);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void Save_StoresReviewStateByNamespaceAndEntity()
    {
        // Arrange
        var sut = new NamespaceInboxReviewStore(_storePath);
        var reviewState = CreateReviewState("prod-a", "orders");

        // Act
        sut.Save(reviewState);
        var loadedReviewState = sut.Get("prod-a", "orders");

        // Assert
        loadedReviewState.Should().NotBeNull();
        loadedReviewState!.NamespaceId.Should().Be("prod-a");
        loadedReviewState.EntityName.Should().Be("orders");
    }

    [Fact]
    public void Save_PreservesSnapshotValuesForDeltaCalculation()
    {
        // Arrange
        var sut = new NamespaceInboxReviewStore(_storePath);
        var reviewState = CreateReviewState(
            "prod-a",
            "orders",
            activeMessageCount: 42,
            deadLetterCount: 5,
            scheduledCount: 11,
            activeAlertCount: 2);

        // Act
        sut.Save(reviewState);
        var loadedReviewState = sut.LoadAll().Single();

        // Assert
        loadedReviewState.ActiveMessageCount.Should().Be(42);
        loadedReviewState.DeadLetterCount.Should().Be(5);
        loadedReviewState.ScheduledCount.Should().Be(11);
        loadedReviewState.ActiveAlertCount.Should().Be(2);
    }

    [Fact]
    public void LoadAll_WhenFileIsMissing_ReturnsEmptyCollection()
    {
        // Arrange
        var sut = new NamespaceInboxReviewStore(_storePath);

        // Act
        var reviewStates = sut.LoadAll();

        // Assert
        reviewStates.Should().BeEmpty();
    }

    [Fact]
    public void LoadAll_WhenFileIsCorrupt_ReturnsEmptyCollection()
    {
        // Arrange
        File.WriteAllText(_storePath, "{ definitely-not-json");
        var sut = new NamespaceInboxReviewStore(_storePath);

        // Act
        var reviewStates = sut.LoadAll();

        // Assert
        reviewStates.Should().BeEmpty();
    }

    [Fact]
    public void Get_AfterInitialLoad_UsesCachedReviewState()
    {
        // Arrange
        var sut = new NamespaceInboxReviewStore(_storePath);
        var reviewState = CreateReviewState("prod-a", "orders", activeMessageCount: 42);
        sut.Save(reviewState);

        sut.Get("prod-a", "orders").Should().NotBeNull();
        File.Delete(_storePath);

        // Act
        var cachedReviewState = sut.Get("prod-a", "orders");

        // Assert
        cachedReviewState.Should().NotBeNull();
        cachedReviewState!.ActiveMessageCount.Should().Be(42);
    }

    private static NamespaceInboxReviewState CreateReviewState(
        string namespaceId,
        string entityName,
        long activeMessageCount = 10,
        long deadLetterCount = 2,
        long scheduledCount = 0,
        int activeAlertCount = 1)
    {
        return new NamespaceInboxReviewState(
            namespaceId,
            entityName,
            ReviewedAt: new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero),
            ActiveMessageCount: activeMessageCount,
            DeadLetterCount: deadLetterCount,
            ScheduledCount: scheduledCount,
            ActiveAlertCount: activeAlertCount);
    }
}
