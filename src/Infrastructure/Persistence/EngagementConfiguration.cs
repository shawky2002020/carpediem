using Carpediem.Infrastructure.Engagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EventFeedbackConfiguration : IEntityTypeConfiguration<EventFeedback>
{
    public void Configure(EntityTypeBuilder<EventFeedback> builder)
    {
        builder.ToTable("EventFeedback", "engagement", table =>
        {
            table.HasCheckConstraint("CK_EventFeedback_Rating", "[Rating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_EventFeedback_Kind", "[Kind] IN ('Event','Leader')");
            table.HasCheckConstraint("CK_EventFeedback_Subject", "([Kind] = 'Event' AND [LeaderUserId] IS NULL) OR ([Kind] = 'Leader' AND [LeaderUserId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(500);
        builder.Property(x => x.SubmittedAt).HasColumnType("datetimeoffset");
        builder.HasIndex(x => new { x.EventId, x.RaterUserId, x.Kind, x.LeaderUserId }).IsUnique().HasFilter(null);
        builder.HasOne<Carpediem.Modules.Events.Domain.Events.Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Carpediem.Modules.Identity.Domain.CarpediemUser>().WithMany().HasForeignKey(x => x.RaterUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Carpediem.Modules.Identity.Domain.CarpediemUser>().WithMany().HasForeignKey(x => x.LeaderUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> builder)
    {
        builder.ToTable("IncidentReports", "engagement");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.OccurredAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.ReportedAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.ReviewedAt).HasColumnType("datetimeoffset");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.EventId, x.ReportedAt });
        builder.HasIndex(x => x.ReviewedAt);
        builder.HasOne<Carpediem.Modules.Events.Domain.Events.Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Carpediem.Modules.Identity.Domain.CarpediemUser>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Carpediem.Modules.Identity.Domain.CarpediemUser>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
