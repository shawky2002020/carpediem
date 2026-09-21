using Carpediem.Modules.Events.Domain.StaffingCycles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Events.Infrastructure.Persistence.Configurations;

public sealed class StaffingCycleConfiguration : IEntityTypeConfiguration<StaffingCycle>
{
    public void Configure(EntityTypeBuilder<StaffingCycle> builder)
    {
        builder.ToTable("StaffingCycles", "events", table =>
            table.HasCheckConstraint("CK_StaffingCycles_Number", "[CycleNumber] >= 1"));
        builder.HasKey(cycle => cycle.Id).HasName("PK_events_StaffingCycles");
        builder.Property(cycle => cycle.Id).ValueGeneratedNever();
        builder.Property(cycle => cycle.EffectiveEventDate).HasColumnType("date").IsRequired();
        builder.Property(cycle => cycle.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(cycle => new { cycle.EventId, cycle.CycleNumber })
            .IsUnique().HasDatabaseName("UX_StaffingCycles_Event_CycleNumber");
        builder.HasIndex(cycle => cycle.EventId)
            .IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UX_StaffingCycles_CurrentPerEvent");
    }
}
