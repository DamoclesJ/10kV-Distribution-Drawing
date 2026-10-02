using DistributionDrawing.Application.Energization;
using DistributionDrawing.Application.GroundingSafety;
using DistributionDrawing.Application.Topology;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Domain.Topology;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class GroundingTargetEnergizationResolverTests
{
    [Theory]
    [InlineData(EnergizationState.Energized)]
    [InlineData(EnergizationState.Deenergized)]
    public void Resolve_TerminalUsesExactTerminalResult(EnergizationState expected)
    {
        Fixture fixture = CreateFixture();
        EnergizationResult result = CreateResult(fixture, expected, expected);

        GroundingTargetEnergizationResolution resolution = new GroundingTargetEnergizationResolver()
            .Resolve(fixture.Drawing, GroundingTarget.ForTerminal(fixture.StartTerminal.Id), result);

        Assert.True(resolution.IsSuccess);
        Assert.Equal(new GroundingElectricalIdentity(
            GroundingElectricalIdentityKind.Terminal, fixture.StartTerminal.Id), resolution.Identity);
        Assert.Equal(expected, resolution.State);
    }

    [Theory]
    [InlineData(EnergizationState.Energized)]
    [InlineData(EnergizationState.Deenergized)]
    public void Resolve_GapUsesConnectionAsIdentityForEveryPhysicalHalfEdge(EnergizationState expected)
    {
        Fixture fixture = CreateFixture();
        GroundingAccessPoint smaller = fixture.Drawing.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.Start.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        GroundingAccessPoint larger = fixture.Drawing.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.End.Id,
            GroundingAccessLineSide.LargerNumberSide);
        EnergizationResult result = CreateResult(fixture, expected, expected);
        var resolver = new GroundingTargetEnergizationResolver();

        GroundingTargetEnergizationResolution smallerResult = resolver.Resolve(
            fixture.Drawing, GroundingTarget.ForGroundingAccessPoint(smaller.GroundingAccessPointId), result);
        GroundingTargetEnergizationResolution largerResult = resolver.Resolve(
            fixture.Drawing, GroundingTarget.ForGroundingAccessPoint(larger.GroundingAccessPointId), result);

        var identity = new GroundingElectricalIdentity(
            GroundingElectricalIdentityKind.Connection, fixture.Connection.Id);
        Assert.True(smallerResult.IsSuccess);
        Assert.True(largerResult.IsSuccess);
        Assert.Equal(identity, smallerResult.Identity);
        Assert.Equal(identity, largerResult.Identity);
        Assert.Equal(expected, smallerResult.State);
        Assert.Equal(expected, largerResult.State);
    }

    [Fact]
    public void Resolve_RejectsMissingOrInconsistentElectricalEvidence()
    {
        Fixture fixture = CreateFixture();
        GroundingAccessPoint point = fixture.Drawing.CreateGroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id, fixture.Start.Id,
            GroundingAccessLineSide.SmallerNumberSide);
        var resolver = new GroundingTargetEnergizationResolver();

        GroundingTargetEnergizationResolution missingTerminal = resolver.Resolve(
            fixture.Drawing,
            GroundingTarget.ForTerminal(fixture.EndTerminal.Id),
            CreateResult(fixture, EnergizationState.Energized, EnergizationState.Energized,
                includeEndTerminal: false));
        GroundingTargetEnergizationResolution missingEdge = resolver.Resolve(
            fixture.Drawing,
            GroundingTarget.ForGroundingAccessPoint(point.GroundingAccessPointId),
            CreateResult(fixture, EnergizationState.Energized, EnergizationState.Energized,
                includeConnectionEdge: false));
        GroundingTargetEnergizationResolution splitConnection = resolver.Resolve(
            fixture.Drawing,
            GroundingTarget.ForGroundingAccessPoint(point.GroundingAccessPointId),
            CreateResult(fixture, EnergizationState.Energized, EnergizationState.Deenergized));

        Assert.False(missingTerminal.IsSuccess);
        Assert.False(missingEdge.IsSuccess);
        Assert.False(splitConnection.IsSuccess);
        Assert.Null(missingTerminal.State);
        Assert.Null(missingEdge.State);
        Assert.Null(splitConnection.State);
    }

    [Fact]
    public void ResolveGroundingAccessPoint_RejectsProvableConflictWithoutAddingCandidate()
    {
        Fixture fixture = CreateFixture();
        var conflicting = new GroundingAccessPoint(
            Guid.NewGuid(), fixture.Connection.Id, fixture.Middle.Id,
            GroundingAdjacentEndpoint.ForPole(fixture.Start.Id),
            GroundingAccessLineSide.LargerNumberSide);
        EnergizationResult result = CreateResult(
            fixture, EnergizationState.Energized, EnergizationState.Energized);

        GroundingTargetEnergizationResolution resolution = new GroundingTargetEnergizationResolver()
            .ResolveGroundingAccessPoint(fixture.Drawing, conflicting, result);

        Assert.False(resolution.IsSuccess);
        Assert.Contains("conflicts", resolution.FailureReason, StringComparison.Ordinal);
        Assert.Empty(fixture.Drawing.GroundingAccessPoints);
    }

    private static EnergizationResult CreateResult(
        Fixture fixture,
        EnergizationState startState,
        EnergizationState endState,
        bool includeEndTerminal = true,
        bool includeConnectionEdge = true)
    {
        var terminals = new Dictionary<Guid, EnergizationPointResult>
        {
            [fixture.StartTerminal.Id] = new(startState, [])
        };
        if (includeEndTerminal)
            terminals[fixture.EndTerminal.Id] = new(endState, []);
        ElectricalConnectivityEdge[] edges = includeConnectionEdge
            ? [new ElectricalConnectivityEdge(
                fixture.Connection.StartTerminalId,
                fixture.Connection.EndTerminalId,
                ElectricalConnectivityEdgeType.Connection,
                fixture.Connection.Id)]
            : [];
        return new EnergizationResult(
            EnergizationValidity.Complete,
            terminals,
            new Dictionary<Guid, EnergizationPointResult>(),
            [],
            edges,
            []);
    }

    private static Fixture CreateFixture()
    {
        var drawing = new DrawingDocument(Guid.NewGuid(), "GS resolver");
        var start = new Pole(Guid.NewGuid(), "P-10");
        var middle = new Pole(Guid.NewGuid(), "P-11");
        var end = new Pole(Guid.NewGuid(), "P-12");
        drawing.AddDevice(start);
        drawing.AddDevice(middle);
        drawing.AddDevice(end);
        Terminal startTerminal = start.CreateOverheadAnchorTerminal(Guid.NewGuid());
        Terminal endTerminal = end.CreateOverheadAnchorTerminal(Guid.NewGuid());
        drawing.AddTerminal(startTerminal);
        drawing.AddTerminal(endTerminal);
        var connection = new Connection(
            Guid.NewGuid(), ConnectionType.OverheadLine,
            startTerminal.Id, endTerminal.Id, "GS resolver line", "10kV");
        drawing.AddConnection(connection);
        drawing.AddOverheadLine(new OverheadLine(
            connection.Id, "JKLYJ", [start.Id, middle.Id, end.Id]));
        return new Fixture(drawing, start, middle, end, startTerminal, endTerminal, connection);
    }

    private sealed record Fixture(
        DrawingDocument Drawing,
        Pole Start,
        Pole Middle,
        Pole End,
        Terminal StartTerminal,
        Terminal EndTerminal,
        Connection Connection);
}
