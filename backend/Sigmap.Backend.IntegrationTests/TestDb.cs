using Microsoft.EntityFrameworkCore;
using Sigmap.Backend.Infrastructure.Persistence;

namespace Sigmap.Backend.IntegrationTests;

public static class TestDb
{
    public static SigmapDbContext CreateDb(string connectionString) => new(
        new DbContextOptionsBuilder<SigmapDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options);
}
