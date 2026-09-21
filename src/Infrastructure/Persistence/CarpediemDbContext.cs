using Carpediem.Modules.Identity.Domain;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Events.Domain.StaffingCycles;
using Carpediem.Modules.Events.Infrastructure.Persistence;
using Carpediem.Infrastructure.Audit;
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
    }
}
