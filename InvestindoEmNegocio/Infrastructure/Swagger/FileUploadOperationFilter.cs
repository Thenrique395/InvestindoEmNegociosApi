using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace InvestindoEmNegocio.Infrastructure.Swagger;

public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
        {
            return;
        }

        var hasFile = actionDescriptor.Parameters
            .Any(parameter => typeof(IFormFile).IsAssignableFrom(parameter.ParameterType));

        if (!hasFile)
        {
            return;
        }

        operation.Parameters.Clear();

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties =
                        {
                            ["avatar"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary"
                            }
                        },
                        Required = new HashSet<string> { "avatar" }
                    }
                }
            }
        };
    }
}
