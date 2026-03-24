using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NabXliffApp.Services;

namespace NabXliffApp;

public record AppPaths(string ConfigDirectory, string UserSettingsFile);

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();

        // Load bundled defaults, then override with user settings
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nab-xliff-web");
        var userConfigFile = Path.Combine(userConfigDir, "appsettings.json");

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(userConfigFile, optional: true, reloadOnChange: true)
            .Build();
        builder.Configuration.AddConfiguration(config);

        // Store path so Settings page can find it
        builder.Services.AddSingleton(new AppPaths(userConfigDir, userConfigFile));

        builder.Services.AddSingleton<McpBridgeService>();
        builder.Services.AddScoped<TranslationAgentService>();
        builder.Services.AddScoped<SessionStateService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
