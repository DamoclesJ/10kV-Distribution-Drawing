using DistributionDrawing.Application.Devices.CustomerStations;
using DistributionDrawing.Domain.Devices.CustomerStations;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction.Professional;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class CustomerStationGroundingEligibilityTests
{
    [Fact]
    public void OnlyCableTerminal_IsEligibleForNewTerminalTarget()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.IndoorStation,
            ["主供", "备供"]);
        document.AddCustomerStation(station);

        Assert.All(station.IncomingFeeders, feeder =>
        {
            Assert.True(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                document,
                feeder.CableTerminalId));
            Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
                document,
                feeder.StationTerminalId));
        });
        Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            document,
            station.Id));
        Assert.False(ProfessionalCommandFactory.IsEligibleNewTerminalTarget(
            document,
            Guid.NewGuid()));
    }

    [Fact]
    public void CreateCommand_UsesFeederIdentityAndPreservesTerminalTarget()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.BoxStation,
            ["用户主供"]);
        document.AddCustomerStation(station);
        IncomingFeeder feeder = station.IncomingFeeders[0];
        var factory = new ProfessionalCommandFactory();
        var command = factory.CreateAddGroundingPoint(
            document,
            feeder.CableTerminalId);

        command.Execute();

        GroundingPoint groundingPoint = Assert.Single(document.GroundingPoints);
        Assert.Equal(
            GroundingTarget.ForTerminal(feeder.CableTerminalId),
            groundingPoint.Target);
        Assert.Equal("用户主供进线", groundingPoint.Location);
        Assert.Equal("S01", groundingPoint.Number);
        command.Undo();
        Assert.Empty(document.GroundingPoints);
        command.Redo();
        Assert.Equal(
            feeder.CableTerminalId,
            Assert.Single(document.GroundingPoints).TerminalId);
    }

    [Fact]
    public void CreateCommand_RejectsStationTerminal()
    {
        DrawingDocument document = CreateDocument();
        CustomerStation station = new CustomerStationCreationFactory().Create(
            StationKind.BoxStation,
            ["主供"]);
        document.AddCustomerStation(station);

        Assert.Throws<InvalidOperationException>(() =>
            new ProfessionalCommandFactory().CreateAddGroundingPoint(
                document,
                station.IncomingFeeders[0].StationTerminalId));
        Assert.Empty(document.GroundingPoints);
    }

    private static DrawingDocument CreateDocument() =>
        new(Guid.NewGuid(), "Customer station grounding tests");
}
