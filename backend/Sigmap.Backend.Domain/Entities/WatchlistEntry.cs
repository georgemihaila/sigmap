namespace Sigmap.Backend.Domain.Entities;

public class WatchlistEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }
    public string? Mac { get; set; }
    public string? Ssid { get; set; }
    public string Tag { get; set; } = string.Empty;
    public WatchlistAction Action { get; set; } = WatchlistAction.Highlight;
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
