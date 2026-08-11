using Sigmap.Backend.Domain.Entities;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Application.Pairing;

public interface IPairingService
{
    /// <summary>Registers (or re-registers) a scanner device and returns the
    /// approval state. Devices start PENDING and scan-nothing until approved.</summary>
    Task<PairingResponse> RequestAsync(PairingRequest request, string? clientIp, CancellationToken ct);

    Task<IReadOnlyList<Device>> PendingAsync(CancellationToken ct);

    /// <summary>Approves a device into a session: assigns it, ensures its durable
    /// config queue, and pushes the built-in "Off" config.</summary>
    Task<Device> ApproveAsync(Guid deviceId, Guid sessionId, CancellationToken ct);

    Task RejectAsync(Guid deviceId, CancellationToken ct);

    /// <summary>Payload encoded in the pairing QR code (host + short-lived token).</summary>
    string BuildQrPayload(Guid sessionId);
}
