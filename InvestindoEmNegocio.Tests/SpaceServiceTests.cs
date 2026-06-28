using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

using BCryptNet = BCrypt.Net.BCrypt;

public class SpaceServiceTests
{
    [Fact]
    public async Task ListAsync_Should_Return_Mapped_Spaces()
    {
        var userId = Guid.NewGuid();
        var defaultSpace = new Space(userId, "Espaço Principal", isDefault: true);
        var otherSpace = new Space(userId, "Negócio", isDefault: false, passwordHash: BCryptNet.HashPassword("segredo123"));

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([defaultSpace, otherSpace]);

        var sut = BuildSut(spaceRepository: spaceRepository);

        var result = await sut.ListAsync(userId);

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "Espaço Principal" && s.IsDefault && !s.HasPassword);
        result.Should().Contain(s => s.Name == "Negócio" && !s.IsDefault && s.HasPassword);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_New_Space_Without_Password()
    {
        var userId = Guid.NewGuid();
        var spaceRepository = new Mock<ISpaceRepository>();
        var sut = BuildSut(spaceRepository: spaceRepository);

        var result = await sut.CreateAsync(userId, new SpaceRequest("Viagens", null));

        result.Name.Should().Be("Viagens");
        result.IsDefault.Should().BeFalse();
        result.HasPassword.Should().BeFalse();
        spaceRepository.Verify(x => x.AddAsync(It.Is<Space>(s => s.Name == "Viagens" && !s.HasPassword), It.IsAny<CancellationToken>()), Times.Once);
        spaceRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_New_Space_With_Hashed_Password()
    {
        var userId = Guid.NewGuid();
        var spaceRepository = new Mock<ISpaceRepository>();
        var sut = BuildSut(spaceRepository: spaceRepository);

        Space? added = null;
        spaceRepository.Setup(x => x.AddAsync(It.IsAny<Space>(), It.IsAny<CancellationToken>()))
            .Callback<Space, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        var result = await sut.CreateAsync(userId, new SpaceRequest("Negócio", "senhaForte123"));

        result.HasPassword.Should().BeTrue();
        added.Should().NotBeNull();
        added!.HasPassword.Should().BeTrue();
        BCryptNet.Verify("senhaForte123", added.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Space_Is_Default()
    {
        var userId = Guid.NewGuid();
        var defaultSpace = new Space(userId, "Espaço Principal", isDefault: true);

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(defaultSpace.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(defaultSpace);
        spaceRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([defaultSpace, new Space(userId, "Outro")]);

        var sut = BuildSut(spaceRepository: spaceRepository);

        Func<Task> act = async () => await sut.DeleteAsync(userId, defaultSpace.Id);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*padrão*");
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_It_Is_The_Last_Remaining_Space()
    {
        var userId = Guid.NewGuid();
        var onlySpace = new Space(userId, "Único espaço", isDefault: false);

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(onlySpace.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(onlySpace);
        spaceRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([onlySpace]);

        var sut = BuildSut(spaceRepository: spaceRepository);

        Func<Task> act = async () => await sut.DeleteAsync(userId, onlySpace.Id);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*último*");
    }

    [Fact]
    public async Task DeleteAsync_Should_Succeed_For_NonDefault_Space_When_Another_Space_Exists()
    {
        var userId = Guid.NewGuid();
        var defaultSpace = new Space(userId, "Espaço Principal", isDefault: true);
        var extraSpace = new Space(userId, "Negócio");

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(extraSpace.Id, userId, It.IsAny<CancellationToken>())).ReturnsAsync(extraSpace);
        spaceRepository.Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([defaultSpace, extraSpace]);

        var sut = BuildSut(spaceRepository: spaceRepository);

        var result = await sut.DeleteAsync(userId, extraSpace.Id);

        result.Should().BeTrue();
        extraSpace.DeletedAt.Should().NotBeNull();
        spaceRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnterAsync_Should_Issue_Session_When_Space_Has_No_Password()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var space = new Space(user.Id, "Espaço Principal", isDefault: true);

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(space.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sessionService = new Mock<IUserSessionService>();
        sessionService.Setup(x => x.IssueAsync(user, space.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponse(user.Id, user.Name, user.Email, user.Role.ToString(), "jwt-token", "refresh-token", DateTime.UtcNow.AddHours(1)));

        var sut = BuildSut(spaceRepository: spaceRepository, userRepository: userRepository, sessionService: sessionService);

        var result = await sut.EnterAsync(user.Id, space.Id, new EnterSpaceRequest(null));

        result.Token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task EnterAsync_Should_Issue_Session_When_Password_Is_Correct()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var space = new Space(user.Id, "Negócio", isDefault: false, passwordHash: BCryptNet.HashPassword("senhaForte123"));

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(space.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sessionService = new Mock<IUserSessionService>();
        sessionService.Setup(x => x.IssueAsync(user, space.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResponse(user.Id, user.Name, user.Email, user.Role.ToString(), "jwt-token", "refresh-token", DateTime.UtcNow.AddHours(1)));

        var sut = BuildSut(spaceRepository: spaceRepository, userRepository: userRepository, sessionService: sessionService);

        var result = await sut.EnterAsync(user.Id, space.Id, new EnterSpaceRequest("senhaForte123"));

        result.Token.Should().Be("jwt-token");
    }

    [Fact]
    public async Task EnterAsync_Should_Throw_When_Password_Is_Wrong()
    {
        var user = new User("Teste", "teste@teste.com", "hash");
        var space = new Space(user.Id, "Negócio", isDefault: false, passwordHash: BCryptNet.HashPassword("senhaForte123"));

        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(space.Id, user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        var sut = BuildSut(spaceRepository: spaceRepository);

        Func<Task> act = async () => await sut.EnterAsync(user.Id, space.Id, new EnterSpaceRequest("senha-errada"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Senha do espaço inválida*");
    }

    [Fact]
    public async Task EnterAsync_Should_Throw_When_Space_Not_Found()
    {
        var userId = Guid.NewGuid();
        var spaceRepository = new Mock<ISpaceRepository>();
        spaceRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>())).ReturnsAsync((Space?)null);

        var sut = BuildSut(spaceRepository: spaceRepository);

        Func<Task> act = async () => await sut.EnterAsync(userId, Guid.NewGuid(), new EnterSpaceRequest(null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    private static SpaceService BuildSut(
        Mock<ISpaceRepository>? spaceRepository = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<IUserSessionService>? sessionService = null)
    {
        return new SpaceService(
            spaceRepository?.Object ?? Mock.Of<ISpaceRepository>(),
            userRepository?.Object ?? Mock.Of<IUserRepository>(),
            sessionService?.Object ?? Mock.Of<IUserSessionService>(),
            NullLogger<SpaceService>.Instance);
    }
}
