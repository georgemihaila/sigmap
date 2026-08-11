using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence.Configuration;

namespace Sigmap.Backend.Infrastructure.Persistence;

public class SigmapDbContext : DbContext
{
    public SigmapDbContext(DbContextOptions<SigmapDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Swarm> Swarms => Set<Swarm>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<SessionDevice> SessionDevices => Set<SessionDevice>();
    public DbSet<SessionDeviceConfig> SessionDeviceConfigs => Set<SessionDeviceConfig>();
    public DbSet<ScanPreset> ScanPresets => Set<ScanPreset>();
    public DbSet<DetectionBatch> DetectionBatches => Set<DetectionBatch>();
    public DbSet<Detection> Detections => Set<Detection>();
    public DbSet<DetectedDevice> DetectedDevices => Set<DetectedDevice>();
    public DbSet<WigleCredential> WigleCredentials => Set<WigleCredential>();
    public DbSet<WatchlistEntry> WatchlistEntries => Set<WatchlistEntry>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<ExportRecord> Exports => Set<ExportRecord>();
    public DbSet<SessionGpsSample> SessionGpsSamples => Set<SessionGpsSample>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
        modelBuilder.ApplyConfiguration(new SwarmConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new SessionDeviceConfiguration());
        modelBuilder.ApplyConfiguration(new SessionDeviceConfigConfiguration());
        modelBuilder.ApplyConfiguration(new ScanPresetConfiguration());
        modelBuilder.ApplyConfiguration(new DetectionBatchConfiguration());
        modelBuilder.ApplyConfiguration(new DetectionConfiguration());
        modelBuilder.ApplyConfiguration(new DetectedDeviceConfiguration());
        modelBuilder.ApplyConfiguration(new WigleCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new WatchlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AlertRuleConfiguration());
        modelBuilder.ApplyConfiguration(new ExportRecordConfiguration());
        modelBuilder.ApplyConfiguration(new SessionGpsSampleConfiguration());
    }
}
