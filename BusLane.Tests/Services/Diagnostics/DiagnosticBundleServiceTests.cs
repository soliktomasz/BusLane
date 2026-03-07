namespace BusLane.Tests.Services.Diagnostics;

using System.IO.Compression;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Dashboard;
using BusLane.Services.Diagnostics;
using BusLane.Services.Infrastructure;
using BusLane.Services.Monitoring;
using BusLane.Services.Storage;
using FluentAssertions;
using NSubstitute;

public class DiagnosticBundleServiceTests : IDisposable
{
    private readonly string _bundleDirectory = Path.Combine(Path.GetTempPath(), $"diagnostics-{Guid.NewGuid():N}");

    [Fact]
    public async Task ExportAsync_RedactsSecretsFromManifestAndPayloads()
    {
        // Arrange
        Directory.CreateDirectory(_bundleDirectory);
        var logSink = new LogSink();
        logSink.Log(new LogEntry(DateTime.UtcNow, LogSource.Application, LogLevel.Info, "Connected using Endpoint=sb://secret.servicebus.windows.net/"));

        var preferences = Substitute.For<IPreferencesService>();
        preferences.Theme.Returns("Light");
        preferences.MessagesPerPage.Returns(100);
        preferences.MaxTotalMessages.Returns(500);
        preferences.OpenTabsJson.Returns("[]");

        var alertService = Substitute.For<IAlertService>();
        alertService.Rules.Returns([]);
        alertService.ActiveAlerts.Returns([]);
        alertService.History.Returns([]);

        var dashboardPersistence = Substitute.For<IDashboardPersistenceService>();
        dashboardPersistence.Load().Returns(new DashboardConfiguration());
        dashboardPersistence.GetPresets().Returns([]);

        var connectionStorage = Substitute.For<IConnectionStorageService>();
        connectionStorage.GetConnectionsAsync().Returns(
        [
            new SavedConnection
            {
                Id = "conn-1",
                Name = "Prod",
                ConnectionString = "Endpoint=sb://secret.servicebus.windows.net/;SharedAccessKeyName=name;SharedAccessKey=super-secret",
                Type = ConnectionType.Namespace,
                CreatedAt = DateTimeOffset.UtcNow
            }
        ]);

        var versionService = Substitute.For<IVersionService>();
        versionService.DisplayVersion.Returns("0.11.0");

        var sut = new DiagnosticBundleService(
            logSink,
            preferences,
            alertService,
            dashboardPersistence,
            connectionStorage,
            versionService,
            _bundleDirectory);

        // Act
        var bundlePath = await sut.ExportAsync(includeMessageBodies: false, CancellationToken.None);

        // Assert
        File.Exists(bundlePath).Should().BeTrue();

        using var archive = ZipFile.OpenRead(bundlePath);
        var manifestEntry = archive.GetEntry("manifest.json");
        manifestEntry.Should().NotBeNull();

        using var reader = new StreamReader(manifestEntry!.Open());
        var manifestJson = await reader.ReadToEndAsync();

        manifestJson.Should().Contain("Prod");
        manifestJson.Should().NotContain("super-secret");
        manifestJson.Should().NotContain("SharedAccessKey=");

        var logsEntry = archive.GetEntry("logs.json");
        logsEntry.Should().NotBeNull();
        using var logsReader = new StreamReader(logsEntry!.Open());
        var logsJson = await logsReader.ReadToEndAsync();
        logsJson.Should().NotContain("secret.servicebus.windows.net");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_bundleDirectory))
            {
                Directory.Delete(_bundleDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}
