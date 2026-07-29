using System.Diagnostics;

namespace JROptimizerPro.Core;

internal sealed class ProcessMonitor
{
    private readonly Dictionary<int, ProcessSample> _previous = new();
    private DateTime _lastSample = DateTime.UtcNow;

    public IReadOnlyList<ProcessUsage> Sample(int limit = 12)
    {
        var now = DateTime.UtcNow;
        var elapsedMs = Math.Max(1, (now - _lastSample).TotalMilliseconds);
        var current = new Dictionary<int, ProcessSample>();
        var usages = new List<ProcessUsage>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var sample = new ProcessSample(process.TotalProcessorTime, process.WorkingSet64, process.ProcessName);
                current[process.Id] = sample;

                var cpu = 0d;
                if (_previous.TryGetValue(process.Id, out var previous))
                {
                    var cpuDeltaMs = (sample.TotalCpu - previous.TotalCpu).TotalMilliseconds;
                    cpu = Math.Clamp(cpuDeltaMs / elapsedMs / Environment.ProcessorCount * 100d, 0, 100);
                }

                usages.Add(new ProcessUsage(
                    process.Id,
                    sample.Name,
                    cpu,
                    sample.WorkingSetBytes / 1024d / 1024d));
            }
            catch
            {
                // O processo pode terminar ou negar acesso durante a leitura.
            }
            finally
            {
                process.Dispose();
            }
        }

        _previous.Clear();
        foreach (var item in current)
            _previous[item.Key] = item.Value;
        _lastSample = now;

        return usages
            .OrderByDescending(item => item.CpuPercent)
            .ThenByDescending(item => item.MemoryMb)
            .Take(limit)
            .ToArray();
    }

    private sealed record ProcessSample(TimeSpan TotalCpu, long WorkingSetBytes, string Name);
}

internal sealed record ProcessUsage(int ProcessId, string Name, double CpuPercent, double MemoryMb);
