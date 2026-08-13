using Sigmap.Domain.Enums;
using Sigmap.Domain.ScanConfig;

namespace Sigmap.Application.Dtos;

public record ScanPresetDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? OwnerId,
    string ConfigJson,
    bool IsBuiltin,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record PresetInputDto(string? Name, string? Description, string? ConfigJson);

public record ConfigChangeDto(string Path, string Before, string After);

public record ConfigDiffDto(
    string DeviceId,
    string DeviceName,
    List<ConfigChangeDto> Changes,
    bool Equal)
{
    public static ConfigDiffDto From(ConfigDiff diff) => new(
        diff.DeviceId,
        diff.DeviceName,
        diff.Changes.Select(c => new ConfigChangeDto(c.Path, c.Before, c.After)).ToList(),
        diff.Equal);
}

public record PreviewRequest(string ConfigJson);

public record ApplyConfigRequest(string ConfigJson, List<Guid>? DeviceIds);

public record ApplyDeviceConfigRequest(string ConfigJson, Guid? PresetId);

public record ApplyPresetRequest(List<Guid>? DeviceIds);
