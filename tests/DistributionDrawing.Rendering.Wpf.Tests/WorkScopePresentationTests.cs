using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class WorkScopePresentationTests
{
    [Fact]
    public void NodeOnlyZeroBoundarySnapshotRemainsSelectableWithoutAnchors()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "scope");
        RingCabinet cabinet = Cabinet(); document.AddDevice(cabinet);
        WorkScope first = document.CreateWorkScope(Guid.NewGuid(), [new([], [cabinet.MainBusNodeId])], []);
        WorkScope second = document.CreateWorkScope(Guid.NewGuid(), [new([], [cabinet.MainBusNodeId])], []);
        ProfessionalSceneResult scene = new ProfessionalSceneBuilder(new SymbolLibrary()).Build(document,
            new DrawingLayout(), new Dictionary<Guid, RingCabinetLayout>(), new Dictionary<Guid, GroundingPointLayout>(), []);
        Assert.Contains(scene.HitTestEntries, entry => entry.Target == new SelectionReference(SelectionTargetKind.WorkScope, first.WorkScopeId));
        Assert.Contains(scene.HitTestEntries, entry => entry.Target == new SelectionReference(SelectionTargetKind.WorkScope, second.WorkScopeId));
        Assert.NotEmpty(scene.Elements);
        Assert.Empty(first.Boundaries);
    }

    [Fact]
    public void InspectorShowsMembershipCountsAndEditsOnlyOptionalDescriptionWithUndo()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "scope");
        RingCabinet cabinet = Cabinet(); document.AddDevice(cabinet);
        WorkScope scope = document.CreateWorkScope(Guid.NewGuid(),
            [new([cabinet.Intervals[0].CableTerminalId!.Value], [cabinet.MainBusNodeId]),
             new([cabinet.Intervals[1].CableTerminalId!.Value], [])], [new(cabinet.Id, BoundarySide.Bus)], "before");
        var selection = new ResolvedSelection
        {
            Reference = new(SelectionTargetKind.WorkScope, scope.WorkScopeId), Document = document, WorkScope = scope
        };
        PropertyRowViewModel[] rows = new PropertyProjector().Project(selection).Sections.SelectMany(section => section.Properties).ToArray();
        Assert.Equal("2", rows.Single(row => row.PropertyKey == "RegionCount").DisplayValue);
        Assert.Equal("2", rows.Single(row => row.PropertyKey == "TerminalCount").DisplayValue);
        Assert.Equal("1", rows.Single(row => row.PropertyKey == "NodeCount").DisplayValue);
        Assert.Equal("1", rows.Single(row => row.PropertyKey == "BoundaryCount").DisplayValue);
        Assert.DoesNotContain(rows, row => row.PropertyKey.Contains("Grounding"));
        Assert.All(rows.Where(row => row.PropertyKey != PropertyCommandFactory.WorkScopeDescriptionPropertyKey), row => Assert.True(row.IsReadOnly));
        var factory = new PropertyCommandFactory();
        Assert.False(factory.TryCreate(selection, "TerminalCount", "99", out _, out PropertyEditError? membershipError));
        Assert.Equal("PropertyReadOnly", membershipError!.Code);
        Assert.True(factory.TryCreateWorkScope(selection, null, out ICommand? command, out _));
        var stack = new CommandStack(); stack.ExecuteCommand(command!);
        Assert.Null(scope.Description);
        Assert.Equal(2, scope.Regions.Count); Assert.Single(scope.Boundaries);
        Assert.True(stack.Undo()); Assert.Equal("before", scope.Description);
        Assert.True(stack.Redo()); Assert.Null(scope.Description);
    }

    [Fact]
    public void ProfessionalFactoryAcceptsNewSnapshotContractWithoutRequiredDescription()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "factory");
        RingCabinet cabinet = Cabinet(); document.AddDevice(cabinet);
        var factory = new ProfessionalCommandFactory();
        var stack = new CommandStack();
        Guid id = Guid.NewGuid();
        stack.ExecuteCommand(factory.CreateAddWorkScope(document,
            [new([], [cabinet.MainBusNodeId])], [], workScopeId: id));
        Assert.Null(document.GetWorkScope(id).Description);
        stack.ExecuteCommand(factory.CreateChangeWorkScope(document, id,
            [new([cabinet.Intervals[0].CableTerminalId!.Value], [])], [new(cabinet.Id, BoundarySide.Line)]));
        Assert.Single(document.GetWorkScope(id).Boundaries);
        Assert.True(stack.Undo()); Assert.Empty(document.GetWorkScope(id).Boundaries);
        stack.ExecuteCommand(factory.CreateRemoveWorkScope(document, id));
        Assert.Empty(document.WorkScopes);
        Assert.True(stack.Undo()); Assert.Null(document.GetWorkScope(id).Description);
    }

    private static RingCabinet Cabinet() => RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "cabinet",
        [RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
         RingCabinetIntervalDefinition.CreateLoadSwitch(2, SwitchState.Open, SwitchState.Open)]));
}
