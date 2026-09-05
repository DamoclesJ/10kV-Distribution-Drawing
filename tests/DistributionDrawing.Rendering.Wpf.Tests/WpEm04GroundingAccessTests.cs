using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class WpEm04GroundingAccessTests
{
    [Fact]
    public void RequiredSupportWaypoints_PassIntermediatePoleAndAvoidUnrelatedObstacle()
    {
        SceneFixture fixture = CreateFixture();
        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Runtime);
        OrthogonalRoute route = Assert.Single(scene.Routes);
        DocumentPoint middle = Center(fixture.Runtime.DrawingLayout.Poles[fixture.Middle.Id]);
        DocumentRect poleBounds = PoleProfessionalGeometry.GetPoleBounds(
            fixture.Runtime.DrawingLayout.Poles[fixture.Unrelated.Id]);
        double clearance = DrawingMetrics.Default.Routing.ObstacleClearance;
        var obstacle = new DocumentRect(
            poleBounds.XMillimeters - clearance,
            poleBounds.YMillimeters - clearance,
            poleBounds.WidthMillimeters + clearance * 2,
            poleBounds.HeightMillimeters + clearance * 2);

        Assert.Contains(route.Segments, segment => Contains(segment, middle));
        Assert.DoesNotContain(route.Segments, segment => IntersectsInterior(segment, obstacle));
    }

    [Fact]
    public void IntermediatePole_ResolvesTwoFinalRouteHalfEdges()
    {
        SceneFixture fixture = CreateFixture();
        OrthogonalRoute route = Assert.Single(
            fixture.Builder.Build(fixture.Document, fixture.Runtime).Routes);
        OverheadLine line = Assert.Single(fixture.Document.OverheadLines);

        Assert.True(SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
            route, line, fixture.Runtime.DrawingLayout, fixture.Middle.Id, fixture.Start.Id,
            out GroundingAccessHalfEdge incoming));
        Assert.True(SupportPoleAwareRouteBuilder.TryResolveHalfEdge(
            route, line, fixture.Runtime.DrawingLayout, fixture.Middle.Id, fixture.End.Id,
            out GroundingAccessHalfEdge outgoing));
        Assert.NotEqual(incoming.DirectionPoint, outgoing.DirectionPoint);
    }

    [Fact]
    public void Marker_UsesAdjacentHalfEdge_NotLineSideOrPoleNumber()
    {
        SceneFixture fixture = CreateFixture();
        GroundingAccessPoint first = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        DocumentPoint before = MarkerCenter(fixture.Builder.Build(fixture.Document, fixture.Runtime), first);
        fixture.Middle.RenamePoleNumber("P-900");
        DocumentPoint renamed = MarkerCenter(fixture.Builder.Build(fixture.Document, fixture.Runtime), first);
        fixture.Document.RemoveGroundingAccessPoint(first.GroundingAccessPointId);
        GroundingAccessPoint overrideSide = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        DocumentPoint overridden = MarkerCenter(
            fixture.Builder.Build(fixture.Document, fixture.Runtime), overrideSide);
        fixture.Document.RemoveGroundingAccessPoint(overrideSide.GroundingAccessPointId);
        GroundingAccessPoint oppositeHalfEdge = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.Start.Id,
            GroundingAccessLineSide.LargerNumberSide);
        DocumentPoint opposite = MarkerCenter(
            fixture.Builder.Build(fixture.Document, fixture.Runtime), oppositeHalfEdge);

        Assert.Equal(before, renamed);
        Assert.Equal(before, overridden);
        Assert.NotEqual(before, opposite);
    }

    [Fact]
    public void Marker_HasTypedSizeHigherHitPriorityAndResolvesSelection()
    {
        SceneFixture fixture = CreateFixture();
        GroundingAccessPoint point = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        DrawingScene scene = fixture.Builder.Build(fixture.Document, fixture.Runtime);
        SelectionReference gapReference = new(
            SelectionTargetKind.GroundingAccessPoint,
            point.GroundingAccessPointId);
        SelectionHitTestEntry gapHit = Assert.Single(
            scene.HitTestIndex.FindAll(gapReference));
        SceneEllipse marker = FindGapMarker(scene, point.GroundingAccessPointId);
        int linePriority = scene.HitTestIndex.FindAll(
            new SelectionReference(SelectionTargetKind.Connection, fixture.Connection.Id)).Max(item => item.Priority);
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Document = fixture.Document,
            GroundingAccessPoints = fixture.Document.GroundingAccessPoints
        });

        Assert.Equal(DrawingMetrics.Default.Line.GroundingAccessMarkerDiameter, marker.Bounds.WidthMillimeters);
        Assert.Equal(Center(gapHit.Bounds), Center(marker.Bounds));
        Assert.DoesNotContain(scene.Elements.OfType<SceneEllipse>(), ellipse =>
            ellipse.Bounds == marker.Bounds && ellipse != marker);
        Assert.True(gapHit.Priority > linePriority);
        Assert.Same(point, resolver.Resolve(gapReference)!.GroundingAccessPoint);
    }

    [Fact]
    public void GapCommands_ReuseIdsAcrossUndoRedoAndExistingGapSurvivesGroundingUndo()
    {
        SceneFixture fixture = CreateFixture();
        var factory = new ProfessionalCommandFactory();
        AddGroundingAccessPointCommand addGap = factory.CreateAddGroundingAccessPoint(
            fixture.Document, fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        var stack = new CommandStack();
        stack.ExecuteCommand(addGap);
        Guid gapId = addGap.After.GroundingAccessPointId;
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());
        Assert.Equal(gapId, Assert.Single(fixture.Document.GroundingAccessPoints).GroundingAccessPointId);

        AddGroundingPointCommand addGround = (AddGroundingPointCommand)factory.CreateAddGroundingPoint(
            fixture.Document, GroundingTarget.ForGroundingAccessPoint(gapId), "大号侧");
        stack.ExecuteCommand(addGround);
        Guid groundingId = addGround.After.GroundingPointId;
        Assert.True(stack.Undo());
        Assert.Single(fixture.Document.GroundingAccessPoints);
        Assert.True(stack.Redo());
        Assert.Equal(groundingId, Assert.Single(fixture.Document.GroundingPoints).GroundingPointId);
    }

    [Fact]
    public void CompositeAndDeleteCommands_PreserveStableIdentity()
    {
        SceneFixture fixture = CreateFixture();
        var factory = new ProfessionalCommandFactory();
        CompositeProfessionalCommand composite = factory.CreateAddGroundingAccessPointWithGroundingPoint(
            fixture.Document, fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide, "大号侧");
        var stack = new CommandStack();
        stack.ExecuteCommand(composite);
        Guid gapId = Assert.Single(fixture.Document.GroundingAccessPoints).GroundingAccessPointId;
        GroundingPoint grounding = Assert.Single(fixture.Document.GroundingPoints);
        (Guid groundingId, string number) = (grounding.GroundingPointId, grounding.Number!);
        Assert.True(stack.Undo());
        Assert.True(stack.Redo());
        Assert.Equal(gapId, Assert.Single(fixture.Document.GroundingAccessPoints).GroundingAccessPointId);
        grounding = Assert.Single(fixture.Document.GroundingPoints);
        Assert.Equal((groundingId, number), (grounding.GroundingPointId, grounding.Number));

        stack.ExecuteCommand(factory.CreateRemoveGroundingPoint(fixture.Document, groundingId));
        Assert.True(stack.Undo());
        Assert.Equal(groundingId, Assert.Single(fixture.Document.GroundingPoints).GroundingPointId);
    }

    [Fact]
    public void DeleteGap_UndoRestoresSameIdentity()
    {
        SceneFixture fixture = CreateFixture();
        GroundingAccessPoint gap = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        var stack = new CommandStack();

        stack.ExecuteCommand(new ProfessionalCommandFactory().CreateRemoveGroundingAccessPoint(
            fixture.Document, gap.GroundingAccessPointId));
        Assert.Empty(fixture.Document.GroundingAccessPoints);
        Assert.True(stack.Undo());
        Assert.Equal(gap.GroundingAccessPointId,
            Assert.Single(fixture.Document.GroundingAccessPoints).GroundingAccessPointId);
    }

    [Fact]
    public void NumberEdit_UndoRedoRestoresBothValues()
    {
        SceneFixture fixture = CreateFixture();
        GroundingPoint grounding = fixture.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(fixture.Connection.StartTerminalId),
            "兼容", "L01");
        GroundingPointCommandSnapshot before = GroundingPointCommandSnapshot.From(grounding);
        var stack = new CommandStack();
        stack.ExecuteCommand(new ChangeGroundingPointCommand(
            fixture.Document, before, before with { Number = "L02" }));
        Assert.Equal("L02", grounding.Number);
        Assert.True(stack.Undo());
        Assert.Equal("L01", grounding.Number);
        Assert.True(stack.Redo());
        Assert.Equal("L02", grounding.Number);
    }

    [Fact]
    public void NumberAllocator_UsesFirstFreeAndEditFailuresAreAtomic()
    {
        SceneFixture fixture = CreateFixture();
        AddNumberedTerminalGrounding(fixture.Document, fixture.Connection.StartTerminalId, "L01");
        AddNumberedTerminalGrounding(fixture.Document, fixture.Connection.EndTerminalId, "L03");
        Assert.Equal("L02", ProfessionalCommandFactory.AllocateGroundingPointNumber(fixture.Document));
        GroundingPoint second = fixture.Document.GroundingPoints[1];
        GroundingPointCommandSnapshot before = GroundingPointCommandSnapshot.From(second);
        var stack = new CommandStack();
        Assert.Throws<InvalidOperationException>(() => stack.ExecuteCommand(
            new ChangeGroundingPointCommand(fixture.Document, before, before with { Number = "L01" })));
        Assert.False(stack.CanUndo);
        Assert.Equal("L03", second.Number);
    }

    [Fact]
    public void NumberAllocator_ReusesGapsAndIgnoresNonStandardNumbers()
    {
        SceneFixture fixture = CreateFixture();
        AddNumberedTerminalGrounding(fixture.Document, fixture.Connection.StartTerminalId, "L01");
        AddNumberedTerminalGrounding(fixture.Document, fixture.Connection.EndTerminalId, "L1");
        Assert.Equal("L02", ProfessionalCommandFactory.AllocateGroundingPointNumber(fixture.Document));

        Terminal temporaryTerminal = fixture.Unrelated.CreateOverheadAnchorTerminal(Guid.NewGuid());
        fixture.Document.AddTerminal(temporaryTerminal);
        GroundingPoint temporary = fixture.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(temporaryTerminal.Id), "临时", "L02");
        Assert.Equal("L03", ProfessionalCommandFactory.AllocateGroundingPointNumber(fixture.Document));
        fixture.Document.RemoveGroundingPoint(temporary.GroundingPointId);
        Assert.Equal("L02", ProfessionalCommandFactory.AllocateGroundingPointNumber(fixture.Document));
    }

    [Fact]
    public void FreeLineCascadeUndoRestoresGap_AndOccupiedFailureIsAtomic()
    {
        SceneFixture fixture = CreateFixture();
        GroundingAccessPoint gap = fixture.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        RemoveOverheadLineCommand remove = new OverheadLineCommandFactory().CreateRemove(
            fixture.Document, fixture.Runtime, fixture.Connection.Id);
        var stack = new CommandStack();
        stack.ExecuteCommand(remove);
        Assert.Empty(fixture.Document.GroundingAccessPoints);
        Assert.True(stack.Undo());
        Assert.Equal(gap.GroundingAccessPointId, Assert.Single(fixture.Document.GroundingAccessPoints).GroundingAccessPointId);

        fixture.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(gap.GroundingAccessPointId),
            "大号侧", "L01");
        var failedStack = new CommandStack();
        Assert.Throws<InvalidOperationException>(() => failedStack.ExecuteCommand(
            new OverheadLineCommandFactory().CreateRemove(
                fixture.Document, fixture.Runtime, fixture.Connection.Id)));
        Assert.False(failedStack.CanUndo);
        Assert.Single(fixture.Document.OverheadLines);
        Assert.Single(fixture.Document.GroundingAccessPoints);
        Assert.Single(fixture.Document.GroundingPoints);
    }

    private static void AddNumberedTerminalGrounding(DrawingDocument document, Guid terminalId, string number) =>
        document.CreateGroundingPoint(Guid.NewGuid(), terminalId, "兼容", number);

    private static DocumentPoint MarkerCenter(DrawingScene scene, GroundingAccessPoint point)
    {
        SelectionHitTestEntry hit = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(SelectionTargetKind.GroundingAccessPoint, point.GroundingAccessPointId)));
        return new DocumentPoint(
            hit.Bounds.XMillimeters + hit.Bounds.WidthMillimeters / 2,
            hit.Bounds.YMillimeters + hit.Bounds.HeightMillimeters / 2);
    }

    private static SceneFixture CreateFixture()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "WP-EM-04 rendering");
        Pole start = AddPole(document, "P-10");
        Pole middle = AddPole(document, "P-11");
        Pole end = AddPole(document, "P-12");
        Pole unrelated = AddPole(document, "P-99");
        Terminal startTerminal = start.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        Terminal endTerminal = end.CreateOverheadAnchorTerminal(Guid.NewGuid(), true);
        document.AddTerminal(startTerminal);
        document.AddTerminal(endTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine, startTerminal.Id, endTerminal.Id,
            "测试架空线", "10kV");
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(
            connection.Id, "JKLYJ", [start.Id, middle.Id, end.Id]));
        var drawing = new DrawingLayout();
        drawing.Add(new PoleLayout(start.Id, new DocumentPoint(0, 0)));
        drawing.Add(new PoleLayout(middle.Id, new DocumentPoint(100, 80)));
        drawing.Add(new PoleLayout(end.Id, new DocumentPoint(220, 0)));
        drawing.Add(new PoleLayout(unrelated.Id, new DocumentPoint(150, 80)));
        drawing.Add(new OverheadLineLayout(
            connection.Id, Center(drawing.Poles[start.Id]), Center(drawing.Poles[end.Id])));
        return new SceneFixture(
            document, start, middle, end, unrelated, connection,
            new RuntimeLayoutDocument(drawing, new Dictionary<Guid, RingCabinetLayout>()),
            new DrawingSceneBuilder());
    }

    private static Pole AddPole(DrawingDocument document, string number)
    {
        var pole = new Pole(Guid.NewGuid(), number);
        document.AddDevice(pole);
        return pole;
    }

    private static DocumentPoint Center(PoleLayout layout) =>
        PoleProfessionalGeometry.GetPoleCenter(layout);

    private static SceneEllipse FindGapMarker(DrawingScene scene, Guid groundingAccessPointId)
    {
        SelectionHitTestEntry gapHit = Assert.Single(scene.HitTestIndex.FindAll(
            new SelectionReference(
                SelectionTargetKind.GroundingAccessPoint,
                groundingAccessPointId)));
        DocumentPoint center = Center(gapHit.Bounds);
        return Assert.Single(scene.Elements.OfType<SceneEllipse>(), ellipse =>
            Center(ellipse.Bounds) == center);
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);

    private static bool Contains(OrthogonalRouteSegment segment, DocumentPoint point) =>
        segment.IsHorizontal
            ? point.YMillimeters == segment.Start.YMillimeters &&
              point.XMillimeters >= Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) &&
              point.XMillimeters <= Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters)
            : point.XMillimeters == segment.Start.XMillimeters &&
              point.YMillimeters >= Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) &&
              point.YMillimeters <= Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters);

    private static bool IntersectsInterior(OrthogonalRouteSegment segment, DocumentRect bounds) =>
        segment.IsHorizontal
            ? segment.Start.YMillimeters > bounds.YMillimeters &&
              segment.Start.YMillimeters < bounds.YMillimeters + bounds.HeightMillimeters &&
              Math.Max(segment.Start.XMillimeters, segment.End.XMillimeters) > bounds.XMillimeters &&
              Math.Min(segment.Start.XMillimeters, segment.End.XMillimeters) < bounds.XMillimeters + bounds.WidthMillimeters
            : segment.Start.XMillimeters > bounds.XMillimeters &&
              segment.Start.XMillimeters < bounds.XMillimeters + bounds.WidthMillimeters &&
              Math.Max(segment.Start.YMillimeters, segment.End.YMillimeters) > bounds.YMillimeters &&
              Math.Min(segment.Start.YMillimeters, segment.End.YMillimeters) < bounds.YMillimeters + bounds.HeightMillimeters;

    private sealed record SceneFixture(
        DrawingDocument Document,
        Pole Start,
        Pole Middle,
        Pole End,
        Pole Unrelated,
        Connection Connection,
        RuntimeLayoutDocument Runtime,
        DrawingSceneBuilder Builder);
}
