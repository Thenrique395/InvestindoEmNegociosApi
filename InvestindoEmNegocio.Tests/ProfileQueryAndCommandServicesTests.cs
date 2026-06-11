using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class ProfileQueryAndCommandServicesTests
{
    [Fact]
    public async Task GetAsync_Should_Use_Cache_On_Second_Call()
    {
        var userId = Guid.NewGuid();
        var profile = NewProfile(userId);
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        var userRepository = new Mock<IUserRepository>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ProfileQueryService(repository.Object, userRepository.Object, memoryCache);

        var first = await sut.GetAsync(userId);
        var second = await sut.GetAsync(userId);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second.Should().BeEquivalentTo(first);
        repository.Verify(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_Should_Create_New_Profile_When_Not_Exists()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var userRepository = new Mock<IUserRepository>();
        var sut = new ProfileCommandService(repository.Object, userRepository.Object, memoryCache, NullLogger<ProfileCommandService>.Instance);

        var result = await sut.UpsertAsync(userId, NewRequest());

        result.UserId.Should().Be(userId);
        result.FullName.Should().Be("Henrique Santos");
        result.FinancialGoal.Should().Be("comecar-investir");
        result.CarryOverDay.Should().Be(10);
        result.IntelligenceMode.Should().Be("C");
        repository.Verify(x => x.AddAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAvatarAsync_Should_Throw_404_When_Profile_Not_Found()
    {
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var userRepository = new Mock<IUserRepository>();
        var sut = new ProfileCommandService(repository.Object, userRepository.Object, memoryCache, NullLogger<ProfileCommandService>.Instance);

        Func<Task> act = async () => await sut.UpdateAvatarAsync(Guid.NewGuid(), "http://cdn/avatar.png");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ex.Which.Title.Should().Be("Perfil não encontrado");
    }

    [Fact]
    public async Task UpsertAsync_Should_Map_ArgumentException_To_400()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewProfile(userId));
        repository
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("campo inválido"));
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var userRepository = new Mock<IUserRepository>();
        var sut = new ProfileCommandService(repository.Object, userRepository.Object, memoryCache, NullLogger<ProfileCommandService>.Instance);

        Func<Task> act = async () => await sut.UpsertAsync(userId, NewRequest());

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.Which.Title.Should().Be("Perfil inválido");
    }

    [Fact]
    public async Task UpdateAvatarAsync_Should_Throw_404_When_User_Not_Found()
    {
        var userId = Guid.NewGuid();
        var profile = NewProfile(userId);
        var repository = new Mock<IUserProfileRepository>();
        repository
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var sut = new ProfileCommandService(repository.Object, userRepository.Object, memoryCache, NullLogger<ProfileCommandService>.Instance);

        Func<Task> act = async () => await sut.UpdateAvatarAsync(userId, "http://cdn/avatar.png");

        var ex = await act.Should().ThrowAsync<AppProblemException>();
        ex.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ex.Which.Title.Should().Be("Perfil não encontrado");
    }

    [Fact]
    public async Task UpsertAsync_Should_Keep_Existing_Currency_When_Updating()
    {
        var userId = Guid.NewGuid();
        var profile = NewProfile(userId);
        var repository = new Mock<IUserProfileRepository>();
        repository.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var userRepository = new Mock<IUserRepository>();
        var sut = new ProfileCommandService(repository.Object, userRepository.Object, memoryCache, NullLogger<ProfileCommandService>.Instance);

        var result = await sut.UpsertAsync(userId, NewRequest());

        result.Currency.Should().Be("BRL");
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UpsertUserProfileRequest NewRequest() =>
        new(
            "Henrique Santos",
            "+55 11 999999999",
            new DateTime(1990, 1, 1),
            string.Empty,
            "Recife",
            "PE",
            "Brasil",
            "comecar-investir",
            "pt-BR",
            10,
            "C");

    private static UserProfile NewProfile(Guid userId) =>
        new(userId, "Henrique Santos", "+55 11 999999999", new DateTime(1990, 1, 1));
}
