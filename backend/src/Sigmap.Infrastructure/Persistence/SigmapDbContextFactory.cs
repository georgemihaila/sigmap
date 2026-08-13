using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sigmap.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` run migrations without the web host up.</summary>
public sealed class SigmapDbContextFactory : IDesignTimeDbContextFactory<SigmapDbContext>
{
    public SigmapDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("Sigmap__ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=sigmap;Username=sigmap;Password=sigmap";

        var options = new DbContextOptionsBuilder<SigmapDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SigmapDbContext(options);
    }
}
