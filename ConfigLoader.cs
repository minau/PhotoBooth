using System;
using Microsoft.Extensions.Configuration;
using Photobooth;

namespace Photobooth;

public static class ConfigLoader
{
    private static AppConfigRoot? _config;

    public static void Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _config = configuration.Get<AppConfigRoot>();
    }

    public static AppConfigRoot Config =>
        _config ?? throw new MissingFieldException("Config non chargée. Appelle ConfigLoader.Load() au démarrage.");
}