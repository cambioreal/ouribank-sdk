# OuriBank — Discovery

Status: descoberta e SDK concluídos (2026-07-15). Sonda §0.8 **verde com mTLS** — cadeia de dois
tokens + leitura real (fx-rate com spot 5.0756 real; transactions com acesso confirmado). Sem
bloqueio externo. O achado que destravou o provider: o path real de token é
**`oauth/v1/access-token`** (o PROVIDER-MAP registrava `/oauth/token → 404, path desconhecido`).
Provider order position: **6 of 9** (`GOAL-provider-standalone-sandbox-loop.md`).
Verified: 2026-07-15, contra `pass cambio-real-v2/ouribank/*` +
`.../providers/ouribank/sandbox-client-{cert,key}` no sandbox vivo (`api.sbx.ouribank.com`) +
legado `cerebro` (read-only).

## 1. Perfil no Provider Protocol

**`Sync`** (submit/status/cancel/refund) — PIX payin (QR dinâmico) + payout (cashout) + fx-rate
informativo. Sem quote vinculante. Sem `ISyncProviderAdapter` formal (lacuna conhecida RFC §6).

## 2. Ambiente, mTLS e auth

| | Sandbox | Produção |
|---|---|---|
| Base URL | `https://api.sbx.ouribank.com/` | `https://api.ouribank.com/` |
| mTLS | **Obrigatório** — cert/key em `pass cambio-real-v2/providers/ouribank/sandbox-client-{cert,key}`; **cert expira 2026-09-06** (plano de renovação ANTES do deploy — goal §Segurança) | material próprio |
| Credenciais | `pass cambio-real-v2/ouribank/{apim-key,username,password,resource-code,customer-code,account-code}` | não aprovisionadas |

**Cadeia de autenticação (confirmada no legado `AbstractService` e ao vivo):**
1. `POST oauth/v1/access-token` com `Authorization: Basic {apim-key}` + JSON
   `{username, password, resourceCode}` → `{accessToken: {token, expirationDate}}`.
2. `POST oauth/v1/represented-customer-token/account` com Bearer do token 1 + JSON
   `{customerCode, accountCode, resourceCode}` → represented token (usado em todos os recursos).
O SDK encapsula a cadeia (`IOuribankTokenProvider`: cache por `expirationDate` real − skew,
single-flight, renovação da cadeia inteira em 401; mTLS nos dois pipelines via PEM→PKCS#12 em
memória). Legado: `verify: false` em ambos — defeito NÃO replicado (curioso: no OuriBank o legado
até valida TLS; nos demais não — aqui `verify: true` no legado, mantido).

## 3. Convenções e peculiaridades

- camelCase; statuses de transação **numéricos**: 1=Starting, 2=Confirmed, 3=Canceled,
  6=ConfirmedDebit (payout efetivado), 7=Reversed — constantes em `OuribankTransactionStatuses`.
- **`options` é uma STRING contendo JSON** (o legado faz `json_encode` aninhado) — helper
  `CnrOptions.ToJsonString(txId, lclInstrm, prxyId)`; `LclInstrm`: `DICT` (chave) / `MANU` (conta).
- Erros: `Error.ErrorMessage` / `error.errorMessage` / `reason` (formas do legado; extração
  defensiva multi-forma). Cashout responde `{accepted, reason}`.
- Sem retry/idempotência no legado; idempotência de negócio via `originId`/`txId`.

## 4. Matriz de cobertura

| # | Endpoint | Recurso SDK | Efeito | Cleanup | Status sandbox |
|---|---|---|---|---|---|
| 1 | `oauth/v1/access-token` + `represented-customer-token/account` | `OuribankTokenProvider` | read/auth | n/a | ✅ vivo (mTLS, via SDK) |
| 2 | `fx/v1/fx-rate/customer-rate/{customerCode}` | `ExchangeRates.GetCustomerRateAsync` | read | n/a | ✅ vivo (spot 5.0756) |
| 3 | `payment-gateway/v1/qrcode-dynamic/create` | `PixQrCodes.CreateDynamicAsync` | non-financial-write (QR não pago; `expirationInSeconds` = cleanup natural) | expira sozinho | 🟡 elegível a E2E com expiração curta (não executado por default) |
| 4 | `payment-gateway/v1/transactions/txid/{txId}` | `Transactions.GetByTxIdAsync` | read | n/a | ✅ vivo (200, records=0 p/ fictício — acesso real) |
| 5 | `payment-gateway/v1/transactions/e2e/{endToEndId}` | `Transactions.GetByEndToEndIdAsync` | read | n/a | ⚪ unit (mesma família de #4) |
| 6 | `payment-gateway/v1/transactions/{origin}/{originId}` | `Transactions.GetByOriginIdAsync` | read | n/a | ⚪ unit |
| 7 | `payment-gateway/v1/transactions/account/{accountNumber}` | `Transactions.GetAccountStatementsAsync` | read (saldo/extrato) | n/a | ⚪ unit (exige account_number, não presente no pass — gap registrado) |
| 8 | `payment-gateway/v1/dict/get?key=` | `PixQrCodes.GetDictKeyAsync` | read (DICT lookup) | n/a | ⚪ unit (consulta PII de chave — não sondar sem necessidade) |
| 9 | `payment-gateway/v1/payment/cashout` (payout chave/conta) | `Payouts.CreateAsync` | **financial-write** | n/a | 🔴 contrato/fixture only (goal §0.5) |
| 10 | `payment-gateway/v1/payment/cashout` (`isDevolucao` = refund) | idem | **financial-write** | n/a | 🔴 idem |
| 11 | `fx/v1/webhook/inscricao` · `cancelar-inscricao/{id}` | `Webhooks.{Create,Delete}Async` | non-financial-write com cleanup | DELETE existe | ⚪ contrato-only (inscrição exige URI pública) |

Gap registrado: `account_number` (usado em #7 e no `debitParty` do cashout) e `partner_code`
(webhook) não estão no `pass` — inventariar de `cerebro/config/ouribank.php`/env de produção
quando o payout for habilitado (§3c: registrar no pass antes de usar).

## 5. Webhooks — gateway sem inbound (decisão)

O legado tem infraestrutura de INSCRIÇÃO de webhook (tipoWebhook.codigo 15 = payin, com token
HMAC e URI de callback) mas o consumo segue o padrão re-poll (PayinNotification/PayoutNotification
consultam transactions). Gateway v1 não expõe inbound; a inscrição está disponível no SDK.

## 6. Limites de responsabilidade

- **SDK**: API nativa (mTLS + cadeia de tokens encapsulados); nunca loga PEM/tokens/PII.
- **Gateway**: `/v1/ouribank/*` no contrato canônico; cashout documentado FINANCIAL-WRITE.
- **Plataforma**: cadência de polling; decisão de rails (financialType 0/1/2); renovação do cert.

## 7. Nenhuma contradição arquitetural

Padrão canônico Sync + SDK/gateway standalone. Decisões locais: cadeia de tokens encapsulada;
options-como-string fielmente modelado; webhook inbound fora do v1.
