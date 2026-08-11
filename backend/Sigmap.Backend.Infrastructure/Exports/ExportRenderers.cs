using System.Text;
using Sigmap.Backend.Application.Exports;
using Sigmap.Contracts.Proto;

namespace Sigmap.Backend.Infrastructure.Exports;

/// <summary>Renders detections into export formats.</summary>
public static class ExportRenderers
{
    /// <summary>WiGLE CSV — the canonical header WiGLE accepts on upload.</summary>
    public const string WigleCsvHeader = "MAC,SSID,AuthMode,FirstTime,LastTime,Channel,RSSI,CurrentLatitude,CurrentLongitude,AltitudeMeters,AccuracyMeters,Type";

    public static string RenderWigleCsv(IEnumerable<ExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(WigleCsvHeader);
        foreach (var row in rows)
        {
            if (row.Lat is null || row.Lon is null)
                continue;
            sb.Append(Csv(row.Mac));
            sb.Append(',').Append(Csv(row.Ssid ?? string.Empty));
            sb.Append(',').Append(Csv(WigleAuthMode(row.Encryption)));
            sb.Append(',').Append(Csv(row.FirstTime.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.Append(',').Append(Csv(row.LastTime.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.Append(',').Append(row.Channel);
            sb.Append(',').Append(row.SignalDbm);
            sb.Append(',').Append(row.Lat.Value.ToString("F6"));
            sb.Append(',').Append(row.Lon.Value.ToString("F6"));
            sb.Append(",0,0,").Append(WigleType(row.DeviceType));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string RenderCsv(IEnumerable<ExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MAC,SSID,Type,Channel,SignalDbm,Latitude,Longitude,DetectedAt,LocationFlag");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.Mac));
            sb.Append(',').Append(Csv(row.Ssid ?? string.Empty));
            sb.Append(',').Append(row.DeviceType.ToString());
            sb.Append(',').Append(row.Channel);
            sb.Append(',').Append(row.SignalDbm);
            sb.Append(',').Append(row.Lat?.ToString("F6") ?? string.Empty);
            sb.Append(',').Append(row.Lon?.ToString("F6") ?? string.Empty);
            sb.Append(',').Append(row.FirstTime.ToString("O"));
            sb.Append(',').Append(row.LocationFlag);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string RenderGeoJson(IEnumerable<ExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{\"type\":\"FeatureCollection\",\"features\":[");
        var first = true;
        foreach (var row in rows)
        {
            if (row.Lat is null || row.Lon is null)
                continue;
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append("{\"type\":\"Feature\",\"geometry\":{\"type\":\"Point\",\"coordinates\":[")
              .Append(row.Lon.Value.ToString("F6")).Append(',')
              .Append(row.Lat.Value.ToString("F6"))
              .Append("]},\"properties\":{")
              .Append("\"mac\":").Append(Json(row.Mac))
              .Append(",\"ssid\":").Append(Json(row.Ssid))
              .Append(",\"type\":").Append(Json(row.DeviceType.ToString()))
              .Append(",\"signalDbm\":").Append(row.SignalDbm)
              .Append(",\"channel\":").Append(row.Channel)
              .Append(",\"detectedAt\":").Append(Json(row.FirstTime.ToString("O")))
              .Append("}}");
        }

        sb.AppendLine("]}");
        return sb.ToString();
    }

    private static string WigleAuthMode(Encryption encryption) => encryption switch
    {
        Encryption.Open => "[ESS]",
        Encryption.Wep => "WEP[ESS]",
        Encryption.Wpa => "WPA[ESS]",
        Encryption.Wpa2 => "WPA2[ESS]",
        Encryption.Wpa3 => "WPA3[ESS]",
        _ => string.Empty,
    };

    private static string WigleType(DeviceType type) => type switch
    {
        DeviceType.Ap => "WAP",
        DeviceType.Bluetooth or DeviceType.BtLe => "BT",
        _ => "WAP",
    };

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string Json(string? value) =>
        value is null ? "null" : $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
