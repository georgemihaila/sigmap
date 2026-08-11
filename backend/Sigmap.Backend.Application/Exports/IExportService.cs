using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Exports;

public sealed record ExportRow(
    string Mac,
    string? Ssid,
    DeviceType DeviceType,
    int Channel,
    int SignalDbm,
    double? Lat,
    double? Lon,
    DateTimeOffset FirstTime,
    DateTimeOffset LastTime,
    Encryption Encryption,
    string LocationFlag);

public enum WigleUploadResultStatus
{
    Ok,
    NoCredentials,
    Failed,
}

public sealed record WigleUploadResult(WigleUploadResultStatus Status, string? Message);

/// <summary>Generates export files and integrates with WiGLE (upload/import).</summary>
public interface IExportService
{
    Task<ExportRecordDto> CreateExportAsync(Guid? sessionId, string format, CancellationToken ct);
    Task<IReadOnlyList<ExportRecordDto>> ListExportsAsync(CancellationToken ct);
    Task<WigleUploadResult> UploadSessionToWigleAsync(Guid sessionId, CancellationToken ct);
    Task<int> ImportFromWigleAsync(CancellationToken ct);
}

public sealed record ExportRecordDto(Guid Id, Guid? SessionId, string Format, string Status, string? FilePath, string? Error, DateTimeOffset CreatedAt);
