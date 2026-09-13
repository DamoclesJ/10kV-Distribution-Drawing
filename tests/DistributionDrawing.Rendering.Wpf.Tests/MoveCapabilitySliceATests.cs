using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Connections;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;
using ApplicationCustomerStationCreationFactory =
    DistributionDrawing.Rendering.Wpf.Interaction.Devices.CustomerStationCreationFactory;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class MoveCapabilitySliceATests
{
    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.DedicatedPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Vertical)]
    public void TransformerDirectDrag_ChangesOnlyPositionAndSupportsUndoRedo(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        TransformerCreation creation = new TransformerCreationFactory().Create(
            kind,
            new DocumentPoint(10, 20),
            "变压器测试",
            orientation);
        DrawingDocument document = new(Guid.NewGuid(), "Transformer move");
        document.AddTransformer(creation.Transformer, creation.HvTerminal);
        RuntimeLayoutDocument layout = Runtime(
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            });
        SelectionReference target = new(SelectionTargetKind.Device, creation.Transformer.Id);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            target,
            creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(35, 48)));
        Assert.Equal(new DocumentPoint(35, 48), layout.TransformerLayouts[
            creation.Transformer.Id].Position);
        Assert.Equal("变压器测试", creation.Transformer.DisplayName);
        Assert.Equal(creation.Transformer.HvTerminalId, creation.HvTerminal.Id);

        MoveTransformerCommand command = Assert.IsType<MoveTransformerCommand>(
            controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.Equal(new DocumentPoint(10, 20), layout.TransformerLayouts[
            creation.Transformer.Id].Position);
        Assert.True(stack.Redo());
        Assert.Equal(new DocumentPoint(35, 48), layout.TransformerLayouts[
            creation.Transformer.Id].Position);
    }

    [Theory]
    [InlineData(TransformerKind.PublicPoleMounted, TransformerOrientation.Vertical)]
    [InlineData(TransformerKind.PublicIndoor, TransformerOrientation.Horizontal)]
    public void TransformerDirectDrag_CancelRestoresExactBeforeAndPreservesIdentity(
        TransformerKind kind,
        TransformerOrientation orientation)
    {
        TransformerCreation creation = new TransformerCreationFactory().Create(
            kind,
            new DocumentPoint(40, 50),
            "取消移动测试",
            orientation);
        DrawingDocument document = new(Guid.NewGuid(), "Transformer cancel");
        document.AddTransformer(creation.Transformer, creation.HvTerminal);
        RuntimeLayoutDocument layout = Runtime(
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            });
        Guid transformerId = creation.Transformer.Id;
        Guid hvTerminalId = creation.Transformer.HvTerminalId;
        string? displayName = creation.Transformer.DisplayName;
        TransformerOrientation beforeOrientation = creation.Layout.Orientation;
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(SelectionTargetKind.Device, transformerId),
            creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(75, 92)));
        controller.Cancel();

        TransformerLayout restored = layout.TransformerLayouts[transformerId];
        Assert.Equal(creation.Layout.Position, restored.Position);
        Assert.Equal(transformerId, creation.Transformer.Id);
        Assert.Equal(hvTerminalId, creation.Transformer.HvTerminalId);
        Assert.Equal(displayName, creation.Transformer.DisplayName);
        Assert.Equal(beforeOrientation, restored.Orientation);
    }

    [Fact]
    public void TransformerNoChange_DoesNotCreateCommand()
    {
        TransformerCreation creation = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(10, 20),
            "变压器测试");
        DrawingDocument document = new(Guid.NewGuid(), "Transformer no change");
        document.AddTransformer(creation.Transformer, creation.HvTerminal);
        RuntimeLayoutDocument layout = Runtime(
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [creation.Transformer.Id] = creation.Layout
            });
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(SelectionTargetKind.Device, creation.Transformer.Id),
            creation.Layout.Position,
            layout,
            document: document));
        Assert.False(controller.UpdatePreview(creation.Layout.Position));
        Assert.Null(controller.Commit());
    }

    [Fact]
    public void PoleMountedTransformerMove_RebuildsOhlAndGapAcrossCancelUndoRedo()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Transformer OHL move");
        RuntimeLayoutDocument layout = Runtime();
        var deviceFactory = new DeviceCommandFactory();
        AddPoleCommand pole = deviceFactory.CreateAddPole(
            document,
            layout,
            new DocumentPoint(30, 80));
        pole.Execute();
        AddTransformerCommand transformer = deviceFactory.CreateAddTransformer(
            document,
            layout,
            TransformerKind.PublicPoleMounted,
            new DocumentPoint(150, 80),
            "柱上公变");
        transformer.Execute();
        AddOverheadLineCommand line = new OverheadLineCommandFactory().CreateAdd(
            document,
            layout,
            pole.Terminal.Id,
            transformer.Creation.HvTerminal.Id,
            pole.Layout.Position,
            transformer.Creation.Layout.Position);
        line.Execute();
        GroundingAccessPoint gap = document.CreateGroundingAccessPoint(
            Guid.NewGuid(),
            line.Connection.Id,
            pole.Pole.Id,
            GroundingAdjacentEndpoint.ForTerminal(transformer.Creation.HvTerminal.Id),
            GroundingAccessLineSide.TransformerSide,
            GroundingAccessPlacementSide.AdjacentEndpointSide);
        var builder = new DrawingSceneBuilder();
        DrawingScene beforeScene = builder.Build(document, layout);
        TerminalAnchor beforeAnchor = Anchor(
            document,
            layout,
            transformer.Creation.HvTerminal.Id);
        DocumentPoint beforeGapAnchor = GapAnchor(gap, document, layout, beforeScene).Position;
        DocumentPoint[] beforeRoute = Route(beforeScene, line.Connection.Id);
        Guid[] supportPoleIds = line.OverheadLine.SupportPoleIds.ToArray();
        Guid transformerId = transformer.Creation.Transformer.Id;
        Guid hvTerminalId = transformer.Creation.Transformer.HvTerminalId;
        var target = new SelectionReference(SelectionTargetKind.Device, transformerId);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            target,
            transformer.Creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(185, 120)));
        DrawingScene previewScene = builder.Build(document, layout);
        TerminalAnchor movedAnchor = Anchor(document, layout, hvTerminalId);
        DocumentPoint[] movedRoute = Route(previewScene, line.Connection.Id);
        DocumentPoint movedGapAnchor = GapAnchor(gap, document, layout, previewScene).Position;
        Assert.NotEqual(beforeAnchor.Position, movedAnchor.Position);
        Assert.NotEqual(beforeRoute, movedRoute);
        Assert.NotEqual(beforeGapAnchor, movedGapAnchor);
        Assert.Equal(line.Connection.Id, Assert.Single(document.Connections).Id);
        Assert.Equal(supportPoleIds, line.OverheadLine.SupportPoleIds);
        Assert.Equal(transformerId, transformer.Creation.Transformer.Id);
        Assert.Equal(hvTerminalId, transformer.Creation.Transformer.HvTerminalId);
        AssertGapIdentity(gap, line.Connection.Id, pole.Pole.Id, hvTerminalId);

        controller.Cancel();
        DrawingScene cancelledScene = builder.Build(document, layout);
        Assert.Equal(beforeAnchor, Anchor(document, layout, hvTerminalId));
        Assert.Equal(beforeRoute, Route(cancelledScene, line.Connection.Id));
        Assert.Equal(beforeGapAnchor, GapAnchor(gap, document, layout, cancelledScene).Position);

        Assert.True(controller.TryBeginDrag(
            target,
            transformer.Creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(185, 120)));
        MoveTransformerCommand command = Assert.IsType<MoveTransformerCommand>(
            controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.Equal(movedRoute, Route(builder.Build(document, layout), line.Connection.Id));
        Assert.True(stack.Undo());
        Assert.Equal(beforeRoute, Route(builder.Build(document, layout), line.Connection.Id));
        Assert.Equal(beforeGapAnchor,
            GapAnchor(gap, document, layout, builder.Build(document, layout)).Position);
        Assert.True(stack.Redo());
        DrawingScene redoScene = builder.Build(document, layout);
        Assert.Equal(movedRoute, Route(redoScene, line.Connection.Id));
        Assert.Equal(movedGapAnchor, GapAnchor(gap, document, layout, redoScene).Position);

        SelectionHitTestEntry gapEntry = Assert.Single(redoScene.HitTestIndex.FindAll(
            new SelectionReference(
                SelectionTargetKind.GroundingAccessPoint,
                gap.GroundingAccessPointId)));
        Assert.False(new DeviceDragController().TryBeginDrag(
            gapEntry.Target,
            movedGapAnchor,
            layout,
            document: document));
    }

    [Fact]
    public void PublicIndoorMove_RebuildsCableAndMovesSelectableCenteredLabel()
    {
        TransformerCreation moving = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(100, 100),
            "站内公变",
            TransformerOrientation.Horizontal);
        TransformerCreation other = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(260, 100),
            "另一台公变",
            TransformerOrientation.Vertical);
        var document = new DrawingDocument(Guid.NewGuid(), "Transformer cable move");
        document.AddTransformer(moving.Transformer, moving.HvTerminal);
        document.AddTransformer(other.Transformer, other.HvTerminal);
        RuntimeLayoutDocument layout = Runtime(
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [moving.Transformer.Id] = moving.Layout,
                [other.Transformer.Id] = other.Layout
            });
        Connection connection = AddCable(
            document,
            moving.HvTerminal.Id,
            other.HvTerminal.Id,
            "变压器电缆");
        var builder = new DrawingSceneBuilder();
        DrawingScene beforeScene = builder.Build(document, layout);
        TerminalAnchor beforeAnchor = Anchor(document, layout, moving.HvTerminal.Id);
        DocumentPoint[] beforeRoute = Route(beforeScene, connection.Id);
        Guid[] endpointIds =
            [connection.StartTerminalId, connection.EndTerminalId];
        SelectionReference productionTarget = Assert.IsType<SelectionReference>(
            beforeScene.HitTestIndex.HitTest(moving.Layout.Position));
        Assert.Equal(SelectionTargetKind.Device, productionTarget.Kind);
        Assert.Equal(moving.Transformer.Id, productionTarget.ObjectId);
        var controller = new DeviceDragController();
        Assert.True(controller.TryBeginDrag(
            productionTarget,
            moving.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(135, 155)));
        DrawingScene movedScene = builder.Build(document, layout);
        TerminalAnchor movedAnchor = Anchor(document, layout, moving.HvTerminal.Id);
        DocumentPoint[] movedRoute = Route(movedScene, connection.Id);

        Assert.NotEqual(beforeAnchor.Position, movedAnchor.Position);
        Assert.NotEqual(beforeRoute, movedRoute);
        Assert.Equal(connection.Id, Assert.Single(document.Connections).Id);
        Guid[] actualEndpointIds =
            [connection.StartTerminalId, connection.EndTerminalId];
        Assert.Equal(endpointIds, actualEndpointIds);
        Assert.Equal("站内公变", moving.Transformer.DisplayName);
        SceneText label = Assert.Single(movedScene.Elements.OfType<SceneText>(),
            item => item.Text == moving.Transformer.DisplayName);
        TransformerProfessionalGeometry geometry = TransformerProfessionalGeometry.Create(
            moving.Transformer,
            layout.TransformerLayouts[moving.Transformer.Id],
            DrawingMetrics.Default.Transformer);
        DocumentRect labelBounds = Assert.IsType<DocumentRect>(label.HitTestBounds);
        double glyphCenterX = geometry.Bounds.XMillimeters + geometry.Bounds.WidthMillimeters / 2;
        double glyphBottom = geometry.Bounds.YMillimeters + geometry.Bounds.HeightMillimeters;
        Assert.Equal(SceneTextHorizontalAlignment.Center, label.HorizontalAlignment);
        Assert.Equal(glyphCenterX, label.Origin.XMillimeters, 8);
        Assert.True(labelBounds.YMillimeters >= glyphBottom);
        Assert.NotEqual(beforeScene.Elements.OfType<SceneText>()
            .Single(item => item.Text == moving.Transformer.DisplayName).Origin, label.Origin);
        SelectionHitTestEntry labelEntry = Assert.Single(
            movedScene.HitTestIndex.Entries,
            entry => entry.Target.Kind == SelectionTargetKind.Device &&
                     entry.Target.ObjectId == moving.Transformer.Id &&
                     entry.Bounds == labelBounds);
        Assert.Equal(SelectionTargetKind.Device, labelEntry.Target.Kind);
        Assert.Equal(moving.Transformer.Id, labelEntry.Target.ObjectId);
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CustomerStationDirectDrag_ChangesOnlyPositionAndSupportsUndoRedo(
        StationKind kind,
        int feederCount)
    {
        CustomerStationCreation creation = CreateCustomerStation(
            kind,
            feederCount,
            new DocumentPoint(20, 30));
        DrawingDocument document = new(Guid.NewGuid(), "Customer station move");
        document.AddCustomerStation(creation.CustomerStation);
        RuntimeLayoutDocument layout = Runtime(
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [creation.CustomerStation.Id] = creation.Layout
            });
        SelectionReference target = new(
            SelectionTargetKind.Device,
            creation.CustomerStation.Id);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            target,
            creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(57, 68)));
        Assert.Equal(new DocumentPoint(57, 68), layout.CustomerStationLayouts[
            creation.CustomerStation.Id].Position);
        Guid[] feederIds = creation.CustomerStation.IncomingFeeders
            .Select(item => item.IncomingFeederId)
            .ToArray();

        MoveCustomerStationCommand command = Assert.IsType<MoveCustomerStationCommand>(
            controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.Equal(new DocumentPoint(20, 30), layout.CustomerStationLayouts[
            creation.CustomerStation.Id].Position);
        Assert.True(stack.Redo());
        Assert.Equal(new DocumentPoint(57, 68), layout.CustomerStationLayouts[
            creation.CustomerStation.Id].Position);
        Assert.Equal(feederIds, creation.CustomerStation.IncomingFeeders.Select(item =>
            item.IncomingFeederId));
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CustomerStationDirectDrag_CancelRestoresBeforeAndPreservesFullIdentity(
        StationKind kind,
        int feederCount)
    {
        CustomerStationCreation creation = CreateCustomerStation(
            kind,
            feederCount,
            new DocumentPoint(90, 120));
        var document = new DrawingDocument(Guid.NewGuid(), "Customer station cancel");
        document.AddCustomerStation(creation.CustomerStation);
        RuntimeLayoutDocument layout = Runtime(
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [creation.CustomerStation.Id] = creation.Layout
            });
        CustomerStationIdentity before = CaptureIdentity(creation.CustomerStation);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.Device,
                creation.CustomerStation.Id),
            creation.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(145, 175)));
        controller.Cancel();

        Assert.Equal(creation.Layout.Position, layout.CustomerStationLayouts[
            creation.CustomerStation.Id].Position);
        AssertStationIdentity(before, creation.CustomerStation);
    }

    [Fact]
    public void CustomerStationNoChange_DoesNotCreateCommandOrHistoryEntry()
    {
        CustomerStationCreation creation = CreateCustomerStation(
            StationKind.BoxStation,
            1,
            new DocumentPoint(20, 30));
        var document = new DrawingDocument(Guid.NewGuid(), "Customer station no change");
        document.AddCustomerStation(creation.CustomerStation);
        RuntimeLayoutDocument layout = Runtime(
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [creation.CustomerStation.Id] = creation.Layout
            });
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginDrag(
            new SelectionReference(
                SelectionTargetKind.Device,
                creation.CustomerStation.Id),
            creation.Layout.Position,
            layout,
            document: document));
        Assert.False(controller.UpdatePreview(creation.Layout.Position));
        Assert.Null(controller.Commit());
        Assert.False(new CommandStack().IsDirty);
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CustomerStationMove_RebuildsAllCableAndGroundingPresentation(
        StationKind kind,
        int feederCount)
    {
        CustomerStationCreation moving = CreateCustomerStation(
            kind,
            feederCount,
            new DocumentPoint(100, 120));
        var document = new DrawingDocument(Guid.NewGuid(), "Customer station route move");
        document.AddCustomerStation(moving.CustomerStation);
        var layouts = new Dictionary<Guid, CustomerStationLayout>
        {
            [moving.CustomerStation.Id] = moving.Layout
        };
        var connections = new List<Connection>();
        for (var index = 0; index < feederCount; index++)
        {
            CustomerStationCreation endpoint = CreateCustomerStation(
                StationKind.BoxStation,
                1,
                new DocumentPoint(290, 70 + index * 110));
            document.AddCustomerStation(endpoint.CustomerStation);
            layouts.Add(endpoint.CustomerStation.Id, endpoint.Layout);
            connections.Add(AddCable(
                document,
                moving.CustomerStation.IncomingFeeders[index].CableTerminalId,
                endpoint.CustomerStation.IncomingFeeders[0].CableTerminalId,
                $"用户站电缆 {index + 1}"));
        }
        RuntimeLayoutDocument layout = Runtime(customerStationLayouts: layouts);
        IncomingFeeder groundedFeeder = moving.CustomerStation.IncomingFeeders[0];
        GroundingPoint grounding = document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForTerminal(groundedFeeder.CableTerminalId),
            "用户站进线接地",
            "S01");
        var groundingLayout = new GroundingPointLayout(
            grounding.GroundingPointId,
            new DocumentPoint(6, -4));
        layout.SetGroundingPointLayout(groundingLayout);
        CustomerStationIdentity identityBefore = CaptureIdentity(moving.CustomerStation);
        var builder = new DrawingSceneBuilder();
        DrawingScene beforeScene = builder.Build(document, layout);
        Dictionary<Guid, DocumentPoint[]> beforeRoutes = connections.ToDictionary(
            item => item.Id,
            item => Route(beforeScene, item.Id));
        GroundingPresentationAnchor beforeGrounding = GroundingAnchor(
            beforeScene,
            grounding.GroundingPointId);
        var controller = new DeviceDragController();
        Assert.True(controller.TryBeginDrag(
            new SelectionReference(SelectionTargetKind.Device, moving.CustomerStation.Id),
            moving.Layout.Position,
            layout,
            document: document));
        Assert.True(controller.UpdatePreview(new DocumentPoint(145, 170)));
        MoveCustomerStationCommand command = Assert.IsType<MoveCustomerStationCommand>(
            controller.Commit());
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        DrawingScene movedScene = builder.Build(document, layout);
        GroundingPresentationAnchor movedGrounding = GroundingAnchor(
            movedScene,
            grounding.GroundingPointId);

        foreach (Connection connection in connections)
        {
            DocumentPoint[] movedRoute = Route(movedScene, connection.Id);
            Assert.NotEqual(beforeRoutes[connection.Id], movedRoute);
            Assert.Equal(connection.Id,
                document.Connections.Single(item => item.Id == connection.Id).Id);
            Assert.Equal(
                new[] { connection.StartTerminalId, connection.EndTerminalId },
                new[]
                {
                    document.Connections.Single(item => item.Id == connection.Id).StartTerminalId,
                    document.Connections.Single(item => item.Id == connection.Id).EndTerminalId
                });
            Guid movingTerminalId = connection.StartTerminalId;
            DocumentPoint movingAnchor = Anchor(document, layout, movingTerminalId).Position;
            Assert.True(movedRoute[0] == movingAnchor || movedRoute[^1] == movingAnchor);
        }
        Assert.NotEqual(beforeGrounding.Position, movedGrounding.Position);
        Assert.Equal(grounding.GroundingPointId,
            Assert.Single(document.GroundingPoints).GroundingPointId);
        Assert.Equal(GroundingTarget.ForTerminal(groundedFeeder.CableTerminalId), grounding.Target);
        Assert.Equal(groundedFeeder.CableTerminalId, grounding.Target.TargetId);
        Assert.Equal(new DocumentPoint(6, -4), layout.GroundingPointLayouts[
            grounding.GroundingPointId].SymbolOffset);
        AssertStationIdentity(identityBefore, moving.CustomerStation);

        Assert.True(stack.Undo());
        DrawingScene undoScene = builder.Build(document, layout);
        foreach (Connection connection in connections)
        {
            Assert.Equal(beforeRoutes[connection.Id], Route(undoScene, connection.Id));
        }
        Assert.Equal(beforeGrounding, GroundingAnchor(undoScene, grounding.GroundingPointId));
        Assert.True(stack.Redo());
        DrawingScene redoScene = builder.Build(document, layout);
        foreach (Connection connection in connections)
        {
            Assert.Equal(Route(movedScene, connection.Id), Route(redoScene, connection.Id));
        }
        Assert.Equal(movedGrounding, GroundingAnchor(redoScene, grounding.GroundingPointId));
        AssertStationIdentity(identityBefore, moving.CustomerStation);
    }

    [Fact]
    public void ProductionSceneHits_StartParentMovesButKeepIncomingSwitchUnsupported()
    {
        CustomerStationCreation station = CreateCustomerStation(
            StationKind.BoxStation,
            1,
            new DocumentPoint(120, 120));
        var document = new DrawingDocument(Guid.NewGuid(), "Production hit wiring");
        document.AddCustomerStation(station.CustomerStation);
        RuntimeLayoutDocument layout = Runtime(
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [station.CustomerStation.Id] = station.Layout
            });
        DrawingScene scene = new DrawingSceneBuilder().Build(document, layout);
        CustomerStationProfessionalGeometry geometry = CustomerStationProfessionalGeometry.Create(
            station.CustomerStation,
            station.Layout,
            DrawingMetrics.Default.CustomerStation);
        CustomerStationUnitGeometry unit = Assert.Single(geometry.Units);
        DocumentPoint bodyCenter = Center(unit.Body);
        SelectionHitTestEntry bodyHit = Assert.IsType<SelectionHitTestEntry>(
            scene.HitTestIndex.HitTestEntry(bodyCenter));
        Assert.Equal(SelectionTargetKind.Device, bodyHit.Target.Kind);
        Assert.Equal(station.CustomerStation.Id, bodyHit.Target.ObjectId);
        Assert.True(bodyHit.CanStartDrag);
        var bodyController = new DeviceDragController();
        Assert.True(bodyController.TryBeginDrag(
            bodyHit.Target,
            bodyCenter,
            layout,
            document: document));
        bodyController.Cancel();

        DocumentPoint roofApex = geometry.Roof[1];
        SelectionHitTestEntry roofHit = Assert.IsType<SelectionHitTestEntry>(
            scene.HitTestIndex.HitTestEntry(roofApex));
        Assert.Equal(station.CustomerStation.Id, roofHit.Target.ObjectId);
        Assert.True(roofHit.CanStartDrag);

        CustomerStationSwitchGeometry switchGeometry = Assert.Single(geometry.Switches);
        SelectionHitTestEntry switchHit = Assert.IsType<SelectionHitTestEntry>(
            scene.HitTestIndex.HitTestEntry(switchGeometry.StationContact));
        Assert.Equal(switchGeometry.SwitchDeviceId, switchHit.Target.ObjectId);
        Assert.Equal(switchGeometry.IncomingFeederId, switchHit.Target.ParentId);
        Assert.False(switchHit.CanStartDrag);
        Assert.DoesNotContain(
            new SelectionMovePlanner().Create(
                SelectionSet.Create([switchHit.Target], switchHit.Target),
                switchHit.Target,
                document,
                layout).Roots,
            root => root.Kind == SelectionMoveRootKind.CustomerStation);
        Assert.False(new DeviceDragController().TryBeginDrag(
            switchHit.Target,
            switchGeometry.StationContact,
            layout,
            document: document));
    }

    [Fact]
    public void GroupMove_TranslatesTransformerAndCustomerStationWithOneDelta()
    {
        TransformerCreation transformer = new TransformerCreationFactory().Create(
            TransformerKind.PublicIndoor,
            new DocumentPoint(10, 20),
            "变压器测试");
        CustomerStationCreation station = CreateCustomerStation(
            StationKind.IndoorStation,
            2,
            new DocumentPoint(80, 90));
        DrawingDocument document = new(Guid.NewGuid(), "Mixed group move");
        document.AddTransformer(transformer.Transformer, transformer.HvTerminal);
        document.AddCustomerStation(station.CustomerStation);
        RuntimeLayoutDocument layout = Runtime(
            transformerLayouts: new Dictionary<Guid, TransformerLayout>
            {
                [transformer.Transformer.Id] = transformer.Layout
            },
            customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
            {
                [station.CustomerStation.Id] = station.Layout
            });
        Connection connection = AddCable(
            document,
            transformer.HvTerminal.Id,
            station.CustomerStation.IncomingFeeders[0].CableTerminalId,
            "混合组移动电缆");
        var builder = new DrawingSceneBuilder();
        DocumentPoint[] beforeRoute = Route(builder.Build(document, layout), connection.Id);
        SelectionReference transformerTarget = new(
            SelectionTargetKind.Device,
            transformer.Transformer.Id);
        SelectionReference stationTarget = new(
            SelectionTargetKind.Device,
            station.CustomerStation.Id);
        SelectionSet selection = SelectionSet.Create(
            [transformerTarget, stationTarget],
            transformerTarget);
        var controller = new DeviceDragController();

        Assert.True(controller.TryBeginGroupDrag(
            selection,
            transformerTarget,
            transformer.Layout.Position,
            document,
            layout));
        Assert.True(controller.UpdatePreview(new DocumentPoint(25, 35)));
        Assert.Equal(new DocumentPoint(25, 35), layout.TransformerLayouts[
            transformer.Transformer.Id].Position);
        Assert.Equal(new DocumentPoint(95, 105), layout.CustomerStationLayouts[
            station.CustomerStation.Id].Position);
        DocumentPoint[] afterRoute = Route(builder.Build(document, layout), connection.Id);
        Assert.NotEqual(beforeRoute, afterRoute);

        GroupMoveCommand command = Assert.IsType<GroupMoveCommand>(controller.Commit());
        Assert.Single(command.After.Transformers);
        Assert.Single(command.After.CustomerStations);
        var stack = new CommandStack();
        stack.ExecuteCommand(command);
        Assert.True(stack.Undo());
        Assert.Equal(new DocumentPoint(10, 20), layout.TransformerLayouts[
            transformer.Transformer.Id].Position);
        Assert.Equal(new DocumentPoint(80, 90), layout.CustomerStationLayouts[
            station.CustomerStation.Id].Position);
        Assert.Equal(beforeRoute, Route(builder.Build(document, layout), connection.Id));
        Assert.True(stack.Redo());
        Assert.Equal(new DocumentPoint(25, 35), layout.TransformerLayouts[
            transformer.Transformer.Id].Position);
        Assert.Equal(new DocumentPoint(95, 105), layout.CustomerStationLayouts[
            station.CustomerStation.Id].Position);
        Assert.Equal(afterRoute, Route(builder.Build(document, layout), connection.Id));
    }

    private static CustomerStationCreation CreateCustomerStation(
        StationKind kind,
        int feederCount,
        DocumentPoint position)
    {
        string[] names = feederCount == 1
            ? ["主供"]
            : ["主供", "备供"];
        return new ApplicationCustomerStationCreationFactory().Create(kind, names, position);
    }

    private static Connection AddCable(
        DrawingDocument document,
        Guid startTerminalId,
        Guid endTerminalId,
        string name)
    {
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            startTerminalId,
            endTerminalId,
            name,
            Transformer.TenKilovolts);
        document.AddCableSegment(
            new CableSegment(
                Guid.NewGuid(),
                name,
                "YJV",
                100,
                Transformer.TenKilovolts,
                connectionId,
                startTerminalId,
                endTerminalId),
            connection);
        return connection;
    }

    private static TerminalAnchor Anchor(
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        Guid terminalId)
    {
        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            layout.DrawingLayout,
            layout.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            layout.TransformerLayouts,
            layout.CustomerStationLayouts);
        Assert.True(anchors.TryGet(terminalId, out TerminalAnchor anchor));
        return anchor;
    }

    private static DocumentPoint[] Route(DrawingScene scene, Guid connectionId) =>
        scene.Routes.Single(item => item.ConnectionId == connectionId).Points.ToArray();

    private static GroundingPresentationAnchor GapAnchor(
        GroundingAccessPoint gap,
        DrawingDocument document,
        RuntimeLayoutDocument layout,
        DrawingScene scene)
    {
        var resolver = new GroundingAccessPointAnchorResolver();
        Assert.True(resolver.TryResolve(
            gap,
            document,
            layout.DrawingLayout,
            scene.Routes.ToDictionary(item => item.ConnectionId),
            layout.TransformerLayouts,
            out GroundingPresentationAnchor anchor));
        return anchor;
    }

    private static GroundingPresentationAnchor GroundingAnchor(
        DrawingScene scene,
        Guid groundingPointId)
    {
        SelectionHitTestEntry[] entries = scene.HitTestIndex.Entries
            .Where(entry =>
                entry.Target.Kind == SelectionTargetKind.GroundingPoint &&
                entry.Target.ObjectId == groundingPointId &&
                entry.GroundingAnchor is not null)
            .ToArray();
        Assert.NotEmpty(entries);
        GroundingPresentationAnchor anchor = entries[0].GroundingAnchor!.Value;
        Assert.All(entries, entry => Assert.Equal(
            anchor,
            entry.GroundingAnchor!.Value));
        return anchor;
    }

    private static void AssertGapIdentity(
        GroundingAccessPoint gap,
        Guid connectionId,
        Guid poleId,
        Guid transformerTerminalId)
    {
        Assert.NotEqual(Guid.Empty, gap.GroundingAccessPointId);
        Assert.Equal(connectionId, gap.ConnectionId);
        Assert.Equal(poleId, gap.PoleId);
        Assert.Equal(
            GroundingAdjacentEndpoint.ForTerminal(transformerTerminalId),
            gap.AdjacentEndpoint);
        Assert.Equal(GroundingAccessLineSide.TransformerSide, gap.LineSide);
        Assert.Equal(
            GroundingAccessPlacementSide.AdjacentEndpointSide,
            gap.PlacementSide);
    }

    private static CustomerStationIdentity CaptureIdentity(CustomerStation station) => new(
        station.Id,
        station.StationKind,
        station.IncomingFeeders.Select(feeder => new IncomingFeederIdentity(
            feeder.IncomingFeederId,
            feeder.Sequence,
            feeder.DisplayName,
            feeder.CableTerminalId,
            feeder.StationTerminalId,
            feeder.ElectricalNodeId,
            feeder.IsolationSwitch.Id,
            feeder.IsolationSwitch.SwitchKind,
            feeder.IsolationSwitch.InstallationType,
            feeder.IsolationSwitch.SwitchState,
            feeder.IsolationSwitch.FirstTerminalId,
            feeder.IsolationSwitch.SecondTerminalId,
            feeder.IsolationSwitch.DisplayName,
            feeder.IsolationSwitch.VoltageLevel,
            feeder.IsolationSwitch.ParentId,
            feeder.IsolationSwitch.DispatchNumber)).ToArray());

    private static void AssertStationIdentity(
        CustomerStationIdentity expected,
        CustomerStation station)
    {
        CustomerStationIdentity actual = CaptureIdentity(station);
        Assert.Equal(expected.CustomerStationId, actual.CustomerStationId);
        Assert.Equal(expected.StationKind, actual.StationKind);
        Assert.Equal(expected.Feeders, actual.Feeders);
    }

    private static DocumentPoint Center(DocumentRect bounds) => new(
        bounds.XMillimeters + bounds.WidthMillimeters / 2,
        bounds.YMillimeters + bounds.HeightMillimeters / 2);

    private sealed record CustomerStationIdentity(
        Guid CustomerStationId,
        StationKind StationKind,
        IReadOnlyList<IncomingFeederIdentity> Feeders);

    private sealed record IncomingFeederIdentity(
        Guid IncomingFeederId,
        int Sequence,
        string DisplayName,
        Guid CableTerminalId,
        Guid StationTerminalId,
        Guid ElectricalNodeId,
        Guid IsolationSwitchId,
        SwitchKind SwitchKind,
        SwitchInstallationType InstallationType,
        SwitchState? SwitchState,
        Guid FirstTerminalId,
        Guid SecondTerminalId,
        string? SwitchDisplayName,
        string? SwitchVoltageLevel,
        Guid? ParentId,
        string? DispatchNumber);

    private static RuntimeLayoutDocument Runtime(
        IReadOnlyDictionary<Guid, TransformerLayout>? transformerLayouts = null,
        IReadOnlyDictionary<Guid, CustomerStationLayout>? customerStationLayouts = null) =>
        new(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>(),
            transformerLayouts: transformerLayouts,
            customerStationLayouts: customerStationLayouts);
}
