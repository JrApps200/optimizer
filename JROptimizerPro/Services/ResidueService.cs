using System.Text.Json;
using System.Text.RegularExpressions;
using JROptimizerPro.Core;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class ResidueService
{
    private static readonly HashSet<string> UnsafeFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "microsoft", "commonfiles", "common files", "packages", "temp", "programs",
        "applicationdata", "application data", "startmenu", "start menu", "desktop", "documents",
        "downloads", "onedrive", "system32", "users"
    };

    public static Task<List<ResidueCandidate>> FindCandidatesAsync(
        IReadOnlyList<InstalledApp> apps,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => FindCandidates(apps, cancellationToken), cancellationToken);

    public static async Task<QuarantineManifest> MoveToQuarantineAsync(
        IReadOnlyList<ResidueCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            AppPaths.EnsureCreated();
            var manifest = new QuarantineManifest();
            var sessionFolder = Path.Combine(AppPaths.Quarantine, $"{DateTime.Now:yyyyMMdd-HHmmss}-{manifest.Id}");
            Directory.CreateDirectory(sessionFolder);
            var manifestFile = Path.Combine(sessionFolder, "manifest.json");
            File.WriteAllText(manifestFile, JsonSerializer.Serialize(manifest, JsonOptions));

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(candidate.OriginalPath))
                    continue;

                var safeName = SanitizeFileName(candidate.AppName);
                var destination = Path.Combine(sessionFolder, safeName + "-" + Guid.NewGuid().ToString("N")[..8]);
                try
                {
                    Directory.Move(candidate.OriginalPath, destination);
                    manifest.Entries.Add(new QuarantineEntry(
                        candidate.AppName,
                        candidate.OriginalPath,
                        destination,
                        DateTime.Now));
                    File.WriteAllText(manifestFile, JsonSerializer.Serialize(manifest, JsonOptions));
                    AppLogger.Info($"Resíduo movido para quarentena: {candidate.OriginalPath}");
                }
                catch (Exception ex)
                {
                    AppLogger.Warning($"Falha ao mover resíduo {candidate.OriginalPath}: {ex.Message}");
                }
            }

            File.WriteAllText(manifestFile, JsonSerializer.Serialize(manifest, JsonOptions));
            return manifest;
        }, cancellationToken);
    }

    public static List<string> RestoreAll()
    {
        var messages = new List<string>();
        if (!Directory.Exists(AppPaths.Quarantine))
            return messages;

        foreach (var manifestFile in Directory.EnumerateFiles(AppPaths.Quarantine, "manifest.json", SearchOption.AllDirectories))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<QuarantineManifest>(File.ReadAllText(manifestFile), JsonOptions);
                if (manifest is null)
                    continue;

                foreach (var entry in manifest.Entries)
                {
                    if (!Directory.Exists(entry.QuarantinePath))
                        continue;
                    if (Directory.Exists(entry.OriginalPath))
                    {
                        messages.Add($"Não restaurado (destino já existe): {entry.OriginalPath}");
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
                    Directory.Move(entry.QuarantinePath, entry.OriginalPath);
                    messages.Add("Restaurado: " + entry.OriginalPath);
                }
            }
            catch (Exception ex)
            {
                messages.Add("Falha em " + manifestFile + ": " + ex.Message);
            }
        }

        return messages;
    }

    public static void DeleteQuarantinePermanently()
    {
        if (!Directory.Exists(AppPaths.Quarantine))
            return;

        Directory.Delete(AppPaths.Quarantine, true);
        Directory.CreateDirectory(AppPaths.Quarantine);
    }

    private static List<ResidueCandidate> FindCandidates(IReadOnlyList<InstalledApp> apps, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, ResidueCandidate>(StringComparer.OrdinalIgnoreCase);
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(app.InstallLocation)
                && Directory.Exists(app.InstallLocation)
                && IsSafeCandidate(app.InstallLocation))
            {
                AddCandidate(results, app.Name, app.InstallLocation);
            }

            var appToken = Normalize(app.Name);
            var publisherToken = Normalize(app.Publisher);
            if (appToken.Length < 4)
                continue;

            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IEnumerable<string> firstLevel;
                try
                {
                    firstLevel = Directory.EnumerateDirectories(root).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var folder in firstLevel)
                {
                    var folderName = Path.GetFileName(folder);
                    var folderToken = Normalize(folderName);
                    if (UnsafeFolderNames.Contains(folderToken))
                        continue;

                    if (MatchesTopLevel(folderToken, appToken) && !folderToken.Equals(publisherToken, StringComparison.OrdinalIgnoreCase))
                        AddCandidate(results, app.Name, folder);

                    if (!string.IsNullOrWhiteSpace(publisherToken) && MatchesPublisher(folderToken, publisherToken))
                    {
                        try
                        {
                            foreach (var child in Directory.EnumerateDirectories(folder))
                            {
                                var childToken = Normalize(Path.GetFileName(child));
                                if (MatchesApp(childToken, appToken))
                                    AddCandidate(results, app.Name, child);
                            }
                        }
                        catch
                        {
                            // Sem acesso ao subdiretório.
                        }
                    }
                }
            }
        }

        return results.Values.OrderBy(item => item.AppName).ThenBy(item => item.OriginalPath).ToList();
    }


    private static bool MatchesTopLevel(string folderToken, string appToken)
    {
        if (folderToken.Length < 4)
            return false;
        return folderToken.Equals(appToken, StringComparison.OrdinalIgnoreCase)
            || (appToken.Length >= 5 && folderToken.Contains(appToken, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesApp(string folderToken, string appToken)
    {
        if (folderToken.Length < 4)
            return false;
        return folderToken.Equals(appToken, StringComparison.OrdinalIgnoreCase)
            || (folderToken.Length >= 5 && appToken.Contains(folderToken, StringComparison.OrdinalIgnoreCase))
            || (appToken.Length >= 5 && folderToken.Contains(appToken, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPublisher(string folderToken, string publisherToken) =>
        folderToken.Equals(publisherToken, StringComparison.OrdinalIgnoreCase)
        || (folderToken.Length >= 5 && publisherToken.Contains(folderToken, StringComparison.OrdinalIgnoreCase));

    private static void AddCandidate(IDictionary<string, ResidueCandidate> results, string appName, string path)
    {
        try
        {
            path = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(path) || !IsSafeCandidate(path) || results.ContainsKey(path))
                return;

            results[path] = new ResidueCandidate(appName, path, GetDirectorySize(path));
        }
        catch
        {
            // Ignora candidato inválido.
        }
    }

    private static bool IsSafeCandidate(string path)
    {
        var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        if (roots.Any(root => normalized.Equals(root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)))
            return false;

        return !normalized.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase);
    }

    private static long GetDirectorySize(string path)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static string Normalize(string text)
    {
        text = Regex.Replace(text ?? string.Empty, @"\([^)]*\)", " ");
        text = Regex.Replace(text, @"\b(x64|x86|64-bit|32-bit|version|versão|setup|installer)\b", " ", RegexOptions.IgnoreCase);
        return new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "Aplicativo" : value;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
