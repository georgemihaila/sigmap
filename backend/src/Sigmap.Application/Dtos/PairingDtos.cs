using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record PairingRequestDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string Platform,
    DeviceCapabilitiesDto Capabilities,
    PairingStatus Status,
    DateTimeOffset RequestedAt,
    Guid? SessionId);

public record PairingQrDto(string Token, string Payload);

public record ApprovePairingRequest(Guid? SessionId);
