namespace JROptimizerPro.Models;

internal sealed record LicenseState(
    string Token,
    string Email,
    string MachineId,
    DateTimeOffset LastValidatedAt,
    string? CustomerName);

internal sealed record LicenseApiRequest(
    string MachineId,
    string AppVersion,
    string? Email = null,
    string? PurchaseCode = null,
    string? Token = null);

internal sealed record LicenseApiResponse(
    bool Valid,
    string? Message,
    string? Token,
    string? CustomerName,
    string? Status);

internal sealed record LicenseCheckResult(
    bool IsValid,
    string Message,
    bool UsedOfflineGrace = false);
