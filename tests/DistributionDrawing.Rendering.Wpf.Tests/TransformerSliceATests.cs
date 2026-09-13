using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class TransformerSliceATests
{
    [Theory]
    [InlineData(
        TransformerKind.PublicPoleMounted,
        TransformerOrientation.Vertical,
        ConnectionType.OverheadLine)]
    [InlineData(
        TransformerKind.DedicatedPoleMounted,
        TransformerOrientation.Vertical,
        ConnectionType.OverheadLine)]
    [InlineData(
        TransformerKind.PublicIndoor,
        TransformerOrientation.Horizontal,
        ConnectionType.Cable)]
    public void CreationFactory_CreatesFrozenAggregateAndDefaultLayout(
        TransformerKind transformerKind,
        TransformerOrientation expectedOrientation,
        ConnectionType expectedConnectionType)
    {
        TransformerCreation creation = new TransformerCreationFactory().Create(
            transformerKind,
            new DocumentPoint(12, 34),
            "测试变压器");

        Assert.Equal(transformerKind, creation.Transformer.TransformerKind);
        Assert.Equal("测试变压器", creation.Transformer.DisplayName);
        Assert.Equal(creation.Transformer.Id, creation.Layout.TransformerId);
        Assert.Equal(creation.Transformer.HvTerminalId, creation.HvTerminal.Id);
        Assert.Equal(expectedOrientation, creation.Layout.Orientation);
        Assert.Equal(
            expectedConnectionType,
            Assert.Single(creation.HvTerminal.AllowedConnectionTypes));
        Assert.Null(creation.HvTerminal.ElectricalNodeId);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted)]
    [InlineData(TransformerKind.DedicatedPoleMounted)]
    public void PoleMountedLayout_RejectsHorizontalOrientation(
        TransformerKind transformerKind)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new TransformerLayout(
                Guid.NewGuid(),
                new DocumentPoint(0, 0),
                TransformerOrientation.Horizontal,
                transformerKind));
    }

    [Theory]
    [InlineData(TransformerOrientation.Horizontal)]
    [InlineData(TransformerOrientation.Vertical)]
    public void PublicIndoorLayout_AcceptsBothOrientations(
        TransformerOrientation orientation)
    {
        TransformerLayout layout = new(
            Guid.NewGuid(),
            new DocumentPoint(0, 0),
            orientation,
            TransformerKind.PublicIndoor);

        Assert.Equal(orientation, layout.Orientation);
    }

    [Fact]
    public void TransformerLayout_DoesNotStoreTransformerKind()
    {
        Assert.DoesNotContain(
            typeof(TransformerLayout).GetProperties(),
            property => property.PropertyType == typeof(TransformerKind));
    }

    [Fact]
    public void AddTransformerCommand_ExecuteUndoRedoPreservesEveryStableFact()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(10, 20),
            "测试变压器",
            TransformerOrientation.Vertical);
        var command = new AddTransformerCommand(document, runtime, creation);

        command.Execute();
        AssertPresent(document, runtime, creation);
        command.Undo();
        AssertAbsent(document, runtime, creation);
        command.Redo();
        AssertPresent(document, runtime, creation);

        Assert.Same(creation.Transformer, Assert.Single(document.Transformers));
        Assert.Same(creation.HvTerminal, Assert.Single(document.Terminals));
        Assert.Same(
            creation.Layout,
            runtime.TransformerLayouts[creation.Transformer.Id]);
        Assert.Equal(TransformerOrientation.Vertical, creation.Layout.Orientation);
    }

    [Fact]
    public void AddTransformerCommand_FailureLeavesNoPartialDomainState()
    {
        DrawingDocument document = CreateDocument();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(0, 0),
            "测试变压器");
        RuntimeLayoutDocument runtime = CreateRuntime(
            new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            });
        var command = new AddTransformerCommand(document, runtime, creation);

        Assert.Throws<InvalidOperationException>(command.Execute);

        Assert.Empty(document.Transformers);
        Assert.Empty(document.Terminals);
        Assert.Empty(document.ElectricalNodes);
        Assert.Same(
            creation.Layout,
            runtime.TransformerLayouts[creation.Transformer.Id]);
    }

    [Fact]
    public void RemoveTransformerCommand_ExecuteUndoRedoPreservesExactLayoutAndIds()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.DedicatedPoleMounted,
            new DocumentPoint(45, 67),
            "测试变压器");
        new AddTransformerCommand(document, runtime, creation).Execute();
        var command = new RemoveTransformerCommand(
            document,
            runtime,
            creation.Transformer.Id);

        command.Execute();
        AssertAbsent(document, runtime, creation);
        command.Undo();
        AssertPresent(document, runtime, creation);
        Assert.Same(creation.Transformer, Assert.Single(document.Transformers));
        Assert.Same(creation.HvTerminal, Assert.Single(document.Terminals));
        Assert.Same(
            creation.Layout,
            runtime.TransformerLayouts[creation.Transformer.Id]);
        command.Redo();
        AssertAbsent(document, runtime, creation);
    }

    [Fact]
    public void RemoveTransformerCommand_DependencyFailureLeavesDomainAndLayoutUnchanged()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(1, 2),
            "测试变压器");
        new AddTransformerCommand(document, runtime, creation).Execute();
        Terminal other = AddOtherCableTerminal(document);
        Connection connection = new(
            Guid.NewGuid(),
            ConnectionType.Cable,
            creation.HvTerminal.Id,
            other.Id,
            "电缆",
            Transformer.TenKilovolts);
        document.AddConnection(connection);
        var command = new RemoveTransformerCommand(
            document,
            runtime,
            creation.Transformer.Id);

        Assert.Throws<InvalidOperationException>(command.Execute);

        AssertPresent(document, runtime, creation);
        Assert.Contains(connection, document.Connections);
    }

    [Fact]
    public void TransformerHvTerminal_IsEligibleForNewGroundingPoint()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(0, 0),
            "测试变压器");
        new AddTransformerCommand(document, runtime, creation).Execute();

        Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            document,
            creation.HvTerminal.Id));
    }

    [Fact]
    public void OverheadLineFactory_AllowsDropoutFuseToTransformerWithoutFakePoleIdentity()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        var pole = new Pole(Guid.NewGuid(), "P-01");
        document.AddDevice(pole);
        Guid fuseFirstId = Guid.NewGuid();
        Guid fuseSecondId = Guid.NewGuid();
        SwitchDevice fuse = SwitchDevice.CreateForPole(
            Guid.NewGuid(),
            SwitchKind.DropoutFuse,
            fuseFirstId,
            fuseSecondId);
        document.AddPoleSwitchAttachment(
            fuse,
            CreateSwitchTerminal(fuse, fuseFirstId, true),
            CreateSwitchTerminal(fuse, fuseSecondId, false),
            new PoleAttachment(Guid.NewGuid(), pole.Id, fuse.Id));
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(100, 0),
            "测试变压器");
        new AddTransformerCommand(document, runtime, transformer).Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            document,
            runtime,
            fuseSecondId,
            transformer.HvTerminal.Id,
            new DocumentPoint(0, 0),
            new DocumentPoint(100, 0));

        line.Execute();

        Assert.Equal(pole.Id, Assert.Single(line.OverheadLine.SupportPoleIds));
        Assert.DoesNotContain(
            transformer.Transformer.Id,
            line.OverheadLine.SupportPoleIds);
        Assert.DoesNotContain(document.PoleAttachments, attachment =>
            attachment.AttachedDeviceId == transformer.Transformer.Id);
    }

    [Fact]
    public void OverheadLineFactory_RejectsTransformerToTransformerWithoutRealSupportPole()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        TransformerCreation start = new TransformerCreationFactory().Create(
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(0, 0),
            "起点变压器");
        TransformerCreation end = new TransformerCreationFactory().Create(
            TransformerKind.DedicatedPoleMounted,
            new DocumentPoint(100, 0),
            "终点变压器");
        new AddTransformerCommand(document, runtime, start).Execute();
        new AddTransformerCommand(document, runtime, end).Execute();

        Assert.Throws<InvalidOperationException>(() =>
            new OverheadLineCommandFactory().CreateAdd(
                document,
                runtime,
                start.HvTerminal.Id,
                end.HvTerminal.Id,
                start.Layout.Position,
                end.Layout.Position));

        Assert.Empty(document.Connections);
        Assert.Empty(document.OverheadLines);
    }

    [Fact]
    public void OverheadLineFactory_RejectsUnsupportedDeviceOwnerWithoutPhysicalPole()
    {
        DrawingDocument document = CreateDocument();
        RuntimeLayoutDocument runtime = CreateRuntime();
        var pole = new Pole(Guid.NewGuid(), "P-Unsupported");
        Terminal poleTerminal = pole.CreateOverheadAnchorTerminal(
            Guid.NewGuid(),
            allowsMultipleConnections: true);
        var unsupported = new Device(Guid.NewGuid(), DeviceType.PT);
        var unsupportedTerminal = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.Device,
            unsupported.Id,
            "Unsupported",
            Transformer.TenKilovolts,
            isExternal: true,
            allowsMultipleConnections: false,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
        document.AddDevice(pole);
        document.AddTerminal(poleTerminal);
        runtime.DrawingLayout.Add(new PoleLayout(pole.Id, new DocumentPoint(0, 0)));
        document.AddDevice(unsupported);
        document.AddTerminal(unsupportedTerminal);

        Assert.Throws<InvalidOperationException>(() =>
            new OverheadLineCommandFactory().CreateAdd(
                document,
                runtime,
                poleTerminal.Id,
                unsupportedTerminal.Id,
                new DocumentPoint(0, 0),
                new DocumentPoint(100, 0)));

        Assert.Empty(document.Connections);
        Assert.Empty(document.OverheadLines);
    }

    private static DrawingDocument CreateDocument() => new(Guid.NewGuid(), "Test");

    private static RuntimeLayoutDocument CreateRuntime(
        IReadOnlyDictionary<Guid, TransformerLayout>? transformerLayouts = null)
    {
        return new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>(),
            transformerLayouts: transformerLayouts);
    }

    private static Terminal AddOtherCableTerminal(DrawingDocument document)
    {
        var device = new Device(Guid.NewGuid(), DeviceType.PT);
        var terminal = new Terminal(
            Guid.NewGuid(),
            TopologyOwnerType.Device,
            device.Id,
            "Other",
            Transformer.TenKilovolts,
            true,
            true,
            allowedConnectionTypes: [ConnectionType.Cable]);
        document.AddDevice(device);
        document.AddTerminal(terminal);
        return terminal;
    }

    private static Terminal CreateSwitchTerminal(
        SwitchDevice switchDevice,
        Guid terminalId,
        bool allowsMultipleConnections)
    {
        return new Terminal(
            terminalId,
            TopologyOwnerType.Device,
            switchDevice.Id,
            "SwitchTerminal",
            Transformer.TenKilovolts,
            true,
            allowsMultipleConnections,
            allowedConnectionTypes: [ConnectionType.OverheadLine]);
    }

    private static void AssertPresent(
        DrawingDocument document,
        RuntimeLayoutDocument runtime,
        TransformerCreation creation)
    {
        Assert.Contains(creation.Transformer, document.Transformers);
        Assert.Contains(creation.HvTerminal, document.Terminals);
        Assert.Same(
            creation.Layout,
            runtime.TransformerLayouts[creation.Transformer.Id]);
    }

    private static void AssertAbsent(
        DrawingDocument document,
        RuntimeLayoutDocument runtime,
        TransformerCreation creation)
    {
        Assert.DoesNotContain(creation.Transformer, document.Transformers);
        Assert.DoesNotContain(creation.HvTerminal, document.Terminals);
        Assert.False(runtime.TransformerLayouts.ContainsKey(creation.Transformer.Id));
    }
}
