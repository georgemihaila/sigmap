using Microsoft.AspNetCore.Http.HttpResults;
using Serilog;
using Sigmap.Backend.Api.Endpoints;
using Sigmap.Backend.Domain;
using Sigmap.Backend.Domain.Entities;
using Sigmap.Backend.Infrastructure;
using Sigmap.Backend.Infrastructure.Persistence;
using Sigmap.Contracts.Proto;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var pg = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured");
var rmq = builder.Configuration.GetConnectionString("RabbitMq")
    ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq is not configured");

builder.Services.AddPersistence(pg);
builder.Services.AddMessaging(rmq);
builder.Services.AddApplicationServices();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("wigle");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();

app.MapOpenApi();

// Migrate + seed before serving (idempotent, safe on every start).
await SeedData.MigrateAndSeedAsync(app.Services);

var api = app.MapGroup("/api/v1");
api.MapSessionEndpoints();
api.MapDeviceEndpoints();
api.MapPresetEndpoints();
api.MapConfigEndpoints();
api.MapIngestEndpoints();
api.MapPairingEndpoints();
api.MapWatchlistEndpoints();
api.MapAlertEndpoints();
api.MapStatsEndpoints();
api.MapExportEndpoints();
api.MapWigleEndpoints();
api.MapCoverageEndpoints();

app.Run();

namespace Sigmap.Backend.Api
{
    public partial class Program
    {
    }
}
