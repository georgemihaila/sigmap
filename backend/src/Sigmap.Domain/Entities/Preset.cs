namespace Sigmap.Domain.Entities;

public class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsBuiltin { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
