using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Validation;

namespace InvestindoEmNegocio.Tests;

public class UpdateOnboardingRequestValidatorTests
{
    private readonly UpdateOnboardingRequestValidator _sut = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Should_Accept_Step_In_Range_When_Not_Completed(int step)
    {
        _sut.Validate(new UpdateOnboardingRequest(step, false)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Should_Reject_Step_Out_Of_Range(int step)
    {
        _sut.Validate(new UpdateOnboardingRequest(step, false)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Accept_Completed_Only_On_Last_Step()
    {
        _sut.Validate(new UpdateOnboardingRequest(3, true)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Should_Reject_Completed_Before_Last_Step(int step)
    {
        // #7: impede pular o onboarding via API (ex.: {step:0, completed:true}).
        _sut.Validate(new UpdateOnboardingRequest(step, true)).IsValid.Should().BeFalse();
    }
}
