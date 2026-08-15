using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Rendering;
using DistributionDrawing.Rendering.Wpf.Scene;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class RingCabinetRealScenarioTests
{
    public static IEnumerable<object[]> NumberingScenarios()
    {
        yield return [
            "LoadSwitch",
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                3,
                SwitchState.Closed,
                SwitchState.Open,
                "负3负荷开关"),
            "-3-7"];
        yield return [
            "UpperIsolationGrounding",
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open),
            "-3-47"];
        yield return [
            "UpperLowerGrounding",
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                GroundingStructureKind.UpperLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open),
            "-3-7"];
        yield return [
            "LowerLowerGrounding",
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                3,
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open),
            "-3-7"];
    }

    public static IEnumerable<object[]> PTScenarios()
    {
        foreach (int bayIndex in new[] { 1, 3, 5 })
        {
            yield return [
                bayIndex,
                RingCabinetIntervalDefinition.CreatePT(
                    bayIndex,
                    SwitchState.Closed,
                    SwitchState.Open)];
        }
    }

    [Theory]
    [MemberData(nameof(NumberingScenarios))]
    public void Render_LoadSwitchAndIntegratedFeederNumbersComeFromDomain(
        string scenario,
        RingCabinetIntervalDefinition intervalDefinition,
        string expectedGroundNumber)
    {
        RingCabinet cabinet = CreateCabinet(intervalDefinition);
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        Guid[] stableIds = interval.SwitchDevices.Select(device => device.Id).ToArray();

        IReadOnlyList<SceneText> labels = Render(cabinet);

        Assert.Contains(labels, label => label.Text == interval.BusinessNumber);
        foreach (SwitchDevice switchDevice in interval.SwitchDevices)
        {
            string? businessNumber = interval.GetSwitchBusinessNumber(switchDevice.Id);
            if (businessNumber is not null)
            {
                Assert.Contains(labels, label => label.Text == businessNumber);
            }
        }

        SwitchDevice groundSwitch = interval.SwitchDevices.Single(device =>
            device.SwitchKind == SwitchKind.GroundSwitch);
        Assert.Equal(expectedGroundNumber, interval.GetSwitchBusinessNumber(groundSwitch.Id));
        Assert.Equal(stableIds, interval.SwitchDevices.Select(device => device.Id));
        Assert.NotEmpty(labels);
        _ = scenario;
    }

    [Theory]
    [MemberData(nameof(PTScenarios))]
    public void Render_PTNumbersFollowBayIndexNotPhysicalPosition(
        int bayIndex,
        RingCabinetIntervalDefinition intervalDefinition)
    {
        RingCabinet cabinet = CreateCabinet(intervalDefinition);
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        IReadOnlyList<SceneText> labels = Render(cabinet);

        Assert.Equal($"-{bayIndex}", interval.BusinessNumber);
        Assert.Contains(labels, label => label.Text == $"-{bayIndex}");
        Assert.Equal(
            $"-{bayIndex}-2",
            interval.GetSwitchBusinessNumber(interval.SwitchDevices.Single(device =>
                device.SwitchKind == SwitchKind.IsolationSwitch).Id));
        Assert.Equal(
            $"-{bayIndex}-7",
            interval.GetSwitchBusinessNumber(interval.SwitchDevices.Single(device =>
                device.SwitchKind == SwitchKind.GroundSwitch).Id));
        Assert.Contains(labels, label => label.Text == $"-{bayIndex}-2");
        Assert.Contains(labels, label => label.Text == $"-{bayIndex}-7");
    }

    [Fact]
    public void BuildCabinetScene_PreservesIntervalAndSwitchSelectionIds()
    {
        RingCabinet cabinet = CreateCabinet(
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                5,
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open));
        RingCabinetInterval interval = Assert.Single(cabinet.Intervals);
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(10, 10));

        DrawingScene scene = new DrawingSceneBuilder().Build(cabinet, layout);

        Assert.Contains(scene.HitTestIndex.Entries, entry =>
            entry.Target.Kind == DistributionDrawing.Rendering.Wpf.Interaction.SelectionTargetKind.RingCabinetInterval &&
            entry.Target.ObjectId == interval.IntervalId);
        foreach (SwitchDevice switchDevice in interval.SwitchDevices)
        {
            Assert.Contains(scene.HitTestIndex.Entries, entry =>
                entry.Target.Kind == DistributionDrawing.Rendering.Wpf.Interaction.SelectionTargetKind.Device &&
                entry.Target.ObjectId == switchDevice.Id);
        }
    }

    private static IReadOnlyList<SceneText> Render(RingCabinet cabinet)
    {
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(0, 0));
        return new RingCabinetRenderer()
            .Render(cabinet, layout)
            .OfType<SceneText>()
            .ToArray();
    }

    private static RingCabinet CreateCabinet(
        RingCabinetIntervalDefinition intervalDefinition)
    {
        return RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "真实场景环网柜",
                [intervalDefinition]));
    }
}
