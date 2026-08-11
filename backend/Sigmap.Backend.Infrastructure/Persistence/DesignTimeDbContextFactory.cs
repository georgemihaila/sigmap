using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sigmap.Backend.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` to build the context at design time.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SigmapDbContext>
{
    public const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=sigmap;Username=sigmap;Password=sigmap;Include Error Detail=true";

    public SigmapDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? DefaultConnectionString;
        var options = new DbContextOptionsBuilder<SigmapDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new SigmapDbContext(options);
    }
}
