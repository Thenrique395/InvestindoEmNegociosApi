using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/accounts/transfers")]
[Route("api/v1/accounts/transfers")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureAccountsManage)]
public class AccountTransfersController(IAccountsService accountsService) : AuthenticatedControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AccountTransferRequest request, CancellationToken cancellationToken)
    {
        return await ExecuteWithProblemMappingAsync(async () =>
        {
            var userId = GetUserId();
            var transfer = await accountsService.TransferAsync(userId, request, cancellationToken);
            if (transfer is null) return NotFound();
            return Ok(transfer);
        }, "Invalid transfer");
    }
}
