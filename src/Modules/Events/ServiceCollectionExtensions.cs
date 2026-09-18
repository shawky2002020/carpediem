using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Events;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services) => services;
}
