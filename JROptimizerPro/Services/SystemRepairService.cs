using System.Diagnostics;
using System.Text;
using JROptimizerPro.Core;

namespace JROptimizerPro.Services;

internal static class SystemRepairService
{
    public static Task<CommandResult> RunSfcAsync(CancellationToken token = default) =>
        CommandService.RunAsync("sfc.exe", "/scannow", TimeSpan.FromHours(1), token);

    public static Task<CommandResult> RunDismRestoreAsync(CancellationToken token = default) =>
        CommandService.RunAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth", TimeSpan.FromHours(2), token);

    public static Task<CommandResult> RunComponentCleanupAsync(CancellationToken token = default) =>
        CommandService.RunAsync("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", TimeSpan.FromHours(2), token);

    public static Task<CommandResult> RunCheckDiskScanAsync(CancellationToken token = default)
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        return CommandService.RunAsync("chkdsk.exe", $"{root} /scan", TimeSpan.FromHours(1), token);
    }

    public static Task<CommandResult> ResetNetworkAsync(CancellationToken token = default) =>
        CommandService.RunAsync(
            "cmd.exe",
            "/d /c ipconfig /flushdns & netsh winsock reset & netsh int ip reset",
            TimeSpan.FromMinutes(5),
            token);

    public static string GenerateSystemReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("JR Optimizer Pro - Relatório do sistema");
        builder.AppendLine("Gerado em: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
        builder.AppendLine(new string('-', 60));
        builder.AppendLine("Windows: " + Environment.OSVersion.VersionString);
        builder.AppendLine("64 bits: " + Environment.Is64BitOperatingSystem);
        builder.AppendLine("Processadores lógicos: " + Environment.ProcessorCount);
        builder.AppendLine("Nome do computador: " + Environment.MachineName);
        builder.AppendLine("Usuário: " + Environment.UserName);
        builder.AppendLine("Tempo ligado: " + TimeSpan.FromMilliseconds(Environment.TickCount64));

        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            builder.AppendLine($"Disco {root}: {drive.AvailableFreeSpace / 1024d / 1024d / 1024d:N1} GB livres de {drive.TotalSize / 1024d / 1024d / 1024d:N1} GB");
        }
        catch { }

        builder.AppendLine();
        builder.AppendLine("Processos com maior uso de memória:");
        var processItems = new List<(string Name, long Memory)>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                processItems.Add((process.ProcessName, process.WorkingSet64));
            }
            catch
            {
                // Processo encerrou durante a leitura.
            }
            finally
            {
                process.Dispose();
            }
        }

        foreach (var process in processItems.OrderByDescending(item => item.Memory).Take(15))
            builder.AppendLine($"- {process.Name}: {process.Memory / 1024d / 1024d:N0} MB");

        AppPaths.EnsureCreated();
        var path = Path.Combine(AppPaths.Logs, $"relatorio-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;
    }
}
