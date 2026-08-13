using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class SettingsService(SigmapDbContext db) : ISettingsService
{
    public async Task<WigleSettingsDto> GetWigleAsync(CancellationToken ct)
    {
        var setting = await GetOrCreateAsync(ct);
        return Mappers.ToDto(setting);
    }

    public async Task<WigleSettingsDto> UpdateWigleAsync(WigleSettingsInputDto input, CancellationToken ct)
    {
        var setting = await GetOrCreateAsync(ct);
        if (input.ApiName is not null) setting.WigleApiName = input.ApiName;
        if (input.ApiKey is not null) setting.WigleApiKeySet = input.ApiKey.Length > 0;
        if (input.Username is not null) setting.WigleUsername = input.Username;
        if (input.Password is not null) setting.WiglePasswordSet = input.Password.Length > 0;
        await db.SaveChangesAsync(ct);
        return Mappers.ToDto(setting);
    }

    private async Task<AppSetting> GetOrCreateAsync(CancellationToken ct)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(ct);
        if (setting is null)
        {
            setting = new AppSetting { Id = 1 };
            db.AppSettings.Add(setting);
            await db.SaveChangesAsync(ct);
        }
        return setting;
    }
}
