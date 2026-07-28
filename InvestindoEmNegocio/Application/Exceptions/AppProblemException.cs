namespace InvestindoEmNegocio.Application.Exceptions;

public sealed class AppProblemException(string title, string detail, int statusCode, string? code = null) : Exception(detail)
{
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public int StatusCode { get; } = statusCode;
    // Código legível por máquina (opcional), exposto em problemDetails.Extensions["code"] para o
    // front distinguir casos (ex.: "email_not_confirmed").
    public string? Code { get; } = code;
}
