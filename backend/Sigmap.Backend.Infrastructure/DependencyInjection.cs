using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sigmap.Backend.Application.Configuration;
using Sigmap.Backend.Application.Ingestion;
using Sigmap.Backend.Application.Lookup;
using Sigmap.Backend.Application.Messaging;
using Sigmap.Backend.Application.Exports;
using Sigmap.Backend.Application.Pairing;
using Sigmap.Backend.Infrastructure.Configuration;
using Sigmap.Backend.Infrastructure.Exports;
using Sigmap.Backend.Infrastructure.Ingestion;
using Sigmap.Backend.Infrastructure.Messaging;
using Sigmap.Backend.Infrastructure.Retention;
using Sigmap.Backend.Infrastructure.Pairing;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Geo;

namespace Sigmap.Backend.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SigmapDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        return services;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(sp => new RabbitMqConnectionProvider(
            connectionString,
            sp.GetRequiredService<ILogger<RabbitMqConnectionProvider>>()));

        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddScoped<IConfigPushPublisher, RabbitMqConfigPushPublisher>();

        services.AddHostedService<IngestConsumerService>();
        services.AddHostedService<HeartbeatConsumerService>();
        services.AddHostedService<ConfigAckConsumerService>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocationResolver, TimeInterpolationLocationResolver>();
        services.AddSingleton<OuiLookup>();
        services.AddScoped<IDetectionBatchProcessor, DetectionBatchProcessor>();
        services.AddScoped<HeartbeatProcessor>();
        services.AddScoped<ConfigAckProcessor>();
        services.AddScoped<ISessionConfigService, SessionConfigService>();
        services.AddScoped<IPairingService, PairingService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddHostedService<RetentionService>();
        return services;
    }
}
