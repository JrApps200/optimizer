using System.Text.Json;
using System.Text.Json.Serialization;
using JROptimizerPro.Core;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class TraySettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static TraySettings Load()
    {
        try
        {
            if (!File.Exists(AppPaths.TraySettingsFile))
                return new TraySettings();

            return JsonSerializer.Deserialize<TraySettings>(
                File.ReadAllText(AppPaths.TraySettingsFile), Options) ?? new TraySettings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Não foi possível carregar as preferências da bandeja.", ex);
            return new TraySettings();
        }
    }

    public static void Save(TraySettings settings)
    {
        settings.RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 1, 10);
        File.WriteAllText(
            AppPaths.TraySettingsFile,
            JsonSerializer.Serialize(settings, Options));
        AppLogger.Info("Preferências do monitor da bandeja salvas.");
    }
}
