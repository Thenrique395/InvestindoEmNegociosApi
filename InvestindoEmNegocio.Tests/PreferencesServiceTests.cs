using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_Should_Return_Default_When_Profile_Not_Found()
    {
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var sut = new PreferencesService(repository.Object);

        var result = await sut.GetAsync(Guid.NewGuid());

        result.Currency.Should().Be("BRL");
        result.Locales.Should().ContainSingle().Which.Should().Be("pt-BR");
        result.Notifications.InAppEnabled.Should().BeTrue();
        result.Notifications.EmailEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_Should_Create_Profile_When_Not_Found()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var sut = new PreferencesService(repository.Object);

        var request = new UpdatePreferencesRequest("USD", ["en-US"], new NotificationPreferencesDto(false, true));
        var result = await sut.UpdateAsync(userId, request);

        result.Currency.Should().Be("USD");
        result.Locales.Should().Equal("en-US");
        result.Notifications.Should().BeEquivalentTo(request.Notifications);
        repository.Verify(x => x.AddAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
