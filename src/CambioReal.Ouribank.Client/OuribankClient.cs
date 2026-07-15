using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CambioReal.Ouribank.Http;
using CambioReal.Ouribank.Resources;
using CambioReal.Ouribank.Serialization;
using Microsoft.Extensions.Options;

namespace CambioReal.Ouribank;

/// <summary>
/// Cliente HTTP da OuriBank Payment Gateway API (mTLS + cadeia de dois tokens, encapsulados nos
/// pipelines nomeados — ver <c>OuribankServiceCollectionExtensions</c>).
/// </summary>
public sealed class OuribankClient
{
    private readonly HttpClient httpClient;

    /// <summary>Cria o cliente sobre um <see cref="HttpClient"/> já configurado (mTLS + auth).</summary>
    public OuribankClient(HttpClient httpClient, IOptions<OuribankOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        this.httpClient = httpClient;
        CustomerCode = options.Value.CustomerCode;

        ExchangeRates = new ExchangeRatesResource(this);
        PixQrCodes = new PixQrCodesResource(this);
        Transactions = new TransactionsResource(this);
        Payouts = new PayoutsResource(this);
        Webhooks = new WebhooksResource(this);
    }

    /// <summary>Customer code configurado — usado no path do fx-rate.</summary>
    public string CustomerCode { get; }

    /// <summary>Cotações. <c>fx/v1/fx-rate</c>.</summary>
    public ExchangeRatesResource ExchangeRates { get; }

    /// <summary>PIX payin (QR dinâmico + DICT). <c>payment-gateway/v1/qrcode-dynamic</c>/<c>dict</c>.</summary>
    public PixQrCodesResource PixQrCodes { get; }

    /// <summary>Consultas de transação. <c>payment-gateway/v1/transactions/*</c>.</summary>
    public TransactionsResource Transactions { get; }

    /// <summary>Payout/cashout/refund. <c>payment-gateway/v1/payment/cashout</c>.</summary>
    public PayoutsResource Payouts { get; }

    /// <summary>Inscrição de webhooks. <c>fx/v1/webhook/*</c>.</summary>
    public WebhooksResource Webhooks { get; }

    internal async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path, content: null);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    internal async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        var content = JsonContent.Create(body, options: OuribankJson.Options);
        using var request = CreateRequest(HttpMethod.Post, path, content);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    internal async Task<TResponse> DeleteAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, path, content: null);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.StartsWith('/'))
        {
            throw new ArgumentException(
                $"O path deve ser relativo e não pode começar com '/'. Recebido: '{path}'.",
                nameof(path));
        }

        return new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = content,
        };
    }

    private async Task<TResponse> SendAndReadAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await ThrowIfUnsuccessfulAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(OuribankJson.Options, cancellationToken);

        return payload ?? throw new OuribankApiException(
            response.StatusCode,
            errorType: null,
            errorCode: null,
            "A OuriBank devolveu um corpo JSON vazio onde um valor era esperado.",
            requestId: null,
            responseBody: null);
    }

    private static async Task ThrowIfUnsuccessfulAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var errorMessage = TryExtractErrorMessage(body);
        var message = $"A OuriBank respondeu HTTP {(int)response.StatusCode} ({response.StatusCode})"
            + (errorMessage is null ? "." : $": {errorMessage}.");

        throw response.StatusCode == HttpStatusCode.Unauthorized
            ? new OuribankAuthenticationException(response.StatusCode, null, errorMessage, message, null, body)
            : new OuribankApiException(response.StatusCode, null, errorMessage, message, null, body);
    }

    /// <summary>
    /// Extrai a mensagem nas formas confirmadas no legado (<c>AbstractService::request</c>):
    /// <c>Error.ErrorMessage</c>, <c>error.errorMessage</c> ou <c>reason</c>.
    /// </summary>
    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("Error", out var upper) && upper.ValueKind == JsonValueKind.Object
                && upper.TryGetProperty("ErrorMessage", out var upperMessage) && upperMessage.ValueKind == JsonValueKind.String)
            {
                return upperMessage.GetString();
            }

            if (root.TryGetProperty("error", out var lower) && lower.ValueKind == JsonValueKind.Object
                && lower.TryGetProperty("errorMessage", out var lowerMessage) && lowerMessage.ValueKind == JsonValueKind.String)
            {
                return lowerMessage.GetString();
            }

            if (root.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
            {
                return reason.GetString();
            }
        }
        catch (JsonException)
        {
            // Corpo não-JSON — status e corpo bruto já vão na exceção.
        }

        return null;
    }
}
