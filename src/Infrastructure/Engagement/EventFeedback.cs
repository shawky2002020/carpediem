namespace Carpediem.Infrastructure.Engagement;

public sealed class EventFeedback
{
    private EventFeedback() { }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid RaterUserId { get; private set; }
    public Guid? LeaderUserId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }

    public static EventFeedback Create(Guid eventId, Guid raterUserId, Guid? leaderUserId,
        int rating, string? comment, DateTimeOffset submittedAt)
    {
        if (eventId == Guid.Empty || raterUserId == Guid.Empty) throw new ArgumentException("Event and rater are required.");
        if (rating is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(rating));
        if (comment?.Trim().Length > 500) throw new ArgumentException("Comment cannot exceed 500 characters.", nameof(comment));
        return new EventFeedback
        {
            Id = Guid.NewGuid(), EventId = eventId, RaterUserId = raterUserId,
            LeaderUserId = leaderUserId, Kind = leaderUserId.HasValue ? "Leader" : "Event",
            Rating = rating, Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            SubmittedAt = submittedAt,
        };
    }
}
