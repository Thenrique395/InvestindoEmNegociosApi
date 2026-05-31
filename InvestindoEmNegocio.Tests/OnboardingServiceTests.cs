using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class OnboardingServiceTests
{
    [Fact]
    public async Task GetStatusAsync_Should_Return_Default_When_Not_Found()
    {
        var repository = new Mock<IUserOnboardingRepository>();
        repository
            .Setup(x => x.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserOnboarding?)null);
        var sut = new OnboardingService(repository.Object, NullLogger<OnboardingService>.Instance);

        var result = await sut.GetStatusAsync(Guid.NewGuid());

        result.Step.Should().Be(0);
        result.Completed.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Clamp_Step_And_Create_When_Not_Found()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IUserOnboardingRepository>();
        repository
            .Setup(x => x.GetByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserOnboarding?)null);
        var sut = new OnboardingService(repository.Object, NullLogger<OnboardingService>.Instance);

        var result = await sut.UpdateStatusAsync(userId, new UpdateOnboardingRequest(99, true));

        result.Step.Should().Be(3);
        result.Completed.Should().BeTrue();
        repository.Verify(x => x.AddAsync(It.IsAny<UserOnboarding>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
