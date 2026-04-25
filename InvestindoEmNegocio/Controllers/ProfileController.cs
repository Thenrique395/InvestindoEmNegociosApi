using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/profile")]
[Route("api/v1/profile")]
[Authorize(Policy = AppAuthorizationPolicies.FeatureProfileManage)]
public class ProfileController(
    IProfileQueryService profileQueryService,
    IProfileCommandService profileCommandService,
    IAvatarStorageService avatarStorageService) : AuthenticatedControllerBase
{
    [HttpGet]
    // Returns the authenticated user's profile, or 204 when it does not exist yet.
    public async Task<ActionResult<UserProfileDto>> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var profile = await profileQueryService.GetAsync(userId, cancellationToken);
        if (profile is null) return NoContent();
        return Ok(profile);
    }

    [HttpPut]
    // Creates or updates the authenticated user's profile.
    public async Task<ActionResult<UserProfileDto>> Upsert([FromBody] UpsertUserProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var profile = await profileCommandService.UpsertAsync(userId, request, cancellationToken);
        return Ok(profile);
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    // Uploads the profile photo and updates AvatarUrl.
    public async Task<ActionResult<UserProfileDto>> UploadAvatar([FromForm] UploadAvatarRequest request, CancellationToken cancellationToken)
    {
        var avatar = request.Avatar;
        if (avatar is null || avatar.Length == 0)
            throw new AppProblemException(
                "Arquivo inválido",
                "Envie uma imagem válida.",
                StatusCodes.Status400BadRequest);


        var userId = GetUserId();
        await using var stream = avatar.OpenReadStream();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var avatarUrl = await avatarStorageService.SaveAsync(
            userId,
            stream,
            avatar.FileName,
            avatar.ContentType,
            baseUrl,
            cancellationToken);

        var profile = await profileCommandService.UpdateAvatarAsync(userId, avatarUrl, cancellationToken);
        return Ok(profile);
    }

}
