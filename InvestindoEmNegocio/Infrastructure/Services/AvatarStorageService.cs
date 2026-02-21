using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class AvatarStorageService(IWebHostEnvironment env) : IAvatarStorageService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<string> SaveAsync(
        Guid userId,
        Stream content,
        string originalFileName,
        string contentType,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.Contains(contentType))
        {
            throw new AppProblemException(
                "Arquivo inválido",
                "Formato não suportado. Use PNG, JPG ou WEBP.",
                StatusCodes.Status400BadRequest);
        }

        var webRoot = env.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var uploadsPath = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(uploadsPath);

        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        var fileName = $"{userId:N}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"{baseUrl}/uploads/avatars/{fileName}";
    }
}
