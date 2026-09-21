using Carpediem.Modules.Events.Application.Time;

namespace Carpediem.Domain.Tests.Events;

public sealed class CairoEventTimeResolverTests
{
    [Fact]
    public void ResolvesOrdinaryCairoTimeToCanonicalUtc()
    {
        var result = CairoEventTimeResolver.Resolve("2026-10-10T14:30", null, "start");

        Assert.Equal(TimeSpan.Zero, result.Utc.Offset);
        Assert.Equal("2026-10-10T14:30:00", result.LocalDateTime);
        Assert.Equal(TimeSpan.FromHours(3), result.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 11, 30, 0, TimeSpan.Zero), result.Utc);
    }

    [Fact]
    public void RejectsNonexistentLocalTime()
    {
        var local = FindTransition((timeZone, time) => timeZone.IsInvalidTime(time), 2026, 4, 6);
        var error = Assert.Throws<CairoTimeValidationException>(() => CairoEventTimeResolver.Resolve(local, null, "start"));
        Assert.Contains("start", error.Errors.Keys);
    }

    [Fact]
    public void AmbiguousTimeNeedsOneOfTheActualOffsets()
    {
        var local = FindTransition((timeZone, time) => timeZone.IsAmbiguousTime(time), 2026, 9, 12);
        var error = Assert.Throws<CairoTimeValidationException>(() => CairoEventTimeResolver.Resolve(local, null, "end"));
        var allowed = Assert.Contains("end", error.AllowedOffsets);
        Assert.Equal(2, allowed.Length);

        var first = CairoEventTimeResolver.Resolve(local, allowed[0], "end");
        var second = CairoEventTimeResolver.Resolve(local, allowed[1], "end");
        Assert.NotEqual(first.Utc, second.Utc);
    }

    [Theory]
    [InlineData("not-a-date", null)]
    [InlineData("2026-10-10T14:30Z", null)]
    [InlineData("2026-10-10T14:30", "+00:00")]
    public void RejectsMalformedOrMismatchedInput(string local, string? offset)
    {
        var error = Assert.Throws<CairoTimeValidationException>(() => CairoEventTimeResolver.Resolve(local, offset, "start"));
        Assert.Contains("start", error.Errors.Keys);
    }

    [Fact]
    public void UtcInstantRoundTripsAcrossTheCairoCalendarBoundary()
    {
        var utc = new DateTimeOffset(2026, 10, 9, 22, 30, 0, TimeSpan.Zero);

        var cairo = CairoEventTimeResolver.FromUtc(utc);
        var roundTrip = CairoEventTimeResolver.Resolve(cairo.LocalDateTime,
            CairoEventTimeResolver.FormatOffset(cairo.Offset), "start");

        Assert.StartsWith("2026-10-10T", cairo.LocalDateTime, StringComparison.Ordinal);
        Assert.Equal(utc, roundTrip.Utc);
    }

    private static string FindTransition(Func<TimeZoneInfo, DateTime, bool> predicate, int year, int firstMonth, int lastMonth)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        for (var month = firstMonth; month <= lastMonth; month++)
        {
            for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
            {
                for (var hour = 0; hour < 24; hour++)
                {
                    var local = new DateTime(year, month, day, hour, 30, 0, DateTimeKind.Unspecified);
                    if (predicate(zone, local)) return local.ToString("yyyy-MM-ddTHH:mm", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }

        throw new InvalidOperationException("Expected Cairo time transition was not found.");
    }
}
