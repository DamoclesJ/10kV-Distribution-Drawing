using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CablePropertyEditingTests
{
    [Fact]
    public void Projector_ExposesCablePropertiesAndKeepsTopologyFieldsReadOnly()
    {
        CableSegment cable = CreateCable();
        PropertyInspectorSnapshot snapshot = new PropertyProjector().Project(
            new ResolvedSelection
            {
                Reference = new SelectionReference(
                    SelectionTargetKind.CableSegment,
                    cable.Id),
                CableSegment = cable
            });

        IReadOnlyList<PropertyRowViewModel> rows = snapshot.Sections
            .SelectMany(section => section.Properties)
            .ToArray();

        Assert.False(rows.Single(row => row.PropertyKey == EditPropertyCommand.CableTypeProperty).IsReadOnly);
        Assert.False(rows.Single(row => row.PropertyKey == EditPropertyCommand.CableLengthProperty).IsReadOnly);
        Assert.True(rows.Single(row => row.DisplayName == "起点端子").IsReadOnly);
        Assert.True(rows.Single(row => row.DisplayName == "终点端子").IsReadOnly);
        Assert.True(rows.Single(row => row.DisplayName == "连接标识").IsReadOnly);
    }

    [Fact]
    public void CableProperties_ExecuteUndoRedo_PreservesStableAndTopologyIds()
    {
        CableSegment cable = CreateCable();
        Guid cableId = cable.Id;
        Guid connectionId = cable.ConnectionId;
        Guid startTerminalId = cable.StartTerminalId;
        Guid endTerminalId = cable.EndTerminalId;
        ResolvedSelection selection = Resolve(cable);
        var factory = new PropertyCommandFactory();
        var stack = new CommandStack();

        Assert.True(factory.TryCreate(
            selection,
            EditPropertyCommand.CableTypeProperty,
            "YJV22-8.7/15kV 3x240",
            out ICommand? typeCommand,
            out PropertyEditError? typeError));
        Assert.Null(typeError);
        stack.ExecuteCommand(typeCommand!);
        Assert.Equal("YJV22-8.7/15kV 3x240", cable.CableType);

        Assert.True(factory.TryCreate(
            selection,
            EditPropertyCommand.CableLengthProperty,
            "120",
            out ICommand? lengthCommand,
            out PropertyEditError? lengthError));
        Assert.Null(lengthError);
        stack.ExecuteCommand(lengthCommand!);
        Assert.Equal(120d, cable.Length);

        Assert.Equal(cableId, cable.Id);
        Assert.Equal(connectionId, cable.ConnectionId);
        Assert.Equal(startTerminalId, cable.StartTerminalId);
        Assert.Equal(endTerminalId, cable.EndTerminalId);

        Assert.True(stack.Undo());
        Assert.Equal(10d, cable.Length);
        Assert.True(stack.Undo());
        Assert.Equal("XLPE", cable.CableType);
        Assert.True(stack.Redo());
        Assert.Equal("YJV22-8.7/15kV 3x240", cable.CableType);
        Assert.True(stack.Redo());
        Assert.Equal(120d, cable.Length);
        Assert.Equal(cableId, cable.Id);
        Assert.Equal(connectionId, cable.ConnectionId);
        Assert.Equal(startTerminalId, cable.StartTerminalId);
        Assert.Equal(endTerminalId, cable.EndTerminalId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-length")]
    public void InvalidCableLength_IsRejectedWithoutHistory(string input)
    {
        CableSegment cable = CreateCable();
        ResolvedSelection selection = Resolve(cable);
        var factory = new PropertyCommandFactory();
        var stack = new CommandStack();

        Assert.False(factory.TryCreate(
            selection,
            EditPropertyCommand.CableLengthProperty,
            input,
            out ICommand? command,
            out PropertyEditError? error));

        Assert.Null(command);
        Assert.NotNull(error);
        Assert.Equal(10d, cable.Length);
        Assert.Empty(stack.History);
    }

    private static ResolvedSelection Resolve(CableSegment cable) =>
        new()
        {
            Reference = new SelectionReference(
                SelectionTargetKind.CableSegment,
                cable.Id),
            CableSegment = cable
        };

    private static CableSegment CreateCable() =>
        new(
            Guid.NewGuid(),
            "Cable",
            "XLPE",
            10,
            "10kV",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
}
