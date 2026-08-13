using Sigmap.Domain.Enums;

namespace Sigmap.Application.Dtos;

public record UserDto(Guid Id, string Username, UserRole Role);

public record LoginRequest(string Username, string Password);

public record LoginResponse(UserDto User);
