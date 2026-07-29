namespace JROptimizerPro.Models;

internal enum CleanupLevel
{
    Light,
    Deep
}

internal enum CleanupActionKind
{
    Files,
    RecycleBin,
    WindowsUpdateCache
}

internal sealed record CleanupPath(string Folder, string SearchPattern = "*", bool Recursive = true);

internal sealed class CleanupTarget
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required CleanupLevel MinimumLevel { get; init; }
    public CleanupActionKind ActionKind { get; init; } = CleanupActionKind.Files;
    public bool Recommended { get; init; } = true;
    public List<CleanupPath> Paths { get; init; } = new();
    public long EstimatedBytes { get; set; }
    public int EstimatedFiles { get; set; }
}

internal sealed record CleanupProgress(string CurrentItem, int Completed, int Total);

internal sealed record CleanupResult(long BytesDeleted, int FilesDeleted, int Errors)
{
    public string FreedText => FormatBytes(BytesDeleted);

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024L * 1024L * 1024L => $"{bytes / 1024d / 1024d / 1024d:N2} GB",
        >= 1024L * 1024L => $"{bytes / 1024d / 1024d:N1} MB",
        >= 1024L => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes:N0} bytes"
    };
}
