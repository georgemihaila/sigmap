using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Retention;

/// <summary>
/// Configurable retention + anonymization. Purges detections (and GPS samples)
/// older than the retention window, and optionally hashes third-party client
/// MACs captured by promiscuous-mode client detection (privacy-sensitive).
/// </summary>
public sealed class RetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<RetentionService> _log;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public RetentionService(IServiceScopeFactory scopes, IConfiguration config, ILogger<RetentionService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Retention run failed");
            }

            try
            {
                await Task.Delay(_interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var days = int.TryParse(_config["Retention:DetectionsDays"], out var d) ? d : 365;
        var anonymize = string.Equals(_config["Retention:AnonymizeClientMacs"], "true", StringComparison.OrdinalIgnoreCase);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();

        if (anonymize)
        {
            var clients = await db.Detections.Where(x => x.DeviceType == DeviceType.Client).ToListAsync(ct);
            var count = 0;
            foreach (var detection in clients)
            {
                var anon = Anonymize(detection.Mac);
                if (anon != detection.Mac)
                {
                    detection.Mac = anon;
                    count++;
                }
            }

            foreach (var device in await db.DetectedDevices.Where(x => x.DeviceType == DeviceType.Client).ToListAsync(ct))
            {
                var anon = Anonymize(device.Mac);
                if (anon != device.Mac)
                {
                    device.Mac = anon;
                    device.MacNormalized = anon.ToLowerInvariant();
                }
            }

            await db.SaveChangesAsync(ct);
            _log.LogInformation("Anonymized {Count} client detections", count);
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        var purgedDetections = await db.Detections
            .Where(x => x.DetectedAt < cutoff)
            .ExecuteDeleteAsync(ct);
        var purgedSamples = await db.SessionGpsSamples
            .Where(x => x.At < cutoff)
            .ExecuteDeleteAsync(ct);

        if (purgedDetections > 0 || purgedSamples > 0)
            _log.LogInformation("Retention purged {Detections} detections and {Samples} gps samples (window {Days}d)",
                purgedDetections, purgedSamples, days);
    }

    private static string Anonymize(string mac) =>
        "anon-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mac)))[..12].ToLowerInvariant();
}
