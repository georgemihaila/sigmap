using Sigmap.Contracts.Proto;

namespace Sigmap.Scanner.Agent;

/// <summary>Thread-safe snapshot of the live scan config, updated by the
/// ConfigConsumer when the backend pushes new settings.</summary>
public sealed class LiveScanConfig
{
    private ScanConfig _config;
    private TaskCompletionSource _changed = New();

    public LiveScanConfig(ScanConfig initial) => _config = initial;

    public ScanConfig Value => Volatile.Read(ref _config);

    /// <summary>Applies a new config and signals waiters (source restarts).</summary>
    public void Set(ScanConfig config)
    {
        Volatile.Write(ref _config, config);
        var old = Interlocked.Exchange(ref _changed, New());
        old.TrySetResult();
    }

    /// <summary>Completes when the config changes or the token cancels.</summary>
    public Task ChangedAsync(CancellationToken ct) => _changed.Task.WaitAsync(ct);

    private static TaskCompletionSource New() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
