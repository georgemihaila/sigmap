using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sigmap.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WigleApiName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WigleApiKeySet = table.Column<bool>(type: "boolean", nullable: false),
                    WigleUsername = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WiglePasswordSet = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "detected_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mac = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    MacNormalized = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    VendorOui = table.Column<string>(type: "text", nullable: true),
                    VendorName = table.Column<string>(type: "text", nullable: true),
                    DeviceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SsidLatest = table.Column<string>(type: "text", nullable: true),
                    BtNameLatest = table.Column<string>(type: "text", nullable: true),
                    ChannelLatest = table.Column<int>(type: "integer", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    DetectionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detected_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "detections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwarmId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Mac = table.Column<string>(type: "text", nullable: false),
                    MacNormalized = table.Column<string>(type: "text", nullable: false),
                    Ssid = table.Column<string>(type: "text", nullable: true),
                    BtName = table.Column<string>(type: "text", nullable: true),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    SignalDbm = table.Column<int>(type: "integer", nullable: false),
                    Encryption = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LocationFlag = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lon = table.Column<double>(type: "double precision", nullable: true),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastKnownIp = table.Column<string>(type: "text", nullable: true),
                    PairedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionName = table.Column<string>(type: "text", nullable: true),
                    Format = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RowCount = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pairings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "presets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    IsBuiltin = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "session_devices",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwarmId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: true),
                    ConfigRev = table.Column<int>(type: "integer", nullable: false),
                    PresetId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigSource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PushState = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LastPushId = table.Column<string>(type: "text", nullable: true),
                    ConfigUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_devices", x => new { x.SessionId, x.DeviceId });
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LatMin = table.Column<double>(type: "double precision", nullable: true),
                    LonMin = table.Column<double>(type: "double precision", nullable: true),
                    LatMax = table.Column<double>(type: "double precision", nullable: true),
                    LonMax = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "swarms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_swarms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detected_devices_LastSeenAt_Id",
                table: "detected_devices",
                columns: new[] { "LastSeenAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_detected_devices_MacNormalized",
                table: "detected_devices",
                column: "MacNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detections_BatchId",
                table: "detections",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_detections_MacNormalized_DetectedAt",
                table: "detections",
                columns: new[] { "MacNormalized", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_detections_SessionId_DetectedAt",
                table: "detections",
                columns: new[] { "SessionId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_exports_CreatedAt",
                table: "exports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_pairings_DeviceId",
                table: "pairings",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_presets_OwnerId",
                table: "presets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_session_devices_DeviceId",
                table: "session_devices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_Status",
                table: "sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_swarms_SessionId",
                table: "swarms",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "detected_devices");

            migrationBuilder.DropTable(
                name: "detections");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "exports");

            migrationBuilder.DropTable(
                name: "pairings");

            migrationBuilder.DropTable(
                name: "presets");

            migrationBuilder.DropTable(
                name: "session_devices");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "swarms");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
