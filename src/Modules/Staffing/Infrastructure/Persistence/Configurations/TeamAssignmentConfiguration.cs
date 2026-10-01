using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Staffing.Infrastructure.Persistence.Configurations;

public sealed class TeamAssignmentConfiguration : IEntityTypeConfiguration<TeamAssignment>
{
    public void Configure(EntityTypeBuilder<TeamAssignment> builder)
    {
        builder.ToTable("TeamAssignments", "staffing", table =>
        {
            table.HasCheckConstraint("CK_TeamAssignments_Status", "[Status] IN ('Active','Cancelled')");
        });

        builder.HasKey(x => x.Id).HasName("PK_staffing_TeamAssignments");
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffingCycleId)
            .IsRequired();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AssignedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.AssignedByUserId)
            .IsRequired();

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.CancelledAt)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.StaffingCycleId, x.UserId })
            .IsUnique()
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("UX_TeamAssignments_ActiveLeaderPerCycle");

        builder.HasIndex(x => new { x.EventId, x.Status })
            .HasDatabaseName("IX_TeamAssignments_Event_Status");
    }
}
