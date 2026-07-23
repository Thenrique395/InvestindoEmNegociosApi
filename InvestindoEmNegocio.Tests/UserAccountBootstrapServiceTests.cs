using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class UserAccountBootstrapServiceTests
{
    private static UserAccountBootstrapService BuildSut(Mock<IAccountRepository> accountRepository) =>
        new(accountRepository.Object, NullLogger<UserAccountBootstrapService>.Instance);

    private static User BasicUser()
    {
        var user = new User("Basic", "basic@local", "hash");
        user.SetRole(UserRole.Basic);
        return user;
    }

    // Regressão do bug do login 500: o bootstrap deve consultar as contas pelo
    // spaceId EXPLÍCITO recebido, não pelo espaço ambiente. Se o usuário Basic já
    // tem conta no SEU espaço, não pode tentar inserir uma duplicada — mesmo que o
    // espaço ambiente (de uma sessão remanescente de outro usuário) apontasse para
    // outro lugar. Antes da correção o código usava ListByUserAsync (ambiente),
    // achava 0 contas e tentava um insert que violava o índice único → 500.
    [Fact]
    public async Task EnsureDefaultAccountForBasic_Should_Query_Explicit_Space_And_Not_Insert_When_Account_Exists()
    {
        var user = BasicUser();
        var spaceId = Guid.NewGuid();
        var existing = new Account(user.Id, spaceId, "Conta principal", AccountType.Checking, 0m);

        var accountRepository = new Mock<IAccountRepository>(MockBehavior.Strict);
        accountRepository
            .Setup(x => x.ListByUserAndSpaceAsync(user.Id, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);

        var sut = BuildSut(accountRepository);

        await sut.EnsureDefaultAccountForBasicAsync(user, spaceId, CancellationToken.None);

        // Não consulta o espaço ambiente e não insere duplicata.
        accountRepository.Verify(
            x => x.ListByUserAndSpaceAsync(user.Id, spaceId, It.IsAny<CancellationToken>()), Times.Once);
        accountRepository.Verify(
            x => x.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureDefaultAccountForBasic_Should_Create_When_No_Account_In_Space()
    {
        var user = BasicUser();
        var spaceId = Guid.NewGuid();

        var accountRepository = new Mock<IAccountRepository>();
        accountRepository
            .Setup(x => x.ListByUserAndSpaceAsync(user.Id, spaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = BuildSut(accountRepository);

        await sut.EnsureDefaultAccountForBasicAsync(user, spaceId, CancellationToken.None);

        accountRepository.Verify(
            x => x.AddAsync(
                It.Is<Account>(a => a.UserId == user.Id && a.SpaceId == spaceId && a.Name == "Conta principal" && a.IsActive),
                It.IsAny<CancellationToken>()),
            Times.Once);
        accountRepository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultAccountForBasic_Should_Do_Nothing_For_NonBasic()
    {
        var user = new User("Adv", "adv@local", "hash");
        user.SetRole(UserRole.Advanced);

        var accountRepository = new Mock<IAccountRepository>(MockBehavior.Strict);
        var sut = BuildSut(accountRepository);

        await sut.EnsureDefaultAccountForBasicAsync(user, Guid.NewGuid(), CancellationToken.None);

        // Retorna cedo: nenhuma chamada ao repositório.
        accountRepository.VerifyNoOtherCalls();
    }
}
