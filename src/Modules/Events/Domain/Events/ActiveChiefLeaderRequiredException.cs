namespace Carpediem.Modules.Events.Domain.Events;

public sealed class ActiveChiefLeaderRequiredException : Exception
{
    public ActiveChiefLeaderRequiredException()
        : base("An Event cannot be published without exactly one accepted, Active Chief Leader assignment.") { }
}
