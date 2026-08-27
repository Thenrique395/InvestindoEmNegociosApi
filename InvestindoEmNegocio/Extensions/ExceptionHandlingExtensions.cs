using InvestindoEmNegocio.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Extensions;

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseGlobalProblemDetails(this IApplicationBuilder app, bool includeExceptionDetails)
    {
        app.UseExceptionHandler(exceptionApp =>
        {
            exceptionApp.Run(async context =>
            {
                var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionHandler?.Error;
                var statusCode = StatusCodes.Status500InternalServerError;
                var title = "Erro interno do servidor.";
                var detail = includeExceptionDetails ? exception?.Message : null;

                string? code = null;
                if (exception is AppProblemException appProblem)
                {
                    statusCode = appProblem.StatusCode;
                    title = appProblem.Title;
                    detail = appProblem.Detail;
                    code = appProblem.Code;
                }
                else if (exception is UnauthorizedAccessException)
                {
                    // Rede de segurança: antes, só quem passasse `unauthorizedAccessTitle` em
                    // ExecuteWithProblemMappingAsync tinha o mapeamento — 3 controllers de 24.
                    // Nos outros, "usuário não encontrado" ou "recurso de outro usuário" saía
                    // como 500 "Erro interno do servidor", com log de Error e alerta falso.
                    // 403: o usuário ESTÁ autenticado (o pipeline já validou o token); o que
                    // falta é acesso ao recurso. Quem precisa de 401 continua mapeando no
                    // controller, que converte antes de chegar aqui.
                    statusCode = StatusCodes.Status403Forbidden;
                    title = "Acesso negado";
                    detail = exception.Message;
                }

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Instance = context.Request.Path,
                    Extensions =
                    {
                        ["traceId"] = context.TraceIdentifier
                    }
                };

                if (code is not null)
                    problemDetails.Extensions["code"] = code;

                if (includeExceptionDetails && exception is not null)
                    problemDetails.Extensions["exception"] = exception.GetType().Name;

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });

        return app;
    }

    /// <summary>
    /// Erro de negócio conhecido: responde 4xx sem stack trace nem log de Error.
    /// <see cref="UnauthorizedAccessException"/> entra aqui pelo mesmo motivo — não é falha
    /// de servidor, é o dono do recurso sendo outro.
    /// </summary>
    private static bool EhErroDeNegocio(Exception ex) =>
        ex is UnauthorizedAccessException
        || (ex is AppProblemException problem && problem.StatusCode < StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Responde os erros de negócio (<see cref="AppProblemException"/> com status
    /// abaixo de 500) antes que eles cheguem ao handler global.
    ///
    /// O motivo é o log: o <c>ExceptionHandlerMiddleware</c> do ASP.NET grava
    /// "An unhandled exception has occurred", em nível Error e com stack trace,
    /// para toda exceção que trata — inclusive um 400 de token inválido ou um 409
    /// de cartão repetido. Quem lê o log vai atrás de um incidente que não houve.
    /// (No .NET 10 dá para resolver com `SuppressDiagnosticsCallback`; aqui, não.)
    ///
    /// Falha de verdade não passa por aqui: segue para o handler global, que
    /// responde 500 e registra como Error.
    /// </summary>
    public static IApplicationBuilder UseBusinessProblemDetails(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception capturada) when (EhErroDeNegocio(capturada))
            {
                var exception = capturada as AppProblemException
                    ?? new AppProblemException("Acesso negado", capturada.Message, StatusCodes.Status403Forbidden);

                // Resposta já iniciada: não há como trocar o status, e insistir
                // corromperia o corpo. Deixa subir para o handler global.
                if (context.Response.HasStarted) throw;

                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("InvestindoEmNegocio.BusinessProblem");

                logger.LogWarning(
                    "{Method} {Path} respondeu {StatusCode}: {Title}",
                    context.Request.Method,
                    context.Request.Path,
                    exception.StatusCode,
                    exception.Title);

                var problemDetails = new ProblemDetails
                {
                    Status = exception.StatusCode,
                    Title = exception.Title,
                    Detail = exception.Detail,
                    Instance = context.Request.Path,
                    Extensions =
                    {
                        ["traceId"] = context.TraceIdentifier
                    }
                };

                if (exception.Code is not null)
                    problemDetails.Extensions["code"] = exception.Code;

                context.Response.StatusCode = exception.StatusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        });
    }
}
