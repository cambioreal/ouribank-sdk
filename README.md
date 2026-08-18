# ouribank-sdk

Cliente .NET tipado (`CambioReal.Ouribank.Client`) para a **OuriBank Payment Gateway API** — PIX
payin via QR dinâmico, payout/cashout, fx-rate, DICT e inscrição de webhooks. Particularidades
encapsuladas: **mTLS obrigatório** (PEM→PKCS#12 em memória, cert/key só via secret store) e
**cadeia de dois tokens** (`Basic {apim-key}` → access token → represented token), com cache pelo
`expirationDate` real, single-flight e renovação da cadeia em 401. Mesmo padrão dos demais SDKs
cambioreal.

Achado desta iteração que destravou o provider: o path real de token é `oauth/v1/access-token`
(o greenfield v3 tentava `/oauth/token` e recebia 404).

## Validação ao vivo (2026-07-15, sandbox mTLS)

3/3 SandboxTests verdes via SDK real: cadeia de tokens ok; `GET fx-rate/customer-rate` com
**spot 5.0756 real**; `GET transactions/txid/{fictício}` 200/records=0 (acesso real ao recurso).
8 testes unit (cadeia Basic→Bearer, cache, options-como-string, erros nas formas do legado,
statuses numéricos). Cashout (payout/refund) = **financial-write**, nunca executado (goal §0.5).

⚠️ Certificado sandbox expira **2026-09-06** — renovar antes do deploy.

## Secrets

`pass cambio-real-v2/ouribank/*` + `pass cambio-real-v2/providers/ouribank/sandbox-client-{cert,key}`
→ variáveis de ambiente. Nunca em appsettings/fixtures; nenhum teste imprime PEM/token/PII.
Discovery completo: `docs/providers/ouribank/discovery.md`.

## Instalação e uso

Pacote no GitHub Packages da org `cambioreal` (feed configurado no `NuGet.config` do repo consumidor):

```bash
dotnet add package CambioReal.Ouribank.Client
```

```csharp
// Registro via DI — credenciais vêm de config segura (env/Secret/pass), nunca versionadas.
builder.Services.AddOuribankClient(builder.Configuration.GetSection(OuribankOptions.SectionName));

// ...injete CambioReal.Ouribank.OuribankClient onde precisar.
```

Também há a sobrecarga `AddOuribankClient(Action<OuribankOptions>)` para configuração inline.
