using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Validation;

namespace InvestindoEmNegocio.Tests;

public class UpsertUserProfileRequestValidatorTests
{
    private readonly UpsertUserProfileRequestValidator _sut = new();

    [Fact]
    public void Validate_Should_Pass_For_Valid_Request()
    {
        var request = new UpsertUserProfileRequest(
            FullName: "Henrique Santos",
            Document: "12345678901",
            Phone: "+55 81 999999999",
            BirthDate: new DateTime(1990, 1, 1),
            AvatarUrl: "https://cdn/avatar.png",
            City: "Recife",
            State: "PE",
            Country: "Brasil",
            FinancialGoal: "comecar-investir",
            Language: "pt-BR");

        var result = _sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_Fail_For_Invalid_Document_And_Phone()
    {
        var request = new UpsertUserProfileRequest(
            FullName: "He",
            Document: "123",
            Phone: "81999999999",
            BirthDate: null,
            AvatarUrl: "",
            City: "",
            State: "",
            Country: "",
            FinancialGoal: "",
            Language: "");

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("CPF deve ter exatamente 11 dígitos", StringComparison.Ordinal));
        result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("Telefone deve estar no formato", StringComparison.Ordinal));
        result.Errors.Select(e => e.ErrorMessage).Should().Contain(m => m.Contains("Nome completo deve ter ao menos 3 caracteres", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Should_Fail_When_FinancialGoal_Exceeds_Max_Length()
    {
        var request = new UpsertUserProfileRequest(
            FullName: "Henrique Santos",
            Document: "12345678901",
            Phone: "(81) 99525-7823",
            BirthDate: new DateTime(1990, 1, 1),
            AvatarUrl: "",
            City: "Recife",
            State: "PE",
            Country: "Brasil",
            FinancialGoal: new string('a', 81),
            Language: "pt-BR");

        var result = _sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .Should().Contain(m => m.Contains("Objetivo financeiro deve ter no máximo 80 caracteres", StringComparison.Ordinal));
    }
}
