using System.Reflection;
using DistributionDrawing.Application.Interaction;
using Xunit;

namespace DistributionDrawing.Application.Tests;

public sealed class SelectionRuntimeTests
{
    [Fact]
    public void Select_SetsCurrentTarget()
    {
        var service = new SelectionService();
        var target = new SelectionTarget(
            SelectionTargetKind.CableSegment,
            Guid.NewGuid());

        service.Select(target);

        Assert.Same(target, service.CurrentSelection);
    }

    [Fact]
    public void Clear_RemovesCurrentTarget()
    {
        var service = new SelectionService();
        service.Select(new SelectionTarget(
            SelectionTargetKind.Pole,
            Guid.NewGuid()));

        service.Clear();

        Assert.Null(service.CurrentSelection);
    }

    [Fact]
    public void SelectAndClear_RaiseSelectionChanged()
    {
        var service = new SelectionService();
        var changeCount = 0;
        service.SelectionChanged += (_, _) => changeCount++;

        service.Select(new SelectionTarget(
            SelectionTargetKind.RingCabinet,
            Guid.NewGuid()));
        service.Clear();

        Assert.Equal(2, changeCount);
    }

    [Fact]
    public void SelectionTarget_PreservesStableId()
    {
        Guid id = Guid.NewGuid();
        var target = new SelectionTarget(
            SelectionTargetKind.IntermediateTerminal,
            id);

        Assert.Equal(id, target.TargetId);
    }

    [Fact]
    public void SelectionTarget_DoesNotHoldDomainObjectReference()
    {
        Assembly domainAssembly = typeof(DistributionDrawing.Domain.Documents.DrawingDocument).Assembly;
        FieldInfo[] fields = typeof(SelectionTarget)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => !field.IsSpecialName)
            .ToArray();

        Assert.DoesNotContain(fields, field =>
            field.FieldType.Assembly == domainAssembly);
    }

    [Fact]
    public void SelectionTarget_SupportsAllFirstVersionKinds()
    {
        Assert.Equal(7, Enum.GetValues<SelectionTargetKind>().Length);
    }
}
