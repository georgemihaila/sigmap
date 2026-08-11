namespace Sigmap.Backend.Domain.Entities;

/// <summary>Rule-based alert. Config is rule-type specific JSON.</summary>
public class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = "{}";
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
