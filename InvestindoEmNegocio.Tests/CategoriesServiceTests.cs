using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class CategoriesServiceTests
{
    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Is_Invalid()
    {
        var sut = new CategoriesService(Mock.Of<ICategoryRepository>(), NullLogger<CategoriesService>.Instance);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), new UpsertCategoryRequest(" ", MoneyType.Expense));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*obrigatório*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Already_Exists()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.NameExistsAsync(It.IsAny<Guid>(), "Alimentação", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new CategoriesService(repository.Object, NullLogger<CategoriesService>.Instance);

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), new UpsertCategoryRequest("Alimentação", MoneyType.Expense));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já existe*");
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_When_Valid()
    {
        var userId = Guid.NewGuid();
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.NameExistsAsync(userId, "Alimentação", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = new CategoriesService(repository.Object, NullLogger<CategoriesService>.Instance);

        var result = await sut.CreateAsync(userId, new UpsertCategoryRequest("Alimentação", MoneyType.Expense));

        result.Name.Should().Be("Alimentação");
        result.IsDefault.Should().BeFalse();
        repository.Verify(x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Not_Found()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = new CategoriesService(repository.Object, NullLogger<CategoriesService>.Instance);

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpsertCategoryRequest("Casa", MoneyType.Expense));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_False_When_Not_Found()
    {
        var repository = new Mock<ICategoryRepository>();
        repository
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = new CategoriesService(repository.Object, NullLogger<CategoriesService>.Instance);

        var removed = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        removed.Should().BeFalse();
        repository.Verify(x => x.Remove(It.IsAny<Category>()), Times.Never);
    }
}
