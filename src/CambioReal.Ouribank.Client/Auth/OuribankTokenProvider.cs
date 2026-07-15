using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CambioReal.Ouribank.Http;
using CambioReal.Ouribank.Serialization;
using Microsoft.Extensions.Options;

namespace CambioReal.Ouribank.Auth;

/// <summary>Fornece o represented token da OuriBank (cadeia de dois tokens), com cache e renovação.</summary>
public interface IOuribankTokenProvider
{
    /// <summary>
    /// Devolve um represented token válido. <paramref name="invalidatedToken"/> força renovação
    /// da cadeia inteira quando o token informado (recebido num 401) ainda for o corrente.
    /// </summary>
    public ValueTask<string> GetRepresentedTokenAsync(string? invalidatedToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementa a cadeia de autenticação confirmada no legado
/// (<c>AbstractService::{accessToken,representedToken,authenticate}</c>) e ao vivo (2026-07-15):
/// (1) <c>POST oauth/v1/access-token</c> com <c>Authorization: Basic {ApimKey}</c> e corpo
/// <c>{username, password, resourceCode}</c>; (2) <c>POST oauth/v1/represented-customer-token/account</c>
/// com Bearer do token (1) e corpo <c>{customerCode, accountCode, resourceCode}</c>. As chamadas
/// exigem mTLS (o <c>HttpClient</c> nomeado já carrega o certificado). Expiração real vem de
/// <c>accessToken.expirationDate</c> de cada etapa; cache com margem, single-flight.
/// </summary>
internal sealed class OuribankTokenProvider : IOuribankTokenProvider, IDisposable
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly OuribankOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private CachedToken? cachedRepresented;

    public OuribankTokenProvider(IHttpClientFactory httpClientFactory, IOptions<OuribankOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<string> GetRepresentedTokenAsync(string? invalidatedToken, CancellationToken cancellationToken = default)
    {
        if (TryUseCached(invalidatedToken, out var token))
        {
            return token;
        }

        await refreshGate.WaitAsync(cancellationToken);
        try
        {
            if (TryUseCached(invalidatedToken, out token))
            {
                return token;
            }

            var fresh = await RequestChainAsync(cancellationToken);
            cachedRepresented = fresh;
            return fresh.Value;
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public void Dispose() => refreshGate.Dispose();

    private bool TryUseCached(string? invalidatedToken, out string token)
    {
        token = string.Empty;
        var current = cachedRepresented;

        if (current is null
            || (invalidatedToken is not null && string.Equals(current.Value, invalidatedToken, StringComparison.Ordinal))
            || timeProvider.GetUtcNow() >= current.ExpiresAtUtc)
        {
            return false;
        }

        token = current.Value;
        return true;
    }

    private async Task<CachedToken> RequestChainAsync(CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(OuribankClientNames.Auth);

        // Etapa 1 — access token (Basic {ApimKey}).
        using var accessRequest = new HttpRequestMessage(HttpMethod.Post, new Uri("oauth/v1/access-token", UriKind.Relative))
        {
            Content = JsonContent.Create(
                new AccessTokenRequestBody(options.Username, options.Password, options.ResourceCode),
                options: OuribankJson.Options),
        };
        accessRequest.Headers.TryAddWithoutValidation("Authorization", $"Basic {options.ApimKey}");

        var accessToken = await SendTokenStepAsync(client, accessRequest, "access-token", cancellationToken);

        // Etapa 2 — represented token (Bearer do access token).
        using var representedRequest = new HttpRequestMessage(
            HttpMethod.Post, new Uri("oauth/v1/represented-customer-token/account", UriKind.Relative))
        {
            Content = JsonContent.Create(
                new RepresentedTokenRequestBody(options.CustomerCode, options.AccountCode, options.ResourceCode),
                options: OuribankJson.Options),
        };
        representedRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken.Token}");

        var represented = await SendTokenStepAsync(client, representedRequest, "represented-customer-token", cancellationToken);

        var expiresAt = represented.ExpirationDate ?? timeProvider.GetUtcNow().AddMinutes(10);
        return new CachedToken(represented.Token, expiresAt - options.TokenExpirationSkew);
    }

    private static async Task<(string Token, DateTimeOffset? ExpirationDate)> SendTokenStepAsync(
        HttpClient client, HttpRequestMessage request, string step, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new OuribankAuthenticationException(
                response.StatusCode,
                errorType: null,
                errorCode: null,
                $"Falha na etapa '{step}' da cadeia de autenticação OuriBank (HTTP {(int)response.StatusCode}).",
                requestId: null,
                errorBody);
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenEnvelope>(OuribankJson.Options, cancellationToken);

        if (payload?.AccessToken?.Token is not { Length: > 0 } token)
        {
            throw new OuribankAuthenticationException($"A OuriBank devolveu um token vazio na etapa '{step}'.");
        }

        return (token, payload.AccessToken.ExpirationDate);
    }

    private sealed record AccessTokenRequestBody(string Username, string Password, string ResourceCode);

    private sealed record RepresentedTokenRequestBody(string CustomerCode, string AccountCode, string ResourceCode);

    /// <summary>Resposta de ambas as etapas: <c>{accessToken: {token, expirationDate}}</c> (confirmado ao vivo).</summary>
    private sealed record TokenEnvelope([property: JsonPropertyName("accessToken")] TokenBody? AccessToken);

    private sealed record TokenBody(string? Token, DateTimeOffset? ExpirationDate);

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAtUtc);
}

/// <summary>Nomes dos <c>HttpClient</c> registrados no container — ambos com mTLS.</summary>
internal static class OuribankClientNames
{
    /// <summary>Cliente dos recursos. Passa pelo <c>OuribankAuthenticationHandler</c>.</summary>
    public const string Api = "ouribank.api";

    /// <summary>Cliente da cadeia de tokens (sem handler de auth, mas COM mTLS).</summary>
    public const string Auth = "ouribank.auth";
}
