using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sigmap.Api.IntegrationTests;

/// <summary>
/// Boots the real API against a disposable Postgres (Testcontainers). The app's
/// startup migrations + seeder run automatically, so every test starts from the
/// seeded dev dataset.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private WebApplicationFactory<Program>? _factory;

    /// <summary>Anonymous client (no cookies) for unauthenticated assertions.</summary>
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithUsername("sigmap")
            .WithPassword("sigmap")
            .WithDatabase("sigmap")
            .Build();
        await _postgres.StartAsync();

        var port = _postgres.GetMappedPublicPort(5432);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        $"Host=127.0.0.1;Port={port};Database=sigmap;Username=sigmap;Password=sigmap",
                })));

        Client = _factory.CreateClient();
    }

    /// <summary>Fresh in-process client (with its own cookie jar) for a single test.</summary>
    public HttpClient CreateClient() => _factory!.CreateClient();

    public async Task DisposeAsync()
    {
        Client.Dispose();
        _factory?.Dispose();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }
}

public sealed class ApiTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public ApiTests(ApiFixture fixture) => _fixture = fixture;

    private async Task<HttpClient> LoginAsync(string username = "operator", string password = "sigmap-dev")
    {
        var client = _fixture.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return client;
    }

    [Fact]
    public async Task AuthLifecycle()
    {
        var anon = await _fixture.Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var bad = await _fixture.Client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var client = await LoginAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("operator", me.GetProperty("user").GetProperty("username").GetString());

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me2 = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me2.StatusCode);
    }

    [Fact]
    public async Task ViewerCannotMutate()
    {
        var viewer = await LoginAsync("viewer", "viewer");
        var resp = await viewer.PostAsJsonAsync("/api/sessions", new { name = "nope" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SessionsListedAndArchiveWorks()
    {
        var client = await LoginAsync();
        var sessions = await client.GetFromJsonAsync<JsonElement[]>("/api/sessions");
        Assert.NotEmpty(sessions);

        var active = sessions.First(s => s.GetProperty("status").GetString() == "active");
        var id = active.GetProperty("id").GetString()!;

        var archived = await client.PostAsync($"/api/sessions/{id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        var body = await archived.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("archived", body.GetProperty("status").GetString());

        var stats = await client.GetAsync($"/api/sessions/{id}/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        var fleet = await client.GetFromJsonAsync<JsonElement[]>($"/api/sessions/{id}/fleet");
        Assert.NotEmpty(fleet);
    }

    [Fact]
    public async Task DetectedDevicesKeysetPaginationIsStable()
    {
        var client = await LoginAsync();

        var seen = new HashSet<string>();
        string? cursor = null;
        var iterations = 0;

        while (iterations++ < 500)
        {
            var url = $"/api/detected-devices?limit=37{(cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}")}";
            var page = await client.GetFromJsonAsync<JsonElement>(url);
            foreach (var item in page.GetProperty("items").EnumerateArray())
            {
                var key = item.GetProperty("macNormalized").GetString()!;
                Assert.False(seen.Contains(key), $"duplicate row {key}");
                seen.Add(key);
            }
            cursor = page.GetProperty("nextCursor").GetString();
            if (cursor is null) break;
        }

        Assert.True(cursor is null, "pagination should terminate");
        Assert.NotEqual(0, seen.Count);
    }

    [Fact]
    public async Task ConfigApplyGoesPendingThenAcked()
    {
        var client = await LoginAsync();
        var fleet = await client.GetFromJsonAsync<JsonElement[]>("/api/fleet");
        var member = fleet.First(f => f.GetProperty("sessionId").ValueKind == JsonValueKind.String
                                      && f.GetProperty("config").ValueKind == JsonValueKind.Object);
        var sessionId = member.GetProperty("sessionId").GetString()!;
        var deviceId = member.GetProperty("device").GetProperty("id").GetString()!;

        var push = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}/devices/{deviceId}/config",
            new { configJson = "{\"channelHopMs\":333}", presetId = (string?)null });
        Assert.Equal(HttpStatusCode.OK, push.StatusCode);
        var pushBody = await push.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", pushBody.GetProperty("pushState").GetString());

        await Task.Delay(2500);

        var status = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sessionId}/devices/{deviceId}/config/push-status");
        var state = status.GetProperty("pushState").GetString();
        Assert.True(state is "acked" or "failed");
    }

    [Fact]
    public async Task ExportsCreatedQueuedAndEventuallyDone()
    {
        var client = await LoginAsync();
        var created = await client.PostAsJsonAsync("/api/exports", new { format = "wigle_csv", sessionId = (string?)null });
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString()!;

        await Task.Delay(4000);
        var download = await client.GetAsync($"/api/exports/{id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var content = await download.Content.ReadAsStringAsync();
        Assert.StartsWith("MAC,SSID", content);
    }

    [Fact]
    public async Task PairingApproveCreatesDevice()
    {
        var client = await LoginAsync();
        var pending = await client.GetFromJsonAsync<JsonElement[]>("/api/pairing/pending");
        Assert.NotEmpty(pending);

        var deviceId = pending[0].GetProperty("deviceId").GetString()!;
        var approve = await client.PostAsJsonAsync($"/api/pairing/{deviceId}/approve", new { sessionId = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var fleet = await client.GetFromJsonAsync<JsonElement[]>("/api/fleet");
        Assert.Contains(fleet, f => f.GetProperty("device").GetProperty("id").GetString() == deviceId);
    }
}
