using CambioReal.Ouribank;
using CambioReal.Ouribank.Auth;
using CambioReal.Ouribank.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace CambioReal.Ouribank.SandboxTests;

/// <summary>
/// Integração sandbox real (mTLS), opt-in — goal-loop §2.5. Credenciais/certs via <c>pass</c>:
///
/// <code>
/// export OURIBANK_SANDBOX_APIM_KEY="$(pass show cambio-real-v2/ouribank/apim-key)"
/// export OURIBANK_SANDBOX_USERNAME="$(pass show cambio-real-v2/ouribank/username)"
/// export OURIBANK_SANDBOX_PASSWORD="$(pass show cambio-real-v2/ouribank/password)"
/// export OURIBANK_SANDBOX_RESOURCE_CODE="$(pass show cambio-real-v2/ouribank/resource-code)"
/// export OURIBANK_SANDBOX_CUSTOMER_CODE="$(pass show cambio-real-v2/ouribank/customer-code)"
/// export OURIBANK_SANDBOX_ACCOUNT_CODE="$(pass show cambio-real-v2/ouribank/account-code)"
/// export OURIBANK_SANDBOX_CERT_PEM="$(pass show cambio-real-v2/providers/ouribank/sandbox-client-cert)"
/// export OURIBANK_SANDBOX_KEY_PEM="$(pass show cambio-real-v2/providers/ouribank/sandbox-client-key)"
/// dotnet test tests/CambioReal.Ouribank.Client.SandboxTests/CambioReal.Ouribank.Client.SandboxTests.csproj
/// </code>
///
/// Leituras apenas — nenhum QR/cashout é criado por default (cashout é financial-write e NUNCA
/// existe aqui; QR create fica como incremento futuro atrás de ALLOW_WRITE quando o caso E2E for
/// exigido). Nenhum teste imprime secrets/PEM/PII.
/// </summary>
public sealed class OuribankSandboxTests
{
    private readonly ITestOutputHelper output;

    public OuribankSandboxTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task TokenChainAuthenticatesLiveOverMtls()
    {
        using var provider = BuildServiceProvider();
        var tokenProvider = provider.GetRequiredService<IOuribankTokenProvider>();

        var token = await tokenProvider.GetRepresentedTokenAsync(invalidatedToken: null);

        token.ShouldNotBeNullOrWhiteSpace();
        output.WriteLine($"cadeia mTLS access→represented: ok, token length={token.Length} (masked).");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task CustomerRateReadsLive()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<OuribankClient>();

        var rate = await client.ExchangeRates.GetCustomerRateAsync();

        rate.FxRate.ShouldNotBeNull();
        rate.FxRate!.SpotRate.ShouldNotBeNull();
        rate.FxRate!.SpotRate!.Value.ShouldBeGreaterThan(0m);
        output.WriteLine($"GET fx-rate/customer-rate: 200, spotRate={rate.FxRate!.SpotRate} (real).");
    }

    [Fact]
    [Trait("Category", "Sandbox")]
    public async Task TransactionsByFictitiousTxIdReturnsEmptyOrDomainError()
    {
        using var provider = BuildServiceProvider();
        var client = provider.GetRequiredService<OuribankClient>();

        try
        {
            var response = await client.Transactions.GetByTxIdAsync("CRSDKPROBE00000000000000");
            output.WriteLine($"GET transactions/txid/{{fictício}}: 200, records={response.Records?.Count ?? 0} — acesso real ao recurso confirmado.");
        }
        catch (OuribankApiException exception)
        {
            // 4xx de domínio também prova acesso real (não é bloqueio de provisionamento).
            ((int)exception.StatusCode).ShouldBeLessThan(500);
            output.WriteLine($"GET transactions/txid/{{fictício}}: HTTP {(int)exception.StatusCode} de domínio — acesso real confirmado.");
        }
    }

    private static ServiceProvider BuildServiceProvider()
    {
        string Req(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException(
                    $"{name} ausente — carregue de `pass cambio-real-v2/ouribank/*` e " +
                    "`pass cambio-real-v2/providers/ouribank/sandbox-client-{cert,key}`.");

        var services = new ServiceCollection();
        services.AddOuribankClient(options =>
        {
            options.Environment = OuribankEnvironment.Sandbox;
            options.ApimKey = Req("OURIBANK_SANDBOX_APIM_KEY");
            options.Username = Req("OURIBANK_SANDBOX_USERNAME");
            options.Password = Req("OURIBANK_SANDBOX_PASSWORD");
            options.ResourceCode = Req("OURIBANK_SANDBOX_RESOURCE_CODE");
            options.CustomerCode = Req("OURIBANK_SANDBOX_CUSTOMER_CODE");
            options.AccountCode = Req("OURIBANK_SANDBOX_ACCOUNT_CODE");
            options.CertificatePem = Req("OURIBANK_SANDBOX_CERT_PEM");
            options.PrivateKeyPem = Req("OURIBANK_SANDBOX_KEY_PEM");
        });

        return services.BuildServiceProvider();
    }
}
