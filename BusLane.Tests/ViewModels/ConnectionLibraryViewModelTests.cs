namespace BusLane.Tests.ViewModels;

using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using BusLane.ViewModels;
using FluentAssertions;
using NSubstitute;

public class ConnectionLibraryViewModelTests
{
    [Fact]
    public async Task ImportConnectionsBackupAsync_RefreshesExternalSavedConnections()
    {
        // Arrange
        var storedConnections = new List<SavedConnection>();
        var connectionStorage = Substitute.For<IConnectionStorageService>();
        connectionStorage.GetConnectionsAsync()
            .Returns(_ => Task.FromResult<IEnumerable<SavedConnection>>(storedConnections.ToList()));
        connectionStorage.SaveConnectionAsync(Arg.Any<SavedConnection>())
            .Returns(callInfo =>
            {
                var connection = callInfo.Arg<SavedConnection>();
                storedConnections.RemoveAll(existing => existing.Id == connection.Id);
                storedConnections.Add(connection);
                return Task.CompletedTask;
            });

        var connectionBackupService = Substitute.For<IConnectionBackupService>();
        connectionBackupService.ImportAsync("/tmp/import.blbackup", "secret")
            .Returns(Task.FromResult<IReadOnlyList<SavedConnection>>(
            [
                new SavedConnection
                {
                    Id = "imported-1",
                    Name = "Imported Connection",
                    ConnectionString = "Endpoint=sb://imported.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abc123",
                    Type = ConnectionType.Namespace,
                    CreatedAt = DateTimeOffset.UtcNow
                }
            ]));

        var fileDialogService = Substitute.For<IFileDialogService>();
        fileDialogService.OpenFileAsync(
                "Import Connection Backup",
                Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns("/tmp/import.blbackup");

        var logSink = Substitute.For<ILogSink>();
        logSink.GetLogs().Returns([]);

        var refreshCalls = 0;
        var sut = new ConnectionLibraryViewModel(
            connectionStorage,
            connectionBackupService,
            Substitute.For<IServiceBusOperationsFactory>(),
            fileDialogService,
            logSink,
            _ => { },
            _ => { },
            onConnectionsChanged: () =>
            {
                refreshCalls++;
                return Task.CompletedTask;
            });

        sut.BackupPassphrase = "secret";

        // Act
        await sut.ImportConnectionsBackupCommand.ExecuteAsync(null);

        // Assert
        refreshCalls.Should().Be(1);
        storedConnections.Should().ContainSingle(connection => connection.Id == "imported-1");
    }
}
