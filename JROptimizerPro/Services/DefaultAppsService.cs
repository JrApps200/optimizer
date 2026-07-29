using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class DefaultAppsService
{
    public static IReadOnlyList<DefaultAssociation> ReadCommonAssociations()
    {
        return new[]
        {
            new DefaultAssociation("Navegador (HTTP)", ReadUrlAssociation("http")),
            new DefaultAssociation("Navegador (HTTPS)", ReadUrlAssociation("https")),
            new DefaultAssociation("Arquivos PDF", ReadFileAssociation(".pdf")),
            new DefaultAssociation("Imagens JPG", ReadFileAssociation(".jpg")),
            new DefaultAssociation("Vídeos MP4", ReadFileAssociation(".mp4")),
            new DefaultAssociation("E-mail", ReadUrlAssociation("mailto"))
        };
    }

    public static void OpenDefaultAppsSettings() => CommandService.StartShell("ms-settings:defaultapps");

    public static void OpenAppsFeaturesSettings() => CommandService.StartShell("ms-settings:appsfeatures");

    private static string ReadUrlAssociation(string protocol)
    {
        var path = $@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{protocol}\UserChoice";
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key?.GetValue("ProgId")?.ToString() ?? "Não definido";
    }

    private static string ReadFileAssociation(string extension)
    {
        var path = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice";
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key?.GetValue("ProgId")?.ToString() ?? "Não definido";
    }
}

internal sealed record DefaultAssociation(string Category, string CurrentHandler);
