using Microsoft.EntityFrameworkCore;
using Sigmap.Domain.Entities;

namespace Sigmap.Infrastructure.Persistence;

public class SigmapDbContext(DbContextOptions<SigmapDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Swarm> Swarms => Set<Swarm>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SessionDevice> SessionDevices => Set<SessionDevice>();
    public DbSet<Preset> Presets => Set<Preset>();
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<DetectedDevice> DetectedDevices => Set<DetectedDevice>();
    public DbSet<Export> Exports => Set<Export>();
    public DbSet<Pairing> Pairings => Set<Pairing>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.LatMin);
            e.Property(x => x.LonMin);
            e.Property(x => x.LatMax);
            e.Property(x => x.LonMax);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Swarm>(e =>
        {
            e.ToTable("swarms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.SessionId);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(64).IsRequired();
            e.Property(x => x.Platform).HasMaxLength(32);
            e.Property(x => x.CapabilitiesJson).HasColumnType("text");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<SessionDevice>(e =>
        {
            e.ToTable("session_devices");
            e.HasKey(x => new { x.SessionId, x.DeviceId });
            e.Property(x => x.Role).HasMaxLength(16);
            e.Property(x => x.ConfigJson).HasColumnType("text");
            e.Property(x => x.ConfigSource).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.PushState).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.DeviceId);
        });

        modelBuilder.Entity<Preset>(e =>
        {
            e.ToTable("presets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.ConfigJson).HasColumnType("text");
            e.HasIndex(x => x.OwnerId);
        });

        modelBuilder.Entity<Detection>(e =>
        {
            e.ToTable("detections");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Encryption).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.LocationFlag).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => new { x.SessionId, x.DetectedAt });
            e.HasIndex(x => new { x.MacNormalized, x.DetectedAt });
        });

        modelBuilder.Entity<DetectedDevice>(e =>
        {
            e.ToTable("detected_devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Mac).HasMaxLength(17).IsRequired();
            e.Property(x => x.MacNormalized).HasMaxLength(12).IsRequired();
            e.HasIndex(x => x.MacNormalized).IsUnique();
            e.Property(x => x.DeviceType).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.LastSeenAt, x.Id });
        });

        modelBuilder.Entity<Export>(e =>
        {
            e.ToTable("exports");
            e.HasKey(x => x.Id);
            e.Property(x => x.Format).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<Pairing>(e =>
        {
            e.ToTable("pairings");
            e.HasKey(x => x.Id);
            e.Property(x => x.CapabilitiesJson).HasColumnType("text");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.DeviceId).IsUnique();
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("app_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.WigleApiName).HasMaxLength(128);
            e.Property(x => x.WigleUsername).HasMaxLength(128);
        });
    }
}
