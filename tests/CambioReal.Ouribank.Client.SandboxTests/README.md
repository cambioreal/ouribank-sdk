# CambioReal.Ouribank.Client.SandboxTests

Integração sandbox real com mTLS, opt-in — fora de `Ouribank.slnx`, nunca em CI.
Variáveis (ver topo de `OuribankSandboxTests.cs`): credenciais de `pass cambio-real-v2/ouribank/*`
e PEMs de `pass cambio-real-v2/providers/ouribank/sandbox-client-{cert,key}`.

## Última execução ao vivo — 2026-07-15

```
Passed CustomerRateReadsLive [1 s]
  GET fx-rate/customer-rate: 200, spotRate=5.0756 (real).
Passed TokenChainAuthenticatesLiveOverMtls [1 s]
  cadeia mTLS access→represented: ok.
Passed TransactionsByFictitiousTxIdReturnsEmptyOrDomainError [1 s]
  GET transactions/txid/{fictício}: 200, records=0 — acesso real ao recurso confirmado.

Passed: 3, Failed: 0, Total: 3
```
