using System.IO;
using System.Security.Claims;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvestindoEmNegocio.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController(IProfileService profileService, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    // Retorna o perfil do usuário autenticado (204 se ainda não existir).
    public async Task<ActionResult<UserProfileDto>> Get(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var profile = await profileService.GetAsync(userId, cancellationToken);
        if (profile is null) return NoContent();
        return Ok(profile);
    }

    [HttpPut]
    // Cria ou atualiza o perfil do usuário autenticado com dados pessoais.
    public async Task<ActionResult<UserProfileDto>> Upsert([FromBody] UpsertUserProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        try
        {
            var profile = await profileService.UpsertAsync(userId, request, cancellationToken);
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Perfil inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    // Faz upload da foto de perfil e atualiza o AvatarUrl.
    public async Task<ActionResult<UserProfileDto>> UploadAvatar([FromForm] UploadAvatarRequest request, CancellationToken cancellationToken)
    {
        var avatar = request.Avatar;
        if (avatar is null || avatar.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Envie uma imagem válida.", Status = StatusCodes.Status400BadRequest });
        }

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        if (!allowedTypes.Contains(avatar.ContentType))
        {
            return BadRequest(new ProblemDetails { Title = "Arquivo inválido", Detail = "Formato não suportado. Use PNG, JPG ou WEBP.", Status = StatusCodes.Status400BadRequest });
        }

        var webRoot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var uploadsPath = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(uploadsPath);

        var ext = Path.GetExtension(avatar.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = avatar.ContentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var fileName = $"{GetUserId():N}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await avatar.CopyToAsync(stream, cancellationToken);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var avatarUrl = $"{baseUrl}/uploads/avatars/{fileName}";

        try
        {
            var profile = await profileService.UpdateAvatarAsync(GetUserId(), avatarUrl, cancellationToken);
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Perfil inválido", Detail = ex.Message, Status = StatusCodes.Status400BadRequest });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (Guid.TryParse(claim, out var id))
        {
            return id;
        }
        throw new UnauthorizedAccessException("Usuário não autenticado.");
    }
}
