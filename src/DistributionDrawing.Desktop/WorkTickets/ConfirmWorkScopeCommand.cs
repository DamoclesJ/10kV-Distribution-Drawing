using DistributionDrawing.Application.WorkScopes;
using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Documents;
using DistributionDrawing.Domain.Professional;
using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.WorkTickets;

internal enum ConfirmWorkScopeCommandStage
{
    ExecuteAfterScope,
    ExecuteAfterTicket,
    UndoAfterTicket,
    UndoAfterScope,
    RedoAfterScope,
    RedoAfterTicket
}

/// <summary>Applies a prepared WorkScope/Ticket snapshot as one atomic history command.</summary>
public sealed class ConfirmWorkScopeCommand : IEnergizationImpactCommand
{
    private readonly DrawingDocument _drawing;
    private readonly WorkTicketDataRoot _tickets;
    private readonly Guid _ticketId;
    private readonly WorkTicketSession? _beforeTicket;
    private readonly WorkTicketSession _afterTicket;
    private readonly WorkScope? _beforeWorkScope;
    private readonly WorkScope _afterWorkScope;
    private readonly Action<ConfirmWorkScopeCommandStage>? _afterMutation;

    public ConfirmWorkScopeCommand(
        DrawingDocument drawing,
        WorkTicketDataRoot tickets,
        WorkScopeConfirmationPlan plan)
        : this(drawing, tickets, plan, null)
    {
    }

    internal ConfirmWorkScopeCommand(
        DrawingDocument drawing,
        WorkTicketDataRoot tickets,
        WorkScopeConfirmationPlan plan,
        Action<ConfirmWorkScopeCommandStage>? afterMutation)
    {
        _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        ArgumentNullException.ThrowIfNull(plan);
        if (tickets.DocumentId != drawing.Id || plan.TicketId != plan.AfterTicket.Id)
            throw new ArgumentException("Confirmation plan does not match the target document/ticket.", nameof(plan));
        _ticketId = plan.TicketId;
        _beforeTicket = plan.BeforeTicket;
        _afterTicket = plan.AfterTicket;
        _beforeWorkScope = Clone(plan.BeforeWorkScope);
        _afterWorkScope = Clone(plan.AfterWorkScope)
            ?? throw new ArgumentException("Confirmation plan has no WorkScope snapshot.", nameof(plan));
        _afterMutation = afterMutation;
    }

    public bool AffectsEnergization => false;

    public void Execute() => ApplyForward(
        _beforeTicket, _beforeWorkScope,
        _afterTicket, _afterWorkScope,
        ConfirmWorkScopeCommandStage.ExecuteAfterScope,
        ConfirmWorkScopeCommandStage.ExecuteAfterTicket);

    public void Undo() => ApplyReverse(
        _afterTicket, _afterWorkScope,
        _beforeTicket, _beforeWorkScope,
        ConfirmWorkScopeCommandStage.UndoAfterTicket,
        ConfirmWorkScopeCommandStage.UndoAfterScope);

    public void Redo() => ApplyForward(
        _beforeTicket, _beforeWorkScope,
        _afterTicket, _afterWorkScope,
        ConfirmWorkScopeCommandStage.RedoAfterScope,
        ConfirmWorkScopeCommandStage.RedoAfterTicket);

    private void ApplyForward(
        WorkTicketSession? expectedTicket,
        WorkScope? expectedScope,
        WorkTicketSession targetTicket,
        WorkScope targetScope,
        ConfirmWorkScopeCommandStage afterScope,
        ConfirmWorkScopeCommandStage afterTicket)
    {
        EnsureState(expectedTicket, expectedScope);
        try
        {
            ApplyScope(targetScope);
            _afterMutation?.Invoke(afterScope);
            ApplyTicket(targetTicket);
            _afterMutation?.Invoke(afterTicket);
        }
        catch (Exception applyError)
        {
            try
            {
                ApplyTicket(expectedTicket);
                ApplyScope(expectedScope);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException(
                    "Confirm WorkScope failed and restoring its Before snapshots also failed.",
                    applyError, rollbackError);
            }
            throw;
        }
    }

    private void ApplyReverse(
        WorkTicketSession currentTicket,
        WorkScope currentScope,
        WorkTicketSession? targetTicket,
        WorkScope? targetScope,
        ConfirmWorkScopeCommandStage afterTicket,
        ConfirmWorkScopeCommandStage afterScope)
    {
        EnsureState(currentTicket, currentScope);
        try
        {
            ApplyTicket(targetTicket);
            _afterMutation?.Invoke(afterTicket);
            ApplyScope(targetScope);
            _afterMutation?.Invoke(afterScope);
        }
        catch (Exception applyError)
        {
            try
            {
                ApplyScope(currentScope);
                ApplyTicket(currentTicket);
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException(
                    "Undo Confirm WorkScope failed and restoring its After snapshots also failed.",
                    applyError, rollbackError);
            }
            throw;
        }
    }

    private void EnsureState(WorkTicketSession? expectedTicket, WorkScope? expectedScope)
    {
        if (!ReferenceEquals(_tickets.Selected(_ticketId), expectedTicket))
            throw new InvalidOperationException("The target WorkTicket changed after confirmation planning.");
        WorkScope? current = _drawing.WorkScopes.SingleOrDefault(item =>
            item.WorkScopeId == _afterWorkScope.WorkScopeId);
        if (!ScopeEquals(current, expectedScope))
            throw new InvalidOperationException("The target WorkScope changed after confirmation planning.");
    }

    private void ApplyTicket(WorkTicketSession? target)
    {
        WorkTicketSession? current = _tickets.Selected(_ticketId);
        if (target is null)
        {
            if (current is not null) _tickets.Remove(_ticketId);
        }
        else if (current is null)
        {
            _tickets.Add(target);
        }
        else
        {
            _tickets.Replace(target);
        }
    }

    private void ApplyScope(WorkScope? target)
    {
        Guid id = _afterWorkScope.WorkScopeId;
        WorkScope? current = _drawing.WorkScopes.SingleOrDefault(item => item.WorkScopeId == id);
        if (target is null)
        {
            if (current is not null) _drawing.RemoveWorkScope(id);
        }
        else if (current is null)
        {
            _drawing.AddWorkScope(Clone(target)!);
        }
        else
        {
            _drawing.UpdateWorkScope(id, target.Regions, target.Boundaries, target.Description);
        }
    }

    private static bool ScopeEquals(WorkScope? left, WorkScope? right)
    {
        if (left is null || right is null) return left is null && right is null;
        return left.WorkScopeId == right.WorkScopeId && left.Description == right.Description &&
            left.Regions.Count == right.Regions.Count &&
            left.Regions.Zip(right.Regions).All(pair =>
                pair.First.TerminalIds.SequenceEqual(pair.Second.TerminalIds) &&
                pair.First.ElectricalNodeIds.SequenceEqual(pair.Second.ElectricalNodeIds)) &&
            left.Boundaries.SequenceEqual(right.Boundaries);
    }

    private static WorkScope? Clone(WorkScope? scope) => scope is null ? null : WorkScope.Create(
        scope.WorkScopeId,
        scope.Regions.Select(region => new WorkScopeRegion(region.TerminalIds, region.ElectricalNodeIds)),
        scope.Boundaries,
        scope.Description);
}
