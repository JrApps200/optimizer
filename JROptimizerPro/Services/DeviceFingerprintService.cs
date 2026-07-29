using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class DeviceFingerprintService
{
    public static string GetMachineId()
    {
        var machineGuid = ReadMachineGuid();
        var source = string.Join(
            "|",
            machineGuid,
            Environment.MachineName,
            Environment.Is64BitOperatingSystem ? "x64" : "x86");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
    }

    private static string ReadMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return key?.GetValue("MachineGuid")?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
