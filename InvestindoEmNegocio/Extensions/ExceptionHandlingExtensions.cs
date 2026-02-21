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
                string? detail = includeExceptionDetails ? exception?.Message : null;

                if (exception is AppProblemException appProblem)
                {
                    statusCode = appProblem.StatusCode;
                    title = appProblem.Title;
                    detail = appProblem.Detail;
                }

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Instance = context.Request.Path
                };

                problemDetails.Extensions["traceId"] = context.TraceIdentifier;
                if (includeExceptionDetails && exception is not null)
                {
                    problemDetails.Extensions["exception"] = exception.GetType().Name;
                }

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            });
        });

        return app;
    }
}
