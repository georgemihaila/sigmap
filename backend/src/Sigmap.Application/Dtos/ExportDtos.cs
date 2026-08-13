using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record ExportRecordDto(
    Guid Id,
    Guid? SessionId,
    string? SessionName,
    ExportFormat Format,
    ExportStatus Status,
    string? FileName,
    DateTimeOffset CreatedAt,
    Guid OwnerId,
    long? SizeBytes,
    long? RowCount);

public record ExportInputDto(
    Guid? SessionId,
    ExportFormat Format,
    DateTimeOffset? From,
    DateTimeOffset? To);

public record ExportCreatedDto(Guid Id, ExportFormat Format, ExportStatus Status, DateTimeOffset CreatedAt, string? FileName);

public record WigleSettingsDto(
    string? ApiName,
    bool ApiKeySet,
    string? Username,
    bool PasswordSet);

public record WigleSettingsInputDto(
    string? ApiName,
    string? ApiKey,
    string? Username,
    string? Password);

public record WigleUploadResultDto(string Status, string? Message);

public record WigleImportResultDto(int Imported);
