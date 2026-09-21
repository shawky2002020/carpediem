using Carpediem.Modules.Events.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Events.Infrastructure.Persistence.Configurations;

public sealed class DraftCreateRequestConfiguration : IEntityTypeConfiguration<DraftCreateRequest>
{
    public void Configure(EntityTypeBuilder<DraftCreateRequest> builder)
    {
        builder.ToTable("DraftCreateRequests", "events");
        builder.HasKey(intent => new { intent.ActorUserId, intent.RequestToken })
            .HasName("PK_events_DraftCreateRequests");
        builder.Property(intent => intent.PayloadFingerprint).HasColumnType("binary(32)").IsRequired();
        builder.Property(intent => intent.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.HasIndex(intent => intent.EventId).HasDatabaseName("IX_DraftCreateRequests_EventId");
    }
}
