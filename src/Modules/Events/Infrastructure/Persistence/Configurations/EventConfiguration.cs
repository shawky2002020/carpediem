using Carpediem.Modules.Events.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Events.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events", "events", table =>
        {
            table.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Draft', 'Published', 'Completed', 'Cancelled')");
            table.HasCheckConstraint("CK_Events_TimeWindow", "[EndDateTime] > [StartDateTime]");
            table.HasCheckConstraint("CK_Events_StartUtc", "DATEPART(TZOFFSET, [StartDateTime]) = 0");
            table.HasCheckConstraint("CK_Events_EndUtc", "DATEPART(TZOFFSET, [EndDateTime]) = 0");
            table.HasCheckConstraint("CK_Events_TitleNonblank", "LEN(LTRIM(RTRIM([Title]))) > 0");
            table.HasCheckConstraint("CK_Events_LocationNonblank", "LEN(LTRIM(RTRIM([Location]))) > 0");
        });
        builder.HasKey(eventItem => eventItem.Id).HasName("PK_events_Events");
        builder.Property(eventItem => eventItem.Id).ValueGeneratedNever();
        builder.Property(eventItem => eventItem.Title).HasMaxLength(200).IsRequired();
        builder.Property(eventItem => eventItem.StartDateTime).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(eventItem => eventItem.EndDateTime).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(eventItem => eventItem.Location).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(eventItem => eventItem.Notes).HasColumnType("nvarchar(max)");
        builder.Property(eventItem => eventItem.Status).HasConversion<string>().HasColumnType("varchar(20)").IsRequired();
        builder.Property(eventItem => eventItem.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(eventItem => eventItem.RowVersion).IsRowVersion();
        builder.HasMany(eventItem => eventItem.StaffingCycles)
            .WithOne()
            .HasForeignKey(cycle => cycle.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_StaffingCycles_Events");
        builder.Navigation(eventItem => eventItem.StaffingCycles).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(eventItem => eventItem.ChiefLeaderAssignments)
            .WithOne()
            .HasForeignKey(assignment => assignment.EventId)
            .OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_ChiefLeaderAssignments_Events");
        builder.Navigation(eventItem => eventItem.ChiefLeaderAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
