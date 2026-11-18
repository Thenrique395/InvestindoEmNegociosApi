using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Authorize(Policy = AppAuthorizationPolicies.FeatureInstallmentsRead)]
[Route("api/v1/receipts")]
[Route("uploads/receipts")]
public sealed class ReceiptsController(IReceiptAccessService receipts) : AuthenticatedControllerBase
{
    [HttpGet("{fileName}")]
    public async Task<IActionResult> Get(string fileName, CancellationToken cancellationToken)
    {
        var file = await receipts.OpenAsync(GetUserId(), fileName, cancellationToken);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return File(file.Content, file.ContentType);
    }
}
