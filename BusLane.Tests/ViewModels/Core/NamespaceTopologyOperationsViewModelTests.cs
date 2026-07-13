namespace BusLane.Tests.ViewModels.Core;

using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using BusLane.ViewModels.Core;
using FluentAssertions;
using NSubstitute;

public class NamespaceTopologyOperationsViewModelTests
{
    [Fact]
    public async Task ExportAsync_WithActiveConnection_WritesTopologyDocument()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"topology-{Guid.NewGuid():N}.json");
        var operations = Substitute.For<IServiceBusOperations>();
        var fileDialog = Substitute.For<IFileDialogService>();
        var topologyService = Substitute.For<INamespaceTopologyService>();
        var status = "";
        fileDialog.SaveFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(path);
        topologyService.ExportAsync(operations, Arg.Any<CancellationToken>())
            .Returns(new NamespaceTopologyDocument(1, DateTimeOffset.UtcNow, [], []));
        var sut = new NamespaceTopologyOperationsViewModel(
            () => operations,
            () => fileDialog,
            topologyService,
            new ConfirmationDialogViewModel(),
            message => status = message,
            _ => { },
            () => Task.CompletedTask);

        try
        {
            // Act
            await sut.ExportAsync();

            // Assert
            NamespaceTopologySerializer.Deserialize(await File.ReadAllTextAsync(path)).SchemaVersion.Should().Be(1);
            status.Should().StartWith("Exported namespace topology");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_WithChanges_RequiresConfirmationBeforeApplying()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"topology-{Guid.NewGuid():N}.json");
        var operations = Substitute.For<IServiceBusOperations>();
        var fileDialog = Substitute.For<IFileDialogService>();
        var topologyService = Substitute.For<INamespaceTopologyService>();
        var confirmation = new ConfirmationDialogViewModel();
        var document = new NamespaceTopologyDocument(1, DateTimeOffset.UtcNow, [], []);
        await File.WriteAllTextAsync(path, NamespaceTopologySerializer.Serialize(document));
        fileDialog.OpenFileAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>>())
            .Returns(path);
        topologyService.BuildImportPlanAsync(operations, Arg.Any<NamespaceTopologyDocument>(), Arg.Any<CancellationToken>())
            .Returns(new TopologyImportPlan([new TopologyImportAction(TopologyImportActionType.CreateQueue, "orders", "Create queue orders")]));
        var sut = new NamespaceTopologyOperationsViewModel(
            () => operations,
            () => fileDialog,
            topologyService,
            confirmation,
            _ => { },
            _ => { },
            () => Task.CompletedTask);

        try
        {
            // Act
            await sut.ImportAsync();

            // Assert
            confirmation.ShowConfirmDialog.Should().BeTrue();
            await topologyService.DidNotReceive().ApplyImportPlanAsync(
                Arg.Any<IServiceBusOperations>(),
                Arg.Any<NamespaceTopologyDocument>(),
                Arg.Any<TopologyImportPlan>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
