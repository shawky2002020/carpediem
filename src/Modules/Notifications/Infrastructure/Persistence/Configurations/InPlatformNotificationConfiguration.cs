using Carpediem.Modules.Notifications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class InPlatformNotificationConfiguration : IEntityTypeConfiguration<InPlatformNotification>
{
    public void Configure(EntityTypeBuilder<InPlatformNotification> builder)
    {
        builder.ToTable("InPlatformNotifications", "notifications", table =>
            table.HasCheckConstraint("CK_Notifications_ReadAt", "[ReadAt] IS NULL OR [ReadAt] >= [CreatedAt]"));
        builder.HasKey(item => item.Id).HasName("PK_notifications_InPlatformNotifications");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NotificationType).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(item => item.ReadAt).HasColumnType("datetimeoffset");
        builder.Property(item => item.ContextPayload).HasMaxLength(500);
        builder.HasIndex(item => new { item.RecipientUserId, item.CreatedAt })
            .HasDatabaseName("IX_Notifications_Recipient_CreatedAt");
    }
}
