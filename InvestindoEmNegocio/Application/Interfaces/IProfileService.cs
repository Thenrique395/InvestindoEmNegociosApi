using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IProfileService
{
    Task<UserProfileDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpsertAsync(Guid userId, UpsertUserProfileRequest request, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateAvatarAsync(Guid userId, string avatarUrl, CancellationToken cancellationToken = default);
}
