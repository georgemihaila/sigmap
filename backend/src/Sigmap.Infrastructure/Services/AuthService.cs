using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class AuthService(SigmapDbContext db) : IAuthService
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<UserDto?> ValidateAsync(string username, string password, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null) return null;
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;
        return Mappers.ToDto(user);
    }

    public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => Mappers.ToDto(u)).FirstOrDefaultAsync(ct);
}
