using System.Text.Json;

namespace CodexQuotaDashboard;

public sealed class SettingsStore
{
    private readonly string _dataDirectory;
    private readonly string _settingsPath;
    private readonly string _cachePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsStore()
    {
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        _settingsPath = Path.Combine(_dataDirectory, "settings.json");
        _cachePath = Path.Combine(_dataDirectory, "quota-cache.json");
    }

    public DashboardSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
                return JsonSerializer.Deserialize<DashboardSettings>(File.ReadAllText(_settingsPath)) ?? new();
        }
        catch { }
        return new();
    }

    public void SaveSettings(DashboardSettings value)
    {
        Directory.CreateDirectory(_dataDirectory);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(value, JsonOptions));
    }

    public QuotaSnapshot? LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
                return JsonSerializer.Deserialize<QuotaSnapshot>(File.ReadAllText(_cachePath));
        }
        catch { }
        return null;
    }

    public void SaveCache(QuotaSnapshot value)
    {
        Directory.CreateDirectory(_dataDirectory);
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(value, JsonOptions));
    }
}
