using DistributionDrawing.Application.WorkTickets;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Domain.Devices.RingCabinets;
using DistributionDrawing.Domain.Documents;

namespace DistributionDrawing.Infrastructure.Persistence;

public sealed record ProjectWorkTicketDto(Guid DocumentId, IReadOnlyList<WorkTicketSession> Tickets)
{
    public static ProjectWorkTicketDto Empty(Guid documentId) => new(documentId, []);
}

internal static class ProjectWorkTicketMapper
{
    public static ProjectWorkTicketDto ToDto(WorkTicketDataRoot root, DrawingDocument drawing)
    {
        Validate(new ProjectWorkTicketDto(root.DocumentId, root.Tickets.ToArray()), drawing);
        return new ProjectWorkTicketDto(root.DocumentId, root.Tickets.ToArray());
    }

    public static WorkTicketDataRoot ToRoot(ProjectWorkTicketDto? dto, DrawingDocument drawing)
    {
        if (dto is null) throw new InvalidDataException("V8 project requires WorkTicketData.");
        Validate(dto, drawing);
        return new WorkTicketDataRoot(dto.DocumentId, dto.Tickets);
    }

    public static void Validate(ProjectWorkTicketDto dto, DrawingDocument drawing)
    {
        if (dto.DocumentId != drawing.Id || dto.Tickets is null)
            throw new InvalidDataException("Work ticket section identity does not match the drawing.");
        if (dto.Tickets.Any(ticket => ticket is null || ticket.Id == Guid.Empty) ||
            dto.Tickets.Select(ticket => ticket.Id).Distinct().Count() != dto.Tickets.Count)
            throw new InvalidDataException("Work ticket IDs must be unique and nonempty.");
        foreach (WorkTicketSession ticket in dto.Tickets)
        {
            if (ticket.Task is null || ticket.IsolationBoundaries is null || ticket.WorkScopeIds is null ||
                ticket.GroundingPointIds is null || ticket.UserFacts is null || ticket.WorkScopeItems is null)
                throw new InvalidDataException($"Ticket {ticket.Id} has missing setup data.");
            if (ticket.WorkScopeIds.Distinct().Count() != ticket.WorkScopeIds.Count ||
                ticket.GroundingPointIds.Distinct().Count() != ticket.GroundingPointIds.Count ||
                ticket.UserFacts.Any(item => item is null || item.References is null))
                throw new InvalidDataException($"Ticket {ticket.Id} has duplicate or incomplete references.");
            foreach (IsolationBoundary boundary in ticket.IsolationBoundaries)
            {
                SwitchDevice? device = drawing.Devices.OfType<SwitchDevice>()
                    .SingleOrDefault(item => item.Id == boundary.DeviceId);
                if (!Enum.IsDefined(boundary.Side) || device is null ||
                    boundary.TerminalId is Guid terminal && !device.OwnsTerminal(terminal) ||
                    boundary.ConnectionId is Guid connection && !drawing.Connections.Any(item => item.Id == connection))
                    throw new InvalidDataException($"Ticket {ticket.Id} has an invalid isolation boundary.");
            }
            foreach (Guid id in ticket.WorkScopeIds)
                if (!drawing.WorkScopes.Any(item => item.WorkScopeId == id))
                    throw new InvalidDataException($"Ticket {ticket.Id} refers to missing work scope {id}.");
            if (ticket.WorkScopeItems.Any(item => item is null || !Enum.IsDefined(item.Kind) || item.TargetId == Guid.Empty) ||
                ticket.WorkScopeItems.Distinct().Count() != ticket.WorkScopeItems.Count)
                throw new InvalidDataException($"Ticket {ticket.Id} has invalid work scope items.");
            foreach (WorkScopeItem item in ticket.WorkScopeItems)
            {
                bool exists = item.Kind switch
                {
                    WorkScopeItemKind.Equipment => drawing.Devices.Any(device => device.Id == item.TargetId),
                    WorkScopeItemKind.ElectricalRange => drawing.WorkScopes.Any(scope => scope.WorkScopeId == item.TargetId),
                    _ => false
                };
                if (!exists) throw new InvalidDataException($"Ticket {ticket.Id} refers to missing {item.Kind} {item.TargetId}.");
            }
            foreach (Guid id in ticket.GroundingPointIds)
                if (!drawing.GroundingPoints.Any(item => item.GroundingPointId == id))
                    throw new InvalidDataException($"Ticket {ticket.Id} refers to missing grounding point {id}.");
            foreach (TicketReference reference in ticket.UserFacts.SelectMany(item => item.References)
                         .Concat(ticket.Draft?.Sections.SelectMany(section => section.Items)
                             .SelectMany(item => item.RelatedModelRefs) ?? [])
                         .Concat(ticket.Analysis?.SwitchingMeasures.SelectMany(item => item.References) ?? [])
                         .Concat(ticket.Analysis?.WorkGroupMeasures.SelectMany(item => item.References) ?? [])
                         .Concat(ticket.Analysis?.RetainedLiveParts.SelectMany(item => item.References) ?? [])
                         .Concat(ticket.Analysis?.RestorationMeasures.SelectMany(item => item.References) ?? []))
                ValidateReference(ticket.Id, reference, drawing);
        }
    }

    private static void ValidateReference(Guid ticketId, TicketReference reference, DrawingDocument drawing)
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
        if (!exists) throw new InvalidDataException($"Ticket {ticketId} refers to missing {reference.Kind} {reference.Id}.");
    }
}
