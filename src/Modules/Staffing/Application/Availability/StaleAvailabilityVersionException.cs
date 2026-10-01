namespace Carpediem.Modules.Staffing.Application.Availability;

public sealed class StaleAvailabilityVersionException : Exception
{
    public StaleAvailabilityVersionException() : base("The availability declaration has changed since it was retrieved.") { }

    public StaleAvailabilityVersionException(string message) : base(message) { }

    public StaleAvailabilityVersionException(string message, Exception innerException) : base(message, innerException) { }
}
