using System.Net;
using CambioReal.Ouribank.Auth;
using CambioReal.Ouribank.Http;
using CambioReal.Ouribank.Models;
using CambioReal.Ouribank.Tests.Fakes;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace CambioReal.Ouribank.Tests;

public sealed class OuribankClientTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    private static OuribankOptions NewOptions() => new()
    {
        Environment = OuribankEnvironment.Sandbox,
        ApimKey = "apim-key-1",
        Username = "user-1",
        Password = "pass-1",
        ResourceCode = "res-1",
        CustomerCode = "cust-1",
        AccountCode = "acc-1",
        CertificatePem = "cert",
        PrivateKeyPem = "key",
    };

    [Fact]
    public void ValidOptionsPassValidation()
        => Should.NotThrow(() => NewOptions().Validate());

    [Fact]
    public void MissingCertificateThrows()
    {
        var options = NewOptions();
        options.CertificatePem = string.Empty;

        Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void EnvironmentsResolveToOfficialHosts()
    {
        OuribankEnvironment.Sandbox.GetBaseAddress().ToString().ShouldBe("https://api.sbx.ouribank.com/");
        OuribankEnvironment.Production.GetBaseAddress().ToString().ShouldBe("https://api.ouribank.com/");
    }

    [Fact]
    public async Task TokenChainSendsBasicThenBearerAndCaches()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.OK, """{"accessToken":{"token":"tok-access","expirationDate":"2026-07-16T06:35:05Z"}}""");
        transport.RespondWith(HttpStatusCode.OK, """{"accessToken":{"token":"tok-represented","expirationDate":"2026-07-16T06:35:05Z"}}""");

        var provider = new OuribankTokenProvider(
            new SingleHandlerHttpClientFactory(transport, NewOptions().ResolveBaseAddress()),
            Options.Create(NewOptions()),
            new MutableTimeProvider(Epoch));

        var token = await provider.GetRepresentedTokenAsync(null);

        token.ShouldBe("tok-represented");
        transport.Requests.Count.ShouldBe(2);

        // Etapa 1: Basic {apim} + username/password/resourceCode.
        transport.Requests[0].RequestUri!.ToString().ShouldBe("https://api.sbx.ouribank.com/oauth/v1/access-token");
        transport.Requests[0].Authorization.ShouldBe("Basic apim-key-1");
        transport.Requests[0].Body!.ShouldContain("\"username\":\"user-1\"");
        transport.Requests[0].Body!.ShouldContain("\"resourceCode\":\"res-1\"");

        // Etapa 2: Bearer do token 1 + customerCode/accountCode/resourceCode.
        transport.Requests[1].RequestUri!.ToString().ShouldBe("https://api.sbx.ouribank.com/oauth/v1/represented-customer-token/account");
        transport.Requests[1].Authorization.ShouldBe("Bearer tok-access");
        transport.Requests[1].Body!.ShouldContain("\"customerCode\":\"cust-1\"");
        transport.Requests[1].Body!.ShouldContain("\"accountCode\":\"acc-1\"");

        // Cache: segunda chamada não refaz a cadeia.
        (await provider.GetRepresentedTokenAsync(null)).ShouldBe("tok-represented");
        transport.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FailedChainStepThrowsAuthenticationException()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.Forbidden, """{"reason":"certificate not allowed"}""");

        var provider = new OuribankTokenProvider(
            new SingleHandlerHttpClientFactory(transport, NewOptions().ResolveBaseAddress()),
            Options.Create(NewOptions()),
            new MutableTimeProvider(Epoch));

        var error = await Should.ThrowAsync<OuribankAuthenticationException>(
            async () => await provider.GetRepresentedTokenAsync(null));

        error.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        error.Message.ShouldContain("access-token");
    }

    [Fact]
    public void CnrOptionsSerializesAsEmbeddedJsonString()
    {
        // Peculiaridade real da API: options é uma STRING contendo JSON (json_encode do legado).
        var options = new CnrOptions
        {
            PayerNameOrderPgto = "Fulano",
            PayerCountryOrderPgto = "US",
            PayerPersonType = "N",
        };

        var embedded = options.ToJsonString(txId: "", localInstrument: "DICT", proxyId: "chave-pix");

        embedded.ShouldContain("\"LclInstrm\":\"DICT\"");
        embedded.ShouldContain("\"PrxyId\":\"chave-pix\"");
        embedded.ShouldContain("\"OptionsCNR\":{\"PayerNameOrderPgto\":\"Fulano\"");
    }

    [Fact]
    public async Task ErrorMessageIsExtractedFromLegacyShapes()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.BadRequest, """{"Error":{"ErrorMessage":"invalid txid"}}""");

        var httpClient = new HttpClient(transport) { BaseAddress = NewOptions().ResolveBaseAddress() };
        var client = new OuribankClient(httpClient, Options.Create(NewOptions()));

        var error = await Should.ThrowAsync<OuribankApiException>(
            async () => await client.Transactions.GetByTxIdAsync("x"));

        error.Message.ShouldContain("invalid txid");
    }

    [Fact]
    public async Task TransactionRecordsParseNumericStatuses()
    {
        var transport = new RecordingHttpMessageHandler();
        transport.RespondWith(HttpStatusCode.OK, """{"records":[{"status":6,"description":"ok","endToEndId":"E123"}]}""");

        var httpClient = new HttpClient(transport) { BaseAddress = NewOptions().ResolveBaseAddress() };
        var client = new OuribankClient(httpClient, Options.Create(NewOptions()));

        var response = await client.Transactions.GetByOriginIdAsync("CambioReal", "op-1");

        response.Records![0].Status.ShouldBe(OuribankTransactionStatuses.ConfirmedDebit);
        transport.Requests.Single().RequestUri!.ToString()
            .ShouldBe("https://api.sbx.ouribank.com/payment-gateway/v1/transactions/CambioReal/op-1");
    }
}
