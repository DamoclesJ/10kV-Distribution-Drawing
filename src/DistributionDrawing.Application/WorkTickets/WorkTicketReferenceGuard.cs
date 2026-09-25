using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Application.WorkTickets;

public static class WorkTicketReferenceGuard
{
    public static void Validate(DrawingDocument drawing, WorkTicketDataRoot tickets)
    {
        foreach (WorkTicketSession ticket in tickets.Tickets)
        {
            IEnumerable<TicketReference> references = ticket.IsolationBoundaries.SelectMany(boundary =>
                new[] { new TicketReference(TicketReferenceKind.Device, boundary.DeviceId) }
                    .Concat(boundary.TerminalId is Guid terminal ?
                        [new TicketReference(TicketReferenceKind.Terminal, terminal)] : [])
                    .Concat(boundary.ConnectionId is Guid connection ?
                        [new TicketReference(TicketReferenceKind.Connection, connection)] : []))
                .Concat(ticket.WorkScopeIds.Select(id => new TicketReference(TicketReferenceKind.WorkScope, id)))
                .Concat(ticket.GroundingPointIds.Select(id => new TicketReference(TicketReferenceKind.GroundingPoint, id)))
                .Concat(ticket.UserFacts.SelectMany(fact => fact.References))
                .Concat(ticket.Draft?.Sections.SelectMany(section => section.Items)
                    .SelectMany(item => item.RelatedModelRefs) ?? [])
                .Concat(ticket.Analysis?.SwitchingMeasures.SelectMany(item => item.References) ?? [])
                .Concat(ticket.Analysis?.WorkGroupMeasures.SelectMany(item => item.References) ?? [])
                .Concat(ticket.Analysis?.RetainedLiveParts.SelectMany(item => item.References) ?? [])
                .Concat(ticket.Analysis?.RestorationMeasures.SelectMany(item => item.References) ?? []);
            foreach (TicketReference reference in references.Distinct())
            {
                bool exists = reference.Kind switch
                {
                    TicketReferenceKind.Device => drawing.Devices.Any(item => item.Id == reference.Id),
                    TicketReferenceKind.Terminal => drawing.Terminals.Any(item => item.Id == reference.Id),
                    TicketReferenceKind.Connection => drawing.Connections.Any(item => item.Id == reference.Id),
                    TicketReferenceKind.RingInterval => drawing.Devices.OfType<RingCabinet>()
                        .SelectMany(item => item.Intervals).Any(item => item.IntervalId == reference.Id),
                    TicketReferenceKind.GroundingPoint => drawing.GroundingPoints.Any(item => item.GroundingPointId == reference.Id),
                    TicketReferenceKind.GroundingAccessPoint => drawing.GroundingAccessPoints.Any(item => item.GroundingAccessPointId == reference.Id),
                    TicketReferenceKind.WorkScope => drawing.WorkScopes.Any(item => item.WorkScopeId == reference.Id),
                    _ => false
                };
                if (!exists)
                    throw new InvalidOperationException($"该对象正在被工作票 {ticket.Id} 引用，请先解除/替换工作票引用或删除对应工作票。({reference.Kind} {reference.Id})");
            }
        }
    }
}
