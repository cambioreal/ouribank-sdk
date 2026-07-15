using CambioReal.Ouribank.Models;

namespace CambioReal.Ouribank.Resources;

/// <summary>Cotações. <c>fx/v1/fx-rate</c>.</summary>
public sealed class ExchangeRatesResource
{
    private readonly OuribankClient client;

    internal ExchangeRatesResource(OuribankClient client) => this.client = client;

    /// <summary>
    /// Cotação do cliente. <c>GET fx/v1/fx-rate/customer-rate/{customerCode}</c> — validado ao
    /// vivo (2026-07-15, spot 5.0756 real). Leitura informativa.
    /// </summary>
    public Task<CustomerRateResponse> GetCustomerRateAsync(
        string baseCurrency = "BRL", string quoteCurrency = "USD", CancellationToken cancellationToken = default) =>
        client.GetAsync<CustomerRateResponse>(
            $"fx/v1/fx-rate/customer-rate/{Uri.EscapeDataString(client.CustomerCode)}"
            + $"?baseCurrency={Uri.EscapeDataString(baseCurrency)}&quoteCurrency={Uri.EscapeDataString(quoteCurrency)}",
            cancellationToken);
}

/// <summary>PIX payin — QR dinâmico e DICT. <c>payment-gateway/v1</c>.</summary>
public sealed class PixQrCodesResource
{
    private readonly OuribankClient client;

    internal PixQrCodesResource(OuribankClient client) => this.client = client;

    /// <summary>
    /// Cria um QR dinâmico. <c>POST payment-gateway/v1/qrcode-dynamic/create</c>. Escrita não
    /// financeira enquanto não pago; expira sozinho (<c>expirationInSeconds</c> — cleanup natural).
    /// </summary>
    public Task<DynamicQrCodeResponse> CreateDynamicAsync(
        CreateDynamicQrCodeRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CreateDynamicQrCodeRequest, DynamicQrCodeResponse>(
            "payment-gateway/v1/qrcode-dynamic/create", request, cancellationToken);

    /// <summary>Consulta uma chave no DICT. <c>GET payment-gateway/v1/dict/get?key=</c> (leitura).</summary>
    public Task<System.Text.Json.JsonElement> GetDictKeyAsync(string key, CancellationToken cancellationToken = default) =>
        client.GetAsync<System.Text.Json.JsonElement>(
            $"payment-gateway/v1/dict/get?key={Uri.EscapeDataString(key)}", cancellationToken);
}

/// <summary>Consultas de transação — a fonte de verdade de status (statuses numéricos).</summary>
public sealed class TransactionsResource
{
    private readonly OuribankClient client;

    internal TransactionsResource(OuribankClient client) => this.client = client;

    /// <summary>Por txId (payin). <c>GET payment-gateway/v1/transactions/txid/{txId}</c>.</summary>
    public Task<OuribankTransactionsResponse> GetByTxIdAsync(string txId, CancellationToken cancellationToken = default) =>
        client.GetAsync<OuribankTransactionsResponse>(
            $"payment-gateway/v1/transactions/txid/{Uri.EscapeDataString(txId)}", cancellationToken);

    /// <summary>Por endToEndId. <c>GET payment-gateway/v1/transactions/e2e/{endToEndId}</c>.</summary>
    public Task<OuribankTransactionsResponse> GetByEndToEndIdAsync(string endToEndId, CancellationToken cancellationToken = default) =>
        client.GetAsync<OuribankTransactionsResponse>(
            $"payment-gateway/v1/transactions/e2e/{Uri.EscapeDataString(endToEndId)}", cancellationToken);

    /// <summary>Por originId (payout). <c>GET payment-gateway/v1/transactions/CambioReal/{originId}</c> — o segmento é o <c>origin</c> usado no cashout.</summary>
    public Task<OuribankTransactionsResponse> GetByOriginIdAsync(
        string origin, string originId, CancellationToken cancellationToken = default) =>
        client.GetAsync<OuribankTransactionsResponse>(
            $"payment-gateway/v1/transactions/{Uri.EscapeDataString(origin)}/{Uri.EscapeDataString(originId)}", cancellationToken);

    /// <summary>
    /// Extrato/saldo da conta.
    /// <c>GET payment-gateway/v1/transactions/account/{accountNumber}?startDate=&amp;endDate=</c> —
    /// confirmado no legado (<c>PayoutService::balance()</c>, sempre envia as duas datas via
    /// <c>now()-&gt;toDateString()</c>). Leitura; também insumo do <c>debitParty.accountNumber</c>
    /// no cashout. Nunca exercitado ao vivo (<c>account_number</c> fora do <c>pass</c> —
    /// discovery.md §4).
    /// </summary>
    public Task<OuribankAccountStatementResponse> GetAccountStatementsAsync(
        string accountNumber, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        client.GetAsync<OuribankAccountStatementResponse>(
            $"payment-gateway/v1/transactions/account/{Uri.EscapeDataString(accountNumber)}"
            + $"?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);
}

/// <summary>Payout/cashout/refund — **FINANCIAL-WRITE**. <c>payment-gateway/v1/payment/cashout</c>.</summary>
public sealed class PayoutsResource
{
    private readonly OuribankClient client;

    internal PayoutsResource(OuribankClient client) => this.client = client;

    /// <summary>
    /// Executa um cashout (payout PIX por chave/conta, ou reembolso com <c>isDevolucao</c>).
    /// **FINANCIAL-WRITE** — não executar contra sandbox sem autorização explícita (goal §0.5).
    /// </summary>
    public Task<CashoutResponse> CreateAsync(CashoutRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<CashoutRequest, CashoutResponse>(
            "payment-gateway/v1/payment/cashout", request, cancellationToken);
}

/// <summary>Inscrição de webhooks. <c>fx/v1/webhook/*</c>.</summary>
public sealed class WebhooksResource
{
    private readonly OuribankClient client;

    internal WebhooksResource(OuribankClient client) => this.client = client;

    /// <summary>Inscreve um webhook. <c>POST fx/v1/webhook/inscricao</c>. Cleanup = <see cref="DeleteAsync"/>.</summary>
    public Task<System.Text.Json.JsonElement> CreateAsync(
        WebhookSubscriptionRequest request, CancellationToken cancellationToken = default) =>
        client.PostAsync<WebhookSubscriptionRequest, System.Text.Json.JsonElement>(
            "fx/v1/webhook/inscricao", request, cancellationToken);

    /// <summary>Cancela uma inscrição. <c>DELETE fx/v1/webhook/cancelar-inscricao/{id}</c>.</summary>
    public Task<System.Text.Json.JsonElement> DeleteAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
        client.DeleteAsync<System.Text.Json.JsonElement>(
            $"fx/v1/webhook/cancelar-inscricao/{Uri.EscapeDataString(subscriptionId)}", cancellationToken);
}
