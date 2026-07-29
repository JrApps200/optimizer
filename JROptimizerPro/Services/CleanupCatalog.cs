using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class CleanupCatalog
{
    public static List<CleanupTarget> Create(CleanupLevel level)
    {
        var windows = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        var targets = new List<CleanupTarget>
        {
            new()
            {
                Id = "user_temp",
                Name = "Temporários do usuário",
                Description = "Arquivos temporários criados por aplicativos e instaladores.",
                MinimumLevel = CleanupLevel.Light,
                Paths = { new CleanupPath(Path.GetTempPath()) }
            },
            new()
            {
                Id = "windows_temp",
                Name = "Temporários do Windows",
                Description = "Arquivos temporários do sistema; itens em uso são preservados.",
                MinimumLevel = CleanupLevel.Light,
                Paths = { new CleanupPath(Path.Combine(windows, "Temp")) }
            },
            new()
            {
                Id = "thumbnails",
                Name = "Cache de miniaturas",
                Description = "Miniaturas do Explorador de Arquivos. O Windows recria quando necessário.",
                MinimumLevel = CleanupLevel.Light,
                Paths = { new CleanupPath(Path.Combine(local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db", false) }
            },
            new()
            {
                Id = "shader_cache",
                Name = "Cache gráfico e DirectX",
                Description = "Cache de shaders. Pode ser reconstruído na próxima abertura de jogos e vídeos.",
                MinimumLevel = CleanupLevel.Light,
                Paths = { new CleanupPath(Path.Combine(local, "D3DSCache")) }
            },
            new()
            {
                Id = "crash_dumps",
                Name = "Relatórios de travamento",
                Description = "Arquivos de diagnóstico gerados por aplicativos que falharam.",
                MinimumLevel = CleanupLevel.Light,
                Paths = { new CleanupPath(Path.Combine(local, "CrashDumps")) }
            },
            new()
            {
                Id = "browser_gpu",
                Name = "Caches gráficos dos navegadores",
                Description = "GPUCache, Code Cache e ShaderCache do Chrome e Edge.",
                MinimumLevel = CleanupLevel.Light,
                Paths = BuildBrowserCachePaths(includeFullCache: false)
            },
            new()
            {
                Id = "recycle_bin",
                Name = "Lixeira",
                Description = "Esvazia a Lixeira de todas as unidades.",
                MinimumLevel = CleanupLevel.Light,
                ActionKind = CleanupActionKind.RecycleBin,
                Recommended = false
            }
        };

        if (level == CleanupLevel.Deep)
        {
            targets.AddRange(new[]
            {
                new CleanupTarget
                {
                    Id = "browser_full",
                    Name = "Cache completo dos navegadores",
                    Description = "Cache de páginas do Chrome e Edge. Feche os navegadores antes de limpar.",
                    MinimumLevel = CleanupLevel.Deep,
                    Paths = BuildBrowserCachePaths(includeFullCache: true),
                    Recommended = false
                },
                new CleanupTarget
                {
                    Id = "wer",
                    Name = "Windows Error Reporting",
                    Description = "Fila e histórico de relatórios de erro do Windows.",
                    MinimumLevel = CleanupLevel.Deep,
                    Paths =
                    {
                        new CleanupPath(Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive")),
                        new CleanupPath(Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"))
                    }
                },
                new CleanupTarget
                {
                    Id = "minidump",
                    Name = "Minidumps do Windows",
                    Description = "Pequenos despejos de memória usados para investigar telas azuis.",
                    MinimumLevel = CleanupLevel.Deep,
                    Paths = { new CleanupPath(Path.Combine(windows, "Minidump")) },
                    Recommended = false
                },
                new CleanupTarget
                {
                    Id = "delivery_optimization",
                    Name = "Cache de Otimização de Entrega",
                    Description = "Arquivos de atualizações compartilhados pelo Windows.",
                    MinimumLevel = CleanupLevel.Deep,
                    Paths =
                    {
                        new CleanupPath(Path.Combine(windows, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"))
                    }
                },
                new CleanupTarget
                {
                    Id = "windows_update",
                    Name = "Downloads do Windows Update",
                    Description = "Remove downloads de atualização já baixados. O serviço será parado e reiniciado.",
                    MinimumLevel = CleanupLevel.Deep,
                    ActionKind = CleanupActionKind.WindowsUpdateCache,
                    Recommended = false,
                    Paths = { new CleanupPath(Path.Combine(windows, @"SoftwareDistribution\Download")) }
                },
                new CleanupTarget
                {
                    Id = "inet_cache",
                    Name = "Cache de internet do Windows",
                    Description = "Cache legado usado por componentes do sistema.",
                    MinimumLevel = CleanupLevel.Deep,
                    Paths = { new CleanupPath(Path.Combine(local, @"Microsoft\Windows\INetCache")) }
                }
            });
        }

        return targets;
    }

    private static List<CleanupPath> BuildBrowserCachePaths(bool includeFullCache)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(local, @"Google\Chrome\User Data"),
            Path.Combine(local, @"Microsoft\Edge\User Data")
        };

        var paths = new List<CleanupPath>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            IEnumerable<string> profiles;
            try
            {
                profiles = Directory.EnumerateDirectories(root)
                    .Where(path =>
                    {
                        var name = Path.GetFileName(path);
                        return name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                            || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var profile in profiles)
            {
                if (includeFullCache)
                {
                    paths.Add(new CleanupPath(Path.Combine(profile, "Cache")));
                }
                else
                {
                    paths.Add(new CleanupPath(Path.Combine(profile, "GPUCache")));
                    paths.Add(new CleanupPath(Path.Combine(profile, "Code Cache")));
                    paths.Add(new CleanupPath(Path.Combine(profile, "ShaderCache")));
                    paths.Add(new CleanupPath(Path.Combine(profile, "GrShaderCache")));
                }
            }
        }

        return paths;
    }
}
