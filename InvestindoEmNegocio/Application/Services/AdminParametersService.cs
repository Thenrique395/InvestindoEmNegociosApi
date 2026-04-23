using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminParametersService(
    IAdminPaymentMethodsService adminPaymentMethodsService,
    IAdminCardBrandsService adminCardBrandsService,
    IAdminInstitutionsService adminInstitutionsService,
    IAdminNotificationSettingsService adminNotificationSettingsService,
    IAdminRobotSettingsService adminRobotSettingsService,
    IAdminEmailDiagnosticsService adminEmailDiagnosticsService) : IAdminParametersService
{
    public async Task<IReadOnlyList<PaymentMethodAdminResponse>> ListPaymentMethodsAsync(CancellationToken cancellationToken)
        => await adminPaymentMethodsService.ListAsync(cancellationToken);

    public async Task<PaymentMethodAdminResponse> UpdatePaymentMethodStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
        => await adminPaymentMethodsService.UpdateStatusAsync(id, isActive, cancellationToken);

    public async Task<PaymentMethodAdminResponse> CreatePaymentMethodAsync(string name, CancellationToken cancellationToken)
        => await adminPaymentMethodsService.CreateAsync(name, cancellationToken);

    public async Task<IReadOnlyList<CardBrandAdminResponse>> ListCardBrandsAsync(CancellationToken cancellationToken)
        => await adminCardBrandsService.ListAsync(cancellationToken);

    public async Task<CardBrandAdminResponse> UpdateCardBrandStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
        => await adminCardBrandsService.UpdateStatusAsync(id, isActive, cancellationToken);

    public async Task<CardBrandAdminResponse> CreateCardBrandAsync(CreateCardBrandRequest request, CancellationToken cancellationToken)
        => await adminCardBrandsService.CreateAsync(request, cancellationToken);

    public async Task<IReadOnlyList<InstitutionAdminResponse>> ListInstitutionsAsync(CancellationToken cancellationToken)
        => await adminInstitutionsService.ListAsync(cancellationToken);

    public async Task<InstitutionAdminResponse> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken)
        => await adminInstitutionsService.CreateAsync(request, cancellationToken);

    public async Task<InstitutionAdminResponse> UpdateInstitutionStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
        => await adminInstitutionsService.UpdateStatusAsync(id, isActive, cancellationToken);

    public async Task<NotificationSettingsDto> GetNotificationSettingsAsync(CancellationToken cancellationToken)
        => await adminNotificationSettingsService.GetAsync(cancellationToken);

    public async Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
        => await adminNotificationSettingsService.UpdateAsync(request, cancellationToken);

    public async Task<RobotSettingsDto> GetRobotSettingsAsync(CancellationToken cancellationToken)
        => await adminRobotSettingsService.GetAsync(cancellationToken);

    public async Task<RobotSettingsDto> UpdateRobotSettingsAsync(UpdateRobotSettingsRequest request, CancellationToken cancellationToken)
        => await adminRobotSettingsService.UpdateAsync(request, cancellationToken);

    public async Task<TestEmailResult> SendTestEmailAsync(string to, CancellationToken cancellationToken)
        => await adminEmailDiagnosticsService.SendTestEmailAsync(to, cancellationToken);
}
