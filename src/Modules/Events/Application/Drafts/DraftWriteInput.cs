namespace Carpediem.Modules.Events.Application.Drafts;

public sealed record CairoTimeInput(string? LocalDateTime, string? Offset);

public sealed record DraftWriteInput(
    string? Title,
    CairoTimeInput? Start,
    CairoTimeInput? End,
    string? Location,
    string? Notes);
