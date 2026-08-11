namespace Sigmap.Backend.Domain.Entities;

/// <summary>Named, reusable scan-config template, not tied to a device or session.</summary>
public class ScanPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>ScanConfig JSON.</summary>
    public string ConfigJson { get; set; } = "{}";

    public bool IsBuiltin { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
