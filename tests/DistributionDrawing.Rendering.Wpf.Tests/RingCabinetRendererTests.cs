using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Building;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetRendererTests
{
    [Fact]
    public void Render_ConventionalCabinetCreatesTwoSwitchSymbolsPerInterval()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()),
            new BayTemplate(2, new LoadSwitchConfiguration()));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));

        IReadOnlyList<SceneElement> elements = new RingCabinetRenderer().Render(
            cabinet,
            layout);

        Assert.Equal(
            4,
            elements.OfType<SceneRectangle>().Count(rectangle =>
                rectangle.Bounds.WidthMillimeters == 16 &&
                rectangle.Bounds.HeightMillimeters == 10));
        Assert.Equal(4, elements.OfType<SceneText>().Count(text =>
            text.Text is "合" or "分"));
    }

    [Fact]
    public void Render_IntegratedCabinetCreatesThreeSwitchSymbolsPerInterval()
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

        Assert.Equal(
            18,
            elements.OfType<SceneRectangle>().Count(rectangle =>
                rectangle.Bounds.WidthMillimeters == 16 &&
                rectangle.Bounds.HeightMillimeters == 10));
    }

    [Fact]
    public void Render_ReflectsSwitchStateChanges()
    {
        RingCabinet cabinet = BuildCabinet(
            new BayTemplate(1, new LoadSwitchConfiguration()));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        var renderer = new RingCabinetRenderer();

        IReadOnlyList<SceneElement> running = renderer.Render(cabinet, layout);
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        SwitchDevice loadSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.LoadSwitch);
        Assert.Contains(running.OfType<SceneText>(), text => text.Text == "合");

        interval.SwitchAssembly.ChangeSwitchState(loadSwitch.Id, SwitchState.Open);
        IReadOnlyList<SceneElement> open = renderer.Render(cabinet, layout);

        Assert.Contains(open.OfType<SceneText>(), text => text.Text == "分");
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
        Assert.Equal(2, elements.OfType<SceneText>().Count(text => text.Text is "合" or "分"));
        Assert.Equal(
            [SwitchState.Closed, SwitchState.Open],
            cabinet.Intervals.Single().SwitchDevices.Select(device => device.SwitchState));
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
        Assert.Equal(20, elements.OfType<SceneText>().Count(text => text.Text is "合" or "分"));
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

        Assert.Contains(initial.OfType<SceneText>(), text => text.Text == "合");
        Assert.Contains(initial.OfType<SceneText>(), text => text.Text == "分");
        Assert.Equal(2, grounded.OfType<SceneText>().Count(text => text.Text == "合"));
        Assert.Equal(0, grounded.OfType<SceneText>().Count(text => text.Text == "分"));
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
