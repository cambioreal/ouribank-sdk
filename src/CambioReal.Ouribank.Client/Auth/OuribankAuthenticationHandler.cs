using System.Net;
using System.Net.Http.Headers;
using CambioReal.Ouribank.Http;

namespace CambioReal.Ouribank.Auth;

/// <summary>
/// Injeta <c>Authorization: Bearer {represented_token}</c> em toda requisição de recurso,
/// renovando a cadeia inteira uma única vez diante de 401.
/// </summary>
internal sealed class OuribankAuthenticationHandler : DelegatingHandler
{
    private readonly IOuribankTokenProvider tokenProvider;

    public OuribankAuthenticationHandler(IOuribankTokenProvider tokenProvider)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        this.tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await tokenProvider.GetRepresentedTokenAsync(invalidatedToken: null, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var retry = await request.CloneAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            retry.Dispose();
            return response;
        }

        response.Dispose();

        var refreshed = await tokenProvider.GetRepresentedTokenAsync(invalidatedToken: token, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);

        return await base.SendAsync(retry, cancellationToken);
    }
}
