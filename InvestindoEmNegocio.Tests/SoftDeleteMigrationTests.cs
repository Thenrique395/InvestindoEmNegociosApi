using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Infrastructure.Data;
using InvestindoEmNegocio.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class SoftDeleteMigrationTests
{
    [Fact]
    public async Task Cards_Partial_Unique_Index_Should_Allow_Recreating_Same_Card_After_SoftDelete()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        await dbContext.CardBrands.AddAsync(new CardBrand(1, "Visa", "visa"));
        await dbContext.SaveChangesAsync();

        var card = new Card(userId, 1, "Titular", "Apelido", "1234", "Banco", 1000m, 10, 20);
        await dbContext.Cards.AddAsync(card);
        await dbContext.SaveChangesAsync();

        card.MarkDeleted(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var recreated = new Card(userId, 1, "Titular", "Apelido", "1234", "Banco", 1000m, 10, 20);
        await dbContext.Cards.AddAsync(recreated);

        await dbContext.Invoking(x => x.SaveChangesAsync()).Should().NotThrowAsync();

        var activeCards = await dbContext.Cards.Where(x => x.UserId == userId).ToListAsync();
        activeCards.Should().ContainSingle(x => x.Id == recreated.Id);

        var allCards = await dbContext.Cards.IgnoreQueryFilters().Where(x => x.UserId == userId).ToListAsync();
        allCards.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteOwnAccountAsync_Should_Physically_Purge_Card_Already_SoftDeleted_Before_Account_Deletion()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Teste", "lgpd@teste.com", BCrypt.Net.BCrypt.HashPassword("Senha123!"));
        await dbContext.Users.AddAsync(user);
        await dbContext.CardBrands.AddAsync(new CardBrand(1, "Visa", "visa"));
        await dbContext.SaveChangesAsync();

        var card = new Card(user.Id, 1, "Titular", "Apelido", "1234", "Banco", 1000m, 10, 20);
        await dbContext.Cards.AddAsync(card);
        await dbContext.SaveChangesAsync();

        // Usuário já tinha excluído (soft-delete) esse cartão antes de pedir a exclusão da conta.
        card.MarkDeleted(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var sut = new UserPrivacyCenterService(
            new UserRepository(dbContext),
            new RefreshTokenRepository(dbContext),
            Mock.Of<IAuditService>(),
            dbContext);

        await sut.DeleteOwnAccountAsync(user.Id, new DeleteOwnAccountRequest("Senha123!", "EXCLUIR"));

        var remainingCards = await dbContext.Cards.IgnoreQueryFilters().Where(x => x.UserId == user.Id).ToListAsync();
        remainingCards.Should().BeEmpty("a exclusão de conta precisa apagar de verdade até linhas já soft-deletadas antes");
    }

    private static InvestDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<InvestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new InvestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
