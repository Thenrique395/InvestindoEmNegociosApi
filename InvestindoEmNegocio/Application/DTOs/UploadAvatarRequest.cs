using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Application.DTOs;

public sealed class UploadAvatarRequest
{
    public IFormFile? Avatar { get; init; }
}
