using Carpediem.Modules.Attendance.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Infrastructure.Persistence.Configurations;

public sealed class AttendanceOutcomeConfiguration : IEntityTypeConfiguration<AttendanceOutcome>
{
    public void Configure(EntityTypeBuilder<AttendanceOutcome> builder)
    {
        builder.ToTable("AttendanceOutcomes", "attendance", table =>
        {
            table.HasCheckConstraint("CK_AttendanceOutcomes_Outcome", "[Outcome] IN ('Attended','NoShow')");
        });

        builder.HasKey(x => x.Id).HasName("PK_attendance_AttendanceOutcomes");
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.TeamAssignmentId)
            .IsRequired();

        builder.Property(x => x.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FinalizedByUserId)
            .IsRequired();

        builder.Property(x => x.FinalizedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired();

        builder.Property(x => x.CorrectedByUserId)
            .IsRequired(false);

        builder.Property(x => x.CorrectedAt)
            .HasColumnType("datetimeoffset")
            .IsRequired(false);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.TeamAssignmentId)
            .IsUnique()
            .HasDatabaseName("UX_AttendanceOutcomes_Assignment");
    }
}
