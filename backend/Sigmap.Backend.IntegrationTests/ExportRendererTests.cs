using Sigmap.Backend.Application.Exports;
using Sigmap.Backend.Infrastructure.Exports;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.IntegrationTests;

public class ExportRendererTests
{
    private static ExportRow Row(string mac, double lat, double lon, DeviceType type = DeviceType.Ap) =>
        new(mac, "TestNet", type, 6, -55, lat, lon,
            DateTimeOffset.Parse("2026-08-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-08-01T10:05:00Z"),
            Encryption.Wpa2, "Gps");

    [Fact]
    public void Wigle_csv_has_header_and_skips_unlocated()
    {
        var csv = ExportRenderers.RenderWigleCsv(new[]
        {
            Row("AA:BB:CC:DD:EE:01", 52.5, 13.4),
            new ExportRow("AA:BB:CC:DD:EE:02", null, DeviceType.Ap, 1, -60, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Encryption.Open, "Unlocated"),
        });

        Assert.StartsWith(ExportRenderers.WigleCsvHeader, csv);
        var lines = csv.Trim().Split('\n');
        Assert.Equal(2, lines.Length); // header + one located row
        Assert.Contains("AA:BB:CC:DD:EE:01", lines[1]);
        Assert.Contains("WPA2", lines[1]);
        Assert.Contains("WAP", lines[1]);
        Assert.DoesNotContain("AA:BB:CC:DD:EE:02", csv);
    }

    [Fact]
    public void Geo_json_emits_feature_collection()
    {
        var json = ExportRenderers.RenderGeoJson(new[]
        {
            Row("AA:BB:CC:DD:EE:03", 52.5, 13.4),
            new ExportRow("AA:BB:CC:DD:EE:04", null, DeviceType.Ap, 1, -60, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Encryption.Open, "Unlocated"),
        });

        Assert.Contains("\"type\":\"FeatureCollection\"", json);
        Assert.Contains("[13.400000,52.500000]", json.Replace(" ", string.Empty));
        Assert.DoesNotContain("AA:BB:CC:DD:EE:04", json);
    }

    [Fact]
    public void Csv_escapes_commas_in_ssid()
    {
        var row = Row("AA:BB:CC:DD:EE:05", 52.5, 13.4) with { Ssid = "My,Net" };
        var csv = ExportRenderers.RenderCsv(new[] { row });
        Assert.Contains("\"My,Net\"", csv);
    }
}
