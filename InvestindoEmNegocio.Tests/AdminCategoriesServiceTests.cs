using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class AdminCategoriesServiceTests
{
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Is_Empty()
    {
        var sut = new AdminCategoriesService(Mock.Of<ICategoryRepository>());

        Func<Task> act = async () => await sut.CreateAsync(new AdminCategoryRequest("   ", null), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_AppliesTo_Is_Invalid()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.DefaultNameExistsAsync("Moradia", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new AdminCategoriesService(repository.Object);

        Func<Task> act = async () => await sut.CreateAsync(new AdminCategoryRequest("Moradia", "invalid"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Deactivate_Category()
    {
        var category = new Category(null, "Transporte", MoneyType.Expense);
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetDefaultByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);

        var sut = new AdminCategoriesService(repository.Object);

        var result = await sut.UpdateStatusAsync(category.Id, false, CancellationToken.None);

        result.IsActive.Should().BeFalse();
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Default_Name_Already_Exists()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.DefaultNameExistsAsync("Moradia", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new AdminCategoriesService(repository.Object);

        Func<Task> act = async () => await sut.CreateAsync(new AdminCategoryRequest("Moradia", "Expense"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Category_Not_Found()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetDefaultByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = new AdminCategoriesService(repository.Object);

        Func<Task> act = async () => await sut.UpdateAsync(Guid.NewGuid(), new AdminCategoryRequest("Moradia", "Expense"), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_Throw_When_Category_Not_Found()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetDefaultByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = new AdminCategoriesService(repository.Object);

        Func<Task> act = async () => await sut.UpdateStatusAsync(Guid.NewGuid(), true, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<AppProblemException>();
        exception.Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ListAsync_Should_Map_Default_Categories()
    {
        var category = new Category(null, "Moradia", MoneyType.Expense);
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.ListDefaultsAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([category]);
        var sut = new AdminCategoriesService(repository.Object);

        var result = await sut.ListAsync(includeInactive: true, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Moradia");
        result[0].AppliesTo.Should().Be("Expense");
    }
}
