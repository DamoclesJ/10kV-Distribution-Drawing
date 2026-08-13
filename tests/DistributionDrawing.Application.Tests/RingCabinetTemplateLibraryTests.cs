using DistributionDrawing.Application.Templates.RingCabinets;
using DistributionDrawing.Application.Templates.RingCabinets.Library;
using DistributionDrawing.Domain.Devices.RingCabinets;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class RingCabinetTemplateLibraryTests
{
    [Fact]
    public void Constructor_PreservesRegistrationOrder()
    {
        RingCabinetTemplate templateC = CreateTestTemplate("test:c");
        RingCabinetTemplate templateA = CreateTestTemplate("test:a");
        RingCabinetTemplate templateB = CreateTestTemplate("test:b");

        var library = new RingCabinetTemplateLibrary(
            [templateC, templateA, templateB]);

        Assert.Equal(
            new[] { templateC, templateA, templateB },
            library.Templates);
    }

    [Fact]
    public void TryGet_ReturnsRegisteredTemplate()
    {
        RingCabinetTemplate registered = CreateTestTemplate("test:registered");
        var library = new RingCabinetTemplateLibrary([registered]);

        bool found = library.TryGet(
            new TemplateId("test:registered"),
            out RingCabinetTemplate? template);

        Assert.True(found);
        Assert.Equal(registered, template);
    }

    [Fact]
    public void TryGet_ReturnsSameTemplateInstance()
    {
        RingCabinetTemplate registered = CreateTestTemplate("test:identity");
        var library = new RingCabinetTemplateLibrary([registered]);

        bool found = library.TryGet(
            registered.TemplateId,
            out RingCabinetTemplate? template);

        Assert.True(found);
        Assert.Same(registered, template);
        Assert.Same(registered, library.Templates[0]);
    }

    [Fact]
    public void TryGet_UnknownTemplateId_ReturnsFalse()
    {
        var library = new RingCabinetTemplateLibrary(
            [CreateTestTemplate("test:registered")]);

        bool found = library.TryGet(
            new TemplateId("test:unknown"),
            out RingCabinetTemplate? template);

        Assert.False(found);
        Assert.Null(template);
    }

    [Fact]
    public void Constructor_RejectsDuplicateTemplateId()
    {
        RingCabinetTemplate first = CreateTestTemplate("test:duplicate", "First");
        RingCabinetTemplate second = CreateTestTemplate("test:duplicate", "Second");

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new RingCabinetTemplateLibrary([first, second]));

        Assert.Contains("duplicate TemplateId", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsNullCollection()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RingCabinetTemplateLibrary(null!));
    }

    [Fact]
    public void Constructor_RejectsNullTemplateElement()
    {
        RingCabinetTemplate[] templates =
        [
            CreateTestTemplate("test:valid"),
            null!
        ];

        Assert.Throws<ArgumentException>(() =>
            new RingCabinetTemplateLibrary(templates));
    }

    [Fact]
    public void SourceCollectionMutation_DoesNotChangeLibrary()
    {
        RingCabinetTemplate registered = CreateTestTemplate("test:source");
        var source = new List<RingCabinetTemplate> { registered };
        var library = new RingCabinetTemplateLibrary(source);

        source.Clear();
        source.Add(CreateTestTemplate("test:replacement"));

        Assert.Single(library.Templates);
        Assert.Same(registered, library.Templates[0]);
        Assert.True(library.TryGet(registered.TemplateId, out _));
        Assert.False(library.TryGet(new TemplateId("test:replacement"), out _));
    }

    [Fact]
    public void Templates_CannotBeModifiedThroughPublicApi()
    {
        var library = new RingCabinetTemplateLibrary(
            [CreateTestTemplate("test:readonly")]);

        IList<RingCabinetTemplate> templates =
            Assert.IsAssignableFrom<IList<RingCabinetTemplate>>(library.Templates);

        Assert.Throws<NotSupportedException>(() =>
            templates.Add(CreateTestTemplate("test:added")));
        Assert.Throws<NotSupportedException>(() => templates.Clear());
        Assert.Throws<NotSupportedException>(() =>
            templates[0] = CreateTestTemplate("test:replacement"));
    }

    [Fact]
    public void CaseSensitivity_FollowsTemplateIdContract()
    {
        RingCabinetTemplate lower = CreateTestTemplate("ring-cabinet.test");
        RingCabinetTemplate upper = CreateTestTemplate("RING-CABINET.TEST");
        var library = new RingCabinetTemplateLibrary([lower, upper]);

        Assert.True(library.TryGet(
            new TemplateId("ring-cabinet.test"),
            out RingCabinetTemplate? lowerResult));
        Assert.True(library.TryGet(
            new TemplateId("RING-CABINET.TEST"),
            out RingCabinetTemplate? upperResult));
        Assert.Same(lower, lowerResult);
        Assert.Same(upper, upperResult);
    }

    [Fact]
    public void Library_PreservesNonSequentialBayIndexesAndCapabilities()
    {
        RingCabinetTemplate registered = CreateTestTemplate(
            "test:content",
            bays:
            [
                new BayTemplate(
                    10,
                    BayFunction.Incoming,
                    new LoadSwitchConfiguration()),
                new BayTemplate(
                    3,
                    BayFunction.Outgoing,
                    new IntegratedFeederConfiguration(
                        GroundingStructureKind.UpperLowerGrounding))
            ]);
        TemplateCapability[] capabilities = registered.RequiredCapabilities.ToArray();
        var library = new RingCabinetTemplateLibrary([registered]);

        Assert.True(library.TryGet(
            registered.TemplateId,
            out RingCabinetTemplate? result));
        Assert.Same(registered, result);
        Assert.Equal(new[] { 10, 3 }, result!.Bays.Select(bay => bay.Index));
        Assert.Equal(
            capabilities.OrderBy(capability => capability),
            result.RequiredCapabilities.OrderBy(capability => capability));
    }

    [Fact]
    public void TryGet_RejectsNullTemplateId()
    {
        var library = new RingCabinetTemplateLibrary(
            [CreateTestTemplate("test:null-id")]);

        Assert.Throws<ArgumentNullException>(() =>
            library.TryGet(null!, out _));
    }

    private static RingCabinetTemplate CreateTestTemplate(
        string id,
        string name = "Test-only template",
        IEnumerable<BayTemplate>? bays = null)
    {
        return new RingCabinetTemplate(
            new TemplateId(id),
            name,
            RingCabinetTemplateType.Mixed,
            bays ??
            [
                new BayTemplate(
                    1,
                    BayFunction.Incoming,
                    new LoadSwitchConfiguration())
            ],
            RingCabinetLayoutRule.Default,
            NoSecondaryConfiguration.Instance);
    }
}
