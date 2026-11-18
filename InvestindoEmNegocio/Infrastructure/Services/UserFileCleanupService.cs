using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Infrastructure.Services;

public sealed class UserFileCleanupService(IWebHostEnvironment env) : IUserFileCleanupService
{
    public Task DeleteUserFilesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath) ? Path.Combine(env.ContentRootPath, "wwwroot") : env.WebRootPath;
        string[] directories = [Path.Combine(env.ContentRootPath, "storage", "receipts"), Path.Combine(webRoot, "uploads", "receipts"), Path.Combine(webRoot, "uploads", "avatars")];
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.EnumerateFiles(directory, $"{userId:N}_*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(path);
            }
        }
        return Task.CompletedTask;
    }
}
