using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class DraftEventValidationTests
{
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);

    [Theory]
    [InlineData(null, "Cairo", "title")]
    [InlineData("", "Cairo", "title")]
    [InlineData(" \u2003\t", "Cairo", "title")]
    [InlineData("Event", null, "location")]
    [InlineData("Event", "", "location")]
    [InlineData("Event", "\u2003\u00A0", "location")]
    public void RequiredTextCannotBeMissingOrUnicodeWhitespace(string? title, string? location, string field)
    {
        var error = Assert.Throws<DraftValidationException>(() =>
            Event.CreateDraft(title, Start, End, location, null, Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2025, 1, 1)));
        Assert.Contains(field, error.Errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EndMustFollowStart(int minuteDelta)
    {
        var error = Assert.Throws<DraftValidationException>(() =>
            Event.CreateDraft("Event", Start, Start.AddMinutes(minuteDelta), "Cairo", null, Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2025, 1, 1)));
        Assert.Contains("end", error.Errors.Keys);
    }

    [Fact]
    public void RequiresCanonicalUtcInstants()
    {
        var error = Assert.Throws<DraftValidationException>(() =>
            Event.CreateDraft("Event", Start.ToOffset(TimeSpan.FromHours(2)), End, "Cairo", null, Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2025, 1, 1)));
        Assert.Contains("start", error.Errors.Keys);
    }
}
