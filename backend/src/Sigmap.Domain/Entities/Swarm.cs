namespace Sigmap.Domain.Entities;

public class Swarm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
}
