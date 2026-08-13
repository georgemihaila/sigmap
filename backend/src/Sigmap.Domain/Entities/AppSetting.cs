namespace Sigmap.Domain.Entities;

/// <summary>Singleton key/value-ish settings row (WiGLE credentials).</summary>
public class AppSetting
{
    public int Id { get; set; } = 1;
    public string? WigleApiName { get; set; }
    public bool WigleApiKeySet { get; set; }
    public string? WigleUsername { get; set; }
    public bool WiglePasswordSet { get; set; }
}
