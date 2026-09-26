using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Desktop.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using Xunit;

namespace DistributionDrawing.Desktop.Tests;

public sealed class WorkTicketDisplayTests
{
    [Fact]
    public void BoundaryDisplayUsesIntervalContextAndChineseSideWithoutTerminalInternals()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "边界显示");
        RingCabinet cabinet = RingCabinet.Create(RingCabinetDefinition.Create(Guid.NewGuid(), "1号环网柜",
            [RingCabinetIntervalDefinition.CreateLoadSwitch(3, SwitchState.Closed, SwitchState.Open),
             RingCabinetIntervalDefinition.CreateLoadSwitch(4, SwitchState.Closed, SwitchState.Open)]));
        drawing.AddDevice(cabinet);
        var device = cabinet.Intervals[0].SwitchDevices[0];

        string display = WorkTicketWorkspace.FormatBoundaryDisplay(drawing,
            new IsolationBoundary(device.Id, BoundarySide.Line, device.SecondTerminalId));

        Assert.Contains("1号环网柜", display);
        Assert.Contains("线路侧", display);
        Assert.DoesNotContain("Terminal", display);
        Assert.DoesNotContain("Guid", display);
    }

    [Theory]
    [InlineData(BoundarySide.Bus, "母线侧")]
    [InlineData(BoundarySide.Line, "线路侧")]
    [InlineData(BoundarySide.SmallerNumber, "小号侧")]
    [InlineData(BoundarySide.LargerNumber, "大号侧")]
    [InlineData(BoundarySide.Source, "电源侧")]
    [InlineData(BoundarySide.Load, "负荷侧")]
    public void BoundarySideLabelsAreChinese(BoundarySide side, string expected) =>
        Assert.Equal(expected, WorkTicketWorkspace.BoundarySideName(side));
}
