namespace JROptimizerPro.Core;

internal static class LicenseOptions
{
    // Substitua pela URL publicada pelo Cloudflare Worker antes de gerar o instalador final.
    public const string ApiBaseUrl = "https://CONFIGURE-SEU-WORKER.workers.dev";
    public const int OfflineGraceDays = 3;

    public static bool IsConfigured =>
        Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _) &&
        !ApiBaseUrl.Contains("CONFIGURE-SEU-WORKER", StringComparison.OrdinalIgnoreCase);
}
