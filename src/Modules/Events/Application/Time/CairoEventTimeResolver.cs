using System.Globalization;

namespace Carpediem.Modules.Events.Application.Time;

public sealed record CairoResolvedTime(string LocalDateTime, TimeSpan Offset, DateTimeOffset Utc);

public sealed class CairoTimeValidationException(
    IReadOnlyDictionary<string, string[]> errors,
    IReadOnlyDictionary<string, string[]>? allowedOffsets = null)
    : Exception("Cairo local time is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
    public IReadOnlyDictionary<string, string[]> AllowedOffsets { get; } =
        allowedOffsets ?? new Dictionary<string, string[]>();
}

public static class CairoEventTimeResolver
{
    private static readonly string[] AcceptedFormats =
    [
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
    ];

    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public static CairoResolvedTime Resolve(string? localDateTime, string? selectedOffset, string field)
    {
        if (string.IsNullOrWhiteSpace(localDateTime) ||
            !DateTime.TryParseExact(localDateTime, AcceptedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            throw Invalid(field, "Enter a valid Cairo local date and time.");
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Cairo.IsInvalidTime(local))
        {
            throw Invalid(field, "This Cairo local time does not exist. Choose another time.");
        }

        TimeSpan offset;
        if (Cairo.IsAmbiguousTime(local))
        {
            var validOffsets = Cairo.GetAmbiguousTimeOffsets(local);
            var allowed = validOffsets.Select(FormatOffset).ToArray();
            var choice = Array.IndexOf(allowed, selectedOffset);
            if (choice < 0)
            {
                throw new CairoTimeValidationException(
                    new Dictionary<string, string[]> { [field] = ["Choose one of the available Cairo offsets for this time."] },
                    new Dictionary<string, string[]> { [field] = allowed });
            }

            offset = validOffsets[choice];
        }
        else
        {
            offset = Cairo.GetUtcOffset(local);
            if (selectedOffset is not null && selectedOffset != FormatOffset(offset))
            {
                throw Invalid(field, "The selected offset does not match Cairo at this time.");
            }
        }

        var utc = new DateTimeOffset(local, offset).ToUniversalTime();
        return new CairoResolvedTime(
            local.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            offset,
            utc);
    }

    public static CairoResolvedTime FromUtc(DateTimeOffset utc)
    {
        if (utc.Offset != TimeSpan.Zero) throw new ArgumentException("A canonical UTC instant is required.", nameof(utc));
        var cairo = TimeZoneInfo.ConvertTime(utc, Cairo);
        return new CairoResolvedTime(
            cairo.DateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
            cairo.Offset,
            utc);
    }

    public static string FormatOffset(TimeSpan offset) =>
        string.Create(CultureInfo.InvariantCulture, $"{(offset < TimeSpan.Zero ? '-' : '+')}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}");

    private static CairoTimeValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
