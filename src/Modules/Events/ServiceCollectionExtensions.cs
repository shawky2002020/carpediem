using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Application.ChiefLeaderExternalAcceptances;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Events;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventsModule(this IServiceCollection services)
    {
        services.AddScoped<CreateDraftEventHandler>();
        services.AddScoped<UpdateDraftEventHandler>();
        services.AddScoped<AppointChiefLeaderHandler>();
        services.AddScoped<RecordChiefLeaderExternalAcceptanceHandler>();
        return services;
    }
}
