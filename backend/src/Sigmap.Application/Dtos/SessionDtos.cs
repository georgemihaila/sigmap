using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record BoundingAreaDto(double LatMin, double LonMin, double LatMax, double LonMax);

public record SessionDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    SessionStatus Status,
    BoundingAreaDto? BoundingArea,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SessionInputDto(string? Name, string? Description, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);

public record SwarmDto(Guid Id, Guid SessionId, string Name, int DeviceCount);

public record SessionStatsDto(
    int TotalDetections,
    int LocatedDetections,
    int UnlocatedDetections,
    int DeviceCount,
    int OnlineDevices,
    Dictionary<string, int> EncryptionBreakdown,
    Dictionary<string, int> VendorBreakdown,
    Dictionary<string, int> DeviceTypeBreakdown);

public record CoveragePointDto(double Lat, double Lon);

public record CoverageDto(Guid SessionId, List<CoveragePointDto> Points, int Count);
