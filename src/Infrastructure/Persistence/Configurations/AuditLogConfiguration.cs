using Carpediem.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Carpediem.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "audit");
        builder.HasKey(log => log.Id).HasName("PK_audit_AuditLogs");
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.OccurredAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(log => log.Action).HasColumnType("varchar(100)").IsRequired();
        builder.Property(log => log.ResourceType).HasColumnType("varchar(50)").IsRequired();
        builder.Property(log => log.Source).HasColumnType("varchar(50)").IsRequired();
        builder.Property(log => log.MandatoryReason).HasMaxLength(1000);
        builder.Property(log => log.PreviousValue).HasColumnType("nvarchar(max)");
        builder.Property(log => log.NewValue).HasColumnType("nvarchar(max)");
        builder.HasIndex(log => log.OccurredAt).HasDatabaseName("IX_AuditLogs_OccurredAt");
        builder.HasIndex(log => log.Action).HasDatabaseName("IX_AuditLogs_Action");
        builder.HasIndex(log => new { log.ResourceType, log.ResourceId }).HasDatabaseName("IX_AuditLogs_Resource");
        builder.HasIndex(log => log.CorrelationId).HasDatabaseName("IX_AuditLogs_CorrelationId");
    }
}
