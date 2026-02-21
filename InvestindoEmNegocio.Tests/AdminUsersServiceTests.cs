using FluentAssertions;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AdminUsersServiceTests
{
    [Fact]
    public async Task UpdateRoleAsync_Should_Throw_When_Role_Is_Invalid()
    {
        var sut = new AdminUsersService(Mock.Of<IUserRepository>());

        Func<Task> act = async () => await sut.UpdateRoleAsync(Guid.NewGuid(), "invalid", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Throw_When_User_Tries_To_Block_Himself()
    {
        var userId = Guid.NewGuid();
        var sut = new AdminUsersService(Mock.Of<IUserRepository>());

        Func<Task> act = async () => await sut.UpdateStatusAsync(userId, false, userId, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_User_When_Found()
    {
        var currentUserId = Guid.NewGuid();
        var targetUser = new User("Tester", "tester@local", "hash");
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(x => x.GetByIdAsync(targetUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var sut = new AdminUsersService(repository.Object);

        await sut.DeleteAsync(targetUser.Id, currentUserId, CancellationToken.None);

        repository.Verify(x => x.Remove(targetUser), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
