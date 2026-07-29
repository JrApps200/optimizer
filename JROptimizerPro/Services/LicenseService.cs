using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using JROptimizerPro.Core;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal sealed class LicenseService : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri(LicenseOptions.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(12)
    };

    public string MachineId { get; } = DeviceFingerprintService.GetMachineId();

    public async Task<LicenseCheckResult> CheckSavedLicenseAsync(CancellationToken cancellationToken = default)
    {
        var state = LicenseStorageService.Load();
        if (state is null || state.MachineId != MachineId)
            return new LicenseCheckResult(false, "Ative sua licença para continuar.");

        if (!LicenseOptions.IsConfigured)
            return new LicenseCheckResult(false, "O servidor de licenças ainda não foi configurado.");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/v1/licenses/validate",
                CreateRequest(token: state.Token),
                cancellationToken);

            var result = await ReadResponseAsync(response, cancellationToken);
            if ((int)response.StatusCode >= 500)
                return CheckOfflineGrace(state);

            if (!response.IsSuccessStatusCode || result is null || !result.Valid)
            {
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                    LicenseStorageService.Delete();

                return new LicenseCheckResult(false, result?.Message ?? "Licença inválida ou revogada.");
            }

            LicenseStorageService.Save(state with { LastValidatedAt = DateTimeOffset.UtcNow });
            return new LicenseCheckResult(true, result.Message ?? "Licença validada.");
        }
        catch (HttpRequestException)
        {
            return CheckOfflineGrace(state);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CheckOfflineGrace(state);
        }
    }

    public async Task<LicenseCheckResult> ActivateAsync(
        string email,
        string purchaseCode,
        CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLowerInvariant();
        purchaseCode = purchaseCode.Trim();

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return new LicenseCheckResult(false, "Digite o mesmo e-mail usado na compra.");

        if (string.IsNullOrWhiteSpace(purchaseCode))
            return new LicenseCheckResult(false, "Digite o código do pedido informado pela Kiwify.");

        if (!LicenseOptions.IsConfigured)
            return new LicenseCheckResult(false, "O servidor de licenças ainda não foi configurado.");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "/v1/licenses/activate",
                CreateRequest(email, purchaseCode),
                cancellationToken);

            var result = await ReadResponseAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode || result is null || !result.Valid || string.IsNullOrWhiteSpace(result.Token))
                return new LicenseCheckResult(false, result?.Message ?? "Não foi possível ativar esta licença.");

            LicenseStorageService.Save(new LicenseState(
                result.Token,
                email,
                MachineId,
                DateTimeOffset.UtcNow,
                result.CustomerName));

            return new LicenseCheckResult(true, result.Message ?? "Licença ativada com sucesso.");
        }
        catch (HttpRequestException)
        {
            return new LicenseCheckResult(false, "Sem conexão com o servidor de licenças. Verifique sua internet.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LicenseCheckResult(false, "O servidor demorou para responder. Tente novamente.");
        }
    }

    private LicenseApiRequest CreateRequest(
        string? email = null,
        string? purchaseCode = null,
        string? token = null) =>
        new(
            MachineId,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            email,
            purchaseCode,
            token);

    private static async Task<LicenseApiResponse?> ReadResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<LicenseApiResponse>(
                cancellationToken: cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static LicenseCheckResult CheckOfflineGrace(LicenseState state)
    {
        var elapsed = DateTimeOffset.UtcNow - state.LastValidatedAt;
        if (elapsed < TimeSpan.FromMinutes(-5))
            return new LicenseCheckResult(
                false,
                "A data ou a hora do computador está incorreta. Corrija-a e tente novamente.");

        return elapsed <= TimeSpan.FromDays(LicenseOptions.OfflineGraceDays)
            ? new LicenseCheckResult(
                true,
                $"Servidor indisponível. Acesso offline liberado por até {LicenseOptions.OfflineGraceDays} dias.",
                true)
            : new LicenseCheckResult(
                false,
                "Conecte-se à internet para validar novamente sua licença.");
    }

    public void Dispose() => _httpClient.Dispose();
}
