namespace JROptimizerPro.Models;

internal sealed record ResidueCandidate(string AppName, string OriginalPath, long EstimatedBytes);

internal sealed record QuarantineEntry(string AppName, string OriginalPath, string QuarantinePath, DateTime MovedAt);

internal sealed class QuarantineManifest
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public List<QuarantineEntry> Entries { get; init; } = new();
}
