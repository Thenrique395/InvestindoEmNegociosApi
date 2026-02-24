using InvestindoEmNegocio.Application.DTOs;

namespace InvestindoEmNegocio.Application.Interfaces;

public interface IAdminParametersService
{
    Task<IReadOnlyList<PaymentMethodAdminResponse>> ListPaymentMethodsAsync(CancellationToken cancellationToken);
    Task<PaymentMethodAdminResponse> UpdatePaymentMethodStatusAsync(int id, bool isActive, CancellationToken cancellationToken);
    Task<PaymentMethodAdminResponse> CreatePaymentMethodAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<CardBrandAdminResponse>> ListCardBrandsAsync(CancellationToken cancellationToken);
    Task<CardBrandAdminResponse> UpdateCardBrandStatusAsync(int id, bool isActive, CancellationToken cancellationToken);
    Task<CardBrandAdminResponse> CreateCardBrandAsync(CreateCardBrandRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<InstitutionAdminResponse>> ListInstitutionsAsync(CancellationToken cancellationToken);
    Task<InstitutionAdminResponse> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken);
    Task<InstitutionAdminResponse> UpdateInstitutionStatusAsync(int id, bool isActive, CancellationToken cancellationToken);

    Task<NotificationSettingsDto> GetNotificationSettingsAsync(CancellationToken cancellationToken);
    Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken);

    Task<RobotSettingsDto> GetRobotSettingsAsync(CancellationToken cancellationToken);
    Task<RobotSettingsDto> UpdateRobotSettingsAsync(UpdateRobotSettingsRequest request, CancellationToken cancellationToken);
}
