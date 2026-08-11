using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Serilog;
using Sigmap.Bff;
using Sigmap.Bff.Live;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var backendBase = builder.Configuration["Backend:BaseUrl"]
    ?? throw new InvalidOperationException("Backend:BaseUrl is not configured");
var rmq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is not configured");

var operatorUser = builder.Configuration["Auth:OperatorUsername"] ?? "operator";
var operatorPass = builder.Configuration["Auth:OperatorPassword"] ?? "sigmap-dev";
var viewerUser = builder.Configuration["Auth:ViewerUsername"] ?? "viewer";
var viewerPass = builder.Configuration["Auth:ViewerPassword"] ?? "viewer-dev";

builder.Services.AddSingleton(new LiveEventBus());
builder.Services.AddSingleton(sp => new RabbitMqConnectionProvider(
    rmq, sp.GetRequiredService<ILogger<RabbitMqConnectionProvider>>()));
builder.Services.AddHostedService<LiveEventConsumerService>();
builder.Services.AddGrpc();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddHttpClient("backend", c => c.BaseAddress = new Uri(backendBase));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "sigmap_session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.Events.OnRedirectToLogin = c =>
        {
            c.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("operator", p => p.RequireClaim(ClaimTypes.Role, "operator"));
});

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseGrpcWeb();

app.MapGrpcService<LiveStreamService>().EnableGrpcWeb();
app.MapGet("/api/health", () => Results.Ok(new { service = "bff", status = "ok" }));

app.MapPost("/api/auth/login", async (LoginRequest req, HttpContext ctx, CancellationToken ct) =>
{
    var (username, password) = req;
    string? role = null;
    if (username == operatorUser && password == operatorPass)
        role = "operator";
    else if (username == viewerUser && password == viewerPass)
        role = "viewer";

    if (role is null)
        return Results.Json(new { error = "invalid credentials" }, statusCode: StatusCodes.Status401Unauthorized);

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, role),
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true });
    return Results.Ok(new { username, role });
});

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
});

app.MapGet("/api/auth/me", (HttpContext ctx) =>
{
    var user = ctx.User.Identity?.IsAuthenticated == true ? ctx.User : null;
    return Results.Ok(new
    {
        authenticated = user is not null,
        username = user?.Identity?.Name,
        role = user?.FindFirst(ClaimTypes.Role)?.Value,
    });
});

// Lightweight REST proxy: the frontend only talks to the BFF; request/response
// operations are forwarded to the core API unchanged.
app.Map("/api/{**rest}", ForwardAsync);

app.Run();

static async Task<IResult> ForwardAsync(
    HttpContext ctx, IHttpClientFactory factory, CancellationToken ct)
{
    var rest = (string?)ctx.Request.RouteValues["rest"] ?? string.Empty;
    var path = "/" + rest;

    if (path == "/api/health")
        return Results.Ok(new { service = "bff", status = "ok" });

    if (ctx.User.Identity?.IsAuthenticated != true)
        return Results.Json(new { error = "unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);

    if (RequiresOperator(path) && !ctx.User.IsInRole("operator"))
        return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    var client = factory.CreateClient("backend");
    var target = $"/api/v1/{rest}{ctx.Request.QueryString}";

    using var request = new HttpRequestMessage(new HttpMethod(ctx.Request.Method), target);
    foreach (var header in ctx.Request.Headers)
    {
        if (header.Key.StartsWith("Content", StringComparison.OrdinalIgnoreCase))
            continue;
        request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    if (ctx.Request.ContentLength is > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        using var body = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(body, ct);
        request.Content = new ByteArrayContent(body.ToArray());
        if (ctx.Request.ContentType is not null)
            request.Content.Headers.TryAddWithoutValidation("Content-Type", ctx.Request.ContentType);
    }

    var response = await client.SendAsync(request, ct);
    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        return Results.NoContent();

    var responseBody = await response.Content.ReadAsByteArrayAsync(ct);
    return Results.Bytes(
        responseBody,
        response.Content.Headers.ContentType?.ToString() ?? "application/json");
}

static bool RequiresOperator(string path) =>
    path.Contains("/config", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/pairing/", StringComparison.OrdinalIgnoreCase)
    || (path.StartsWith("/sessions", StringComparison.OrdinalIgnoreCase) && path.Contains("archive"))
    || path.StartsWith("/presets", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/wigle", StringComparison.OrdinalIgnoreCase)
    || path.StartsWith("/settings/wigle", StringComparison.OrdinalIgnoreCase);

record LoginRequest(string Username, string Password);
