using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Interaction.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Routing;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CustomerStationSliceCTests
{
    [Fact]
    public void BoxStation_GlyphHasOneUnitTriangleRoofAndVisibleSwitch()
    {
        CustomerStationCreation creation = Create(StationKind.BoxStation, ["用户主供"]);
        CustomerStationProfessionalGeometry geometry = Geometry(creation);
        IReadOnlyList<SceneElement> elements = new CustomerStationRenderer().Render(
            creation.CustomerStation,
            creation.Layout);

        Assert.Single(geometry.Units);
        Assert.Equal(3, Assert.Single(geometry.Units).Triangle.Count);
        Assert.Equal(3, geometry.Roof.Count);
        Assert.Single(geometry.Switches);
        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "用户主供");
        Assert.Equal(2, elements.OfType<ScenePolyline>().Count());
        DocumentRect body = Assert.Single(geometry.Units).Body;
        Assert.True(geometry.Roof[0].XMillimeters < body.XMillimeters);
        Assert.True(geometry.Roof[2].XMillimeters > body.XMillimeters + body.WidthMillimeters);
        Assert.Equal(5, body.XMillimeters - geometry.Roof[0].XMillimeters, precision: 6);
        Assert.Equal(5,
            geometry.Roof[2].XMillimeters - (body.XMillimeters + body.WidthMillimeters),
            precision: 6);
        Assert.Equal(
            body.XMillimeters - geometry.Roof[0].XMillimeters,
            geometry.Roof[2].XMillimeters - (body.XMillimeters + body.WidthMillimeters),
            precision: 6);
        Assert.Equal(15,
            body.YMillimeters - geometry.Roof[1].YMillimeters,
            precision: 6);
        Assert.Equal(body.XMillimeters + body.WidthMillimeters / 2,
            geometry.Roof[1].XMillimeters,
            precision: 6);
        double roofAngleDegrees = Math.Atan2(
            body.YMillimeters - geometry.Roof[1].YMillimeters,
            geometry.Roof[1].XMillimeters - body.XMillimeters) *
            180 / Math.PI;
        Assert.InRange(roofAngleDegrees, 30, 40);
        double leftEaveDrop = geometry.Roof[0].YMillimeters - body.YMillimeters;
        double rightEaveDrop = geometry.Roof[2].YMillimeters - body.YMillimeters;
        Assert.Equal(5 * (15d / (body.WidthMillimeters / 2)), leftEaveDrop, precision: 6);
        Assert.Equal(leftEaveDrop, rightEaveDrop, precision: 6);
        Assert.True(geometry.Roof[0].YMillimeters > body.YMillimeters);
        Assert.True(geometry.Roof[2].YMillimeters > body.YMillimeters);
        var topLeft = new DocumentPoint(body.XMillimeters, body.YMillimeters);
        var topRight = new DocumentPoint(
            body.XMillimeters + body.WidthMillimeters,
            body.YMillimeters);
        double leftCrossProduct =
            (topLeft.XMillimeters - geometry.Roof[0].XMillimeters) *
            (geometry.Roof[1].YMillimeters - geometry.Roof[0].YMillimeters) -
            (topLeft.YMillimeters - geometry.Roof[0].YMillimeters) *
            (geometry.Roof[1].XMillimeters - geometry.Roof[0].XMillimeters);
        double rightCrossProduct =
            (topRight.XMillimeters - geometry.Roof[1].XMillimeters) *
            (geometry.Roof[2].YMillimeters - geometry.Roof[1].YMillimeters) -
            (topRight.YMillimeters - geometry.Roof[1].YMillimeters) *
            (geometry.Roof[2].XMillimeters - geometry.Roof[1].XMillimeters);
        Assert.Equal(0, leftCrossProduct, precision: 6);
        Assert.Equal(0, rightCrossProduct, precision: 6);
        Assert.Empty(Geometry(Create(StationKind.IndoorStation, ["室内站"])).Roof);
    }

    [Fact]
    public void IndoorSingle_VisibilityOnlyChangesPresentationAnchor()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供"]);
        IncomingFeeder feeder = Assert.Single(creation.CustomerStation.IncomingFeeders);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        TerminalAnchor shown = Anchor(document, runtime, feeder.CableTerminalId);
        var stack = new CommandStack();
        stack.ExecuteCommand(new SetCustomerStationIncomingSwitchVisibilityCommand(
            runtime,
            creation.CustomerStation,
            feeder.IncomingFeederId,
            false));
        TerminalAnchor hidden = Anchor(document, runtime, feeder.CableTerminalId);

        Assert.Empty(Geometry(creation.CustomerStation, runtime.CustomerStationLayouts[creation.CustomerStation.Id]).Roof);
        Assert.Empty(Geometry(creation.CustomerStation, runtime.CustomerStationLayouts[creation.CustomerStation.Id]).Switches);
        Assert.Equal(feeder.CableTerminalId, shown.TerminalId);
        Assert.Equal(feeder.CableTerminalId, hidden.TerminalId);
        Assert.Equal(Assert.Single(Geometry(creation).Switches).CableLeadOuterEnd, shown.Position);
        Assert.Equal(Assert.Single(Geometry(creation).Units).Body.XMillimeters,
            hidden.Position.XMillimeters);
        Assert.NotEqual(shown.Position, hidden.Position);
        Assert.Equal(TerminalAnchorDirection.Left, hidden.Direction);
        Assert.True(stack.Undo());
        Assert.Equal(shown, Anchor(document, runtime, feeder.CableTerminalId));
        Assert.True(stack.Redo());
        Assert.Equal(hidden, Anchor(document, runtime, feeder.CableTerminalId));
    }

    [Fact]
    public void IndoorDual_UsesCanonicalLeftRightUnitsAndIndependentVisibility()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供", "备供"]);
        IncomingFeeder first = creation.CustomerStation.IncomingFeeders.Single(item => item.Sequence == 1);
        IncomingFeeder second = creation.CustomerStation.IncomingFeeders.Single(item => item.Sequence == 2);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        new SetCustomerStationIncomingSwitchVisibilityCommand(
            runtime,
            creation.CustomerStation,
            second.IncomingFeederId,
            false).Execute();
        CustomerStationProfessionalGeometry geometry = Geometry(
            creation.CustomerStation,
            runtime.CustomerStationLayouts[creation.CustomerStation.Id]);

        Assert.Equal(2, geometry.Units.Count);
        CustomerStationUnitGeometry leftUnit = geometry.Units.Single(item => item.Sequence == 1);
        CustomerStationUnitGeometry rightUnit = geometry.Units.Single(item => item.Sequence == 2);
        Assert.True(leftUnit.Body.XMillimeters < rightUnit.Body.XMillimeters);
        Assert.Equal(
            leftUnit.Body.XMillimeters + leftUnit.Body.WidthMillimeters,
            rightUnit.Body.XMillimeters,
            precision: 6);
        Assert.NotEqual(leftUnit.IncomingFeederId, rightUnit.IncomingFeederId);
        Assert.Equal(first.IncomingFeederId, Assert.Single(geometry.Switches).IncomingFeederId);
        Assert.Equal(TerminalAnchorDirection.Left,
            geometry.CableTerminalAnchors[first.CableTerminalId].Direction);
        Assert.Equal(TerminalAnchorDirection.Right,
            geometry.CableTerminalAnchors[second.CableTerminalId].Direction);
    }

    [Fact]
    public void VisibleSwitches_UseTypedShortLeadsAndMirroredOuterAnchors()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供", "备供"]);
        CustomerStationProfessionalGeometry geometry = Geometry(creation);
        CustomerStationSwitchGeometry left = geometry.Switches.Single(item =>
            item.IncomingFeederId == creation.CustomerStation.IncomingFeeders
                .Single(feeder => feeder.Sequence == 1).IncomingFeederId);
        CustomerStationSwitchGeometry right = geometry.Switches.Single(item =>
            item.IncomingFeederId == creation.CustomerStation.IncomingFeeders
                .Single(feeder => feeder.Sequence == 2).IncomingFeederId);
        double leadLength = DrawingMetrics.Default.CustomerStation.IncomingSwitchLeadLength;

        Assert.Equal(2, leadLength);
        Assert.Equal(leadLength,
            left.CableContact.XMillimeters - left.CableLeadOuterEnd.XMillimeters, 6);
        Assert.Equal(leadLength,
            left.StationEntry.XMillimeters - left.StationContact.XMillimeters, 6);
        Assert.Equal(leadLength,
            right.CableLeadOuterEnd.XMillimeters - right.CableContact.XMillimeters, 6);
        Assert.Equal(leadLength,
            right.StationContact.XMillimeters - right.StationEntry.XMillimeters, 6);
        Assert.Equal(left.CableLeadOuterEnd,
            geometry.CableTerminalAnchors[left.CableTerminalId].Position);
        Assert.Equal(right.CableLeadOuterEnd,
            geometry.CableTerminalAnchors[right.CableTerminalId].Position);
        Assert.Equal(TerminalAnchorDirection.Left, left.CableDirection);
        Assert.Equal(TerminalAnchorDirection.Right, right.CableDirection);
        Assert.NotEqual(left.CableTerminalId, right.CableTerminalId);

        IReadOnlyList<SceneLine> lines = new CustomerStationRenderer().Render(
                creation.CustomerStation,
                creation.Layout)
            .OfType<SceneLine>()
            .ToArray();
        Assert.Contains(lines, line =>
            line.Start == left.CableLeadOuterEnd && line.End == left.CableContact);
        Assert.Contains(lines, line =>
            line.Start == left.StationContact && line.End == left.StationEntry);
        Assert.Contains(lines, line =>
            line.Start == right.CableLeadOuterEnd && line.End == right.CableContact);
        Assert.Contains(lines, line =>
            line.Start == right.StationContact && line.End == right.StationEntry);
        Assert.Empty(new CustomerStationRenderer().Render(
                creation.CustomerStation,
                creation.Layout)
            .OfType<SceneEllipse>());
    }

    [Fact]
    public void UserStationNumberLabel_TracksUnifiedTypographySettings()
    {
        CustomerStationCreation creation = Create(StationKind.BoxStation, ["丰华路1号用户站"]);
        var typography = new DrawingTypographyMetrics(16, 8, 10.5, 7, 8, 7,
            CustomerStationNumberFontSize: 6);
        DrawingMetrics metrics = DrawingMetrics.Default with { Typography = typography };
        var renderer = new CustomerStationRenderer(metrics);

        SceneText first = Assert.Single(renderer.Render(creation.CustomerStation, creation.Layout)
            .OfType<SceneText>());
        typography.Update(16, 8, 10.5, 7, 8, 7,
            customerStationNumberFontSize: 9);
        SceneText second = Assert.Single(renderer.Render(creation.CustomerStation, creation.Layout)
            .OfType<SceneText>());

        Assert.Equal("丰华路1号用户站", first.Text);
        Assert.Equal(6, first.FontSizeMillimeters);
        Assert.Equal(9, second.FontSizeMillimeters);
    }

    [Fact]
    public void TerminalAnchorIndex_ExposesOnlyCableTerminalForCustomerStation()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供", "备供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        TerminalAnchorIndex anchors = Anchors(document, runtime);

        Assert.All(creation.CustomerStation.IncomingFeeders, feeder =>
        {
            Assert.True(anchors.TryGet(feeder.CableTerminalId, out _));
            Assert.False(anchors.TryGet(feeder.StationTerminalId, out _));
            Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                document,
                feeder.CableTerminalId));
            Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                document,
                feeder.StationTerminalId));
        });
    }

    [Fact]
    public void CableAndGroundingPresentationFollowVisibilityWithoutIdentityChanges()
    {
        CustomerStationCreation firstCreation = Create(StationKind.IndoorStation, ["主供"]);
        CustomerStationCreation secondCreation = new CustomerStationCreationFactory().Create(
            StationKind.BoxStation,
            ["用户进线"],
            new DocumentPoint(180, 100));
        DrawingDocument document = DocumentWith(firstCreation);
        document.AddCustomerStation(secondCreation.CustomerStation);
        RuntimeLayoutDocument runtime = RuntimeWith(firstCreation);
        runtime.AddCustomerStation(secondCreation.Layout, secondCreation.CustomerStation);
        IncomingFeeder first = Assert.Single(firstCreation.CustomerStation.IncomingFeeders);
        IncomingFeeder second = Assert.Single(secondCreation.CustomerStation.IncomingFeeders);
        Guid connectionId = Guid.NewGuid();
        var connection = new Connection(
            connectionId,
            ConnectionType.Cable,
            first.CableTerminalId,
            second.CableTerminalId,
            "用户站电缆",
            IncomingFeeder.TenKilovolts);
        Guid cableId = Guid.NewGuid();
        document.AddCableSegment(new CableSegment(
            cableId,
            "用户站电缆",
            "YJV",
            80,
            IncomingFeeder.TenKilovolts,
            connectionId,
            first.CableTerminalId,
            second.CableTerminalId), connection);
        var groundingCommand = new ProfessionalCommandFactory().CreateAddGroundingPoint(
            document,
            first.CableTerminalId);
        groundingCommand.Execute();
        GroundingPoint groundingPoint = Assert.Single(document.GroundingPoints);
        Guid groundingId = groundingPoint.GroundingPointId;
        GroundingTarget groundingTarget = groundingPoint.Target;
        Guid cableTerminalId = first.CableTerminalId;
        TerminalAnchor shown = Anchor(document, runtime, first.CableTerminalId);
        DrawingScene shownScene = new DrawingSceneBuilder().Build(document, runtime);

        var visibilityStack = new CommandStack();
        visibilityStack.ExecuteCommand(new SetCustomerStationIncomingSwitchVisibilityCommand(
            runtime,
            firstCreation.CustomerStation,
            first.IncomingFeederId,
            false));
        TerminalAnchor hidden = Anchor(document, runtime, first.CableTerminalId);
        DrawingScene hiddenScene = new DrawingSceneBuilder().Build(document, runtime);
        Assert.Equal(shown.Position, Assert.Single(shownScene.Routes).Points[0]);
        Assert.Equal(hidden.Position, Assert.Single(hiddenScene.Routes).Points[0]);
        Assert.Equal(shown.Position, shownScene.HitTestIndex.Entries.First(entry =>
            entry.Target.Kind == SelectionTargetKind.GroundingPoint &&
            entry.GroundingAnchor is not null).GroundingAnchor!.Value.Position);
        Assert.Equal(hidden.Position, hiddenScene.HitTestIndex.Entries.First(entry =>
            entry.Target.Kind == SelectionTargetKind.GroundingPoint &&
            entry.GroundingAnchor is not null).GroundingAnchor!.Value.Position);
        Assert.NotEqual(shown.Position, hidden.Position);
        Assert.Equal(connectionId, Assert.Single(document.Connections).Id);
        Assert.Equal(cableId, Assert.Single(document.CableSegments).Id);
        Assert.Equal(cableTerminalId, first.CableTerminalId);
        Assert.Equal(groundingId, Assert.Single(document.GroundingPoints).GroundingPointId);
        Assert.Equal(groundingTarget, Assert.Single(document.GroundingPoints).Target);
        Assert.True(visibilityStack.Undo());
        Assert.Equal(shown, Anchor(document, runtime, first.CableTerminalId));
        Assert.Equal(cableTerminalId, first.CableTerminalId);
        Assert.Equal(groundingTarget, Assert.Single(document.GroundingPoints).Target);
        Assert.True(visibilityStack.Redo());
        Assert.Equal(hidden, Anchor(document, runtime, first.CableTerminalId));
        Assert.Equal(cableTerminalId, first.CableTerminalId);
        Assert.Equal(groundingTarget, Assert.Single(document.GroundingPoints).Target);
        var resolver = new GroundingPresentationAnchorResolver();
        Assert.True(resolver.TryResolve(
            groundingPoint,
            document,
            runtime.DrawingLayout,
            Anchors(document, runtime),
            out GroundingPresentationAnchor resolved));
        Assert.Equal(hidden.Position, resolved.Position);
    }

    [Fact]
    public void ShownIncomingSwitchGroundingLeaderStartsDirectlyVertical()
    {
        CustomerStationCreation creation = Create(StationKind.BoxStation, ["用户主供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        IncomingFeeder feeder = Assert.Single(creation.CustomerStation.IncomingFeeders);
        GroundingPoint groundingPoint = document.CreateGroundingPoint(
            Guid.NewGuid(),
            feeder.CableTerminalId,
            "用户站进线接地",
            "G01");
        TerminalAnchor formalAnchor = Anchor(document, runtime, feeder.CableTerminalId);
        var resolver = new GroundingPresentationAnchorResolver();

        Assert.True(resolver.TryResolve(
            groundingPoint,
            document,
            runtime.DrawingLayout,
            Anchors(document, runtime),
            new Dictionary<Guid, OrthogonalRoute>(),
            runtime.CustomerStationLayouts,
            out GroundingPresentationAnchor presentationAnchor));
        GroundingPointResolvedLayout resolved = new GroundingPointLayoutResolver().Resolve(
            groundingPoint,
            presentationAnchor,
            null);

        Assert.Equal(GroundingPresentationPolicy.CustomerStationShownIncomingSwitch,
            presentationAnchor.Policy);
        Assert.Equal(formalAnchor.Position, presentationAnchor.Position);
        OrthogonalRouteSegment leader = Assert.Single(resolved.LeaderSegments);
        Assert.Equal(presentationAnchor.Position, leader.Start);
        Assert.Equal(presentationAnchor.Position.XMillimeters,
            leader.End.XMillimeters,
            precision: 6);
        Assert.True(leader.End.YMillimeters > leader.Start.YMillimeters);
        Assert.Equal(feeder.CableTerminalId, groundingPoint.Target.TargetId);
        Assert.Equal(GroundingTargetKind.Terminal, groundingPoint.Target.Kind);
    }

    [Fact]
    public void SceneSelection_PrioritizesVisibleSwitchAndOmitsHiddenSwitchHit()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        IncomingFeeder feeder = Assert.Single(creation.CustomerStation.IncomingFeeders);
        DrawingScene shown = new DrawingSceneBuilder().Build(document, runtime);
        CustomerStationSwitchGeometry switchGeometry = Assert.Single(Geometry(creation).Switches);
        SelectionHitTestEntry hit = Assert.IsType<SelectionHitTestEntry>(
            shown.HitTestIndex.HitTestEntry(switchGeometry.StationContact));

        Assert.Equal(feeder.IsolationSwitch.Id, hit.Target.ObjectId);
        Assert.Equal(40, hit.Priority);
        Assert.NotNull(shown.HitTestIndex.Find(new SelectionReference(
            SelectionTargetKind.Device,
            creation.CustomerStation.Id)));
        SelectionHitTestEntry leadHit = Assert.IsType<SelectionHitTestEntry>(
            shown.HitTestIndex.HitTestEntry(switchGeometry.CableLeadOuterEnd));
        Assert.Equal(feeder.IsolationSwitch.Id, leadHit.Target.ObjectId);
        new SetCustomerStationIncomingSwitchVisibilityCommand(
            runtime,
            creation.CustomerStation,
            feeder.IncomingFeederId,
            false).Execute();
        DrawingScene hidden = new DrawingSceneBuilder().Build(document, runtime);
        Assert.Null(hidden.HitTestIndex.Find(new SelectionReference(
            SelectionTargetKind.Device,
            feeder.IsolationSwitch.Id)));
    }

    [Fact]
    public void ResolverInspectorAndPropertyCommands_PreserveChildIndependence()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供", "备供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        var resolver = Resolver(document, runtime);
        var reference = new SelectionReference(
            SelectionTargetKind.Device,
            creation.CustomerStation.Id);
        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(resolver.Resolve(reference));
        PropertyInspectorSnapshot snapshot = new PropertyProjector().Project(resolved);
        var stack = new CommandStack();
        var editor = new PropertyEditor(resolver, stack, runtime);
        IncomingFeeder first = creation.CustomerStation.IncomingFeeders[0];
        IncomingFeeder second = creation.CustomerStation.IncomingFeeders[1];

        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.PropertyKey == "CustomerStation.StationKind" && row.IsReadOnly);
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.PropertyKey == "CustomerStation.FeederCount" && row.DisplayValue == "2");
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "用户站号1" && row.DisplayValue == "主供");
        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName == "用户站号2" && row.DisplayValue == "备供");
        Assert.DoesNotContain(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.DisplayName is "进线 1 名称" or "进线 2 名称");
        Assert.True(editor.TryEdit(
            reference,
            PropertyCommandFactory.CustomerStationFeederDisplayNamePropertyKey(first.IncomingFeederId),
            "  新主供  ").IsSuccess);
        Assert.Equal("新主供", first.DisplayName);
        Assert.Equal("备供", second.DisplayName);
        Assert.True(editor.TryEdit(
            reference,
            PropertyCommandFactory.CustomerStationFeederVisibilityPropertyKey(second.IncomingFeederId),
            "隐藏").IsSuccess);
        Assert.False(runtime.CustomerStationLayouts[creation.CustomerStation.Id]
            .IncomingFeeders[second.IncomingFeederId].ShowIncomingSwitch);
        Assert.True(stack.Undo());
        Assert.True(runtime.CustomerStationLayouts[creation.CustomerStation.Id]
            .IncomingFeeders[second.IncomingFeederId].ShowIncomingSwitch);
        Assert.True(stack.Undo());
        Assert.Equal("主供", first.DisplayName);
    }

    [Fact]
    public void BoxInspectorVisibilityIsReadOnlyAndCommandRejectsHide()
    {
        CustomerStationCreation creation = Create(StationKind.BoxStation, ["用户主供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(Resolver(document, runtime).Resolve(
            new SelectionReference(SelectionTargetKind.Device, creation.CustomerStation.Id)));
        PropertyInspectorSnapshot snapshot = new PropertyProjector().Project(resolved);

        Assert.Contains(snapshot.Sections.SelectMany(section => section.Properties), row =>
            row.PropertyKey.EndsWith(".ShowIncomingSwitch", StringComparison.Ordinal) &&
            row.IsReadOnly && row.DisplayValue == "是");
        Assert.Throws<InvalidOperationException>(() =>
            new SetCustomerStationIncomingSwitchVisibilityCommand(
                runtime,
                creation.CustomerStation,
                creation.CustomerStation.IncomingFeeders[0].IncomingFeederId,
                false));
    }

    [Fact]
    public void VisibleFeederSwitch_ResolvesAndUsesExistingStateCommand()
    {
        CustomerStationCreation creation = Create(StationKind.IndoorStation, ["主供", "备供"]);
        DrawingDocument document = DocumentWith(creation);
        RuntimeLayoutDocument runtime = RuntimeWith(creation);
        IncomingFeeder first = creation.CustomerStation.IncomingFeeders[0];
        IncomingFeeder second = creation.CustomerStation.IncomingFeeders[1];
        var reference = new SelectionReference(
            SelectionTargetKind.Device,
            first.IsolationSwitch.Id,
            first.IncomingFeederId);
        ResolvedSelection resolved = Assert.IsType<ResolvedSelection>(
            Resolver(document, runtime).Resolve(reference));
        var command = new DistributionDrawing.Application.Devices.ChangeSwitchStateCommand(
            document,
            first.IsolationSwitch.Id,
            SwitchState.Closed);

        Assert.Same(first.IsolationSwitch, resolved.SwitchDevice);
        command.Execute();
        Assert.Equal(SwitchState.Closed, first.IsolationSwitch.SwitchState);
        Assert.Equal(SwitchState.Open, second.IsolationSwitch.SwitchState);
        command.Undo();
        Assert.Equal(SwitchState.Open, first.IsolationSwitch.SwitchState);
        command.Redo();
        Assert.Equal(SwitchState.Closed, first.IsolationSwitch.SwitchState);
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 0)]
    [InlineData(StationKind.BoxStation, 2)]
    [InlineData(StationKind.IndoorStation, 0)]
    [InlineData(StationKind.IndoorStation, 3)]
    public void CreationFactory_RejectsIllegalFeederCount(StationKind kind, int count)
    {
        string[] names = Enumerable.Range(1, count).Select(index => $"进线 {index}").ToArray();
        Assert.Throws<InvalidOperationException>(() =>
            new CustomerStationCreationFactory().Create(
                kind,
                names,
                new DocumentPoint(10, 10)));
    }

    [Fact]
    public void CreationFactory_RejectsBlankFeederName()
    {
        Assert.Throws<ArgumentException>(() =>
            new CustomerStationCreationFactory().Create(
                StationKind.IndoorStation,
                ["主供", "  "],
                new DocumentPoint(10, 10)));
    }

    [Theory]
    [InlineData(StationKind.BoxStation, 1)]
    [InlineData(StationKind.IndoorStation, 1)]
    [InlineData(StationKind.IndoorStation, 2)]
    public void CreationCommand_DefaultsOpenVisibleAndKeepsStableIdsAcrossUndoRedo(
        StationKind kind,
        int feederCount)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Customer station creation");
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());
        string[] names = feederCount == 1 ? ["  主供  "] : ["  主供  ", "备供"];
        AddCustomerStationWithLayoutCommand command = new DeviceCommandFactory()
            .CreateAddCustomerStation(document, runtime, kind, names, new DocumentPoint(90, 70));
        Guid stationId = command.Creation.CustomerStation.Id;
        Guid[] childIds = command.Creation.CustomerStation.IncomingFeeders.SelectMany(feeder =>
            new[] { feeder.IncomingFeederId, feeder.IsolationSwitch.Id, feeder.CableTerminalId,
                feeder.StationTerminalId, feeder.ElectricalNodeId }).ToArray();
        var stack = new CommandStack();

        stack.ExecuteCommand(command);
        Assert.Equal("主供", command.Creation.CustomerStation.IncomingFeeders[0].DisplayName);
        Assert.All(command.Creation.CustomerStation.IncomingFeeders, feeder =>
            Assert.Equal(SwitchState.Open, feeder.IsolationSwitch.SwitchState));
        Assert.All(command.Creation.Layout.IncomingFeeders.Values, feeder =>
            Assert.True(feeder.ShowIncomingSwitch));
        Assert.True(stack.Undo());
        Assert.Empty(document.CustomerStations);
        Assert.Empty(runtime.CustomerStationLayouts);
        Assert.True(stack.Redo());
        Assert.Equal(stationId, Assert.Single(document.CustomerStations).Id);
        Assert.Equal(childIds, Assert.Single(document.CustomerStations).IncomingFeeders.SelectMany(feeder =>
            new[] { feeder.IncomingFeederId, feeder.IsolationSwitch.Id, feeder.CableTerminalId,
                feeder.StationTerminalId, feeder.ElectricalNodeId }));
    }

    [Fact]
    public void SceneBuilderRejectsMissingCustomerStationLayout()
    {
        CustomerStationCreation creation = Create(StationKind.BoxStation, ["主供"]);
        DrawingDocument document = DocumentWith(creation);
        var runtime = new RuntimeLayoutDocument(
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout>());

        Assert.Throws<InvalidOperationException>(() =>
            new DrawingSceneBuilder().Build(document, runtime));
    }

    private static CustomerStationCreation Create(
        StationKind kind,
        IReadOnlyList<string> names) =>
        new CustomerStationCreationFactory().Create(kind, names, new DocumentPoint(100, 100));

    private static DrawingDocument DocumentWith(CustomerStationCreation creation)
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Customer station scene");
        document.AddCustomerStation(creation.CustomerStation);
        return document;
    }

    private static RuntimeLayoutDocument RuntimeWith(CustomerStationCreation creation) => new(
        new DrawingLayout(),
        new Dictionary<Guid, RingCabinetLayout>(),
        customerStationLayouts: new Dictionary<Guid, CustomerStationLayout>
        {
            [creation.CustomerStation.Id] = creation.Layout
        });

    private static CustomerStationProfessionalGeometry Geometry(CustomerStationCreation creation) =>
        Geometry(creation.CustomerStation, creation.Layout);

    private static CustomerStationProfessionalGeometry Geometry(
        CustomerStation station,
        CustomerStationLayout layout) => CustomerStationProfessionalGeometry.Create(
            station,
            layout,
            DrawingMetrics.Default.CustomerStation);

    private static TerminalAnchorIndex Anchors(
        DrawingDocument document,
        RuntimeLayoutDocument runtime) => TerminalAnchorIndex.Build(
            document,
            runtime.DrawingLayout,
            runtime.RingCabinetLayouts,
            document.Connections,
            document.CableSegments,
            runtime.TransformerLayouts,
            runtime.CustomerStationLayouts);

    private static TerminalAnchor Anchor(
        DrawingDocument document,
        RuntimeLayoutDocument runtime,
        Guid terminalId)
    {
        Assert.True(Anchors(document, runtime).TryGet(terminalId, out TerminalAnchor anchor));
        return anchor;
    }

    private static SelectionObjectResolver Resolver(
        DrawingDocument document,
        RuntimeLayoutDocument runtime)
    {
        var resolver = new SelectionObjectResolver();
        resolver.SetSource(new PropertyInspectionSource
        {
            Document = document,
            Devices = document.Devices,
            CustomerStationLayouts = runtime.CustomerStationLayouts
        });
        return resolver;
    }
}
