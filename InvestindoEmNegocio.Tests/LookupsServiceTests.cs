using FluentAssertions;
using InvestindoEmNegocio.Application.Services;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class LookupsServiceTests
{
    [Fact]
    public async Task GetPaymentMethodsAsync_Should_Return_Active_Methods()
    {
        var paymentRepo = new Mock<IPaymentMethodRepository>();
        paymentRepo
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethod> { new(1, "Pix") });
        var sut = BuildSut(paymentMethodRepository: paymentRepo);

        var result = await sut.GetPaymentMethodsAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Pix");
    }

    [Fact]
    public async Task GetInstitutionsAsync_Should_Forward_Filter_To_Repository()
    {
        var institutionRepo = new Mock<IInstitutionRepository>();
        institutionRepo
            .Setup(x => x.ListActiveAsync(InstitutionType.Broker, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Institution> { new("XP", InstitutionType.Broker) });
        var sut = BuildSut(institutionRepository: institutionRepo);

        var result = await sut.GetInstitutionsAsync(InstitutionType.Broker);

        result.Should().HaveCount(1);
        institutionRepo.Verify(x => x.ListActiveAsync(InstitutionType.Broker, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static LookupsService BuildSut(
        Mock<IPaymentMethodRepository>? paymentMethodRepository = null,
        Mock<ICardBrandRepository>? cardBrandRepository = null,
        Mock<IInstitutionRepository>? institutionRepository = null)
    {
        return new LookupsService(
            paymentMethodRepository?.Object ?? Mock.Of<IPaymentMethodRepository>(),
            cardBrandRepository?.Object ?? Mock.Of<ICardBrandRepository>(),
            institutionRepository?.Object ?? Mock.Of<IInstitutionRepository>());
    }
}
