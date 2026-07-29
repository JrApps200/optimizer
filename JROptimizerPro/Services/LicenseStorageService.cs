using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class LicenseStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string StatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "JR Optimizer Pro",
            "license.json");

    public static LicenseState? Load()
    {
        try
        {
            if (!File.Exists(StatePath))
                return null;

            var envelope = JsonSerializer.Deserialize<EncryptedEnvelope>(
                File.ReadAllText(StatePath),
                JsonOptions);
            if (envelope is null)
                return null;

            var nonce = Convert.FromBase64String(envelope.Nonce);
            var tag = Convert.FromBase64String(envelope.Tag);
            var cipherText = Convert.FromBase64String(envelope.Data);
            var plainText = new byte[cipherText.Length];

            using var aes = new AesGcm(DeriveLocalKey(), tag.Length);
            aes.Decrypt(nonce, cipherText, tag, plainText);
            return JsonSerializer.Deserialize<LicenseState>(plainText, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(LicenseState state)
    {
        var directory = Path.GetDirectoryName(StatePath)!;
        Directory.CreateDirectory(directory);

        var plainText = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipherText = new byte[plainText.Length];

        using var aes = new AesGcm(DeriveLocalKey(), tag.Length);
        aes.Encrypt(nonce, plainText, cipherText, tag);

        var envelope = new EncryptedEnvelope(
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(cipherText));
        File.WriteAllText(StatePath, JsonSerializer.Serialize(envelope, JsonOptions));
        CryptographicOperations.ZeroMemory(plainText);
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(StatePath))
                File.Delete(StatePath);
        }
        catch
        {
            // Uma falha ao remover o cache não libera o aplicativo sem validação online.
        }
    }

    private static byte[] DeriveLocalKey()
    {
        const string applicationSalt = "JR-Optimizer-Pro-License-State-v1";
        var material = $"{DeviceFingerprintService.GetMachineId()}|{applicationSalt}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(material));
    }

    private sealed record EncryptedEnvelope(string Nonce, string Tag, string Data);
}
