using System.Security.Cryptography.X509Certificates;
using CambioReal.Ouribank.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CambioReal.Ouribank;

/// <summary>Registro do cliente OuriBank no container.</summary>
public static class OuribankServiceCollectionExtensions
{
    /// <summary>Registra o cliente a partir de uma seção de configuração.</summary>
    /// <remarks>
    /// Credenciais e material mTLS chegam por secret store (<c>pass cambio-real-v2/ouribank/*</c>
    /// e <c>.../providers/ouribank/sandbox-client-{cert,key}</c>) — nunca versionados. O
    /// certificado sandbox expira 2026-09-06.
    /// </remarks>
    public static IServiceCollection AddOuribankClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddOuribankClient(configuration.Bind);
    }

    /// <summary>
    /// Registra <see cref="OuribankClient"/>, o provedor da cadeia de tokens e os dois pipelines
    /// HTTP — AMBOS com mTLS (o handshake exige o certificado do cliente já na etapa de token).
    /// </summary>
    public static IServiceCollection AddOuribankClient(this IServiceCollection services, Action<OuribankOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddOptions<OuribankOptions>().Validate(
            options =>
            {
                try
                {
                    options.Validate();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            },
            "A configuração do OuribankOptions é inválida.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOuribankTokenProvider, OuribankTokenProvider>();

        services.AddHttpClient(OuribankClientNames.Auth, ConfigureTransport)
            .ConfigurePrimaryHttpMessageHandler(CreateMtlsHandler);

        services.AddHttpClient(OuribankClientNames.Api, ConfigureTransport)
            .ConfigurePrimaryHttpMessageHandler(CreateMtlsHandler)
            .AddHttpMessageHandler(provider =>
                new OuribankAuthenticationHandler(provider.GetRequiredService<IOuribankTokenProvider>()));

        services.TryAddTransient(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            return new OuribankClient(
                factory.CreateClient(OuribankClientNames.Api),
                provider.GetRequiredService<IOptions<OuribankOptions>>());
        });

        return services;
    }

    private static void ConfigureTransport(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<OuribankOptions>>().Value;
        options.Validate();

        client.BaseAddress = options.ResolveBaseAddress();
        client.Timeout = options.Timeout;
    }

    /// <summary>
    /// Handler primário com o certificado mTLS do cliente carregado a partir dos PEMs (conteúdo,
    /// não path — compatível com Secret k8s montado em env var).
    /// </summary>
    private static HttpMessageHandler CreateMtlsHandler(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<OuribankOptions>>().Value;

        // Reexportado via PKCS#12 em memória: o SslStream do Windows/Linux exige a chave privada
        // associada de forma persistível — padrão consolidado para PEM efêmero.
        using var pem = X509Certificate2.CreateFromPem(options.CertificatePem, options.PrivateKeyPem);
        var certificate = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pkcs12), password: null);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificate);
        return handler;
    }
}
