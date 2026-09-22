using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Events.Infrastructure.Persistence.Configurations;

public sealed class ChiefLeaderAssignmentConfiguration : IEntityTypeConfiguration<ChiefLeaderAssignment>
{
    public void Configure(EntityTypeBuilder<ChiefLeaderAssignment> builder)
    {
        builder.ToTable("ChiefLeaderAssignments", "events", table =>
        {
            table.HasCheckConstraint("CK_ChiefLeaderAssignments_Status", "[Status] IN ('Pending','Active','Declined')");
            table.HasCheckConstraint("CK_ChiefLeaderAssignments_Source", "[AcceptanceSource] IS NULL OR [AcceptanceSource] IN ('Direct','AdminExternal')");
        });
        builder.HasKey(item => item.Id).HasName("PK_events_ChiefLeaderAssignments");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Status).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();
        builder.Property(item => item.AppointedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(item => item.AcceptedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.AcceptanceSource).HasColumnType("varchar(20)");
        builder.Property(item => item.DeclinedAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.HasIndex(item => item.EventId).IsUnique()
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("UX_ChiefLeaderAssignments_ActivePerEvent");
    }
}
