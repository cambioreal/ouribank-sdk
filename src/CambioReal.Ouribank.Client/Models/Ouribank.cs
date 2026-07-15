using System.Text.Json;
using System.Text.Json.Nodes;

namespace CambioReal.Ouribank.Models;

/// <summary>
/// Statuses numéricos de transação — confirmados no legado
/// (<c>PixService::details</c>/<c>PayoutService::checkPayed</c>, comentários inline).
/// </summary>
public static class OuribankTransactionStatuses
{
    /// <summary>Transação iniciada.</summary>
    public const int Starting = 1;

    /// <summary>Transação confirmada (payin pago; payout entregue ao rail).</summary>
    public const int Confirmed = 2;

    /// <summary>Cancelada.</summary>
    public const int Canceled = 3;

    /// <summary>Confirmação de débito (payout efetivado).</summary>
    public const int ConfirmedDebit = 6;

    /// <summary>Valor de transação cancelada foi estornado (payin confirmado em conta, pós-estorno).</summary>
    public const int Reversed = 7;
}

/// <summary>
/// Corpo de <c>POST payment-gateway/v1/qrcode-dynamic/create</c> — confirmado campo a campo no
/// legado (<c>PixService::create</c>). <see cref="Options"/> é uma STRING contendo JSON
/// (peculiaridade real da API); use <see cref="CnrOptions.ToJsonString"/>.
/// </summary>
public sealed record CreateDynamicQrCodeRequest
{
    /// <summary>Chave PIX recebedora da conta OuriBank da plataforma.</summary>
    public required string Key { get; init; }

    public required long ExpirationInSeconds { get; init; }
    public required string PayerDocument { get; init; }
    public required string PayerName { get; init; }
    public required decimal Amount { get; init; }
    public required string TxId { get; init; }
    public required string PaymentRequest { get; init; }

    /// <summary>JSON embutido como string — <c>{"OptionsCNR": {...}}</c>.</summary>
    public string? Options { get; init; }
}

/// <summary>Bloco <c>OptionsCNR</c> (beneficiário/pagador da ordem CNR) — sempre embutido como string JSON.</summary>
public sealed record CnrOptions
{
    public string? BeneNameOrderPgto { get; init; }
    public string? BeneCountryOrderPgto { get; init; }

    /// <summary><c>"L"</c> (legal) ou <c>"N"</c> (natural).</summary>
    public string? BenePersonType { get; init; }

    public string? PayerNameOrderPgto { get; init; }
    public string? PayerCountryOrderPgto { get; init; }
    public string? PayerPersonType { get; init; }

    /// <summary>Serializa como a string JSON aninhada que a API espera (<c>{"OptionsCNR": {...}}</c>).</summary>
    public string ToJsonString(string? txId = null, string? localInstrument = null, string? proxyId = null)
    {
        var node = new JsonObject();

        if (txId is not null)
        {
            node["TxId"] = txId;
        }

        if (localInstrument is not null)
        {
            node["LclInstrm"] = localInstrument;
        }

        if (proxyId is not null)
        {
            node["PrxyId"] = proxyId;
        }

        var cnr = new JsonObject();

        void Add(string name, string? value)
        {
            if (value is not null)
            {
                cnr[name] = value;
            }
        }

        Add("BeneNameOrderPgto", BeneNameOrderPgto);
        Add("BeneCountryOrderPgto", BeneCountryOrderPgto);
        Add("BenePersonType", BenePersonType);
        Add("PayerNameOrderPgto", PayerNameOrderPgto);
        Add("PayerCountryOrderPgto", PayerCountryOrderPgto);
        Add("PayerPersonType", PayerPersonType);

        node["OptionsCNR"] = cnr;

        return node.ToJsonString();
    }
}

/// <summary>Resposta do create QR — o EMV/txId ficam aninhados (confirmado no legado).</summary>
public sealed record DynamicQrCodeResponse
{
    public DynamicQrCodeEnvelope? DynamicQrCode { get; init; }
}

public sealed record DynamicQrCodeEnvelope
{
    public CreateDynamicQrCodeInner? CreateDynamicQrCodeResponse { get; init; }
}

public sealed record CreateDynamicQrCodeInner
{
    public OuribankQrCode? QrCode { get; init; }
}

public sealed record OuribankQrCode
{
    public string? TxId { get; init; }

    /// <summary>Payload EMV copia-e-cola.</summary>
    public string? Emv { get; init; }
}

/// <summary>Registro de transação (elemento de <c>records</c>) — statuses numéricos.</summary>
public sealed record OuribankTransactionRecord
{
    public int? Status { get; init; }
    public string? Description { get; init; }
    public string? TxId { get; init; }
    public string? EndToEndId { get; init; }
    public decimal? Amount { get; init; }

    /// <summary>Campos adicionais variam por rail — expostos crus.</summary>
    public JsonElement? Details { get; init; }
}

/// <summary>Resposta das consultas de transação (<c>records</c> na raiz).</summary>
public sealed record OuribankTransactionsResponse
{
    public IReadOnlyList<OuribankTransactionRecord>? Records { get; init; }
}

/// <summary>Parte de um cashout (<c>creditParty</c>/<c>debitParty</c>) — confirmado no legado.</summary>
public sealed record OuribankParty
{
    public required string Name { get; init; }
    public required string AccountNumber { get; init; }
    public required string Branch { get; init; }

    /// <summary><c>"F"</c> (física), <c>"J"</c> (jurídica), <c>"L"</c>/<c>"N"</c> nos options CNR.</summary>
    public required string PersonType { get; init; }

    /// <summary><c>"CACC"</c> (corrente) ou <c>"SVGS"</c> (poupança).</summary>
    public required string AccountType { get; init; }

    public required string Ispb { get; init; }
    public required string Document { get; init; }
    public required string Bank { get; init; }
}

/// <summary>
/// Corpo de <c>POST payment-gateway/v1/payment/cashout</c> — payout PIX por chave
/// (<c>creditParty</c> nulo + <c>PrxyId</c> nos options) ou por conta (<c>creditParty</c>
/// preenchido + <c>LclInstrm: MANU</c>), e reembolso (<c>isDevolucao</c> +
/// <c>amountReversion</c> + <c>endToEndId</c>). Confirmado no legado (<c>PayoutService</c>/
/// <c>PixService::refund</c>). **FINANCIAL-WRITE.**
/// </summary>
public sealed record CashoutRequest
{
    public required decimal Amount { get; init; }
    public required string Origin { get; init; }
    public required string OriginId { get; init; }
    public OuribankParty? CreditParty { get; init; }
    public required OuribankParty DebitParty { get; init; }

    /// <summary>JSON embutido como string (<see cref="CnrOptions.ToJsonString"/>).</summary>
    public string? Options { get; init; }

    public string? Description { get; init; }

    /// <summary><c>0</c>=PIX, <c>1</c>=TED, <c>2</c>=TEF (comentário do legado).</summary>
    public int FinancialType { get; init; }

    /// <summary>Reembolso de payin (com <see cref="AmountReversion"/>/<see cref="EndToEndId"/>).</summary>
    public bool? IsDevolucao { get; init; }

    public decimal? AmountReversion { get; init; }
    public string? EndToEndId { get; init; }
}

/// <summary>Resposta do cashout (<c>{accepted, reason}</c> — confirmado no legado).</summary>
public sealed record CashoutResponse
{
    public bool? Accepted { get; init; }
    public string? Reason { get; init; }
}

/// <summary>Resposta de <c>fx/v1/fx-rate/customer-rate/{customerCode}</c> — validada ao vivo (2026-07-15).</summary>
public sealed record CustomerRateResponse
{
    public JsonElement? Customer { get; init; }
    public DateTimeOffset? PaymentDate { get; init; }
    public OuribankFxRate? FxRate { get; init; }

    /// <summary>JWT com os detalhes da cotação (observado ao vivo) — repassado cru.</summary>
    public string? Token { get; init; }
}

public sealed record OuribankFxRate
{
    public decimal? SpotRate { get; init; }
    public JsonElement? Details { get; init; }
}

/// <summary>
/// Inscrição de webhook (<c>fx/v1/webhook/inscricao</c>) — payload confirmado no legado
/// (<c>PixService::createWebhook</c>): <c>tipoWebhook.codigo</c> (15 = payin),
/// <c>correspondente.codigo</c>, <c>token</c> (HMAC do lado do inscrito) e <c>uri</c>.
/// Cleanup: <c>DELETE fx/v1/webhook/cancelar-inscricao/{id}</c>.
/// </summary>
public sealed record WebhookSubscriptionRequest
{
    public required WebhookCode TipoWebhook { get; init; }
    public required WebhookCode Correspondente { get; init; }
    public required string Token { get; init; }
    public required string Uri { get; init; }
}

/// <summary>Wrapper <c>{codigo}</c> usado por tipoWebhook/correspondente.</summary>
public sealed record WebhookCode(int Codigo);
