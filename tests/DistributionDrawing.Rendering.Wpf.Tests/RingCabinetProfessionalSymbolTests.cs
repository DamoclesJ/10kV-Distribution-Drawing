using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Labels;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Professional;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetProfessionalSymbolTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ConventionalCabinet_UsesOneStandardWidthForEveryInterval(int intervalCount)
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(intervalCount);
        RingCabinetLayout layout = CreateLayout(cabinet);

        Assert.Equal(intervalCount, layout.IntervalLayouts.Count);
        Assert.All(
            layout.IntervalLayouts.Values,
            interval => Assert.Equal(
                DrawingMetrics.Default.RingCabinet.StandardIntervalWidth,
                interval.WidthMillimeters));
        Assert.All(
            layout.IntervalLayouts.Values.SelectMany(interval => interval.SwitchLayouts.Values),
            switchLayout => Assert.Equal(
                DrawingMetrics.Default.Switch.LogicalHitHeight *
                DrawingMetrics.Default.RingCabinet.SwitchSymbolScale,
                switchLayout.HeightMillimeters));
    }

    [Fact]
    public void RingCabinetTypography_IsAdjustedByCategoryFromOneMetricsEntry()
    {
        var metrics = new DrawingTypographyMetrics(16, 8, 10.5, 7, 8, 7);

        metrics.Update(18, 9, 12, 8, 10, 9);

        Assert.Equal(18, metrics.CabinetNameFontSize);
        Assert.Equal(9, metrics.LineNameFontSize);
        Assert.Equal(12, metrics.IntervalNumberFontSize);
        Assert.Equal(8, metrics.SwitchNumberFontSize);
        Assert.Equal(10, metrics.PoleNumberFontSize);
        Assert.Equal(9, metrics.PTLabelFontSize);
    }

    [Fact]
    public void SwitchScale_DoesNotChangeCabinetOrIntervalBounds()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(3);
        RingCabinetLayout baseline = new RingCabinetLayoutFactory(
            WithSwitchScale(1)).Create(cabinet, new DocumentPoint(10, 20));
        RingCabinetLayout enlarged = new RingCabinetLayoutFactory(
            WithSwitchScale(2)).Create(cabinet, new DocumentPoint(10, 20));

        Assert.Equal(baseline.Position, enlarged.Position);
        Assert.Equal(baseline.WidthMillimeters, enlarged.WidthMillimeters);
        Assert.Equal(baseline.HeightMillimeters, enlarged.HeightMillimeters);
        Assert.Equal(
            baseline.IntervalLayouts.Values.Select(layout =>
                (layout.RelativePosition, layout.WidthMillimeters, layout.HeightMillimeters)),
            enlarged.IntervalLayouts.Values.Select(layout =>
                (layout.RelativePosition, layout.WidthMillimeters, layout.HeightMillimeters)));
        Assert.All(enlarged.IntervalLayouts.Values, enlargedInterval =>
        {
            RingCabinetIntervalLayout baselineInterval = baseline.IntervalLayouts[
                enlargedInterval.IntervalId];
            Assert.All(enlargedInterval.SwitchLayouts.Values, enlargedSwitch =>
                Assert.Equal(
                    baselineInterval.SwitchLayouts[enlargedSwitch.SwitchDeviceId]
                        .HeightMillimeters * 2,
                    enlargedSwitch.HeightMillimeters));
        });
    }

    [Fact]
    public void LoadSwitchContactCircle_IsBelowAndTangentToFixedContactLine()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(3);
        RingCabinetInterval interval = cabinet.Intervals.Single(candidate =>
            candidate.BayIndex == 1);
        RingCabinetLayout layout = CreateLayout(cabinet);
        RingCabinetIntervalLayout intervalLayout = layout.IntervalLayouts[interval.IntervalId];
        double centerX = layout.Position.XMillimeters +
                         intervalLayout.RelativePosition.XMillimeters +
                         intervalLayout.WidthMillimeters / 2;
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet, layout);
        SceneEllipse contact = Assert.Single(elements.OfType<SceneEllipse>(), ellipse =>
            Math.Abs(
                ellipse.Bounds.XMillimeters + ellipse.Bounds.WidthMillimeters / 2 -
                centerX) < 0.001);
        SceneLine tangent = Assert.Single(elements.OfType<SceneLine>(), line =>
            Math.Abs(line.Start.YMillimeters - contact.Bounds.YMillimeters) < 0.001 &&
            Math.Abs(line.End.YMillimeters - contact.Bounds.YMillimeters) < 0.001 &&
            line.Start.XMillimeters < contact.Bounds.XMillimeters &&
            line.End.XMillimeters >
            contact.Bounds.XMillimeters + contact.Bounds.WidthMillimeters);

        Assert.Equal(contact.Bounds.YMillimeters, tangent.Start.YMillimeters, 6);
        Assert.True(contact.Bounds.YMillimeters + contact.Bounds.HeightMillimeters >
                    tangent.Start.YMillimeters);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void IntegratedCabinet_UsesSameStandardIntervalSystem(int intervalCount)
    {
        RingCabinet cabinet = CreateIntegratedCabinet(
            intervalCount,
            GroundingStructureKind.UpperLowerGrounding);
        RingCabinetLayout layout = CreateLayout(cabinet);

        Assert.Equal(intervalCount, layout.IntervalLayouts.Count);
        Assert.All(
            layout.IntervalLayouts.Values,
            interval => Assert.Equal(
                DrawingMetrics.Default.RingCabinet.StandardIntervalWidth,
                interval.WidthMillimeters));
    }

    [Fact]
    public void IntegratedAndPTIntervalNumbersShareBreakerLevel()
    {
        RingCabinet cabinet = CreateCabinet(
            "Integrated with PT",
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                1,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                2,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open),
            RingCabinetIntervalDefinition.CreatePT(
                4,
                SwitchState.Open,
                SwitchState.Open));
        RingCabinetLayout layout = CreateLayout(cabinet);
        LabelRequest[] labels = new RingCabinetSymbol(new SymbolLibrary())
            .CreateLabelRequests(cabinet, layout)
            .Where(request => request.TargetKind == LabelTargetKind.Interval)
            .ToArray();
        RingCabinetInterval integrated = cabinet.Intervals.Single(interval =>
            interval.BayIndex == 1);
        RingCabinetInterval pt = cabinet.Intervals.Single(interval =>
            interval.IntervalKind == IntervalKind.PTInterval);
        SwitchDevice breaker = integrated.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);
        RingCabinetSwitchLayout breakerLayout = layout.IntervalLayouts[integrated.IntervalId]
            .SwitchLayouts[breaker.Id];
        LabelRequest integratedLabel = Assert.Single(labels, label =>
            label.TargetId == integrated.IntervalId);
        LabelRequest ptLabel = Assert.Single(labels, label =>
            label.TargetId == pt.IntervalId);

        Assert.Equal(
            layout.Position.YMillimeters +
            layout.IntervalLayouts[integrated.IntervalId].RelativePosition.YMillimeters +
            breakerLayout.RelativePosition.YMillimeters,
            integratedLabel.Anchor.YMillimeters);
        Assert.Equal(integratedLabel.Anchor.YMillimeters, ptLabel.Anchor.YMillimeters);
    }

    [Fact]
    public void RingCabinet_HasContinuousBusbarAndCenteredNameAnchor()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(4, "NK1191");
        RingCabinetLayout layout = CreateLayout(cabinet);
        var symbol = new RingCabinetSymbol(new SymbolLibrary());

        IReadOnlyList<SceneElement> elements = symbol.CreateElements(cabinet, layout);
        double busY = layout.Position.YMillimeters + layout.MainBusYMillimeters;
        SceneLine busbar = Assert.Single(elements.OfType<SceneLine>(), line =>
            line.Start.YMillimeters == busY &&
            line.End.YMillimeters == busY &&
            line.ThicknessMillimeters == DrawingMetrics.Default.RingCabinet.BusbarHeight);
        RingCabinetIntervalLayout first = layout.IntervalLayouts.Values
            .OrderBy(interval => interval.RelativePosition.XMillimeters)
            .First();
        RingCabinetIntervalLayout last = layout.IntervalLayouts.Values
            .OrderBy(interval => interval.RelativePosition.XMillimeters)
            .Last();

        Assert.Equal(
            layout.Position.XMillimeters + first.RelativePosition.XMillimeters,
            busbar.Start.XMillimeters);
        Assert.Equal(
            layout.Position.XMillimeters + last.RelativePosition.XMillimeters +
            last.WidthMillimeters,
            busbar.End.XMillimeters);
        Assert.Equal(
            layout.Position.XMillimeters + layout.WidthMillimeters / 2,
            Assert.Single(
                symbol.CreateLabelRequests(cabinet, layout),
                request => request.TargetId == cabinet.Id).Anchor.XMillimeters);
    }

    [Fact]
    public void RingCabinet_UsesLogicalBoundsWithoutVisibleCabinetOrIntervalFrames()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(3);
        RingCabinetLayout layout = CreateLayout(cabinet);
        DrawingScene scene = new DrawingSceneBuilder().Build(cabinet, layout);

        SceneLogicalBounds logicalBounds = Assert.Single(
            scene.Elements.OfType<SceneLogicalBounds>());
        Assert.Equal(
            new DocumentRect(
                layout.Position.XMillimeters,
                layout.Position.YMillimeters,
                layout.WidthMillimeters,
                layout.HeightMillimeters),
            logicalBounds.Bounds);
        Assert.Empty(scene.Elements.OfType<SceneRectangle>());
        Assert.True(DrawingSceneBoundsCalculator.TryCalculate(scene, out DocumentRect bounds));
        Assert.True(bounds.WidthMillimeters >= layout.WidthMillimeters);
        Assert.True(bounds.HeightMillimeters >= layout.HeightMillimeters);
    }

    [Theory]
    [InlineData(SwitchKind.LoadSwitch)]
    [InlineData(SwitchKind.GroundSwitch)]
    public void LoadSwitchInterval_OpenAndClosedStatesProduceDifferentGeometry(
        SwitchKind switchKind)
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(1);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        SwitchDevice switchDevice = interval.SwitchDevices.Single(device =>
            device.SwitchKind == switchKind);
        RingCabinetLayout layout = CreateLayout(cabinet);
        var renderer = new RingCabinetRenderer();

        SceneLine[] openGeometry = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();
        interval.SwitchAssembly.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        SceneLine[] closedGeometry = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();

        Assert.NotEqual(openGeometry, closedGeometry);
    }

    [Fact]
    public void LoadSwitchInterlock_RejectedOperationLeavesSceneGeometryUnchanged()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(1);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        SwitchDevice loadSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.LoadSwitch);
        SwitchDevice groundSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        interval.SwitchAssembly.ChangeSwitchState(loadSwitch.Id, SwitchState.Closed);
        RingCabinetLayout layout = CreateLayout(cabinet);
        var renderer = new RingCabinetRenderer();
        SceneElement[] before = renderer.Render(cabinet, layout).ToArray();

        Assert.Throws<InvalidOperationException>(() =>
            interval.SwitchAssembly.ChangeSwitchState(groundSwitch.Id, SwitchState.Closed));

        SceneElementAssertions.Equal(before, renderer.Render(cabinet, layout));
    }

    [Theory]
    [InlineData(SwitchKind.CircuitBreaker)]
    [InlineData(SwitchKind.IsolationSwitch)]
    [InlineData(SwitchKind.GroundSwitch)]
    public void IntegratedFeeder_OpenAndClosedStatesProduceDifferentGeometry(
        SwitchKind switchKind)
    {
        RingCabinet cabinet = CreateIntegratedCabinet(
            1,
            GroundingStructureKind.UpperLowerGrounding);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        SwitchDevice switchDevice = interval.SwitchDevices.Single(device =>
            device.SwitchKind == switchKind);
        RingCabinetLayout layout = CreateLayout(cabinet);
        var renderer = new RingCabinetRenderer();

        SceneLine[] openGeometry = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();
        interval.SwitchAssembly.ChangeSwitchState(switchDevice.Id, SwitchState.Closed);
        SceneLine[] closedGeometry = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();

        Assert.NotEqual(openGeometry, closedGeometry);
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding, false)]
    [InlineData(GroundingStructureKind.UpperLowerGrounding, false)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding, true)]
    public void IntegratedFeeder_GroundingStructureControlsUpperAndLowerDeviceOrder(
        GroundingStructureKind structure,
        bool breakerIsUpper)
    {
        RingCabinet cabinet = CreateIntegratedCabinet(1, structure);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        RingCabinetIntervalLayout layout = CreateLayout(cabinet)
            .IntervalLayouts[interval.IntervalId];
        SwitchDevice isolation = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.IsolationSwitch);
        SwitchDevice breaker = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);

        double isolationY = layout.SwitchLayouts[isolation.Id].RelativePosition.YMillimeters;
        double breakerY = layout.SwitchLayouts[breaker.Id].RelativePosition.YMillimeters;

        Assert.Equal(breakerIsUpper, breakerY < isolationY);
    }

    [Fact]
    public void UpperLowerGrounding_AloneUsesLowerCompactGroundContact()
    {
        RingCabinet cabinet = CreateIntegratedCabinet(
            4,
            GroundingStructureKind.UpperLowerGrounding);
        RingCabinetInterval interval = cabinet.Intervals.Single(candidate =>
            candidate.BayIndex == 1);
        RingCabinetIntervalLayout intervalLayout = CreateLayout(cabinet)
            .IntervalLayouts[interval.IntervalId];
        SwitchDevice breaker = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.CircuitBreaker);
        SwitchDevice ground = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        RingCabinetSwitchLayout breakerLayout = intervalLayout.SwitchLayouts[breaker.Id];
        RingCabinetSwitchLayout groundLayout = intervalLayout.SwitchLayouts[ground.Id];
        double scaledContactRadius = DrawingMetrics.Default.Switch.ContactRadius *
                                     DrawingMetrics.Default.RingCabinet.SwitchSymbolScale;
        double breakerInset = Math.Max(
            scaledContactRadius,
            Math.Min(
                breakerLayout.HeightMillimeters / 4,
                DrawingMetrics.Default.Switch.StandardSwitchLength *
                DrawingMetrics.Default.RingCabinet.SwitchSymbolScale / 4));
        double breakerBottom = breakerLayout.RelativePosition.YMillimeters +
                               breakerLayout.HeightMillimeters - breakerInset;
        double groundY = groundLayout.RelativePosition.YMillimeters +
                         groundLayout.HeightMillimeters / 2;
        double mainX = breakerLayout.RelativePosition.XMillimeters +
                       breakerLayout.WidthMillimeters / 2;
        double groundInset = Math.Max(
            scaledContactRadius,
            Math.Min(
                groundLayout.WidthMillimeters / 4,
                DrawingMetrics.Default.Switch.GroundSwitchLength *
                DrawingMetrics.Default.RingCabinet.SwitchSymbolScale / 4));
        double groundX = groundLayout.RelativePosition.XMillimeters +
                         groundLayout.WidthMillimeters - groundInset;

        Assert.True(groundY > breakerBottom);
        Assert.True(Math.Abs(mainX - groundX) <
                    DrawingMetrics.Default.RingCabinet.DeviceVerticalSpacing);

        RingCabinetLayout cabinetLayout = CreateLayout(cabinet);
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(
            cabinet,
            cabinetLayout);
        DocumentPoint origin = new(
            cabinetLayout.Position.XMillimeters +
            intervalLayout.RelativePosition.XMillimeters,
            cabinetLayout.Position.YMillimeters +
            intervalLayout.RelativePosition.YMillimeters);
        double terminalTop = origin.YMillimeters + intervalLayout.HeightMillimeters -
                             DrawingMetrics.Default.CableTermination.TriangleHeight;
        double expectedBranchY = origin.YMillimeters +
                                 (breakerBottom + terminalTop -
                                  origin.YMillimeters) / 2;

        Assert.Equal(expectedBranchY, origin.YMillimeters + groundY, 3);
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            Math.Abs(line.Start.YMillimeters - expectedBranchY) < 0.001 &&
            Math.Abs(line.End.YMillimeters - expectedBranchY) < 0.001 &&
            Math.Min(line.Start.XMillimeters, line.End.XMillimeters) <
            origin.XMillimeters + mainX);
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            Math.Abs(line.Start.YMillimeters - line.End.YMillimeters) < 0.001 &&
            Math.Abs(Math.Abs(line.Start.XMillimeters - line.End.XMillimeters) -
                     scaledContactRadius * 1.5) < 0.001);
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            Math.Abs(Math.Max(line.Start.YMillimeters, line.End.YMillimeters) -
                     expectedBranchY) < 0.001 &&
            Math.Abs(line.Start.YMillimeters - line.End.YMillimeters) > 0.001 &&
            Math.Abs(Math.Abs(line.Start.XMillimeters - line.End.XMillimeters) -
                     groundLayout.WidthMillimeters / 4) < 0.001);

        DrawingScene scene = new DrawingSceneBuilder().Build(cabinet, cabinetLayout);
        SelectionHitTestEntry hit = Assert.Single(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == SelectionTargetKind.Device &&
            entry.Target.ObjectId == ground.Id);
        Assert.Equal(
            expectedBranchY,
            hit.Bounds.YMillimeters + hit.Bounds.HeightMillimeters / 2,
            3);

        LabelRequest groundNumber = Assert.Single(
            new RingCabinetSymbol(new SymbolLibrary())
                .CreateLabelRequests(cabinet, cabinetLayout),
            request => request.TargetKind == LabelTargetKind.SwitchDevice &&
                       request.TargetId == ground.Id);
        Assert.True(groundNumber.Offset.YMillimeters < 0);
    }

    [Theory]
    [InlineData(GroundingStructureKind.UpperIsolationGrounding)]
    [InlineData(GroundingStructureKind.LowerLowerGrounding)]
    public void OtherGroundingStructuresKeepOriginalGroundLayout(
        GroundingStructureKind structure)
    {
        RingCabinet cabinet = CreateIntegratedCabinet(4, structure);
        RingCabinetInterval interval = cabinet.Intervals.Single(candidate =>
            candidate.BayIndex == 1);
        RingCabinetIntervalLayout intervalLayout = CreateLayout(cabinet)
            .IntervalLayouts[interval.IntervalId];
        SwitchDevice ground = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        RingCabinetSwitchLayout groundLayout = intervalLayout.SwitchLayouts[ground.Id];
        double expectedX = DrawingMetrics.Default.RingCabinet.StandardIntervalWidth / 2 -
                           DrawingMetrics.Default.RingCabinet.DeviceVerticalSpacing / 2 -
                           DrawingMetrics.Default.Switch.GroundSwitchLength *
                           DrawingMetrics.Default.RingCabinet.SwitchSymbolScale;

        Assert.Equal(expectedX, groundLayout.RelativePosition.XMillimeters);
        double scaledContactRadius = DrawingMetrics.Default.Switch.ContactRadius *
                                     DrawingMetrics.Default.RingCabinet.SwitchSymbolScale;
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(
            cabinet,
            CreateLayout(cabinet));
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            Math.Abs(line.Start.YMillimeters - line.End.YMillimeters) < 0.001 &&
            Math.Abs(Math.Abs(line.Start.XMillimeters - line.End.XMillimeters) -
                     scaledContactRadius * 3) < 0.001);
    }

    [Fact]
    public void IntegratedFeeder_RunningDisconnectedAndGroundedCombinationsAreVisuallyDistinct()
    {
        IReadOnlyList<SceneElement> running = RenderIntegratedState(
            SwitchState.Closed,
            SwitchState.Closed,
            SwitchState.Open);
        IReadOnlyList<SceneElement> disconnected = RenderIntegratedState(
            SwitchState.Open,
            SwitchState.Open,
            SwitchState.Open);
        IReadOnlyList<SceneElement> grounded = RenderIntegratedState(
            SwitchState.Open,
            SwitchState.Open,
            SwitchState.Closed);

        Assert.NotEqual(
            running.OfType<SceneLine>().ToArray(),
            disconnected.OfType<SceneLine>().ToArray());
        Assert.NotEqual(
            disconnected.OfType<SceneLine>().ToArray(),
            grounded.OfType<SceneLine>().ToArray());
        Assert.NotEqual(
            running.OfType<SceneLine>().ToArray(),
            grounded.OfType<SceneLine>().ToArray());
    }

    [Fact]
    public void PTInterval_UsesTwoCoilsLabelAndCommonBusbarBranch()
    {
        RingCabinet cabinet = CreatePTCabinet();
        RingCabinetLayout layout = CreateLayout(cabinet);
        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet, layout);
        double diameter = DrawingMetrics.Default.PT.CoilRadius * 2;

        Assert.Equal(2, elements.OfType<SceneEllipse>().Count(ellipse =>
            ellipse.Bounds.WidthMillimeters == diameter &&
            ellipse.Bounds.HeightMillimeters == diameter));
        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "PT");
        Assert.Empty(elements.OfType<SceneRectangle>());
        Assert.Contains(elements.OfType<SceneLine>(), line =>
            line.Start.YMillimeters == layout.Position.YMillimeters +
            layout.MainBusYMillimeters);

        SceneEllipse lowerCoil = elements.OfType<SceneEllipse>()
            .Where(ellipse => ellipse.Bounds.WidthMillimeters == diameter)
            .OrderByDescending(ellipse => ellipse.Bounds.YMillimeters)
            .First();
        SceneText ptLabel = Assert.Single(elements.OfType<SceneText>(), text => text.Text == "PT");
        Assert.True(ptLabel.Origin.YMillimeters >
            lowerCoil.Bounds.YMillimeters + lowerCoil.Bounds.HeightMillimeters);
        Assert.Equal(
            DrawingMetrics.Default.Typography.PTLabelFontSize,
            ptLabel.FontSizeMillimeters);

        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        RingCabinetIntervalLayout intervalLayout = layout.IntervalLayouts[interval.IntervalId];
        LabelRequest intervalNumber = Assert.Single(
            new RingCabinetSymbol(new SymbolLibrary()).CreateLabelRequests(cabinet, layout),
            request => request.TargetKind == LabelTargetKind.Interval);
        Assert.Equal(
            layout.Position.YMillimeters +
            intervalLayout.RelativePosition.YMillimeters +
            DrawingMetrics.Default.RingCabinet.BusbarOffset -
            DrawingMetrics.Default.RingCabinet.CabinetPadding +
            DrawingMetrics.Default.RingCabinet.DeviceVerticalSpacing +
            DrawingMetrics.Default.Switch.LogicalHitHeight *
            DrawingMetrics.Default.RingCabinet.SwitchSymbolScale +
            DrawingMetrics.Default.RingCabinet.DeviceVerticalSpacing,
            intervalNumber.Anchor.YMillimeters);
    }

    [Fact]
    public void SceneSelection_PreservesCabinetIntervalAndEverySwitchTarget()
    {
        RingCabinet cabinet = CreateIntegratedCabinet(
            1,
            GroundingStructureKind.LowerLowerGrounding);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        DrawingScene scene = new DrawingSceneBuilder().Build(cabinet, CreateLayout(cabinet));

        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == SelectionTargetKind.RingCabinet &&
            entry.Target.ObjectId == cabinet.Id);
        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == SelectionTargetKind.RingCabinetInterval &&
            entry.Target.ObjectId == interval.IntervalId);
        Assert.All(interval.SwitchDevices, switchDevice =>
        {
            SelectionHitTestEntry entry = Assert.Single(scene.HitTestIndex.Entries, candidate =>
                candidate.Target.Kind == SelectionTargetKind.Device &&
                candidate.Target.ObjectId == switchDevice.Id);
            SelectionReference? hit = scene.HitTestIndex.HitTest(new DocumentPoint(
                entry.Bounds.XMillimeters + entry.Bounds.WidthMillimeters / 2,
                entry.Bounds.YMillimeters + entry.Bounds.HeightMillimeters / 2));
            Assert.Equal(switchDevice.Id, hit?.ObjectId);
        });
    }

    [Fact]
    public void TerminalAnchors_MatchCableTerminationTipsWithoutChangingTerminalIds()
    {
        RingCabinet cabinet = CreateCabinet(
            "Anchor cabinet",
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                1,
                SwitchState.Open,
                SwitchState.Open),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                2,
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Open,
                SwitchState.Open,
                SwitchState.Open),
            RingCabinetIntervalDefinition.CreatePT(
                3,
                SwitchState.Open,
                SwitchState.Open));
        var document = new DrawingDocument(Guid.NewGuid(), "Anchor document");
        document.AddDevice(cabinet);
        RingCabinetLayout layout = CreateLayout(cabinet);
        Guid[] stableIds = cabinet.Intervals.Select(interval => interval.ExternalTerminalId).ToArray();

        TerminalAnchorIndex anchors = TerminalAnchorIndex.Build(
            document,
            new DrawingLayout(),
            new Dictionary<Guid, RingCabinetLayout> { [cabinet.Id] = layout });

        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            Assert.True(anchors.TryGet(interval.ExternalTerminalId, out TerminalAnchor anchor));
            RingCabinetIntervalLayout intervalLayout = layout.IntervalLayouts[interval.IntervalId];
            double expectedX = interval.IntervalKind == IntervalKind.PTInterval
                ? layout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters +
                  intervalLayout.PTSymbolPosition!.Value.XMillimeters +
                  DrawingMetrics.Default.PT.CoilRadius
                : layout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters +
                  intervalLayout.WidthMillimeters / 2;
            Assert.Equal(expectedX, anchor.Position.XMillimeters);
            Assert.Equal(
                layout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters +
                intervalLayout.HeightMillimeters,
                anchor.Position.YMillimeters);
            Assert.Equal(TerminalAnchorDirection.Down, anchor.Direction);
        }

        Assert.Equal(stableIds, cabinet.Intervals.Select(interval => interval.ExternalTerminalId));
    }

    [Fact]
    public void SwitchCommand_UndoAndRedoRestoreProfessionalGeometryAndStableId()
    {
        RingCabinet cabinet = CreateLoadSwitchCabinet(1);
        var document = new DrawingDocument(Guid.NewGuid(), "Switch geometry command");
        document.AddDevice(cabinet);
        RingCabinetInterval interval = cabinet.Intervals.Single(
            candidate => candidate.BayIndex == 1);
        SwitchDevice loadSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.LoadSwitch);
        Guid stableId = loadSwitch.Id;
        RingCabinetLayout layout = CreateLayout(cabinet);
        var renderer = new RingCabinetRenderer();
        SceneLine[] original = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();
        var command = new ChangeSwitchStateCommand(document, stableId, SwitchState.Closed);

        command.Execute();
        SceneLine[] changed = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();
        command.Undo();
        SceneLine[] undone = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();
        command.Redo();
        SceneLine[] redone = renderer.Render(cabinet, layout).OfType<SceneLine>().ToArray();

        Assert.NotEqual(original, changed);
        Assert.Equal(original, undone);
        Assert.Equal(changed, redone);
        Assert.Equal(stableId, loadSwitch.Id);
    }

    private static IReadOnlyList<SceneElement> RenderIntegratedState(
        SwitchState isolation,
        SwitchState breaker,
        SwitchState ground)
    {
        RingCabinet cabinet = CreateCabinet(
            "Integrated state",
            Enumerable.Range(1, 4)
                .Select(index => index == 1
                    ? RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                        index,
                        GroundingStructureKind.UpperIsolationGrounding,
                        isolation,
                        breaker,
                        ground)
                    : RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                        index,
                        GroundingStructureKind.UpperIsolationGrounding,
                        SwitchState.Open,
                        SwitchState.Open,
                        SwitchState.Open))
                .ToArray());
        return new RingCabinetRenderer().Render(cabinet, CreateLayout(cabinet));
    }

    private static RingCabinet CreateLoadSwitchCabinet(
        int intervalCount,
        string name = "Load switch cabinet") =>
        CreateCabinet(
            name,
            Enumerable.Range(1, Math.Max(intervalCount, 3))
                .Select(index => RingCabinetIntervalDefinition.CreateLoadSwitch(
                    index,
                    SwitchState.Open,
                    SwitchState.Open))
                .ToArray());

    private static RingCabinet CreateIntegratedCabinet(
        int intervalCount,
        GroundingStructureKind structure,
        SwitchState isolation = SwitchState.Open,
        SwitchState breaker = SwitchState.Open,
        SwitchState ground = SwitchState.Open) =>
        CreateCabinet(
            "Integrated cabinet",
            Enumerable.Range(1, Math.Max(intervalCount, 4))
                .Select(index => index == 1
                    ? RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                        index,
                        structure,
                        isolation,
                        breaker,
                        ground)
                    : RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                        index,
                        structure,
                        SwitchState.Open,
                        SwitchState.Open,
                        SwitchState.Open))
                .ToArray());

    private static RingCabinet CreatePTCabinet() =>
        CreateCabinet(
            "PT cabinet",
            RingCabinetIntervalDefinition.CreatePT(
                1,
                SwitchState.Closed,
                SwitchState.Open));

    private static RingCabinet CreateCabinet(
        string name,
        params RingCabinetIntervalDefinition[] intervals) =>
        RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), name, intervals));

    private static RingCabinetLayout CreateLayout(RingCabinet cabinet) =>
        new RingCabinetLayoutFactory().Create(cabinet, new DocumentPoint(20, 30));

    private static DrawingMetrics WithSwitchScale(double scale)
    {
        RingCabinetDrawingMetrics source = DrawingMetrics.Default.RingCabinet;
        return DrawingMetrics.Default with
        {
            RingCabinet = new RingCabinetDrawingMetrics(
                source.CabinetPadding,
                source.StandardIntervalWidth,
                source.StandardIntervalHeight,
                source.BusbarOffset,
                source.BusbarHeight,
                source.IntervalSpacing,
                source.CabinetNameOffset,
                source.DeviceVerticalSpacing,
                scale)
        };
    }
}
