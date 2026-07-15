namespace CambioReal.Ouribank;

/// <summary>Ambiente da OuriBank Payment Gateway API.</summary>
public enum OuribankEnvironment
{
    /// <summary>Sandbox — <c>https://api.sbx.ouribank.com/</c>.</summary>
    Sandbox = 0,

    /// <summary>Produção — <c>https://api.ouribank.com/</c>.</summary>
    Production = 1,
}

/// <summary>Resolve o endereço base de cada <see cref="OuribankEnvironment"/>.</summary>
public static class OuribankEnvironmentExtensions
{
    /// <summary>Endereço base, confirmado em <c>cerebro/config/ouribank.php</c> e validado ao vivo (2026-07-15).</summary>
    public static Uri GetBaseAddress(this OuribankEnvironment environment) => environment switch
    {
        OuribankEnvironment.Production => new Uri("https://api.ouribank.com/", UriKind.Absolute),
        OuribankEnvironment.Sandbox => new Uri("https://api.sbx.ouribank.com/", UriKind.Absolute),
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "Ambiente OuriBank desconhecido."),
    };
}

/// <summary>Configuração do <see cref="OuribankClient"/>.</summary>
public sealed class OuribankOptions
{
    /// <summary>Nome da seção de configuração sugerida.</summary>
    public const string SectionName = "Ouribank";

    /// <summary>
    /// Chave APIM (base64 de <c>id:secret</c>) enviada como <c>Authorization: Basic</c> só no
    /// primeiro token da cadeia. Origem: <c>pass cambio-real-v2/ouribank/apim-key</c>.
    /// </summary>
    public string ApimKey { get; set; } = string.Empty;

    /// <summary>Usuário do access token. Origem: <c>pass cambio-real-v2/ouribank/username</c>.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Senha do access token. Origem: <c>pass cambio-real-v2/ouribank/password</c>.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Resource code das duas etapas da cadeia. Origem: <c>pass cambio-real-v2/ouribank/resource-code</c>.</summary>
    public string ResourceCode { get; set; } = string.Empty;

    /// <summary>Customer code (cliente representado + fx-rate). Origem: <c>pass cambio-real-v2/ouribank/customer-code</c>.</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>Account code do represented token. Origem: <c>pass cambio-real-v2/ouribank/account-code</c>.</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Certificado mTLS (PEM, conteúdo — não path). Origem:
    /// <c>pass cambio-real-v2/providers/ouribank/sandbox-client-cert</c>. Expira 2026-09-06 —
    /// plano de renovação antes do deploy (goal §Segurança).
    /// </summary>
    public string CertificatePem { get; set; } = string.Empty;

    /// <summary>Chave privada mTLS (PEM, conteúdo). Origem: <c>pass .../sandbox-client-key</c>.</summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>Ambiente alvo. O padrão é <see cref="OuribankEnvironment.Sandbox"/>, deliberadamente.</summary>
    public OuribankEnvironment Environment { get; set; } = OuribankEnvironment.Sandbox;

    /// <summary>Sobrescreve o endereço base. Precisa terminar em <c>/</c>.</summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>Margem de renovação antecipada dos tokens (a expiração real vem de <c>expirationDate</c>).</summary>
    public TimeSpan TokenExpirationSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout de cada requisição HTTP (paridade com o legado: 30s).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Endereço base efetivo.</summary>
    public Uri ResolveBaseAddress() => BaseAddress ?? Environment.GetBaseAddress();

    /// <summary>Valida a configuração e lança se estiver inconsistente.</summary>
    /// <exception cref="InvalidOperationException">Alguma credencial obrigatória está ausente ou o base address é inválido.</exception>
    public void Validate()
    {
        foreach (var (value, name) in new[]
        {
            (ApimKey, nameof(ApimKey)),
            (Username, nameof(Username)),
            (Password, nameof(Password)),
            (ResourceCode, nameof(ResourceCode)),
            (CustomerCode, nameof(CustomerCode)),
            (AccountCode, nameof(AccountCode)),
            (CertificatePem, nameof(CertificatePem)),
            (PrivateKeyPem, nameof(PrivateKeyPem)),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{nameof(OuribankOptions)}.{name} é obrigatório.");
            }
        }

        var baseAddress = ResolveBaseAddress();

        if (!baseAddress.IsAbsoluteUri || !baseAddress.AbsolutePath.EndsWith('/'))
        {
            throw new InvalidOperationException($"{nameof(BaseAddress)} precisa ser absoluto e terminar em '/'.");
        }

        if (TokenExpirationSkew < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(TokenExpirationSkew)} não pode ser negativo.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(Timeout)} precisa ser positivo.");
        }
    }
}
