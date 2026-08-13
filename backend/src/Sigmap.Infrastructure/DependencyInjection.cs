using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigmap.Application.Abstractions;
using Sigmap.Application.Services;
using Sigmap.Infrastructure.Exports;
using Sigmap.Infrastructure.Live;
using Sigmap.Infrastructure.Persistence;
using Sigmap.Infrastructure.Seeding;
using Sigmap.Infrastructure.Services;
using Sigmap.Infrastructure.Simulation;

namespace Sigmap.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SigmapDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=sigmap;Username=sigmap;Password=sigmap"));
        services.AddScoped<IDbSeeder, DbSeeder>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IFleetService, FleetService>();
        services.AddScoped<IConfigService, ConfigService>();
        services.AddScoped<IPresetService, PresetService>();
        services.AddScoped<IDetectedService, DetectedService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IPairingService, PairingService>();
        services.AddScoped<ISettingsService, SettingsService>();

        services.AddSingleton<ILiveEventBus, InMemoryLiveEventBus>();
        services.AddSingleton<IConfigPushQueue, ConfigPushQueue>();
        services.AddSingleton<IExportFileStore, FileExportFileStore>();

        services.AddHostedService<ConfigAckProcessor>();
        services.AddHostedService<DetectionSimulatorService>();
        services.AddHostedService<ExportProcessorService>();

        return services;
    }
}
