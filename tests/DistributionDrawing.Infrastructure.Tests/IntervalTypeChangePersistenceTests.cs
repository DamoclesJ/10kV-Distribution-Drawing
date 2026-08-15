using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Infrastructure.Persistence;
using Xunit;

namespace DistributionDrawing.Infrastructure.Tests;

public sealed class IntervalTypeChangePersistenceTests
{
    [Fact]
    public void IntegratedFeederToPT_RoundTripPreservesNumberingAndStableIds()
    {
        DrawingDocument document = CreateIntegratedDocument();
        RingCabinet cabinet = GetCabinet(document);
        RingCabinetInterval source = GetInterval(cabinet, 3);
        cabinet.ChangeIntervalType(source.IntervalId, IntervalKind.PTInterval);
        RingCabinetInterval expected = GetInterval(cabinet, 3);
        string filePath = CreateTemporaryPath();

        try
        {
            RingCabinet restored = RoundTrip(document, filePath);
            RingCabinetInterval actual = GetInterval(restored, 3);

            Assert.Equal(ProjectFileFormat.Version6, GetSavedVersion(filePath));
            Assert.Equal(cabinet.Id, restored.Id);
            AssertIntervalIdentity(expected, actual);
            Assert.Equal(IntervalKind.PTInterval, actual.IntervalKind);
            Assert.Equal("-3", actual.BusinessNumber);
            Assert.Equal("-3-2", NumberFor(actual, SwitchKind.IsolationSwitch));
            Assert.Equal("-3-7", NumberFor(actual, SwitchKind.GroundSwitch));
            Assert.Equal(
                expected.SwitchDevices.Select(device => device.Id),
                actual.SwitchDevices.Select(device => device.Id));
            Assert.Equal(
                expected.SwitchDevices.Select(device => device.TerminalIds),
                actual.SwitchDevices.Select(device => device.TerminalIds));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void PTToIntegratedFeeder_RoundTripPreservesStructureAndIds()
    {
        DrawingDocument document = CreatePTDocument();
        RingCabinet cabinet = GetCabinet(document);
        RingCabinetInterval source = GetInterval(cabinet, 3);
        cabinet.ChangeIntervalType(
            source.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.UpperLowerGrounding);
        RingCabinetInterval expected = GetInterval(cabinet, 3);
        string filePath = CreateTemporaryPath();

        try
        {
            RingCabinet restored = RoundTrip(document, filePath);
            RingCabinetInterval actual = GetInterval(restored, 3);

            AssertIntervalIdentity(expected, actual);
            Assert.Equal(GroundingStructureKind.UpperLowerGrounding, actual.GroundingStructureKind);
            Assert.Equal("-3", NumberFor(actual, SwitchKind.CircuitBreaker));
            Assert.Equal("-3-4", NumberFor(actual, SwitchKind.IsolationSwitch));
            Assert.Equal("-3-7", NumberFor(actual, SwitchKind.GroundSwitch));
            Assert.Equal(expected.SwitchAssembly.AssemblyId, actual.SwitchAssembly.AssemblyId);
            Assert.Equal(expected.SwitchDevices.Select(device => device.Id), actual.SwitchDevices.Select(device => device.Id));
            Assert.Equal(expected.SwitchDevices.Select(device => device.TerminalIds), actual.SwitchDevices.Select(device => device.TerminalIds));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void SameTypeGroundingChange_RoundTripPreservesNewInternalIds()
    {
        DrawingDocument document = CreateIntegratedDocument();
        RingCabinet cabinet = GetCabinet(document);
        RingCabinetInterval source = GetInterval(cabinet, 3);
        cabinet.ChangeIntervalType(
            source.IntervalId,
            IntervalKind.IntegratedFeederInterval,
            GroundingStructureKind.LowerLowerGrounding);
        RingCabinetInterval expected = GetInterval(cabinet, 3);
        string filePath = CreateTemporaryPath();

        try
        {
            RingCabinet restored = RoundTrip(document, filePath);
            RingCabinetInterval actual = GetInterval(restored, 3);

            AssertIntervalIdentity(expected, actual);
            Assert.Equal(GroundingStructureKind.LowerLowerGrounding, actual.GroundingStructureKind);
            Assert.Equal("-3-2", NumberFor(actual, SwitchKind.IsolationSwitch));
            Assert.Equal("-3-7", NumberFor(actual, SwitchKind.GroundSwitch));
            Assert.Equal(expected.SwitchAssembly.AssemblyId, actual.SwitchAssembly.AssemblyId);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void TypeChangeToLoadSwitch_RoundTripPreservesIdentityAndConfirmedNumber()
    {
        DrawingDocument document = CreatePTDocument();
        RingCabinet cabinet = GetCabinet(document);
        RingCabinetInterval source = GetInterval(cabinet, 3);
        cabinet.ChangeIntervalType(source.IntervalId, IntervalKind.LoadSwitchInterval);
        RingCabinetInterval expected = GetInterval(cabinet, 3);
        string filePath = CreateTemporaryPath();

        try
        {
            RingCabinet restored = RoundTrip(document, filePath);
            RingCabinetInterval actual = GetInterval(restored, 3);

            AssertIntervalIdentity(expected, actual);
            Assert.Equal(IntervalKind.LoadSwitchInterval, actual.IntervalKind);
            Assert.Equal("-3-7", NumberFor(actual, SwitchKind.GroundSwitch));
            Assert.Null(NumberFor(actual, SwitchKind.LoadSwitch));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    private static DrawingDocument CreateIntegratedDocument()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "Interval type persistence project");
        document.AddDevice(RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "Integrated cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateIntegratedFeeder(
                    3,
                    GroundingStructureKind.UpperIsolationGrounding,
                    SwitchState.Open,
                    SwitchState.Open,
                    SwitchState.Open)
            ])));
        return document;
    }

    private static DrawingDocument CreatePTDocument()
    {
        var document = new DrawingDocument(Guid.NewGuid(), "PT type persistence project");
        document.AddDevice(RingCabinet.Create(RingCabinetDefinition.Create(
            Guid.NewGuid(),
            "PT cabinet",
            [
                RingCabinetIntervalDefinition.CreateLoadSwitch(1, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreatePT(3, SwitchState.Open, SwitchState.Open),
                RingCabinetIntervalDefinition.CreateLoadSwitch(5, SwitchState.Open, SwitchState.Open)
            ])));
        return document;
    }

    private static RingCabinet RoundTrip(DrawingDocument document, string filePath)
    {
        var container = new ProjectFileContainer();
        container.Save(filePath, CreateFileDocument(document));
        ProjectFileDocument opened = container.Open(filePath);
        DrawingDocument restored = ProjectDomainMapper.ToDomain(opened.Domain!);
        return GetCabinet(restored);
    }

    private static ProjectFileDocument CreateFileDocument(DrawingDocument document)
    {
        return new ProjectFileDocument(
            ProjectFileManifest.Create(
                document.Id,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            new ProjectFileMetadata(document.Title),
            ProjectDomainMapper.ToDto(document),
            ProjectLayoutDto.Empty(document.Id),
            ProjectProfessionalDto.Empty(document.Id));
    }

    private static RingCabinet GetCabinet(DrawingDocument document)
    {
        return Assert.Single(document.Devices.OfType<RingCabinet>());
    }

    private static RingCabinetInterval GetInterval(RingCabinet cabinet, int bayIndex)
    {
        return Assert.Single(cabinet.Intervals, interval => interval.BayIndex == bayIndex);
    }

    private static string? NumberFor(RingCabinetInterval interval, SwitchKind kind)
    {
        SwitchDevice switchDevice = Assert.Single(
            interval.SwitchDevices,
            device => device.SwitchKind == kind);
        return interval.GetSwitchBusinessNumber(switchDevice.Id);
    }

    private static void AssertIntervalIdentity(
        RingCabinetInterval expected,
        RingCabinetInterval actual)
    {
        Assert.Equal(expected.IntervalId, actual.IntervalId);
        Assert.Equal(expected.Sequence, actual.Sequence);
        Assert.Equal(expected.BayIndex, actual.BayIndex);
        Assert.Equal(expected.CircuitNodeId, actual.CircuitNodeId);
        Assert.Equal(expected.IntermediateNodeId, actual.IntermediateNodeId);
        Assert.Equal(expected.EarthNodeId, actual.EarthNodeId);
        Assert.Equal(expected.ExternalTerminalId, actual.ExternalTerminalId);
    }

    private static int GetSavedVersion(string filePath)
    {
        var container = new ProjectFileContainer();
        return container.Open(filePath).Manifest.FormatVersion;
    }

    private static string CreateTemporaryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"distribution-drawing-interval-type-{Guid.NewGuid():N}.kvdrawing");
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
