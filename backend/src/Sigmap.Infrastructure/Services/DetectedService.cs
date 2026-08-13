using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Keyset;
using Sigmap.Application.Services;
using Sigmap.Domain;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class DetectedService(SigmapDbContext db) : IDetectedService
{
    public async Task<DetectedDevicePageDto> ListAsync(DetectedQuery query, CancellationToken ct)
    {
        IQueryable<Domain.Entities.DetectedDevice> q = db.DetectedDevices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.DeviceType))
        {
            var parsed = Enum.TryParse<DeviceType>(query.DeviceType, ignoreCase: true, out var dt)
                ? dt
                : (DeviceType?)null;
            if (parsed is not null) q = q.Where(d => d.DeviceType == parsed);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var needle = query.Search.Trim().ToLowerInvariant();
            q = q.Where(d =>
                d.Mac.ToLower().Contains(needle) ||
                (d.SsidLatest != null && d.SsidLatest.ToLower().Contains(needle)) ||
                (d.VendorName != null && d.VendorName.ToLower().Contains(needle)) ||
                (d.BtNameLatest != null && d.BtNameLatest.ToLower().Contains(needle)));
        }

        var count = await q.CountAsync(ct);

        var cursor = CursorCodec.Decode(query.Cursor);
        if (cursor is not null)
        {
            var sortKey = DateTimeOffset.Parse(cursor.SortKey, System.Globalization.CultureInfo.InvariantCulture);
            var id = Guid.Parse(cursor.Id);
            q = q.Where(d =>
                d.LastSeenAt < sortKey ||
                (d.LastSeenAt == sortKey && d.Id.CompareTo(id) < 0));
        }

        q = q.OrderByDescending(d => d.LastSeenAt).ThenByDescending(d => d.Id);

        var limit = Math.Clamp(query.Limit, 1, 200);
        var rows = await q.Take(limit + 1).ToListAsync(ct);

        var items = rows.Take(limit).Select(Mappers.ToDto).ToList();
        var last = rows.Count > limit ? rows[limit - 1] : null;

        return new DetectedDevicePageDto(
            items,
            last is null ? null : CursorCodec.Encode(new Cursor(last.LastSeenAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"), last.Id.ToString())),
            count);
    }

    public Task<DetectedDeviceDto?> GetByMacAsync(string mac, CancellationToken ct)
    {
        var normalized = MacNormalizer.Normalize(mac);
        return db.DetectedDevices.AsNoTracking()
            .Where(d => d.MacNormalized == normalized)
            .Select(d => Mappers.ToDto(d))
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<DetectedDeviceDto>> ListLocatedAsync(CancellationToken ct) =>
        db.DetectedDevices.AsNoTracking()
            .Where(d => d.Latitude != null && d.Longitude != null)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => Mappers.ToDto(d))
            .ToListAsync(ct);

    public async Task<List<SignalPointDto>> GetSignalSeriesAsync(string mac, CancellationToken ct)
    {
        var normalized = MacNormalizer.Normalize(mac);
        return await db.Detections.AsNoTracking()
            .Where(d => d.MacNormalized == normalized)
            .OrderBy(d => d.DetectedAt)
            .Select(d => new SignalPointDto(d.DetectedAt, d.SignalDbm, d.Channel, d.LocationFlag, d.Lat, d.Lon))
            .ToListAsync(ct);
    }
}
