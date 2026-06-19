using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Domain.Entities;

namespace InvestindoEmNegocio.Application.Services;

internal static class ProfileDtoFactory
{
    internal static UserProfileDto CreateDto(UserProfile profile, User? user)
    {
        var language = string.IsNullOrWhiteSpace(profile.Language) ? "pt-BR" : profile.Language;
        var locales = language
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (locales.Count == 0) locales.Add("pt-BR");
        var currency = profile.Currency ?? "BRL";
        return new UserProfileDto(profile.UserId, user?.Email ?? string.Empty, user?.Name ?? string.Empty, user?.Document ?? string.Empty, user?.Phone ?? string.Empty, user?.BirthDate,
            user?.AvatarUrl ?? string.Empty, user?.City ?? string.Empty, user?.State ?? string.Empty, user?.Country ?? string.Empty,
            profile.FinancialGoal, profile.CarryOverDay, profile.IntelligenceMode, language, currency, locales);
    }

    // Usado quando o usuário ainda não salvou um UserProfile: traz os dados já
    // cadastrados em User (nome, CPF, telefone, data de nascimento, localização) para
    // pré-preencher o onboarding.
    internal static UserProfileDto CreateDefaultDto(User user)
    {
        const string language = "pt-BR";
        return new UserProfileDto(user.Id, user.Email, user.Name, user.Document, user.Phone, user.BirthDate,
            user.AvatarUrl, user.City, user.State, user.Country,
            string.Empty, 1, "B", language, "BRL", [language]);
    }
}
