using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;

namespace Sigmap.Backend.Infrastructure.Persistence.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).IsRequired().HasMaxLength(128);
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
    }
}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.BoundingArea).HasColumnType("geometry(Polygon,4326)");
    }
}

public class SwarmConfiguration : IEntityTypeConfiguration<Swarm>
{
    public void Configure(EntityTypeBuilder<Swarm> b)
    {
        b.ToTable("swarms");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.HasOne(x => x.Session).WithMany(x => x.Swarms).HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("devices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(256);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}

public class SessionDeviceConfiguration : IEntityTypeConfiguration<SessionDevice>
{
    public void Configure(EntityTypeBuilder<SessionDevice> b)
    {
        b.ToTable("session_devices");
        b.HasKey(x => new { x.SessionId, x.DeviceId });
        b.HasOne(x => x.Session).WithMany(x => x.Devices).HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Device).WithMany(x => x.Sessions).HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Swarm).WithMany(x => x.Members).HasForeignKey(x => x.SwarmId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class SessionDeviceConfigConfiguration : IEntityTypeConfiguration<SessionDeviceConfig>
{
    public void Configure(EntityTypeBuilder<SessionDeviceConfig> b)
    {
        b.ToTable("session_device_config");
        b.HasKey(x => new { x.SessionId, x.DeviceId });
        b.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Preset).WithMany().HasForeignKey(x => x.PresetId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.PushState).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
    }
}

public class ScanPresetConfiguration : IEntityTypeConfiguration<ScanPreset>
{
    public void Configure(EntityTypeBuilder<ScanPreset> b)
    {
        b.ToTable("scan_presets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(128);
        b.HasIndex(x => new { x.OwnerId, x.Name });
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class DetectionBatchConfiguration : IEntityTypeConfiguration<DetectionBatch>
{
    public void Configure(EntityTypeBuilder<DetectionBatch> b)
    {
        b.ToTable("detection_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceId).IsRequired().HasMaxLength(64);
        b.Property(x => x.BatchId).IsRequired().HasMaxLength(64);
        b.HasIndex(x => new { x.DeviceId, x.BatchId }).IsUnique();
        b.HasIndex(x => x.SessionId);
    }
}

public class DetectionConfiguration : IEntityTypeConfiguration<Detection>
{
    public void Configure(EntityTypeBuilder<Detection> b)
    {
        b.ToTable("detections");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Batch).WithMany(x => x.Detections).HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Mac).IsRequired().HasMaxLength(32);
        b.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Encryption).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.LocationFlag).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Geom).HasColumnType("geometry(Point,4326)");
        b.HasIndex(x => x.SessionId);
        // Keyset pagination index.
        b.HasIndex(x => new { x.DetectedAt, x.Id });
    }
}

public class DetectedDeviceConfiguration : IEntityTypeConfiguration<DetectedDevice>
{
    public void Configure(EntityTypeBuilder<DetectedDevice> b)
    {
        b.ToTable("detected_devices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Mac).IsRequired().HasMaxLength(32);
        b.Property(x => x.MacNormalized).IsRequired().HasMaxLength(32);
        b.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Geom).HasColumnType("geometry(Point,4326)");
        b.HasIndex(x => x.Mac).IsUnique();
        b.HasIndex(x => x.MacNormalized);
        // Keyset pagination index.
        b.HasIndex(x => new { x.LastSeenAt, x.Id });
    }
}

public class WigleCredentialConfiguration : IEntityTypeConfiguration<WigleCredential>
{
    public void Configure(EntityTypeBuilder<WigleCredential> b)
    {
        b.ToTable("wigle_credentials");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WatchlistEntryConfiguration : IEntityTypeConfiguration<WatchlistEntry>
{
    public void Configure(EntityTypeBuilder<WatchlistEntry> b)
    {
        b.ToTable("watchlist");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<string>().HasMaxLength(16);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.OwnerId, x.Mac });
    }
}

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> b)
    {
        b.ToTable("alert_rules");
        b.HasKey(x => x.Id);
        b.Property(x => x.RuleType).IsRequired().HasMaxLength(64);
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExportRecordConfiguration : IEntityTypeConfiguration<ExportRecord>
{
    public void Configure(EntityTypeBuilder<ExportRecord> b)
    {
        b.ToTable("exports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Format).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.HasIndex(x => x.OwnerId);
    }
}

public class SessionGpsSampleConfiguration : IEntityTypeConfiguration<SessionGpsSample>
{
    public void Configure(EntityTypeBuilder<SessionGpsSample> b)
    {
        b.ToTable("session_gps_samples");
        b.HasKey(x => x.Id);
        b.Property(x => x.Geom).HasColumnType("geometry(Point,4326)").IsRequired();
        b.HasIndex(x => x.SessionId);
        b.HasIndex(x => x.Geom);
    }
}
