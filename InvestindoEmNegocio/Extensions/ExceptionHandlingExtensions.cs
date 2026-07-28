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
}
