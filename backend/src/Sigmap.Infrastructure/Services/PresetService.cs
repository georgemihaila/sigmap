using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Domain.ScanConfig;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class PresetService(SigmapDbContext db) : IPresetService
{
    public Task<List<ScanPresetDto>> ListAsync(CancellationToken ct) =>
        db.Presets.AsNoTracking().OrderBy(p => p.CreatedAt).Select(p => Mappers.ToDto(p)).ToListAsync(ct);

    public async Task<ScanPresetDto> CreateAsync(PresetInputDto input, Guid ownerId, CancellationToken ct)
    {
        var preset = new Preset
        {
            Name = string.IsNullOrWhiteSpace(input.Name) ? "Untitled preset" : input.Name,
            Description = input.Description,
            OwnerId = ownerId,
            ConfigJson = string.IsNullOrWhiteSpace(input.ConfigJson) ? ScanConfigParser.Stringify(ScanConfigParser.Default()) : input.ConfigJson,
            IsBuiltin = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Presets.Add(preset);
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(preset);
    }

    public async Task<ScanPresetDto?> UpdateAsync(Guid id, PresetInputDto input, CancellationToken ct)
    {
        var preset = await db.Presets.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (preset is null) return null;
        if (input.Name is not null) preset.Name = input.Name;
        if (input.Description is not null) preset.Description = input.Description;
        if (input.ConfigJson is not null) preset.ConfigJson = input.ConfigJson;
        preset.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(preset);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var preset = await db.Presets.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (preset is null) return false;
        db.Presets.Remove(preset);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
