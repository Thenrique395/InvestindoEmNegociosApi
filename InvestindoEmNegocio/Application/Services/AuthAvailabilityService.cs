using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Validation;
using InvestindoEmNegocio.Domain.Repositories;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AuthAvailabilityService(IUserRepository userRepository) : IAuthAvailabilityService
{
    public async Task<CheckAvailabilityResponse> CheckAsync(CheckAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        var emailExists = false;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = AuthServicePolicies.NormalizeEmail(request.Email);
            emailExists = await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken);
        }

        var documentExists = false;
        if (!string.IsNullOrWhiteSpace(request.Document))
        {
            var normalizedDocument = CpfValidation.Normalize(request.Document);
            documentExists = await userRepository.DocumentExistsAsync(normalizedDocument, cancellationToken);
        }

        return new CheckAvailabilityResponse(emailExists, documentExists);
    }
}
