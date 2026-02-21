namespace InvestindoEmNegocio.Application.Exceptions;

public sealed class AppProblemException(string title, string detail, int statusCode) : Exception(detail)
{
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public int StatusCode { get; } = statusCode;
}
