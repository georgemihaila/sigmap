using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigmap.Application.Abstractions;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Simulation;

/// <summary>
/// Simulates the scanner ACK round-trip: ~1.4s after a config push it flips the
/// row to acked (90%) or failed (10%) and broadcasts the final state.
/// </summary>
public sealed class ConfigAckProcessor(
    IConfigPushQueue queue,
    ILiveEventBus liveBus,
    IServiceScopeFactory scopeFactory,
    ILogger<ConfigAckProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan AckDelay = TimeSpan.FromMilliseconds(1400);
    private readonly SimRng _rng = new(20260812);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var push in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await Task.Delay(AckDelay, stoppingToken);
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SigmapDbContext>();
                var sd = await db.SessionDevices
                    .FirstOrDefaultAsync(x => x.SessionId == push.SessionId && x.DeviceId == push.DeviceId, stoppingToken);
                if (sd is null || sd.LastPushId != push.PushId) continue;

                sd.PushState = _rng.Next() < 0.1 ? PushState.Failed : PushState.Acked;
                sd.ConfigUpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(stoppingToken);

                liveBus.Publish(new LiveEvent.Config
                {
                    SessionId = push.SessionId,
                    DeviceId = push.DeviceId,
                    PushId = push.PushId,
                    Status = sd.PushState,
                });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Config ACK processing failed for push {PushId}", push.PushId);
            }
        }
    }
}
