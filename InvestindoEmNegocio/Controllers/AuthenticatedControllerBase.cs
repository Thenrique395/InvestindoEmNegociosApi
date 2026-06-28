using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Infrastructure.Auth;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected async Task<IActionResult> ExecuteWithProblemMappingAsync(
        Func<Task<IActionResult>> action,
        string argumentTitle,
        int argumentStatusCode = StatusCodes.Status400BadRequest,
        string? unauthorizedAccessTitle = null,
        int unauthorizedAccessStatusCode = StatusCodes.Status403Forbidden,
        string? invalidOperationTitle = null,
        int invalidOperationStatusCode = StatusCodes.Status409Conflict)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException(argumentTitle, ex.Message, argumentStatusCode);
        }
        catch (UnauthorizedAccessException ex) when (!string.IsNullOrWhiteSpace(unauthorizedAccessTitle))
        {
            throw new AppProblemException(unauthorizedAccessTitle!, ex.Message, unauthorizedAccessStatusCode);
        }
        catch (InvalidOperationException ex) when (!string.IsNullOrWhiteSpace(invalidOperationTitle))
        {
            throw new AppProblemException(invalidOperationTitle!, ex.Message, invalidOperationStatusCode);
        }
    }

    protected async Task<ActionResult<T>> ExecuteWithProblemMappingAsync<T>(
        Func<Task<ActionResult<T>>> action,
        string argumentTitle,
        int argumentStatusCode = StatusCodes.Status400BadRequest,
        string? unauthorizedAccessTitle = null,
        int unauthorizedAccessStatusCode = StatusCodes.Status403Forbidden,
        string? invalidOperationTitle = null,
        int invalidOperationStatusCode = StatusCodes.Status409Conflict)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            throw new AppProblemException(argumentTitle, ex.Message, argumentStatusCode);
        }
        catch (UnauthorizedAccessException ex) when (!string.IsNullOrWhiteSpace(unauthorizedAccessTitle))
        {
            throw new AppProblemException(unauthorizedAccessTitle!, ex.Message, unauthorizedAccessStatusCode);
        }
        catch (InvalidOperationException ex) when (!string.IsNullOrWhiteSpace(invalidOperationTitle))
        {
            throw new AppProblemException(invalidOperationTitle!, ex.Message, invalidOperationStatusCode);
        }
    }

    protected Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    protected Guid? GetUserIdOrNull()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    protected Guid GetSpaceId()
    {
        var claim = User.FindFirstValue(JwtTokenGenerator.SpaceIdClaim);
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("Espaço ativo não encontrado.");
    }

    protected string? GetIpAddress()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    protected string? GetUserAgent()
    {
        return Request.Headers.UserAgent.ToString();
    }
}
