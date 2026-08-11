using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Exports;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Exports;

public sealed class ExportService : IExportService
{
    private const string WigleUploadUrl = "https://api.wigle.net/api/v2/file/upload";
    private const string WigleTransactionsUrl = "https://api.wigle.net/api/v2/file/transactions?pagestart=0&pageend=50";
    private const string WigleKmlUrl = "https://api.wigle.net/api/v2/file/kml?transid={0}";

    private readonly SigmapDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly string _exportDir;
    private readonly ILogger<ExportService> _log;

    public ExportService(SigmapDbContext db, IHttpClientFactory http, IConfiguration configuration, ILogger<ExportService> log)
    {
        _db = db;
        _http = http;
        _exportDir = configuration["Export:Directory"] ?? Path.Combine(AppContext.BaseDirectory, "exports");
        _log = log;
        Directory.CreateDirectory(_exportDir);
    }

    public async Task<ExportRecordDto> CreateExportAsync(Guid? sessionId, string format, CancellationToken ct)
    {
        var kind = format.ToLowerInvariant() switch
        {
            "wigle_csv" => ExportFormat.WigleCsv,
            "csv" => ExportFormat.Csv,
            "geojson" => ExportFormat.GeoJson,
            _ => throw new ArgumentException($"Unsupported export format '{format}'"),
        };

        var record = new ExportRecord
        {
            SessionId = sessionId,
            Format = kind,
            Status = ExportStatus.Running,
            CreatedAt = DateTimeOffset.UtcNow,
            OwnerId = Guid.Empty,
        };
        _db.Exports.Add(record);
        await _db.SaveChangesAsync(ct);

        try
        {
            var rows = await LoadRowsAsync(sessionId, kind == ExportFormat.Csv, ct);
            var content = kind switch
            {
                ExportFormat.WigleCsv => ExportRenderers.RenderWigleCsv(rows),
                ExportFormat.Csv => ExportRenderers.RenderCsv(rows),
                _ => ExportRenderers.RenderGeoJson(rows),
            };

            var ext = kind switch
            {
                ExportFormat.WigleCsv or ExportFormat.Csv => "csv",
                _ => "geojson",
            };
            var fileName = $"{record.Id:N}.{ext}";
            var path = Path.Combine(_exportDir, fileName);
            await File.WriteAllTextAsync(path, content, ct);

            record.FilePath = path;
            record.Status = ExportStatus.Done;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Export {Id} failed", record.Id);
            record.Status = ExportStatus.Failed;
            record.Error = ex.Message;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(record);
    }

    public async Task<IReadOnlyList<ExportRecordDto>> ListExportsAsync(CancellationToken ct)
    {
        var records = await _db.Exports.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<WigleUploadResult> UploadSessionToWigleAsync(Guid sessionId, CancellationToken ct)
    {
        var cred = await _db.WigleCredentials.AsNoTracking()
            .OrderByDescending(c => c.IsDefault).FirstOrDefaultAsync(ct);
        if (cred is null)
            return new WigleUploadResult(WigleUploadResultStatus.NoCredentials, "No WiGLE credentials configured");

        var rows = await LoadRowsAsync(sessionId, aggregateByMac: true, ct);
        var csv = ExportRenderers.RenderWigleCsv(rows);
        var bytes = Encoding.UTF8.GetBytes(csv);

        using var client = _http.CreateClient("wigle");
        client.DefaultRequestHeaders.Authorization = BuildAuthHeader(cred);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "transmit.csv");

        try
        {
            var response = await client.PostAsync(WigleUploadUrl, form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("WiGLE upload rejected: {Status} {Body}", response.StatusCode, body);
                return new WigleUploadResult(WigleUploadResultStatus.Failed, $"HTTP {response.StatusCode}: {Truncate(body)}");
            }

            return new WigleUploadResult(WigleUploadResultStatus.Ok, body);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "WiGLE upload failed");
            return new WigleUploadResult(WigleUploadResultStatus.Failed, ex.Message);
        }
    }

    public async Task<int> ImportFromWigleAsync(CancellationToken ct)
    {
        var cred = await _db.WigleCredentials.AsNoTracking()
            .OrderByDescending(c => c.IsDefault).FirstOrDefaultAsync(ct);
        if (cred is null)
            throw new InvalidOperationException("No WiGLE credentials configured");

        using var client = _http.CreateClient("wigle");
        client.DefaultRequestHeaders.Authorization = BuildAuthHeader(cred);

        var txResponse = await client.GetStringAsync(WigleTransactionsUrl, ct);
        var transactions = System.Text.Json.JsonSerializer.Deserialize<WigleTransactionsResponse>(txResponse,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var imported = 0;
        foreach (var tx in transactions?.Results ?? new List<WigleTransaction>())
        {
            if (tx.Transid is null)
                continue;
            try
            {
                var kml = await client.GetStringAsync(string.Format(WigleKmlUrl, tx.Transid), ct);
                imported += await MergeKmlAsync(kml, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to import WiGLE transaction {Transid}", tx.Transid);
            }
        }

        await _db.SaveChangesAsync(ct);
        return imported;
    }

    private async Task<int> MergeKmlAsync(string kml, CancellationToken ct)
    {
        var doc = XDocument.Parse(kml);
        var ns = XNamespace.Get("http://www.opengis.net/kml/2.2");
        var imported = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var placemark in doc.Descendants(ns + "Placemark"))
        {
            var name = (string?)placemark.Element(ns + "name") ?? string.Empty;
            var coordinates = (string?)placemark.Element(ns + "Point")?.Element(ns + "coordinates");
            if (coordinates is null)
                continue;

            var parts = coordinates.Trim().Split(',');
            if (parts.Length < 2 || !double.TryParse(parts[0], out var lon) || !double.TryParse(parts[1], out var lat))
                continue;

            // WiGLE KML names the placemark after the SSID; MACs are a known
            // subset of names (12 hex chars). Skip rows we can't identify.
            var mac = NormalizeMacCandidate(name);
            if (mac is null)
                continue;

            var normalized = mac;
            var existing = await _db.DetectedDevices.FirstOrDefaultAsync(d => d.MacNormalized == normalized, ct);
            if (existing is null)
            {
                _db.DetectedDevices.Add(new DetectedDevice
                {
                    Mac = mac,
                    MacNormalized = normalized,
                    DeviceType = DeviceType.Ap,
                    SsidLatest = name,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    Geom = new NetTopologySuite.Geometries.Point(lon, lat) { SRID = 4326 },
                });
                imported++;
            }
            else
            {
                existing.LastSeenAt = now;
                existing.Geom = new NetTopologySuite.Geometries.Point(lon, lat) { SRID = 4326 };
                existing.SsidLatest = name;
            }
        }

        return imported;
    }

    private static string? NormalizeMacCandidate(string name)
    {
        var cleaned = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return cleaned.Length == 12 ? string.Join("", cleaned) : null;
    }

    private async Task<List<ExportRow>> LoadRowsAsync(Guid? sessionId, bool aggregateByMac, CancellationToken ct)
    {
        var query = _db.Detections.AsNoTracking().Where(d => d.Geom != null);
        if (sessionId is not null)
            query = query.Where(d => d.SessionId == sessionId);

        var rows = await query
            .Select(d => new
            {
                d.Mac,
                d.Ssid,
                d.DeviceType,
                d.Channel,
                d.SignalDbm,
                Lon = d.Geom!.X,
                Lat = d.Geom!.Y,
                d.DetectedAt,
                d.Encryption,
                d.LocationFlag,
            })
            .ToListAsync(ct);

        if (!aggregateByMac)
        {
            return rows.Select(r => new ExportRow(
                r.Mac, r.Ssid, r.DeviceType, r.Channel, r.SignalDbm,
                r.Lat, r.Lon, r.DetectedAt, r.DetectedAt, r.Encryption, r.LocationFlag.ToString()))
                .ToList();
        }

        return rows
            .GroupBy(r => r.Mac.ToUpperInvariant())
            .Select(g =>
            {
                var latest = g.OrderByDescending(r => r.DetectedAt).First();
                return new ExportRow(
                    latest.Mac, latest.Ssid, latest.DeviceType, latest.Channel, latest.SignalDbm,
                    latest.Lat, latest.Lon, g.Min(r => r.DetectedAt), g.Max(r => r.DetectedAt),
                    latest.Encryption, latest.LocationFlag.ToString());
            })
            .ToList();
    }

    private static AuthenticationHeaderValue BuildAuthHeader(WigleCredential cred)
    {
        var user = cred.ApiName ?? cred.Username;
        var pass = cred.ApiKey ?? cred.Password;
        return new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
    }

    private static string Truncate(string s) => s.Length > 200 ? s[..200] : s;

    private static ExportRecordDto ToDto(ExportRecord e) =>
        new(e.Id, e.SessionId, e.Format.ToString(), e.Status.ToString(), e.FilePath, e.Error, e.CreatedAt);

    private sealed class WigleTransactionsResponse
    {
        public List<WigleTransaction>? Results { get; set; }
    }

    private sealed class WigleTransaction
    {
        public string? Transid { get; set; }
    }
}
