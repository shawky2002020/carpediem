using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Events.Infrastructure.Persistence.Configurations;

public sealed class ChiefLeaderAppointmentRequestConfiguration : IEntityTypeConfiguration<ChiefLeaderAppointmentRequest>
{
    public void Configure(EntityTypeBuilder<ChiefLeaderAppointmentRequest> builder)
    {
        builder.ToTable("ChiefLeaderAppointmentRequests", "events");
        builder.HasKey(item => new { item.ActorUserId, item.RequestToken })
            .HasName("PK_events_ChiefLeaderAppointmentRequests");
        builder.Property(item => item.PayloadFingerprint).HasMaxLength(32).IsFixedLength().IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(item => item.AssignmentId).HasDatabaseName("IX_ChiefLeaderAppointmentRequests_AssignmentId");
    }
}
