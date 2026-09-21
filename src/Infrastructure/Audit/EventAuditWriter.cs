using System.Text.Json;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Infrastructure.Audit;

public static class EventAuditWriter
{
    public static AuditLog Created(Event draft, Guid actorUserId, Guid correlationId, DateTimeOffset occurredAt)
    {
        var fieldNames = new[] { "Title", "StartDateTime", "EndDateTime", "Location", "Notes" };
        var current = JsonSerializer.Serialize(new { status = "Draft", fields = fieldNames });
        return AuditLog.Create(actorUserId, "Events.EventCreated", draft.Id, correlationId, occurredAt, null, current);
    }

    public static AuditLog Updated(Event draft, DraftEditChanges changes,
        Guid actorUserId, Guid correlationId, DateTimeOffset occurredAt)
    {
        var previous = new Dictionary<string, object?>();
        var current = new Dictionary<string, object?>();

        AddChanged("title", changes.PreviousTitle, draft.Title);
        AddChanged("startDateTimeUtc", changes.PreviousStartDateTime, draft.StartDateTime);
        AddChanged("endDateTimeUtc", changes.PreviousEndDateTime, draft.EndDateTime);
        AddChanged("location", changes.PreviousLocation, draft.Location);
        if (changes.NotesChanged) current["notesChanged"] = true;
        current["fields"] = current.Keys.ToArray();

        return AuditLog.Create(actorUserId, "Events.DraftEventUpdated", draft.Id,
            correlationId, occurredAt, JsonSerializer.Serialize(previous), JsonSerializer.Serialize(current));

        void AddChanged<T>(string field, T before, T after)
        {
            if (EqualityComparer<T>.Default.Equals(before, after)) return;
            previous[field] = before;
            current[field] = after;
        }
    }
}
