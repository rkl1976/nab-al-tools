using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NabXliffApp.Services;

public class SessionStateService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".nab-xliff-web");
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    private readonly ILogger<SessionStateService> _logger;

    public string? AppFolderPath { get; set; }
    public string? WorkspaceFilePath { get; set; }
    public string? XlfFilePath { get; set; }
    public string? TargetLanguage { get; set; }
    public int PageOffset { get; set; }
    public int PageSize { get; set; } = 50;
    public string? StateFilter { get; set; }
    public string? SearchKeyword { get; set; }
    public bool SearchInTarget { get; set; }
    public List<string> AvailableXlfFiles { get; set; } = [];

    public SessionStateService(ILogger<SessionStateService> logger)
    {
        _logger = logger;
        LoadConfig();
    }

    public void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var config = new SavedConfig
            {
                AppFolderPath = AppFolderPath,
                WorkspaceFilePath = WorkspaceFilePath,
                XlfFilePath = XlfFilePath,
                TargetLanguage = TargetLanguage
            };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
            _logger.LogInformation("[Session] Config saved to {Path}", ConfigFile);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Session] Failed to save config to {Path}", ConfigFile);
        }
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigFile))
            {
                _logger.LogInformation("[Session] No config file found at {Path}", ConfigFile);
                return;
            }

            var json = File.ReadAllText(ConfigFile);
            var config = JsonSerializer.Deserialize<SavedConfig>(json);
            if (config is null) return;

            AppFolderPath = config.AppFolderPath;
            WorkspaceFilePath = config.WorkspaceFilePath;
            XlfFilePath = config.XlfFilePath;
            TargetLanguage = config.TargetLanguage;

            _logger.LogInformation("[Session] Config loaded: appFolder={AppFolder}, xlfFile={XlfFile}, lang={Lang}",
                AppFolderPath ?? "(none)", XlfFilePath ?? "(none)", TargetLanguage ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Session] Failed to load config from {Path}", ConfigFile);
        }
    }

    private class SavedConfig
    {
        public string? AppFolderPath { get; set; }
        public string? WorkspaceFilePath { get; set; }
        public string? XlfFilePath { get; set; }
        public string? TargetLanguage { get; set; }
    }
}
