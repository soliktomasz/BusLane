using System.Text.Json;
using BusLane.Services.Abstractions;

namespace BusLane.Services.Infrastructure;

/// <summary>
/// Implementation of IPreferencesService that persists preferences to a JSON file.
/// </summary>
public class PreferencesService : IPreferencesService
{
    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BusLane",
        "preferences.json"
    );

    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool ConfirmBeforePurge { get; set; } = true;
    public bool AutoRefreshMessages { get; set; }
    public int AutoRefreshIntervalSeconds { get; set; } = 30;
    public int DefaultMessageCount { get; set; } = 100;
    public bool ShowDeadLetterBadges { get; set; } = true;
    public bool EnableMessagePreview { get; set; } = true;
    public string Theme { get; set; } = "Light";

    public event EventHandler? PreferencesChanged;

    public PreferencesService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var json = File.ReadAllText(PreferencesPath);
                var data = JsonSerializer.Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    ConfirmBeforeDelete = data.ConfirmBeforeDelete;
                    ConfirmBeforePurge = data.ConfirmBeforePurge;
                    AutoRefreshMessages = data.AutoRefreshMessages;
                    AutoRefreshIntervalSeconds = data.AutoRefreshIntervalSeconds;
                    DefaultMessageCount = data.DefaultMessageCount;
                    ShowDeadLetterBadges = data.ShowDeadLetterBadges;
                    EnableMessagePreview = data.EnableMessagePreview;
                    Theme = data.Theme ?? "Light";
                }
            }
        }
        catch
        {
            // Use defaults if loading fails
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new PreferencesData
            {
                ConfirmBeforeDelete = ConfirmBeforeDelete,
                ConfirmBeforePurge = ConfirmBeforePurge,
                AutoRefreshMessages = AutoRefreshMessages,
                AutoRefreshIntervalSeconds = AutoRefreshIntervalSeconds,
                DefaultMessageCount = DefaultMessageCount,
                ShowDeadLetterBadges = ShowDeadLetterBadges,
                EnableMessagePreview = EnableMessagePreview,
                Theme = Theme
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(PreferencesPath, json);
            
            PreferencesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // Silently fail if saving doesn't work
        }
    }

    private class PreferencesData
    {
        public bool ConfirmBeforeDelete { get; set; }
        public bool ConfirmBeforePurge { get; set; }
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; }
        public int DefaultMessageCount { get; set; }
        public bool ShowDeadLetterBadges { get; set; }
        public bool EnableMessagePreview { get; set; }
        public string? Theme { get; set; }
    }
}
