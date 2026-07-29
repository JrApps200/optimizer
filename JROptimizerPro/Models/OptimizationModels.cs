namespace JROptimizerPro.Models;

internal enum PerformanceProfileType
{
    DayToDay,
    Gamer,
    Multitasking,
    Streaming,
    Economy
}

internal enum PowerPlanMode
{
    Unchanged,
    Balanced,
    HighPerformance,
    PowerSaver
}

internal sealed class OptimizationOptions
{
    public bool DisableTransparency { get; init; } = true;
    public bool ReduceAnimations { get; init; } = true;
    public bool DisableGameDvr { get; init; } = true;
    public bool DisableBackgroundApps { get; init; }
    public bool DisableWebSearch { get; init; } = true;
    public bool DisableSuggestions { get; init; } = true;
    public bool DisableWidgets { get; init; } = true;
    public bool ReduceTelemetry { get; init; }
    public bool HighPerformancePlan { get; init; } = true;
    public bool DisableSysMain { get; init; }
    public bool DisableSearchIndexing { get; init; }
    public bool DisableHibernation { get; init; }
    public PowerPlanMode PowerPlan { get; init; } = PowerPlanMode.Unchanged;
}

internal sealed record HardwareProfile(
    string Processor,
    double MemoryGb,
    int LogicalProcessors,
    bool HasBattery,
    bool IsLowMemory,
    bool IsEntryLevel,
    PerformanceProfileType RecommendedProfile);

internal sealed record PerformanceProfileDefinition(
    PerformanceProfileType Type,
    string Name,
    string Description,
    OptimizationOptions Options);

internal sealed class OptimizationBackup
{
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string PreviousPowerScheme { get; set; } = string.Empty;
    public bool HibernationWasEnabled { get; set; }
    public List<RegistryBackupEntry> RegistryEntries { get; init; } = new();
    public List<ServiceBackupEntry> Services { get; init; } = new();
}

internal sealed class RegistryBackupEntry
{
    public string Hive { get; init; } = string.Empty;
    public string KeyPath { get; init; } = string.Empty;
    public string ValueName { get; init; } = string.Empty;
    public bool Existed { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string ValueData { get; init; } = string.Empty;
}

internal sealed class ServiceBackupEntry
{
    public string ServiceName { get; init; } = string.Empty;
    public int StartType { get; init; }
}

internal sealed record OptimizationResult(IReadOnlyList<string> Changes, IReadOnlyList<string> Errors);
