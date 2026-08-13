using System.Reflection;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Topology;

namespace DistributionDrawing.Domain.Tests;

internal static class TestFixtures
{
    public const string TenKilovolts = "10kV";

    public static DrawingDocument CreateDocument()
    {
        return new DrawingDocument(Guid.NewGuid(), "Domain test document");
    }

    public static CableTermination CreateCableTermination(
        Guid? id = null,
        Guid? cableSideTerminalId = null,
        Guid? overheadSideTerminalId = null,
        Guid? internalNodeId = null)
    {
        return new CableTermination(
            id ?? Guid.NewGuid(),
            cableSideTerminalId ?? Guid.NewGuid(),
            overheadSideTerminalId ?? Guid.NewGuid(),
            internalNodeId ?? Guid.NewGuid(),
            "电缆终端");
    }

    public static RingCabinet CreateLoadSwitchRingCabinet(
        IReadOnlyList<int> bayIndexes)
    {
        RingCabinetIntervalDefinition[] intervals = bayIndexes
            .Select(bayIndex => RingCabinetIntervalDefinition.CreateLoadSwitch(
                bayIndex,
                SwitchState.Open,
                SwitchState.Open,
                $"负{bayIndex}间隔"))
            .ToArray();

        return RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "测试环网柜",
            intervals));
    }

    public static ElectricalNode CreateCableTerminationNode(CableTermination termination)
    {
        return new ElectricalNode(
            termination.InternalNodeId,
            ElectricalNodeType.Intermediate,
            TopologyOwnerType.Device,
            termination.Id);
    }

    public static Terminal CreateCableSideTerminal(CableTermination termination)
    {
        return new Terminal(
            termination.CableSideTerminalId,
            TopologyOwnerType.Device,
            termination.Id,
            "CableSide",
            TenKilovolts,
            true,
            false,
            termination.InternalNodeId,
            [ConnectionType.Cable]);
    }

    public static Terminal CreateOverheadSideTerminal(CableTermination termination)
    {
        return new Terminal(
            termination.OverheadSideTerminalId,
            TopologyOwnerType.Device,
            termination.Id,
            "OverheadSide",
            TenKilovolts,
            true,
            false,
            termination.InternalNodeId,
            [ConnectionType.OverheadLine]);
    }

    public static SwitchDevice CreatePoleSwitch(
        SwitchKind switchKind = SwitchKind.CircuitBreaker)
    {
        ConstructorInfo constructor = typeof(SwitchDevice).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(Guid),
                    typeof(SwitchKind),
                    typeof(SwitchInstallationType),
                    typeof(Guid),
                    typeof(Guid),
                    typeof(SwitchState),
                    typeof(string),
                    typeof(string),
                    typeof(Guid?),
                    typeof(string)
                ],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "The existing SwitchDevice constructor could not be found.");

        return (SwitchDevice)constructor.Invoke(
        [
            Guid.NewGuid(),
            switchKind,
            SwitchInstallationType.Pole,
            Guid.NewGuid(),
            Guid.NewGuid(),
            SwitchState.Open,
            "柱上断路器",
            TenKilovolts,
            null,
            null
        ]);
    }

    public static Terminal CreatePoleAnchorTerminal(Pole pole, bool junction = false)
    {
        return pole.CreateOverheadAnchorTerminal(Guid.NewGuid(), junction);
    }

    public static void AddCableTerminationTopology(
        DrawingDocument document,
        CableTermination termination)
    {
        document.AddDevice(termination);
        document.AddElectricalNode(CreateCableTerminationNode(termination));
        document.AddTerminal(CreateCableSideTerminal(termination));
        document.AddTerminal(CreateOverheadSideTerminal(termination));
    }
}
