using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using Carpediem.Modules.Events.Application.Time;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Modules.Events.Application.Drafts;

public sealed record UpdatedDraft(Event Draft, CairoResolvedTime Start, CairoResolvedTime End);

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Handler names a use case, not a CLR event delegate.")]
public sealed class UpdateDraftEventHandler(IDraftEventStore store)
{
    public async Task<UpdatedDraft> HandleAsync(
        Guid eventId,
        DraftWriteInput input,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var start = CairoEventTimeResolver.Resolve(input.Start?.LocalDateTime, input.Start?.Offset, "start");
        var end = CairoEventTimeResolver.Resolve(input.End?.LocalDateTime, input.End?.Offset, "end");
        var effectiveDate = DateOnly.ParseExact(start.LocalDateTime.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var values = new DraftEditValues(input.Title, start.Utc, end.Utc, input.Location, input.Notes, effectiveDate);
        var updated = await store.UpdateAsync(eventId, values, expectedVersion, actorUserId,
            correlationId, cancellationToken);
        return new UpdatedDraft(updated, start, end);
    }
}
