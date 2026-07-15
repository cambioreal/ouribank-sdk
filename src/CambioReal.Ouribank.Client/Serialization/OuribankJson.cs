using System.Text.Json;
using System.Text.Json.Serialization;

namespace CambioReal.Ouribank.Serialization;

/// <summary>Convenções de JSON da OuriBank Payment Gateway API.</summary>
public static class OuribankJson
{
    /// <summary>
    /// Campos em <c>camelCase</c> (<c>payerDocument</c>, <c>expirationInSeconds</c>,
    /// <c>creditParty</c>, <c>accessToken.expirationDate</c>, …) — confirmado no legado
    /// (<c>cerebro/app/Libraries/Ouribank/*</c>) e nas respostas vivas (2026-07-15). Statuses de
    /// transação são NUMÉRICOS (1=Starting, 2=Confirmed, 3=Canceled, 6=ConfirmedDebit,
    /// 7=Reversed) — modelados como <see cref="int"/> + constantes. O campo <c>options</c> dos
    /// requests é uma STRING contendo JSON (peculiaridade real da API — o legado faz
    /// <c>json_encode</c> aninhado).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
