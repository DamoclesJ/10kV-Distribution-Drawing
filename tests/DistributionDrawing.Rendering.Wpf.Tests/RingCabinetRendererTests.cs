using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Metrics;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using DistributionDrawing.Rendering.Wpf.Symbols;
using DistributionDrawing.Rendering.Wpf.Symbols.Library;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetRendererTests
{
    [Fact]
    public void Render_UsesEditableCabinetAndLineNamesWithCategoryFontSizes()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));
        cabinet.Rename("NK1991");
        cabinet.RenameLineName("10kV 奥东783线路");
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        SceneText[] labels = new RingCabinetRenderer().Render(cabinet, layout)
            .OfType<SceneText>()
            .ToArray();

        Assert.Contains(labels, label =>
            label.Text == "NK1991" &&
            label.FontSizeMillimeters ==
            DrawingMetrics.Default.Typography.CabinetNameFontSize);
        Assert.Contains(labels, label =>
            label.Text == "10kV 奥东783线路" &&
            label.FontSizeMillimeters ==
            DrawingMetrics.Default.Typography.LineNameFontSize);
        Assert.Contains(labels, label =>
            label.Text == "负1" &&
            label.FontSizeMillimeters ==
            DrawingMetrics.Default.Typography.IntervalNumberFontSize);
    }

    [Fact]
    public void Render_ConventionalCabinetCreatesProfessionalSwitchGeometryPerInterval()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(
            cabinet,
            layout);

        Assert.Empty(elements.OfType<SceneRectangle>());
        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            SwitchDevice loadSwitch = interval.SwitchDevices.Single(device =>
                device.SwitchKind == SwitchKind.LoadSwitch);
            Assert.Single(elements.OfType<SceneEllipse>(), ellipse =>
                IsInsideSwitchBounds(layout, interval, loadSwitch, ellipse));

            SwitchDevice groundSwitch = interval.SwitchDevices.Single(device =>
                device.SwitchKind == SwitchKind.GroundSwitch);
            Assert.DoesNotContain(elements.OfType<SceneEllipse>(), ellipse =>
                IsInsideSwitchBounds(layout, interval, groundSwitch, ellipse));
            Assert.True(HasSwitchGeometry(elements, layout, interval, groundSwitch));
        }

        Assert.Equal(3, elements.OfType<ScenePolyline>().Count(polyline => polyline.IsClosed));
        Assert.DoesNotContain(elements.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
    }

    [Fact]
    public void Render_IntegratedCabinetCreatesProfessionalSwitchGeometryPerInterval()
    {
        RingCabinet cabinet = BuildCabinet(
            Enumerable.Range(1, 6)
                .Select(index => new BayTemplate(
                    index,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding)))
                .ToArray());
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(
            cabinet,
            layout);

        Assert.Empty(elements.OfType<SceneRectangle>());
        foreach (RingCabinetInterval interval in cabinet.Intervals)
        {
            foreach (SwitchDevice switchDevice in interval.SwitchDevices)
            {
                Assert.True(HasSwitchGeometry(elements, layout, interval, switchDevice));
            }
        }

        Assert.Equal(6, elements.OfType<ScenePolyline>().Count(polyline => polyline.IsClosed));
        Assert.DoesNotContain(elements.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
    }

    private static bool IsInsideSwitchBounds(
        RingCabinetLayout cabinetLayout,
        RingCabinetInterval interval,
        SwitchDevice switchDevice,
        SceneEllipse ellipse) =>
        Contains(
            GetSwitchBounds(cabinetLayout, interval, switchDevice),
            new DocumentPoint(
                ellipse.Bounds.XMillimeters + ellipse.Bounds.WidthMillimeters / 2,
                ellipse.Bounds.YMillimeters + ellipse.Bounds.HeightMillimeters / 2));

    private static bool HasSwitchGeometry(
        IReadOnlyList<SceneElement> elements,
        RingCabinetLayout cabinetLayout,
        RingCabinetInterval interval,
        SwitchDevice switchDevice)
    {
        if (switchDevice.SwitchKind == SwitchKind.GroundSwitch)
        {
            RingCabinetIntervalLayout intervalLayout =
                cabinetLayout.IntervalLayouts[interval.IntervalId];
            DocumentPoint origin = new(
                cabinetLayout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
                cabinetLayout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
            double right = origin.XMillimeters + intervalLayout.WidthMillimeters;
            return elements.OfType<SceneLine>().Any(line =>
                Math.Abs(line.Start.YMillimeters - line.End.YMillimeters) < 0.001 &&
                line.Start.YMillimeters > cabinetLayout.Position.YMillimeters +
                    cabinetLayout.MainBusYMillimeters &&
                Math.Min(line.Start.XMillimeters, line.End.XMillimeters) >= origin.XMillimeters &&
                Math.Max(line.Start.XMillimeters, line.End.XMillimeters) <= right);
        }

        DocumentRect bounds = GetSwitchBounds(cabinetLayout, interval, switchDevice);
        return elements.OfType<SceneLine>().Any(line =>
            Contains(bounds, Midpoint(line.Start, line.End)));
    }

    private static DocumentRect GetSwitchBounds(
        RingCabinetLayout cabinetLayout,
        RingCabinetInterval interval,
        SwitchDevice switchDevice)
    {
        RingCabinetIntervalLayout intervalLayout = cabinetLayout.IntervalLayouts[interval.IntervalId];
        RingCabinetSwitchLayout switchLayout = intervalLayout.SwitchLayouts[switchDevice.Id];
        DocumentPoint origin = new(
            cabinetLayout.Position.XMillimeters + intervalLayout.RelativePosition.XMillimeters,
            cabinetLayout.Position.YMillimeters + intervalLayout.RelativePosition.YMillimeters);
        return new DocumentRect(
            origin.XMillimeters + switchLayout.RelativePosition.XMillimeters,
            origin.YMillimeters + switchLayout.RelativePosition.YMillimeters,
            switchLayout.WidthMillimeters,
            switchLayout.HeightMillimeters);
    }

    private static DocumentPoint Midpoint(DocumentPoint first, DocumentPoint second) =>
        new(
            (first.XMillimeters + second.XMillimeters) / 2,
            (first.YMillimeters + second.YMillimeters) / 2);

    private static bool Contains(DocumentRect bounds, DocumentPoint point) =>
        point.XMillimeters >= bounds.XMillimeters &&
        point.XMillimeters <= bounds.XMillimeters + bounds.WidthMillimeters &&
        point.YMillimeters >= bounds.YMillimeters &&
        point.YMillimeters <= bounds.YMillimeters + bounds.HeightMillimeters;

    [Fact]
    public void Render_ReflectsSwitchStateChanges()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        var renderer = new RingCabinetRenderer();
        SwitchDevice loadSwitch = cabinet.Intervals
            .Single(item => item.BayIndex == 1)
            .SwitchDevices
            .Single(device => device.SwitchKind == SwitchKind.LoadSwitch);
        cabinet.Intervals
            .Single(item => item.BayIndex == 1)
            .SwitchAssembly
            .ChangeSwitchState(loadSwitch.Id, SwitchState.Closed);

        IReadOnlyList<SceneElement> running = renderer.Render(cabinet, layout);
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 1);
        interval.SwitchAssembly.ChangeSwitchState(loadSwitch.Id, SwitchState.Open);
        IReadOnlyList<SceneElement> open = renderer.Render(cabinet, layout);

        Assert.DoesNotContain(open.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
        Assert.NotEqual(
            running.OfType<SceneLine>().ToArray(),
            open.OfType<SceneLine>().ToArray());
    }

    [Fact]
    public void Render_PTIntervalCreatesSwitchAndPTSymbols()
    {
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "PT cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Closed,
                    SwitchState.Open,
                    "负7 PT间隔")]));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet, layout);

        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "PT");
        Assert.DoesNotContain(elements.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
        Assert.Equal(
            [SwitchState.Closed, SwitchState.Open],
            cabinet.Intervals.Single().SwitchDevices.Select(device => device.SwitchState));

        SceneEllipse topCoil = elements.OfType<SceneEllipse>()
            .OrderBy(ellipse => ellipse.Bounds.YMillimeters)
            .First(ellipse => ellipse.Bounds.WidthMillimeters ==
                DrawingMetrics.Default.PT.CoilRadius * 2);
        double coilCenterX = topCoil.Bounds.XMillimeters +
                             topCoil.Bounds.WidthMillimeters / 2;
        Assert.DoesNotContain(elements.OfType<SceneLine>(), line =>
            line.Start.XMillimeters == coilCenterX &&
            line.End.XMillimeters == coilCenterX &&
            Math.Max(line.Start.YMillimeters, line.End.YMillimeters) >
                topCoil.Bounds.YMillimeters &&
            Math.Min(line.Start.YMillimeters, line.End.YMillimeters) <
                topCoil.Bounds.YMillimeters + topCoil.Bounds.HeightMillimeters);
        ScenePolyline terminal = Assert.Single(elements.OfType<ScenePolyline>(), polyline =>
            polyline.IsClosed && polyline.Points.Count == 3);
        Assert.Equal(
            DrawingMetrics.Default.CableTermination.TriangleWidth,
            terminal.Points.Max(point => point.XMillimeters) -
            terminal.Points.Min(point => point.XMillimeters));
        Assert.Equal(
            DrawingMetrics.Default.CableTermination.TriangleHeight,
            terminal.Points.Max(point => point.YMillimeters) -
            terminal.Points.Min(point => point.YMillimeters));
    }

    [Fact]
    public void Render_PTIntervalReflectsSwitchStateWithoutChangingDomain()
    {
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "PT cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Closed,
                    SwitchState.Open)]));
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        Guid intervalId = interval.IntervalId;
        Guid[] switchIds = interval.SwitchDevices.Select(device => device.Id).ToArray();
        SwitchState?[] states = interval.SwitchDevices.Select(device => device.SwitchState).ToArray();
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        new RingCabinetRenderer().Render(cabinet, layout);

        Assert.Equal(intervalId, interval.IntervalId);
        Assert.Equal(switchIds, interval.SwitchDevices.Select(device => device.Id));
        Assert.Equal(states, interval.SwitchDevices.Select(device => device.SwitchState));
    }

    [Fact]
    public void Render_IntegratedCabinetWithPTIntervalCreatesAllIntervalSymbols()
    {
        var definitions = Enumerable.Range(1, 6)
            .Select(index => RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                index,
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open))
            .Append(RingCabinetIntervalDefinition.CreatePT(
                7,
                SwitchState.Closed,
                SwitchState.Open,
                "负7 PT间隔"))
            .ToArray();
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(Guid.NewGuid(), "Integrated PT cabinet", definitions));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(cabinet, layout);

        Assert.Contains(elements.OfType<SceneText>(), text => text.Text == "PT");
        Assert.DoesNotContain(elements.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
    }

    [Fact]
    public void Render_PTIntervalReflectsIsolationAndGroundSwitchStateChanges()
    {
        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "PT cabinet",
                [RingCabinetIntervalDefinition.CreatePT(
                    7,
                    SwitchState.Closed,
                    SwitchState.Open)]));
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        SwitchDevice isolation = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.IsolationSwitch);
        SwitchDevice ground = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        var renderer = new RingCabinetRenderer();

        IReadOnlyList<SceneElement> initial = renderer.Render(cabinet, layout);
        interval.SwitchAssembly.ChangeSwitchState(isolation.Id, SwitchState.Open);
        interval.SwitchAssembly.ChangeSwitchState(ground.Id, SwitchState.Closed);
        IReadOnlyList<SceneElement> grounded = renderer.Render(cabinet, layout);

        Assert.NotEqual(
            initial.OfType<SceneLine>().ToArray(),
            grounded.OfType<SceneLine>().ToArray());
        Assert.DoesNotContain(initial.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
        Assert.DoesNotContain(grounded.OfType<SceneText>(), text =>
            text.Text is "合" or "分");
    }

    [Fact]
    public void Render_IncludesCabinetAndIntervalLabels()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new IntegratedFeederConfiguration(
                GroundingStructureKind.UpperLowerGrounding)));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneText> labels = new RingCabinetRenderer()
            .Render(cabinet, layout)
            .OfType<SceneText>()
            .ToArray();

        Assert.Contains(labels, label => label.Text == "Rendering Test Cabinet");
        Assert.Equal(1, labels.Count(label => label.Text == "Rendering Test Cabinet"));
        Assert.Contains(labels, label => label.Text == "负1");
        Assert.Contains(labels, label => label.Text == "负2");
        Assert.DoesNotContain(labels, label => label.Text is "1#" or "2#");
    }

    [Fact]
    public void RingCabinetSymbol_DoesNotCreateDirectBusinessLabels()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(
                3,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding)),
            new BayTemplate(
                4,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding)),
            new BayTemplate(
                5,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding)),
            new BayTemplate(
                6,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding)));
        RingCabinetInterval interval = cabinet.Intervals.Single(item => item.BayIndex == 3);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneText> labels = new RingCabinetSymbol(new SymbolLibrary())
            .CreateElements(cabinet, layout, includeLabels: false)
            .OfType<SceneText>()
            .ToArray();

        Assert.DoesNotContain(labels, label => label.Text == cabinet.DisplayName);
        Assert.DoesNotContain(labels, label => label.Text == interval.DisplayName);
        Assert.DoesNotContain(labels, label => label.Text == "3#");
        Assert.DoesNotContain(
            labels,
            label => interval.SwitchDevices.Any(device => device.DisplayName == label.Text));
        Assert.DoesNotContain(labels, label => label.Text is "合" or "分");
    }

    [Fact]
    public void LowLevelIntervalSymbolsDoNotCreateLegacyLabels()
    {
        RingCabinet[] cabinets =
        [
            BuildCabinet(
                new BayTemplate(1, new LoadSwitchConfiguration()),
                new BayTemplate(2, new LoadSwitchConfiguration()),
                new BayTemplate(3, new LoadSwitchConfiguration())),
            BuildCabinet(new BayTemplate(
                2,
                new IntegratedFeederConfiguration(
                    GroundingStructureKind.UpperLowerGrounding)),
                new BayTemplate(
                    3,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding)),
                new BayTemplate(
                    4,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding)),
                new BayTemplate(
                    5,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding))),
            RingCabinet.Create(
                RingCabinetDefinition.Create(
                    Guid.NewGuid(),
                    "PT cabinet",
                    [RingCabinetIntervalDefinition.CreatePT(
                        5,
                        SwitchState.Closed,
                        SwitchState.Open,
                        "PT interval")]))
        ];
        var layoutFactory = new RingCabinetLayoutFactory();

        foreach (RingCabinet cabinet in cabinets)
        {
            RingCabinetInterval interval = cabinet.Intervals[0];
            RingCabinetLayout cabinetLayout = layoutFactory.Create(
                cabinet,
                new DocumentPoint(0, 0));
            RingCabinetIntervalLayout intervalLayout =
                cabinetLayout.IntervalLayouts[interval.IntervalId];

            IReadOnlyList<SceneText> labels = new RingCabinetSymbol(new SymbolLibrary())
                .IntervalSymbol
                .CreateElements(
                    interval,
                    intervalLayout,
                    cabinetLayout.Position,
                    includeLabels: true)
                .OfType<SceneText>()
                .ToArray();

            Assert.DoesNotContain(labels, label => label.Text.EndsWith("#"));
            Assert.DoesNotContain(labels, label => label.Text == interval.DisplayName);
            Assert.DoesNotContain(
                labels,
                label => interval.SwitchDevices.Any(device => device.DisplayName == label.Text));
            Assert.DoesNotContain(labels, label => label.Text is "合" or "分");
        }
    }

    [Fact]
    public void Render_PTIntervalUsesDomainBusinessNumbersAtAnyBayIndex()
    {
        foreach (int bayIndex in new[] { 1, 3, 5 })
        {
            RingCabinet cabinet = RingCabinet.Create(
                RingCabinetDefinition.Create(
                    Guid.NewGuid(),
                    "PT cabinet",
                    [RingCabinetIntervalDefinition.CreatePT(
                        bayIndex,
                        SwitchState.Closed,
                        SwitchState.Open,
                        $"PT {bayIndex}")]));
            RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
                cabinet,
                new DocumentPoint(0, 0));

            IReadOnlyList<string> labels = new RingCabinetRenderer()
                .Render(cabinet, layout)
                .OfType<SceneText>()
                .Select(label => label.Text)
                .ToArray();
            string intervalNumber = $"负{bayIndex}";

            Assert.Contains(intervalNumber, labels);
            Assert.Contains($"{intervalNumber}-2", labels);
            Assert.Contains($"{intervalNumber}-7", labels);
        }
    }

    [Fact]
    public void Render_IntegratedFeederUsesDomainBusinessNumbersForAllGroundingStructures()
    {
        foreach (GroundingStructureKind structureKind in Enum.GetValues<GroundingStructureKind>())
        {
            RingCabinet cabinet = BuildCabinet(
                new BayTemplate(
                    3,
                    new IntegratedFeederConfiguration(structureKind)),
                new BayTemplate(
                    4,
                    new IntegratedFeederConfiguration(structureKind)),
                new BayTemplate(
                    5,
                    new IntegratedFeederConfiguration(structureKind)),
                new BayTemplate(
                    6,
                    new IntegratedFeederConfiguration(structureKind)));
            RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
                cabinet,
                new DocumentPoint(0, 0));

            IReadOnlyList<string> labels = new RingCabinetRenderer()
                .Render(cabinet, layout)
                .OfType<SceneText>()
                .Select(label => label.Text)
                .ToArray();
            string isolationNumber = structureKind == GroundingStructureKind.LowerLowerGrounding
                ? "负3-2"
                : "负3-4";
            string groundNumber = structureKind == GroundingStructureKind.UpperIsolationGrounding
                ? "负3-47"
                : "负3-7";

            Assert.Contains("负3", labels);
            Assert.Contains(isolationNumber, labels);
            Assert.Contains(groundNumber, labels);
        }
    }

    [Fact]
    public void Render_LoadSwitchOnlyDisplaysDomainProvidedGroundSwitchNumber()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(3, new LoadSwitchConfiguration()),
            new BayTemplate(4, new LoadSwitchConfiguration()),
            new BayTemplate(5, new LoadSwitchConfiguration()));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<string> labels = new RingCabinetRenderer()
            .Render(cabinet, layout)
            .OfType<SceneText>()
            .Select(label => label.Text)
            .ToArray();

        Assert.Equal(1, labels.Count(label => label == "负3"));
        Assert.Contains("负3-7", labels);
        Assert.DoesNotContain(labels, label => label is "负3-2" or "负3-4" or "负3-47");
    }

    [Fact]
    public void Render_RingCabinetLabelsAreDeterministicAndDoNotChangeDomain()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()),
            new BayTemplate(3, new LoadSwitchConfiguration()));
        Guid cabinetId = cabinet.Id;
        Guid[] intervalIds = cabinet.Intervals.Select(interval => interval.IntervalId).ToArray();
        SwitchState?[] switchStates = cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.SwitchState)
            .ToArray();
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        RingCabinetRenderer renderer = new();

        DocumentPoint[] first = renderer.Render(cabinet, layout)
            .OfType<SceneText>()
            .Select(text => text.Origin)
            .ToArray();
        DocumentPoint[] second = renderer.Render(cabinet, layout)
            .OfType<SceneText>()
            .Select(text => text.Origin)
            .ToArray();

        Assert.Equal(first, second);
        Assert.Equal(cabinetId, cabinet.Id);
        Assert.Equal(intervalIds, cabinet.Intervals.Select(interval => interval.IntervalId));
        Assert.Equal(switchStates, cabinet.Intervals
            .SelectMany(interval => interval.SwitchDevices)
            .Select(device => device.SwitchState));
    }

    private static RingCabinet BuildCabinet(params BayTemplate[] bays)
    {
        var template = new RingCabinetTemplate(
            new TemplateId("test:ring-cabinet:rendering"),
            "Rendering Test Cabinet",
            RingCabinetTemplateType.Mixed,
            bays,
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);
        RingCabinetDomainBuildOutcome outcome =
            new RingCabinetTemplateDomainBuilder().Build(template, "Rendering Test Cabinet");
        return Assert.IsType<RingCabinetDomainBuildResult>(outcome.Result).Cabinet;
    }
}
