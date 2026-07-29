using System.Diagnostics;
using System.Runtime.InteropServices;
using JROptimizerPro.Core;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class CleanupService
{
    public static Task AnalyzeAsync(
        IReadOnlyList<CleanupTarget> targets,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(targets, progress, cancellationToken), cancellationToken);

    public static Task<CleanupResult> CleanAsync(
        IReadOnlyList<CleanupTarget> targets,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Clean(targets, progress, cancellationToken), cancellationToken);

    private static void Analyze(
        IReadOnlyList<CleanupTarget> targets,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < targets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = targets[index];
            progress?.Report(new CleanupProgress(target.Name, index, targets.Count));

            long bytes = 0;
            var files = 0;
            if (target.ActionKind != CleanupActionKind.RecycleBin)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in target.Paths)
                {
                    foreach (var file in EnumerateFiles(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!seen.Add(file))
                            continue;
                        try
                        {
                            bytes += new FileInfo(file).Length;
                            files++;
                        }
                        catch
                        {
                            // Arquivos inacessíveis não entram na estimativa.
                        }
                    }
                }
            }

            target.EstimatedBytes = bytes;
            target.EstimatedFiles = files;
        }

        progress?.Report(new CleanupProgress("Análise concluída", targets.Count, targets.Count));
    }

    private static CleanupResult Clean(
        IReadOnlyList<CleanupTarget> targets,
        IProgress<CleanupProgress>? progress,
        CancellationToken cancellationToken)
    {
        long bytesDeleted = 0;
        var filesDeleted = 0;
        var errors = 0;

        for (var index = 0; index < targets.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = targets[index];
            progress?.Report(new CleanupProgress(target.Name, index, targets.Count));

            AppLogger.Info($"Limpando: {target.Name}");
            if (target.ActionKind == CleanupActionKind.RecycleBin)
            {
                TryEmptyRecycleBin(ref errors);
                continue;
            }

            var restartUpdateServices = target.ActionKind == CleanupActionKind.WindowsUpdateCache;
            if (restartUpdateServices)
                StopUpdateServices();

            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in target.Paths)
                {
                    foreach (var file in EnumerateFiles(path))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!seen.Add(file))
                            continue;

                        try
                        {
                            var info = new FileInfo(file);
                            var size = info.Exists ? info.Length : 0;
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                            bytesDeleted += size;
                            filesDeleted++;
                        }
                        catch
                        {
                            errors++;
                        }
                    }

                    RemoveEmptySubfolders(path.Folder);
                }
            }
            finally
            {
                if (restartUpdateServices)
                    StartUpdateServices();
            }
        }

        progress?.Report(new CleanupProgress("Limpeza concluída", targets.Count, targets.Count));
        AppLogger.Info($"Limpeza finalizada: {CleanupResult.FormatBytes(bytesDeleted)}, {filesDeleted} arquivos, {errors} ignorados.");
        return new CleanupResult(bytesDeleted, filesDeleted, errors);
    }

    private static IEnumerable<string> EnumerateFiles(CleanupPath path)
    {
        if (!Directory.Exists(path.Folder))
            yield break;

        var option = path.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        IEnumerator<string>? enumerator = null;
        try
        {
            enumerator = Directory.EnumerateFiles(path.Folder, path.SearchPattern, option).GetEnumerator();
            while (true)
            {
                string current;
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                    current = enumerator.Current;
                }
                catch
                {
                    break;
                }

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) == 0)
                    yield return current;
            }
        }
        finally
        {
            enumerator?.Dispose();
        }
    }

    private static void RemoveEmptySubfolders(string root)
    {
        if (!Directory.Exists(root))
            return;

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .OrderByDescending(item => item.Length))
            {
                try
                {
                    var attributes = File.GetAttributes(directory);
                    if ((attributes & FileAttributes.ReparsePoint) == 0 && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory, false);
                }
                catch
                {
                    // Mantém pastas em uso ou protegidas.
                }
            }
        }
        catch
        {
            // Mantém o restante.
        }
    }

    private static void StopUpdateServices()
    {
        RunServiceCommand("wuauserv", "stop");
        RunServiceCommand("bits", "stop");
    }

    private static void StartUpdateServices()
    {
        RunServiceCommand("bits", "start");
        RunServiceCommand("wuauserv", "start");
    }

    private static void RunServiceCommand(string service, string action)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"{action} {service}",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(10_000);
        }
        catch
        {
            // O arquivo ainda será tentado; itens bloqueados serão ignorados.
        }
    }

    private static void TryEmptyRecycleBin(ref int errors)
    {
        try
        {
            const uint flags = 0x00000001 | 0x00000002 | 0x00000004;
            var result = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
            if (result != 0)
                errors++;
        }
        catch
        {
            errors++;
        }
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);
}
