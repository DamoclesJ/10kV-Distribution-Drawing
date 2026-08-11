using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Domain.Tests;

public sealed class OverheadLineTests
{
    [Fact]
    public void Overhead_line_detail_is_associated_with_its_connection()
    {
        var document = TestFixtures.CreateDocument();
        var startPole = new Pole(Guid.NewGuid(), "P-20");
        var supportPole = new Pole(Guid.NewGuid(), "P-21");
        var endPole = new Pole(Guid.NewGuid(), "P-22");
        var startAnchor = TestFixtures.CreatePoleAnchorTerminal(startPole);
        var endAnchor = TestFixtures.CreatePoleAnchorTerminal(endPole);

        document.AddDevice(startPole);
        document.AddDevice(supportPole);
        document.AddDevice(endPole);
        document.AddTerminal(startAnchor);
        document.AddTerminal(endAnchor);

        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            startAnchor.Id,
            endAnchor.Id,
            "架空线路",
            TestFixtures.TenKilovolts);
        document.AddConnection(connection);

        var overheadLine = new OverheadLine(
            connection.Id,
            "JKLYJ-10kV",
            126.5,
            [startPole.Id, supportPole.Id, endPole.Id],
            isContinued: true,
            continuationTerminalId: endAnchor.Id,
            continuationState: ContinuationState.Energized,
            continuationDescription: "继续至下级杆位");
        document.AddOverheadLine(overheadLine);

        Assert.Same(overheadLine, Assert.Single(document.OverheadLines));
        Assert.Equal(connection.Id, overheadLine.ConnectionId);
        Assert.Equal(ConnectionType.OverheadLine, connection.Type);
        Assert.Equal("JKLYJ-10kV", overheadLine.LineModel);
        Assert.Equal(126.5, overheadLine.LengthMeters);
        Assert.Equal(
            new[] { startPole.Id, supportPole.Id, endPole.Id },
            overheadLine.SupportPoleIds);
        Assert.Equal(ContinuationState.Energized, overheadLine.ContinuationState);
    }

    [Fact]
    public void Continuation_state_is_required_only_when_the_line_is_continued()
    {
        var pole = new Pole(Guid.NewGuid(), "P-23");

        Assert.Throws<ArgumentException>(() =>
            new OverheadLine(
                Guid.NewGuid(),
                "JKLYJ-10kV",
                [pole.Id],
                isContinued: true));

        Assert.Throws<ArgumentException>(() =>
            new OverheadLine(
                Guid.NewGuid(),
                "JKLYJ-10kV",
                [pole.Id],
                continuationState: ContinuationState.Unknown));
    }

    [Fact]
    public void Overhead_line_rejects_a_continuation_terminal_that_is_not_an_endpoint()
    {
        var document = TestFixtures.CreateDocument();
        var startPole = new Pole(Guid.NewGuid(), "P-24");
        var endPole = new Pole(Guid.NewGuid(), "P-25");
        var startAnchor = TestFixtures.CreatePoleAnchorTerminal(startPole);
        var endAnchor = TestFixtures.CreatePoleAnchorTerminal(endPole);

        document.AddDevice(startPole);
        document.AddDevice(endPole);
        document.AddTerminal(startAnchor);
        document.AddTerminal(endAnchor);

        var connection = new Connection(
            Guid.NewGuid(),
            ConnectionType.OverheadLine,
            startAnchor.Id,
            endAnchor.Id,
            "架空线路",
            TestFixtures.TenKilovolts);
        document.AddConnection(connection);

        var detail = new OverheadLine(
            connection.Id,
            "JKLYJ-10kV",
            [startPole.Id, endPole.Id],
            isContinued: true,
            continuationTerminalId: Guid.NewGuid(),
            continuationState: ContinuationState.Unknown);

        Assert.Throws<InvalidOperationException>(() => document.AddOverheadLine(detail));
    }
}
