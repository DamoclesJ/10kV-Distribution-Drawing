using DistributionDrawing.Rendering.Wpf.Interaction;
using Xunit;

namespace DistributionDrawing.Rendering.Wpf.Tests;

public sealed class SelectionSetTests
{
    private readonly SelectionReference _first = new(
        SelectionTargetKind.Device,
        Guid.NewGuid());
    private readonly SelectionReference _second = new(
        SelectionTargetKind.RingCabinet,
        Guid.NewGuid());
    private readonly SelectionReference _third = new(
        SelectionTargetKind.Connection,
        Guid.NewGuid());

    [Fact]
    public void EmptySingleReplaceAndClearPreserveSelectedCompatibilityProjection()
    {
        var manager = new SelectionManager();

        Assert.Empty(manager.SelectionSet.SelectedReferences);
        Assert.Null(manager.Selected);

        manager.Select(_first);
        Assert.Equal([_first], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_first, manager.Selected);
        Assert.True(manager.HasSingleSelection);

        manager.Select(_second);
        Assert.Equal([_second], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_second, manager.Selected);

        manager.Clear();
        Assert.Empty(manager.SelectionSet.SelectedReferences);
        Assert.Null(manager.Selected);
    }

    [Fact]
    public void AddAndToggleMaintainStableOrderAndPrimarySelection()
    {
        var manager = new SelectionManager();
        manager.Select(_first);

        manager.AddRange([_second, _third]);

        Assert.Equal([_first, _second, _third], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_third, manager.Selected);

        manager.Toggle(_second);
        Assert.Equal([_first, _third], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_third, manager.Selected);

        manager.Toggle(_third);
        Assert.Equal([_first], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_first, manager.Selected);
    }

    [Fact]
    public void ToggleExistingPrimaryFallsBackToLastRemainingSelection()
    {
        var manager = new SelectionManager();
        manager.Replace([_first, _second, _third]);

        manager.Toggle(_third);

        Assert.Equal([_first, _second], manager.SelectionSet.SelectedReferences);
        Assert.Equal(_second, manager.Selected);
    }

    [Fact]
    public void IdentityDeduplicationUsesTargetKindAndObjectId()
    {
        var manager = new SelectionManager();
        SelectionReference alternateParent = _first with { ParentId = Guid.NewGuid() };

        manager.Replace([_first, alternateParent, _second]);

        Assert.Equal([_first, _second], manager.SelectionSet.SelectedReferences);
    }

    [Fact]
    public void CompatibilityAndSetNotificationsFireOncePerRealChange()
    {
        var manager = new SelectionManager();
        int legacyChanges = 0;
        int setChanges = 0;
        int countChanges = 0;
        manager.SelectionChanged += (_, _) => legacyChanges++;
        manager.SelectionSetChanged += (_, _) => setChanges++;
        manager.SelectionCountChanged += (_, _) => countChanges++;

        manager.Select(_first);
        manager.Select(_first);
        manager.AddRange([_second]);
        manager.Toggle(_second);

        Assert.Equal(3, legacyChanges);
        Assert.Equal(3, setChanges);
        Assert.Equal(3, countChanges);
    }
}
