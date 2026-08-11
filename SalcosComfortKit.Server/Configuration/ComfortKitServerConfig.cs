using System.Text.Json;
using SPTarkov.Common.Models.Logging;

namespace SalcosComfortKit.Server.Configuration;

internal sealed class ComfortKitServerConfig
{
    private const string FileName = "config.json";

    public bool EnableExtendedSpecialSlots { get; init; } = true;

    internal static ComfortKitServerConfig Load<T>(ISptLogger<T> logger)
    {
        var modDirectory = Path.GetDirectoryName(typeof(ComfortKitServerConfig).Assembly.Location);
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} Could not locate the server configuration; Extended Special Slots remain enabled."
            );
            return new ComfortKitServerConfig();
        }

        var path = Path.Combine(modDirectory, FileName);
        if (!File.Exists(path))
        {
            TryCreateDefault(path, logger);
            return new ComfortKitServerConfig();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<ComfortKitServerConfig>(
                File.ReadAllText(path),
                JsonOptions
            );
            return settings ?? new ComfortKitServerConfig();
        }
        catch (Exception exception)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} Could not read config.json; Extended Special Slots remain enabled: {exception.Message}"
            );
            return new ComfortKitServerConfig();
        }
    }

    private static void TryCreateDefault<T>(string path, ISptLogger<T> logger)
    {
        try
        {
            File.WriteAllText(
                path,
                "{\n  \"enableExtendedSpecialSlots\": true\n}\n"
            );
            logger.Info($"{ComfortKitInfo.LogPrefix} Created the default config.json.");
        }
        catch (Exception exception)
        {
            logger.Warning(
                $"{ComfortKitInfo.LogPrefix} Could not create config.json: {exception.Message}"
            );
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}
