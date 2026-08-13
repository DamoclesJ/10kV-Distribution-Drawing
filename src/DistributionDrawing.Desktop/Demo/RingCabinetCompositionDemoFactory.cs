using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Rendering.Wpf.Layout;
using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Demo;

internal static class RingCabinetCompositionDemoFactory
{
    public static (RingCabinet Cabinet, RingCabinetLayout Layout) Create()
    {
        RingCabinetIntervalDefinition[] definitions =
        [
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                1,
                SwitchState.Closed,
                SwitchState.Open,
                "负荷开关间隔1"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                2,
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed,
                SwitchState.Open,
                SwitchState.Open,
                "一二次融合馈线"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                3,
                SwitchState.Open,
                SwitchState.Open,
                "负荷开关间隔3"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                4,
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open,
                "一二次融合间隔4")
        ];

        RingCabinet cabinet = RingCabinet.Create(
            RingCabinetDefinition.Create(
                Guid.NewGuid(),
                "混合型环网柜演示",
                definitions));
        RingCabinetLayout layout = new RingCabinetLayoutFactory().Create(
            cabinet,
            new DocumentPoint(45, 80));
        return (cabinet, layout);
    }
}
