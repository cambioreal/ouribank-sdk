using System.Net;

namespace CambioReal.Ouribank.Http;

/// <summary>Erro devolvido pela Ouribank API.</summary>
public class OuribankApiException : Exception
{
    /// <summary>Cria uma exceção sem contexto de resposta.</summary>
    public OuribankApiException()
    {
    }

    /// <summary>Cria uma exceção com mensagem.</summary>
    public OuribankApiException(string message)
        : base(message)
    {
    }

    /// <summary>Cria uma exceção com mensagem e causa.</summary>
    public OuribankApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Cria uma exceção a partir de uma resposta da API.</summary>
    public OuribankApiException(
        HttpStatusCode statusCode, string? errorType, string? errorCode, string message, string? requestId, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
        RequestId = requestId;
        ResponseBody = responseBody;
    }

    /// <summary>Status HTTP da resposta.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Reservado (a OuriBank não tem catálogo de tipo de erro; fica nulo).</summary>
    public string? ErrorType { get; }

    /// <summary>Mensagem extraída de <c>Error.ErrorMessage</c>/<c>error.errorMessage</c>/<c>reason</c> (formas do legado).</summary>
    public string? ErrorCode { get; }

    /// <summary>Reservado para correlação futura (a OuriBank não devolve request id).</summary>
    public string? RequestId { get; }

    /// <summary>Corpo bruto da resposta, para diagnóstico.</summary>
    public string? ResponseBody { get; }
}

/// <summary>Falha na cadeia de autenticação (access/represented token) ou 401 pós-retry.</summary>
public sealed class OuribankAuthenticationException : OuribankApiException
{
    /// <inheritdoc/>
    public OuribankAuthenticationException()
    {
    }

    /// <inheritdoc/>
    public OuribankAuthenticationException(string message)
        : base(message)
    {
    }

    /// <inheritdoc/>
    public OuribankAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public OuribankAuthenticationException(
        HttpStatusCode statusCode, string? errorType, string? errorCode, string message, string? requestId, string? responseBody)
        : base(statusCode, errorType, errorCode, message, requestId, responseBody)
    {
    }
}
