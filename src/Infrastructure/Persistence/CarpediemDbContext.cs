using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Domain.StaffingCycles;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Notifications.Domain;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Engagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class CarpediemDbContext(DbContextOptions<CarpediemDbContext> options)
    : IdentityDbContext<CarpediemUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<StaffingCycle> StaffingCycles => Set<StaffingCycle>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DraftCreateRequest> DraftCreateRequests => Set<DraftCreateRequest>();
    public DbSet<ChiefLeaderAssignment> ChiefLeaderAssignments => Set<ChiefLeaderAssignment>();
    public DbSet<ChiefLeaderAppointmentRequest> ChiefLeaderAppointmentRequests => Set<ChiefLeaderAppointmentRequest>();
    public DbSet<InPlatformNotification> InPlatformNotifications => Set<InPlatformNotification>();
    public DbSet<AvailabilityResponse> AvailabilityResponses => Set<AvailabilityResponse>();
    public DbSet<TeamAssignment> TeamAssignments => Set<TeamAssignment>();
    public DbSet<AttendanceOutcome> AttendanceOutcomes => Set<AttendanceOutcome>();
    public DbSet<EventFeedback> EventFeedback => Set<EventFeedback>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identity");
        builder.Entity<CarpediemUser>().ToTable("Users", "identity");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "identity");

        builder.ApplyConfigurationsFromAssembly(typeof(CarpediemDbContext).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Carpediem.Modules.Events.ServiceCollectionExtensions).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(InPlatformNotification).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(Carpediem.Modules.Staffing.ServiceCollectionExtensions).Assembly);

        builder.Entity<Event>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(eventItem => eventItem.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Events_Users_CreatedBy");
        builder.Entity<AuditLog>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(log => log.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AuditLogs_Users");
        builder.Entity<DraftCreateRequest>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(intent => intent.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_DraftCreateRequests_Users");
        builder.Entity<DraftCreateRequest>()
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(intent => intent.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_DraftCreateRequests_Events");
        builder.Entity<ChiefLeaderAssignment>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.ChiefLeaderUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAssignments_Users");
        builder.Entity<ChiefLeaderAssignment>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.AppointedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAssignments_AppointedBy");
        builder.Entity<ChiefLeaderAssignment>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.AcceptedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAssignments_AcceptedBy");
        builder.Entity<ChiefLeaderAppointmentRequest>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.ActorUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAppointmentRequests_Users");
        builder.Entity<ChiefLeaderAppointmentRequest>()
            .HasOne<ChiefLeaderAssignment>()
            .WithMany()
            .HasForeignKey(item => item.AssignmentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAppointmentRequests_Assignments");
        builder.Entity<InPlatformNotification>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Notifications_Users");
        builder.Entity<InPlatformNotification>()
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(item => item.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_Notifications_Events");
        builder.Entity<AvailabilityResponse>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AvailabilityResponses_Users");
        builder.Entity<AvailabilityResponse>()
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(item => item.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AvailabilityResponses_Events");
        builder.Entity<AvailabilityResponse>()
            .HasOne<StaffingCycle>()
            .WithMany()
            .HasForeignKey(item => item.StaffingCycleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AvailabilityResponses_StaffingCycles");
        builder.Entity<TeamAssignment>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TeamAssignments_Users");
        builder.Entity<TeamAssignment>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TeamAssignments_AssignedByUsers");
        builder.Entity<TeamAssignment>()
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(item => item.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TeamAssignments_Events");
        builder.Entity<TeamAssignment>()
            .HasOne<StaffingCycle>()
            .WithMany()
            .HasForeignKey(item => item.StaffingCycleId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_TeamAssignments_StaffingCycles");
        builder.Entity<AttendanceOutcome>()
            .HasOne<TeamAssignment>()
            .WithMany()
            .HasForeignKey(item => item.TeamAssignmentId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AttendanceOutcomes_TeamAssignments");
        builder.Entity<AttendanceOutcome>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.FinalizedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AttendanceOutcomes_FinalizedByUsers");
        builder.Entity<AttendanceOutcome>()
            .HasOne<CarpediemUser>()
            .WithMany()
            .HasForeignKey(item => item.CorrectedByUserId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_AttendanceOutcomes_CorrectedByUsers");
    }
}
