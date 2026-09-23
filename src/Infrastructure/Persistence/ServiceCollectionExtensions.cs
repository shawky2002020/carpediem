using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarpediemPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CarpediemDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Carpediem")
                ?? throw new InvalidOperationException("ConnectionStrings:Carpediem is required."), sql =>
            {
                sql.MigrationsAssembly(typeof(CarpediemDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure();
            }));
        services.AddScoped<IDraftEventStore, EfDraftEventStore>();
        services.AddScoped<IChiefLeaderAppointmentStore, EfChiefLeaderAppointmentStore>();
        services.AddScoped<IChiefLeaderResponseStore, EfChiefLeaderResponseStore>();
        services.AddScoped<IActiveChiefLeaderLookup, EfActiveChiefLeaderLookup>();
        services.AddScoped<IAppointmentNotificationWriter, EfAppointmentNotificationWriter>();
        services.AddScoped<IChiefLeaderDeclineNotificationWriter, EfChiefLeaderDeclineNotificationWriter>();

        services
            .AddIdentityCore<CarpediemUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<CarpediemDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "Carpediem.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        return services;
    }
}
