using Carpediem.Modules.Staffing.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Staffing.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityResponseConfiguration : IEntityTypeConfiguration<AvailabilityResponse>
{
    public void Configure(EntityTypeBuilder<AvailabilityResponse> builder)
    {
        builder.ToTable("AvailabilityResponses", "staffing");
        builder.HasKey(x => x.Id).HasName("PK_staffing_AvailabilityResponses");
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StaffingCycleId)
            .IsRequired();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Response)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.DeclaredAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.StaffingCycleId, x.UserId })
            .IsUnique()
            .HasDatabaseName("UX_AvailabilityResponses_StaffingCycle_User");

        builder.HasIndex(x => new { x.EventId, x.Response })
            .HasDatabaseName("IX_AvailabilityResponses_Event_Response");
    }
}
