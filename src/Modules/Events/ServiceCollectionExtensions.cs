using Carpediem.Modules.Events.Application.Drafts;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Events;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services)
    {
        services.AddScoped<CreateDraftEventHandler>();
        services.AddScoped<UpdateDraftEventHandler>();
        return services;
    }
}
