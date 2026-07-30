using Microsoft.Win32;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JROptimizerPro.Services;

internal static class LicenseService
{
    // Substituído pelo endereço definitivo durante a publicação.
    private const string ActivationUrl = "https://jr-optimizer-pro.valternanjunior8.workers.dev/api/licenses/activate";
    private static readonly string LicenseFile = Path.Combine(Core.AppPaths.DataRoot, "license.dat");

    public static string GetDeviceId()
    {
        var machineGuid = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
            "MachineGuid",
            null)?.ToString();

        var source = $"{machineGuid}|{Environment.MachineName}|{Environment.Is64BitOperatingSystem}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    public static string? LoadSavedKey()
    {
        try
        {
            if (!File.Exists(LicenseFile))
                return null;

            return File.ReadAllText(LicenseFile, Encoding.UTF8).Trim();
        }
        catch
        {
            return null;
        }
    }

    public static void SaveKey(string key)
    {
        File.WriteAllText(LicenseFile, key.Trim().ToUpperInvariant(), Encoding.UTF8);
    }

    public static async Task<(bool Valid, string Message)> ActivateAsync(string key)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var response = await client.PostAsJsonAsync(ActivationUrl, new
            {
                licenseKey = key.Trim().ToUpperInvariant(),
                deviceId = GetDeviceId()
            });

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var valid = document.RootElement.TryGetProperty("valid", out var value) && value.GetBoolean();
            var message = document.RootElement.TryGetProperty("message", out var text)
                ? text.GetString() ?? string.Empty
                : valid ? "Licença ativada." : "Não foi possível validar esta licença.";

            if (valid)
                SaveKey(key);

            return (valid, message);
        }
        catch
        {
            return (false, "Não foi possível conectar ao servidor. Verifique sua internet e tente novamente.");
        }
    }
}
