using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Application.Services;

public sealed class AdminParametersService(
    IPaymentMethodRepository paymentMethodRepository,
    ICardBrandRepository cardBrandRepository,
    IInstitutionRepository institutionRepository,
    INotificationSettingsRepository notificationSettingsRepository,
    IRobotSettingsRepository robotSettingsRepository) : IAdminParametersService
{
    public async Task<IReadOnlyList<PaymentMethodAdminResponse>> ListPaymentMethodsAsync(CancellationToken cancellationToken)
    {
        var items = await paymentMethodRepository.ListAllAsync(cancellationToken);
        return items.Select(p => new PaymentMethodAdminResponse(p.Id, p.Name, p.IsActive)).ToList();
    }

    public async Task<PaymentMethodAdminResponse> UpdatePaymentMethodStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var method = await paymentMethodRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Não encontrado", "Forma de pagamento não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) method.Activate();
        else method.Deactivate();

        await paymentMethodRepository.SaveChangesAsync(cancellationToken);
        return new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive);
    }

    public async Task<PaymentMethodAdminResponse> CreatePaymentMethodAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AppProblemException("Nome inválido", "Informe o nome da forma de pagamento.", StatusCodes.Status400BadRequest);
        }

        var existing = await paymentMethodRepository.ListAllAsync(cancellationToken);
        if (existing.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppProblemException("Forma já existe", "Já existe uma forma de pagamento com esse nome.", StatusCodes.Status409Conflict);
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(p => p.Id) + 1;
        var method = new PaymentMethod(nextId, normalized, true);

        try
        {
            await paymentMethodRepository.AddAsync(method, cancellationToken);
            await paymentMethodRepository.SaveChangesAsync(cancellationToken);
            return new PaymentMethodAdminResponse(method.Id, method.Name, method.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a forma de pagamento. Verifique se já existe um registro parecido.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<IReadOnlyList<CardBrandAdminResponse>> ListCardBrandsAsync(CancellationToken cancellationToken)
    {
        var items = await cardBrandRepository.ListAllAsync(cancellationToken);
        return items.Select(b => new CardBrandAdminResponse(b.Id, b.Name, b.Code, b.IsActive)).ToList();
    }

    public async Task<CardBrandAdminResponse> UpdateCardBrandStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var brand = await cardBrandRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Não encontrado", "Bandeira não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) brand.Activate();
        else brand.Deactivate();

        await cardBrandRepository.SaveChangesAsync(cancellationToken);
        return new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive);
    }

    public async Task<CardBrandAdminResponse> CreateCardBrandAsync(CreateCardBrandRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        {
            throw new AppProblemException("Dados inválidos", "Informe nome e código da bandeira.", StatusCodes.Status400BadRequest);
        }

        var existing = await cardBrandRepository.ListAllAsync(cancellationToken);
        if (existing.Any(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AppProblemException("Código já existe", "Já existe uma bandeira com esse código.", StatusCodes.Status409Conflict);
        }

        var nextId = existing.Count == 0 ? 1 : existing.Max(b => b.Id) + 1;
        var brand = new CardBrand(nextId, name, code, true);

        try
        {
            await cardBrandRepository.AddAsync(brand, cancellationToken);
            await cardBrandRepository.SaveChangesAsync(cancellationToken);
            return new CardBrandAdminResponse(brand.Id, brand.Name, brand.Code, brand.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a bandeira. Verifique se o código já está em uso.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<IReadOnlyList<InstitutionAdminResponse>> ListInstitutionsAsync(CancellationToken cancellationToken)
    {
        var items = await institutionRepository.ListAllAsync(cancellationToken);
        return items.Select(i => new InstitutionAdminResponse(i.Id, i.Name, i.Type.ToString(), i.IsActive)).ToList();
    }

    public async Task<InstitutionAdminResponse> CreateInstitutionAsync(CreateInstitutionRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(request.Type))
        {
            throw new AppProblemException("Dados inválidos", "Informe nome e tipo da instituição.", StatusCodes.Status400BadRequest);
        }

        if (!Enum.TryParse<InstitutionType>(request.Type, true, out var type))
        {
            throw new AppProblemException("Tipo inválido", "Tipo informado não é válido.", StatusCodes.Status400BadRequest);
        }

        if (await institutionRepository.ExistsAsync(name, type, cancellationToken))
        {
            throw new AppProblemException("Instituição já existe", "Já existe uma instituição com esse nome e tipo.", StatusCodes.Status409Conflict);
        }

        var institution = new Institution(name, type, true);
        try
        {
            await institutionRepository.AddAsync(institution, cancellationToken);
            await institutionRepository.SaveChangesAsync(cancellationToken);
            return new InstitutionAdminResponse(institution.Id, institution.Name, institution.Type.ToString(), institution.IsActive);
        }
        catch (DbUpdateException)
        {
            throw new AppProblemException(
                "Falha ao salvar",
                "Não foi possível salvar a instituição. Verifique se já existe um registro parecido.",
                StatusCodes.Status409Conflict);
        }
    }

    public async Task<InstitutionAdminResponse> UpdateInstitutionStatusAsync(int id, bool isActive, CancellationToken cancellationToken)
    {
        var institution = await institutionRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppProblemException("Não encontrado", "Instituição não encontrada.", StatusCodes.Status404NotFound);

        if (isActive) institution.Activate();
        else institution.Deactivate();

        await institutionRepository.SaveChangesAsync(cancellationToken);
        return new InstitutionAdminResponse(institution.Id, institution.Name, institution.Type.ToString(), institution.IsActive);
    }

    public async Task<NotificationSettingsDto> GetNotificationSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await notificationSettingsRepository.GetOrCreateAsync(cancellationToken);
        return ToNotificationSettingsDto(settings);
    }

    public async Task<NotificationSettingsDto> UpdateNotificationSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = await notificationSettingsRepository.GetOrCreateAsync(cancellationToken);
        settings.Update(
            request.IncomeUpcomingEnabled,
            request.IncomeDaysBefore,
            request.ExpenseUpcomingEnabled,
            request.ExpenseDaysBefore,
            request.ExpenseOverdueEnabled,
            request.CardCloseSoonEnabled,
            request.CardCloseDaysBefore,
            request.CardCloseDayEnabled,
            request.MonthCloseEnabled,
            request.MonthSummaryEnabled,
            request.GoalBelowExpectedEnabled,
            request.GoalCompletedEnabled,
            request.GoalInactivityEnabled,
            request.GoalInactivityDays);
        await notificationSettingsRepository.SaveChangesAsync(cancellationToken);
        return ToNotificationSettingsDto(settings);
    }

    public async Task<RobotSettingsDto> GetRobotSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await robotSettingsRepository.GetOrCreateAsync(cancellationToken);
        return ToRobotSettingsDto(settings);
    }

    public async Task<RobotSettingsDto> UpdateRobotSettingsAsync(UpdateRobotSettingsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DailyRunTimeUtc) || !TimeOnly.TryParse(request.DailyRunTimeUtc, out _))
        {
            throw new AppProblemException(
                "Horário inválido",
                "Informe o horário no formato HH:mm (UTC).",
                StatusCodes.Status400BadRequest);
        }

        var settings = await robotSettingsRepository.GetOrCreateAsync(cancellationToken);
        settings.Update(request.Enabled, request.DailyRunTimeUtc);
        await robotSettingsRepository.SaveChangesAsync(cancellationToken);
        return ToRobotSettingsDto(settings);
    }

    private static NotificationSettingsDto ToNotificationSettingsDto(NotificationSettings settings) =>
        new(
            settings.IncomeUpcomingEnabled,
            settings.IncomeDaysBefore,
            settings.ExpenseUpcomingEnabled,
            settings.ExpenseDaysBefore,
            settings.ExpenseOverdueEnabled,
            settings.CardCloseSoonEnabled,
            settings.CardCloseDaysBefore,
            settings.CardCloseDayEnabled,
            settings.MonthCloseEnabled,
            settings.MonthSummaryEnabled,
            settings.GoalBelowExpectedEnabled,
            settings.GoalCompletedEnabled,
            settings.GoalInactivityEnabled,
            settings.GoalInactivityDays);

    private static RobotSettingsDto ToRobotSettingsDto(RobotSettings settings) =>
        new(
            settings.Enabled,
            settings.DailyRunTimeUtc);
}
