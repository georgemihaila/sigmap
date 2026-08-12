using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Sigmap.Scanner.Agent.Linux;

public sealed record WirelessInterface(string Name, string Driver, bool SupportsMonitor);

/// <summary>
/// Isolated seam for monitor-mode / channel control. v1 shells out to
/// <c>iw</c>; a raw nl80211/netlink implementation can be swapped in later
/// without touching the hop loop or capture code.
/// </summary>
public interface ILinuxWireless
{
    Task<IReadOnlyList<WirelessInterface>> GetInterfacesAsync(CancellationToken ct);
    Task SetMonitorModeAsync(string iface, bool enabled, CancellationToken ct);
    Task SetChannelAsync(string iface, int channel, CancellationToken ct);
    Task<int?> GetCurrentChannelAsync(string iface, CancellationToken ct);
}

/// <summary>iw-based implementation. Requires CAP_NET_ADMIN (or root).</summary>
public sealed class IwLinuxWireless : ILinuxWireless
{
    private static readonly Regex IfaceLine = new(@"^\s*(\S+)\s+([0-9]+)?\s*(unassociated|[0-9a-fA-F:]+)?\s*(.*)$");
    private static readonly Regex ChannelLine = new(@"\bchannel\s+(\d+)\b", RegexOptions.IgnoreCase);

    private readonly ILogger<IwLinuxWireless> _log;

    public IwLinuxWireless(ILogger<IwLinuxWireless> log) => _log = log;

    public async Task<IReadOnlyList<WirelessInterface>> GetInterfacesAsync(CancellationToken ct)
    {
        var (exit, stdout, stderr) = await RunAsync("iw", "dev", ct);
        if (exit != 0)
        {
            _log.LogWarning("iw dev failed: {Stderr}", stderr);
            return Array.Empty<WirelessInterface>();
        }

        var result = new List<WirelessInterface>();
        string? currentIface = null;
        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Interface ", StringComparison.Ordinal))
            {
                currentIface = line["Interface ".Length..].Trim();
                result.Add(new WirelessInterface(currentIface, string.Empty, false));
            }
            else if (line.StartsWith("driver ", StringComparison.Ordinal) && currentIface is not null)
            {
                var idx = result.FindIndex(r => r.Name == currentIface);
                if (idx >= 0)
                {
                    result[idx] = result[idx] with { Driver = line["driver ".Length..].Trim() };
                }
            }
            else if (line.StartsWith("supported interface modes", StringComparison.Ordinal) && currentIface is not null)
            {
                var idx = result.FindIndex(r => r.Name == currentIface);
                if (idx >= 0)
                {
                    result[idx] = result[idx] with { SupportsMonitor = line.Contains("monitor", StringComparison.OrdinalIgnoreCase) };
                }
            }
        }

        return result;
    }

    public async Task SetMonitorModeAsync(string iface, bool enabled, CancellationToken ct)
    {
        // Mode switches require the interface to be administratively down;
        // otherwise iw fails with "Device or resource busy".
        var down = await RunAsync("ip", $"link set {iface} down", ct);
        if (down.ExitCode != 0)
            _log.LogWarning("Failed to bring {Iface} down before mode switch: {Stderr}", iface, down.Stderr);

        var mode = enabled ? "monitor" : "managed";
        var (exit, _, stderr) = await RunAsync("iw", $"dev {iface} set type {mode}", ct);
        if (exit != 0)
            throw new InvalidOperationException($"Failed to set {iface} to {mode}: {stderr}");
        _log.LogInformation("{Iface} -> {Mode}", iface, mode);

        var up = await RunAsync("ip", $"link set {iface} up", ct);
        if (up.ExitCode != 0)
            _log.LogWarning("Failed to bring {Iface} up after mode switch: {Stderr}", iface, up.Stderr);
    }

    public async Task SetChannelAsync(string iface, int channel, CancellationToken ct)
    {
        var (exit, _, stderr) = await RunAsync("iw", $"dev {iface} set channel {channel}", ct);
        if (exit != 0)
            throw new InvalidOperationException($"Failed to set {iface} channel {channel}: {stderr}");
    }

    public async Task<int?> GetCurrentChannelAsync(string iface, CancellationToken ct)
    {
        var (exit, stdout, _) = await RunAsync("iw", $"dev {iface} info", ct);
        if (exit != 0)
            return null;
        var match = ChannelLine.Match(stdout);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string command, string args, CancellationToken ct)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        return (proc.ExitCode, stdout, stderr);
    }
}
