using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.IntegrationTests;

[Collection("integration")]
public class SchemaTests
{
    private readonly IntegrationFixture _fixture;

    public SchemaTests(IntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_apply_and_postgis_is_enabled()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();

        var extension = await db.Database
            .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'postgis'")
            .FirstOrDefaultAsync();
        Assert.Equal("postgis", extension);
    }

    [Fact]
    public async Task Seed_data_creates_builtin_off_preset()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();
        await SeedData.EnsureSeededAsync(db);
        await SeedData.EnsureSeededAsync(db); // idempotent

        var preset = await db.ScanPresets.SingleOrDefaultAsync(p => p.IsBuiltin && p.Name == SeedData.OffPresetName);
        Assert.NotNull(preset);

        var config = Sigmap.Backend.Application.Configuration.ScanConfigFactory.FromJson(preset!.ConfigJson);
        Assert.False(config.ScanWifi);
        Assert.False(config.ScanBluetooth);
        Assert.False(config.ScanBtLe);
        Assert.False(config.ScanClientsPromiscuous);
        Assert.Empty(config.Interfaces);
    }

    [Fact]
    public async Task Geometry_columns_round_trip()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        var detected = new DetectedDevice
        {
            Mac = "AA:BB:CC:DD:EE:FF",
            MacNormalized = "aabbccddeeff",
            DeviceType = DeviceType.Ap,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
            Geom = new Point(13.405, 52.52) { SRID = 4326 },
        };
        db.DetectedDevices.Add(detected);
        await db.SaveChangesAsync();

        // Detach tracked entities, then re-query from the DB within the same
        // transaction to prove the geometry column round-trips.
        db.ChangeTracker.Clear();
        var loaded = await db.DetectedDevices.FirstOrDefaultAsync(d => d.Mac == "AA:BB:CC:DD:EE:FF");
        Assert.NotNull(loaded);
        Assert.Equal(13.405, loaded!.Geom!.X, 5);
        Assert.Equal(52.52, loaded.Geom.Y, 5);
        Assert.Equal(4326, loaded.Geom.SRID);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Detection_batch_idempotency_constraint_rejects_duplicates()
    {
        await using var db = TestDb.CreateDb(_fixture.PostgresConnectionString);
        await db.Database.MigrateAsync();

        await using var tx = await db.Database.BeginTransactionAsync();
        db.DetectionBatches.Add(new Sigmap.Backend.Domain.Entities.DetectionBatch { DeviceId = "dev-1", BatchId = "batch-1", SessionId = Guid.NewGuid() });
        await db.SaveChangesAsync();

        db.DetectionBatches.Add(new Sigmap.Backend.Domain.Entities.DetectionBatch { DeviceId = "dev-1", BatchId = "batch-1", SessionId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await tx.RollbackAsync();
    }
}
