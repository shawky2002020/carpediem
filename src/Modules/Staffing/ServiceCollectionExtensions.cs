using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Staffing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStaffingModule(this IServiceCollection services)
    {
        return services;
    }
}
