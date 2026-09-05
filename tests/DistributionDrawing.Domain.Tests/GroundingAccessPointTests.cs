using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class GroundingAccessPointTests
{
    [Fact]
    public void Create_PreservesStableIdentityAndRequiresOverheadLine()
    {
        Scenario scenario = CreateScenario();
        Guid id = Guid.NewGuid();
        GroundingAccessPoint point = scenario.Document.CreateGroundingAccessPoint(
            id, scenario.Connection.Id, scenario.Start.Id, scenario.Middle.Id,
            GroundingAccessLineSide.LargerNumberSide);

        Assert.Equal(id, point.GroundingAccessPointId);
        Assert.Throws<InvalidOperationException>(() => scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), Guid.NewGuid(), scenario.Start.Id, scenario.Middle.Id,
            GroundingAccessLineSide.SmallerNumberSide));
    }

    [Fact]
    public void Create_RejectsExistingNonOverheadConnection()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Pole start = AddPole(document, "P-20");
        Pole end = AddPole(document, "P-21");
        CableTermination startTermination = TestFixtures.CreateCableTermination();
        CableTermination endTermination = TestFixtures.CreateCableTermination();
        TestFixtures.AddCableTerminationTopology(document, startTermination);
        TestFixtures.AddCableTerminationTopology(document, endTermination);
        var cable = new Connection(
            Guid.NewGuid(), ConnectionType.Cable,
            startTermination.CableSideTerminalId, endTermination.CableSideTerminalId,
            "测试电缆", TestFixtures.TenKilovolts);
        document.AddConnection(cable);

        Assert.Throws<InvalidOperationException>(() => document.CreateGroundingAccessPoint(
            Guid.NewGuid(), cable.Id, start.Id, end.Id,
            GroundingAccessLineSide.SmallerNumberSide));
    }

    [Fact]
    public void Create_RejectsPoleOutsideSupportList()
    {
        Scenario scenario = CreateScenario();
        Pole unrelated = AddPole(scenario.Document, "P-99");

        Assert.Throws<InvalidOperationException>(() => scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, unrelated.Id, scenario.Start.Id,
            GroundingAccessLineSide.SmallerNumberSide));
    }

    [Fact]
    public void IntermediatePole_AllowsBothAdjacentHalfEdges_IndependentOfLineSide()
    {
        Scenario scenario = CreateScenario();
        int nodesBefore = scenario.Document.ElectricalNodes.Count;
        int terminalsBefore = scenario.Document.Terminals.Count;
        int connectionsBefore = scenario.Document.Connections.Count;

        GroundingAccessPoint first = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.Start.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingAccessPoint second = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.End.Id,
            GroundingAccessLineSide.LargerNumberSide);

        Assert.Equal(2, scenario.Document.GroundingAccessPoints.Count);
        Assert.NotEqual(first.AdjacentPoleId, second.AdjacentPoleId);
        Assert.Equal(nodesBefore, scenario.Document.ElectricalNodes.Count);
        Assert.Equal(terminalsBefore, scenario.Document.Terminals.Count);
        Assert.Equal(connectionsBefore, scenario.Document.Connections.Count);
        Assert.Single(scenario.Document.OverheadLines);
    }

    [Fact]
    public void Add_RejectsNonAdjacentPoleAndDuplicatePhysicalHalfEdge()
    {
        Scenario scenario = CreateScenario();
        Pole unrelated = AddPole(scenario.Document, "P-99");
        scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.Start.Id,
            GroundingAccessLineSide.SmallerNumberSide);

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Document.CreateGroundingAccessPoint(
                Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, unrelated.Id,
                GroundingAccessLineSide.LargerNumberSide));
        Assert.Throws<InvalidOperationException>(() =>
            scenario.Document.CreateGroundingAccessPoint(
                Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.Start.Id,
                GroundingAccessLineSide.LargerNumberSide));
        Assert.Single(scenario.Document.GroundingAccessPoints);
    }

    [Fact]
    public void OccupiedAccessPoint_BlocksPointAndLineDeletion_UntilGroundingPointRemoved()
    {
        Scenario scenario = CreateScenario();
        GroundingAccessPoint accessPoint = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint groundingPoint = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(),
            GroundingTarget.ForGroundingAccessPoint(accessPoint.GroundingAccessPointId),
            "大号侧",
            "L01");

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Document.RemoveGroundingAccessPoint(accessPoint.GroundingAccessPointId));
        Assert.Throws<InvalidOperationException>(() =>
            scenario.Document.RemoveOverheadLine(scenario.Connection.Id));

        scenario.Document.RemoveGroundingPoint(groundingPoint.GroundingPointId);
        Assert.Single(scenario.Document.GroundingAccessPoints);
        scenario.Document.RemoveOverheadLine(scenario.Connection.Id);
        Assert.Empty(scenario.Document.GroundingAccessPoints);
    }

    [Fact]
    public void GroundingPointNumber_EditRequiresUniqueNonEmptyValue()
    {
        Scenario scenario = CreateScenario();
        GroundingAccessPoint firstAccess = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.Start.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        GroundingAccessPoint secondAccess = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint first = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(firstAccess.GroundingAccessPointId),
            "小号侧", "L01");
        GroundingPoint second = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(secondAccess.GroundingAccessPointId),
            "大号侧", "L02");

        Assert.Throws<InvalidOperationException>(() => scenario.Document.UpdateGroundingPoint(
            second.GroundingPointId, second.Target, second.Location, " L01 ", second.Note));
        Assert.Throws<InvalidOperationException>(() => scenario.Document.UpdateGroundingPoint(
            second.GroundingPointId, second.Target, second.Location, " ", second.Note));
        Assert.Equal("L02", second.Number);
        Assert.Equal("L01", first.Number);
    }

    [Fact]
    public void GroundingTargetOccupancy_IsUniqueAcrossTypedTarget()
    {
        Scenario scenario = CreateScenario();
        GroundingAccessPoint access = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingTarget target = GroundingTarget.ForGroundingAccessPoint(access.GroundingAccessPointId);
        scenario.Document.CreateGroundingPoint(Guid.NewGuid(), target, "大号侧", "L01");

        Assert.Throws<InvalidOperationException>(() => scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), target, "大号侧", "L02"));
        Assert.Throws<InvalidOperationException>(() => scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForTerminal(scenario.Connection.StartTerminalId),
            "兼容端子", "L01"));
    }

    [Fact]
    public void GroundingPointDelete_PreservesAccessPoint()
    {
        Scenario scenario = CreateScenario();
        GroundingAccessPoint access = scenario.Document.CreateGroundingAccessPoint(
            Guid.NewGuid(), scenario.Connection.Id, scenario.Middle.Id, scenario.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        GroundingPoint grounding = scenario.Document.CreateGroundingPoint(
            Guid.NewGuid(), GroundingTarget.ForGroundingAccessPoint(access.GroundingAccessPointId),
            "大号侧", "L01");

        scenario.Document.RemoveGroundingPoint(grounding.GroundingPointId);

        Assert.Same(access, Assert.Single(scenario.Document.GroundingAccessPoints));
    }

    private static Scenario CreateScenario()
    {
        DrawingDocument document = TestFixtures.CreateDocument();
        Pole start = AddPole(document, "P-10");
        Pole middle = AddPole(document, "P-11");
        Pole end = AddPole(document, "P-12");
        Terminal startTerminal = TestFixtures.CreatePoleAnchorTerminal(start);
        Terminal endTerminal = TestFixtures.CreatePoleAnchorTerminal(end);
        document.AddTerminal(startTerminal);
        document.AddTerminal(endTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            startTerminal.Id, endTerminal.Id, "测试架空线", TestFixtures.TenKilovolts);
        document.AddConnection(connection);
        document.AddOverheadLine(new OverheadLine(
            connection.Id, "JKLYJ-10kV", [start.Id, middle.Id, end.Id]));
        return new Scenario(document, start, middle, end, connection);
    }

    private static Pole AddPole(DrawingDocument document, string number)
    {
        var pole = new Pole(Guid.NewGuid(), number);
        document.AddDevice(pole);
        return pole;
    }

    private sealed record Scenario(
        DrawingDocument Document,
        Pole Start,
        Pole Middle,
        Pole End,
        Connection Connection);
}
