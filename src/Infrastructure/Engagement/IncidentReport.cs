namespace Carpediem.Infrastructure.Engagement;

public sealed class IncidentReport
{
    private IncidentReport() { }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? ReviewNote { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static IncidentReport Create(Guid eventId, Guid reporterUserId, string title,
        string description, DateTimeOffset occurredAt, DateTimeOffset reportedAt)
    {
        if (eventId == Guid.Empty || reporterUserId == Guid.Empty) throw new ArgumentException("Event and reporter are required.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new ArgumentException("Title must be 1–200 characters.", nameof(title));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 4000) throw new ArgumentException("Description must be 1–4000 characters.", nameof(description));
        if (occurredAt.Offset != TimeSpan.Zero || reportedAt.Offset != TimeSpan.Zero || occurredAt > reportedAt)
            throw new ArgumentException("Occurrence time must be UTC and cannot be in the future.", nameof(occurredAt));
        return new IncidentReport { Id = Guid.NewGuid(), EventId = eventId, ReporterUserId = reporterUserId,
            Title = title.Trim(), Description = description.Trim(), OccurredAt = occurredAt, ReportedAt = reportedAt };
    }

    public void Review(Guid adminId, string? note, DateTimeOffset reviewedAt)
    {
        if (ReviewedAt.HasValue) throw new InvalidOperationException("Incident has already been reviewed.");
        if (adminId == Guid.Empty || reviewedAt.Offset != TimeSpan.Zero) throw new ArgumentException("An Admin and UTC review time are required.");
        if (note?.Trim().Length > 1000) throw new ArgumentException("Review note cannot exceed 1000 characters.", nameof(note));
        ReviewedByUserId = adminId;
        ReviewedAt = reviewedAt;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
