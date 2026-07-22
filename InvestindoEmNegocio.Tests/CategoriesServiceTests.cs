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
    private readonly Mock<ICategoryRepository> repository = new();
    private readonly Mock<IMoneyPlanRepository> moneyPlans = new();

    private CategoriesService CreateSut() =>
        new(repository.Object, moneyPlans.Object, NullLogger<CategoriesService>.Instance);

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Is_Invalid()
    {
        var sut = CreateSut();

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), new UpsertCategoryRequest(" ", MoneyType.Expense));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*obrigatório*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Name_Already_Exists()
    {
        repository
            .Setup(x => x.NameExistsAsync(It.IsAny<Guid>(), "Alimentação", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();

        Func<Task> act = async () => await sut.CreateAsync(Guid.NewGuid(), new UpsertCategoryRequest("Alimentação", MoneyType.Expense));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*já existe*");
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_When_Valid()
    {
        var userId = Guid.NewGuid();
        repository
            .Setup(x => x.NameExistsAsync(userId, "Alimentação", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        var result = await sut.CreateAsync(userId, new UpsertCategoryRequest("Alimentação", MoneyType.Expense));

        result.Name.Should().Be("Alimentação");
        result.IsDefault.Should().BeFalse();
        result.IsActive.Should().BeTrue();
        repository.Verify(x => x.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Null_When_Not_Found()
    {
        repository
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = CreateSut();

        var result = await sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpsertCategoryRequest("Casa", MoneyType.Expense));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_NotFound_When_Missing()
    {
        repository
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        var sut = CreateSut();

        var outcome = await sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        outcome.Should().Be(CategoryDeletionOutcome.NotFound);
        repository.Verify(x => x.Remove(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_Should_Hard_Delete_When_Not_In_Use()
    {
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Lazer", MoneyType.Expense);
        repository
            .Setup(x => x.GetByIdForUserAsync(category.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        moneyPlans
            .Setup(x => x.ExistsByCategoryAsync(category.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var sut = CreateSut();

        var outcome = await sut.DeleteAsync(userId, category.Id);

        outcome.Should().Be(CategoryDeletionOutcome.Deleted);
        repository.Verify(x => x.Remove(category), Times.Once);
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_Should_Deactivate_When_In_Use()
    {
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Alimentação", MoneyType.Expense);
        repository
            .Setup(x => x.GetByIdForUserAsync(category.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        moneyPlans
            .Setup(x => x.ExistsByCategoryAsync(category.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = CreateSut();

        var outcome = await sut.DeleteAsync(userId, category.Id);

        outcome.Should().Be(CategoryDeletionOutcome.Deactivated);
        category.IsActive.Should().BeFalse();
        repository.Verify(x => x.Remove(It.IsAny<Category>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetStatusAsync_Should_Reactivate_User_Category()
    {
        var userId = Guid.NewGuid();
        var category = new Category(userId, "Alimentação", MoneyType.Expense);
        category.Deactivate();
        repository
            .Setup(x => x.GetByIdForUserAsync(category.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        var sut = CreateSut();

        var result = await sut.SetStatusAsync(userId, category.Id, true);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
        category.IsActive.Should().BeTrue();
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
