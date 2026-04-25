using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Stripe;

namespace InvestindoEmNegocio.Application.Services;

public sealed class StripeBillingWebhookService(
    IBillingWebhookEventRepository billingWebhookEventRepository,
    IStripeBillingGateway stripeBillingGateway,
    IStripeBillingWebhookProcessor stripeBillingWebhookProcessor,
    ILogger<StripeBillingWebhookService> logger) : IStripeBillingWebhookService
{
    public async Task ProcessStripeWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = stripeBillingGateway.ConstructWebhookEvent(payload, signatureHeader ?? string.Empty);
        }
        catch (AppProblemException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stripe webhook signature validation failed.");
            throw new AppProblemException("Webhook inválido", "Assinatura do webhook inválida.", StatusCodes.Status400BadRequest);
        }

        var existing = await billingWebhookEventRepository.GetByProviderEventIdAsync("stripe", stripeEvent.Id, cancellationToken);
        if (existing is not null)
            return;

        var webhookLog = new BillingWebhookEvent(stripeEvent.Id, stripeEvent.Type, payload);
        await billingWebhookEventRepository.AddAsync(webhookLog, cancellationToken);
        await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await stripeBillingWebhookProcessor.ProcessAsync(stripeEvent, webhookLog, cancellationToken);
            webhookLog.MarkProcessed(true, null, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            webhookLog.MarkProcessed(false, ex.Message, DateTime.UtcNow);
            await billingWebhookEventRepository.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Failed to process Stripe webhook {EventType}", stripeEvent.Type);
            throw;
        }
    }
}
