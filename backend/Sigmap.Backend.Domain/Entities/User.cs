using NetTopologySuite.Geometries;

namespace Sigmap.Backend.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Operator;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
