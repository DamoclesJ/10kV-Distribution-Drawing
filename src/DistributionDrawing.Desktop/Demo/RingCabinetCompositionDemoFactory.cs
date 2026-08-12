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
                SwitchState.Closed,
                SwitchState.Open,
                "进线负荷开关"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                GroundingStructureKind.UpperIsolationGrounding,
                SwitchState.Closed,
                SwitchState.Open,
                SwitchState.Open,
                "一二次融合馈线"),
            RingCabinetIntervalDefinition.CreateLoadSwitch(
                SwitchState.Open,
                SwitchState.Open,
                "出线负荷开关"),
            RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                GroundingStructureKind.LowerLowerGrounding,
                SwitchState.Closed,
                SwitchState.Closed,
                SwitchState.Open,
                "融合联络馈线")
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
