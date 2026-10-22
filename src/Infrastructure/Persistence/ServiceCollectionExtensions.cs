using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Events.Application.ChiefLeaderExternalAcceptances;
using Carpediem.Modules.Events.Application.EventPublication;
using Carpediem.Infrastructure.Audit;
using Carpediem.Modules.Staffing.Application.Availability;
using Carpediem.Modules.Staffing.Application.TeamSelection;
using Carpediem.Modules.Staffing.Application.DirectAssignment;
using Carpediem.Modules.Staffing.Application.SelfCancellation;
using Carpediem.Modules.Staffing.Application.ReplacementAssignment;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Modules.Attendance.Application;
using Carpediem.Modules.Events.Application.Dashboard;
using Carpediem.Modules.Identity.Application;
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
        services.AddScoped<IChiefLeaderExternalAcceptanceStore, EfChiefLeaderExternalAcceptanceStore>();
        services.AddScoped<IChiefLeaderExternalAcceptanceAuditWriter, ChiefLeaderExternalAcceptanceAuditWriter>();
        services.AddScoped<IActiveChiefLeaderLookup, EfActiveChiefLeaderLookup>();
        services.AddScoped<IAppointmentNotificationWriter, EfAppointmentNotificationWriter>();
        services.AddScoped<IChiefLeaderDeclineNotificationWriter, EfChiefLeaderDeclineNotificationWriter>();
        services.AddScoped<IEventPublicationStore, EfEventPublicationStore>();
        services.AddScoped<IEventPublishedNotificationWriter, EfEventPublishedNotificationWriter>();
        services.AddScoped<IAvailabilityStore, EfAvailabilityStore>();
        services.AddScoped<ITeamSelectionStore, EfTeamSelectionStore>();
        services.AddScoped<IVolunteerSelectedNotificationWriter, EfVolunteerSelectedNotificationWriter>();
        services.AddScoped<IDirectAssignmentStore, EfDirectAssignmentStore>();
        services.AddScoped<IDirectLeaderAssignedNotificationWriter, EfDirectLeaderAssignedNotificationWriter>();
        services.AddScoped<ISelfCancellationStore, EfSelfCancellationStore>();
        services.AddScoped<ITeamMemberCancelledNotificationWriter, EfTeamMemberCancelledNotificationWriter>();
        services.AddScoped<IReplacementAssignmentStore, EfReplacementAssignmentStore>();
        services.AddScoped<IReplacementLeaderAssignedNotificationWriter, EfReplacementLeaderAssignedNotificationWriter>();
        services.AddScoped<Carpediem.Modules.Events.Application.Reschedule.IEventRescheduleStore, EfEventRescheduleStore>();
        services.AddScoped<IEventRescheduledNotificationWriter, EfEventRescheduledNotificationWriter>();
        services.AddScoped<Carpediem.Modules.Events.Application.Completion.IEventCompletionStore, EfEventCompletionStore>();
        services.AddScoped<IAttendanceStore, EfAttendanceStore>();
        services.AddScoped<ILeaderHistoryStore, EfLeaderHistoryStore>();
        services.AddScoped<INotificationInboxStore, EfNotificationInboxStore>();
        services.AddScoped<IOperationalDashboardStore, EfOperationalDashboardStore>();

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
