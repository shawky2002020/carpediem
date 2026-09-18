namespace Carpediem.Modules.Events.Domain.Events;

public sealed class DraftValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Draft Event values are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
