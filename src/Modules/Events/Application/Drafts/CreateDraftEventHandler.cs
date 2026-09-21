using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carpediem.Modules.Events.Application.Time;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Modules.Events.Application.Drafts;

public sealed record CreatedDraft(Event Draft, CairoResolvedTime Start, CairoResolvedTime End, bool IsReplay);

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Handler names a use case, not a CLR event delegate.")]
public sealed class CreateDraftEventHandler(IDraftEventStore store)
{
    public async Task<CreatedDraft> HandleAsync(
        DraftWriteInput input,
        Guid actorUserId,
        Guid requestToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var start = CairoEventTimeResolver.Resolve(input.Start?.LocalDateTime, input.Start?.Offset, "start");
        var end = CairoEventTimeResolver.Resolve(input.End?.LocalDateTime, input.End?.Offset, "end");
        var createdAt = DateTimeOffset.UtcNow;
        var effectiveDate = DateOnly.ParseExact(start.LocalDateTime.AsSpan(0, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var draft = Event.CreateDraft(input.Title, start.Utc, end.Utc, input.Location,
            input.Notes, actorUserId, createdAt, effectiveDate);
        var fingerprint = Fingerprint(input, start, end);
        var stored = await store.CreateAsync(draft, actorUserId, requestToken, fingerprint,
            correlationId, cancellationToken);
        return new CreatedDraft(stored.Draft, start, end, stored.IsReplay);
    }

    private static byte[] Fingerprint(DraftWriteInput input, CairoResolvedTime start, CairoResolvedTime end)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            input.Title,
            StartUtcTicks = start.Utc.Ticks,
            StartOffset = CairoEventTimeResolver.FormatOffset(start.Offset),
            EndUtcTicks = end.Utc.Ticks,
            EndOffset = CairoEventTimeResolver.FormatOffset(end.Offset),
            input.Location,
            input.Notes,
        });
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }
}
