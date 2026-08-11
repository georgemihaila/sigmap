using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Sigmap.Backend.IntegrationTests;

/// <summary>
/// One Postgres+PostGIS and one RabbitMQ container per test collection.
/// Real dependencies — never mocks.
/// </summary>
public sealed class IntegrationFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; }
    public RabbitMqContainer RabbitMq { get; }

    public string PostgresConnectionString => Postgres.GetConnectionString();
    public string RabbitMqConnectionString => RabbitMq.GetConnectionString();

    public IntegrationFixture()
    {
        Postgres = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
        RabbitMq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    }

    public async Task InitializeAsync()
    {
        await Postgres.StartAsync();
        await RabbitMq.StartAsync();
    }

    public Task DisposeAsync()
    {
        Postgres.DisposeAsync().GetAwaiter().GetResult();
        RabbitMq.DisposeAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture>
{
}
