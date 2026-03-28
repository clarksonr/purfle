using Microsoft.Extensions.Logging;
using Purfle.App.Pages;
using Purfle.App.Services;

namespace Purfle.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<MarketplaceService>();
        builder.Services.AddSingleton<AgentStore>();

        // Pages
        builder.Services.AddTransient<SearchPage>();
        builder.Services.AddTransient<AgentDetailPage>();
        builder.Services.AddTransient<MyAgentsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
