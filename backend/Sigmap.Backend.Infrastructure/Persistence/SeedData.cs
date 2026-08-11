using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Domain.Entities;

namespace Sigmap.Backend.Infrastructure.Persistence;

public static class SeedData
{
    public const string OffPresetName = "Off";

    /// <summary>Idempotent seed: ensures the built-in "Off" preset exists.</summary>
    public static async Task EnsureSeededAsync(SigmapDbContext db, ILogger? log = null, CancellationToken ct = default)
    {
        if (await db.ScanPresets.AnyAsync(p => p.IsBuiltin && p.Name == OffPresetName, ct))
            return;

        db.ScanPresets.Add(new ScanPreset
        {
            Name = OffPresetName,
            Description = "Built-in default: everything disabled. New devices start here.",
            IsBuiltin = true,
            ConfigJson = ScanConfigFactory.ToJson(ScanConfigFactory.Off()),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        log?.LogInformation("Seeded built-in '{Preset}' preset", OffPresetName);
    }

    public static async Task MigrateAndSeedAsync(
        IServiceProvider services,
        ILogger? log = null,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();
        await db.Database.MigrateAsync(ct);
        await EnsureSeededAsync(db, log, ct);
    }
}
