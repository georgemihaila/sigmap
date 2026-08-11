namespace Sigmap.Backend.Domain.Entities;

public class Swarm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Session? Session { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SessionDevice> Members { get; set; } = new List<SessionDevice>();
}
