using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class ReceiptStorageService(IWebHostEnvironment env) : IReceiptStorageService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf"
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
            throw new AppProblemException("Arquivo inválido", "Formato não suportado. Use PNG, JPG, WEBP ou PDF.", StatusCodes.Status400BadRequest);

        var uploadsPath = Path.Combine(env.ContentRootPath, "storage", "receipts");
        Directory.CreateDirectory(uploadsPath);
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => ".jpg"
        };

        var fileName = $"{userId:N}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return $"{baseUrl}/api/v1/receipts/{fileName}";
    }
    public Task<ReceiptFile?> OpenAsync(Guid userId, string fileName, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, $"^{userId:N}_[a-f0-9]{{32}}\\.(pdf|png|jpg|jpeg|webp)$"))
            return Task.FromResult<ReceiptFile?>(null);

        var path = Path.Combine(env.ContentRootPath, "storage", "receipts", fileName);
        if (!File.Exists(path))
        {
            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath) ? Path.Combine(env.ContentRootPath, "wwwroot") : env.WebRootPath;
            path = Path.Combine(webRoot, "uploads", "receipts", fileName);
        }
        if (!File.Exists(path)) return Task.FromResult<ReceiptFile?>(null);
        var type = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf", ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg"
        };
        return Task.FromResult<ReceiptFile?>(new(File.OpenRead(path), type));
    }

}
