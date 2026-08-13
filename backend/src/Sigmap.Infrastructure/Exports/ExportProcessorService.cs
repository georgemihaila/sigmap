using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigmap.Application.Abstractions;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Exports;

/// <summary>
/// Runs queued export jobs to completion: builds CSV/GeoJSON/WiGLE content,
/// writes it to the file store and stamps the record done.
/// </summary>
public sealed class ExportProcessorService(
    IExportFileStore fileStore,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<ExportProcessorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(1500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Export processor error");
            }
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();

        var job = await db.Exports
            .Where(e => e.Status == ExportStatus.Queued)
            .OrderBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (job is null) return;

        job.Status = ExportStatus.Running;
        await db.SaveChangesAsync(ct);

        try
        {
            var rows = await db.Detections.AsNoTracking()
                .Where(d => (job.SessionId == null || d.SessionId == job.SessionId))
                .OrderBy(d => d.DetectedAt)
                .ToListAsync(ct);

            var (content, extension) = BuildContent(job.Format, rows);
            var fileName = $"sigmap-{job.Id:N}"[..^24] + "-" + job.CreatedAt.ToString("yyyyMMdd-HHmmss") + "." + extension;
            var (path, size) = await fileStore.WriteAsync(job.Id, fileName, content, ct);

            job.Status = ExportStatus.Done;
            job.FileName = path;
            job.RowCount = rows.Count;
            job.SizeBytes = size;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export job {ExportId} failed", job.Id);
            job.Status = ExportStatus.Failed;
            await db.SaveChangesAsync(ct);
        }
    }

    private static (byte[] Content, string Extension) BuildContent(ExportFormat format, List<Detection> rows)
    {
        return format switch
        {
            ExportFormat.WigleCsv => (Encoding.UTF8.GetBytes(BuildWigleCsv(rows)), "csv"),
            ExportFormat.Csv => (Encoding.UTF8.GetBytes(BuildCsv(rows)), "csv"),
            ExportFormat.GeoJson => (Encoding.UTF8.GetBytes(BuildGeoJson(rows)), "geojson"),
            _ => (Encoding.UTF8.GetBytes(BuildCsv(rows)), "csv"),
        };
    }

    private static string BuildWigleCsv(List<Detection> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MAC,SSID,AuthMode,FirstTime,LastTime,Channel,RSSI,CurrentLatitude,CurrentLongitude,AltitudeMeters,AccuracyMeters,Type");
        foreach (var r in rows)
        {
            var auth = r.Encryption == Encryption.Unspecified ? "" : $"[{AuthText(r.Encryption)}][ESS]";
            sb.AppendLine(string.Join(',',
                r.Mac,
                Csv(r.Ssid ?? ""),
                auth,
                FmtTime(r.DetectedAt),
                FmtTime(r.DetectedAt),
                r.Channel,
                r.SignalDbm,
                r.Lat?.ToString("0.0000000") ?? "",
                r.Lon?.ToString("0.0000000") ?? "",
                0,
                8,
                TypeText(r.DeviceType)));
        }
        return sb.ToString();
    }

    private static string BuildCsv(List<Detection> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("mac,ssid,deviceType,encryption,channel,signalDbm,detectedAt,locationFlag,lat,lon");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                r.Mac,
                Csv(r.Ssid ?? ""),
                r.DeviceType.ToString().ToLowerInvariant(),
                r.Encryption.ToString().ToLowerInvariant(),
                r.Channel,
                r.SignalDbm,
                r.DetectedAt.ToUniversalTime().ToString("o"),
                r.LocationFlag.ToString().ToLowerInvariant(),
                r.Lat?.ToString("0.0000000") ?? "",
                r.Lon?.ToString("0.0000000") ?? ""));
        }
        return sb.ToString();
    }

    private static string BuildGeoJson(List<Detection> rows)
    {
        var features = rows
            .Where(r => r.Lat is not null && r.Lon is not null)
            .Select(r => new
            {
                type = "Feature",
                geometry = new { type = "Point", coordinates = new[] { r.Lon!.Value, r.Lat!.Value } },
                properties = new
                {
                    mac = r.Mac,
                    ssid = r.Ssid,
                    deviceType = r.DeviceType.ToString().ToUpperInvariant(),
                    encryption = r.Encryption.ToString().ToLowerInvariant(),
                    signalDbm = r.SignalDbm,
                    channel = r.Channel,
                    detectedAt = r.DetectedAt.ToUniversalTime().ToString("o"),
                },
            })
            .ToList();

        var fc = new { type = "FeatureCollection", features };
        return JsonSerializer.Serialize(fc, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string AuthText(Encryption e) => e switch
    {
        Encryption.Open => "WPA",
        Encryption.Wep => "WEP-64",
        Encryption.Wpa => "WPA-PSK",
        Encryption.Wpa2 => "WPA2-PSK-CCMP",
        Encryption.Wpa3 => "WPA3-SAE",
        _ => "Unknown",
    };

    private static string TypeText(DeviceType t) => t switch
    {
        DeviceType.Ap => "WIFI",
        DeviceType.Client => "WIFI",
        DeviceType.Bluetooth => "BT",
        DeviceType.BtLe => "BLE",
        _ => "WIFI",
    };

    private static string FmtTime(DateTimeOffset t) => t.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string Csv(string v) => v.Contains(',') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
