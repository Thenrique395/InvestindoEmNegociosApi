using System.Text.RegularExpressions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using InvestindoEmNegocio.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvestindoEmNegocio.Application.Services;

public sealed class CategorizationService(
    ICategoryRepository categoryRepository,
    IMoneyPlanRepository planRepository,
    IInvestDbContext dbContext,
    ILogger<CategorizationService> logger) : ICategorizationService
{
    private static readonly MerchantRule[] Rules =
    [
        new(["uber", "99", "combust", "ipiranga", "shell", "posto"], ["transporte", "mobilidade", "combust"], "merchant_transport", 95),
        new(["ifood", "restaurante", "lanch", "padaria", "mercado", "supermerc", "horti"], ["aliment", "mercado", "supermerc", "restaurante"], "merchant_food", 95),
        new(["netflix", "spotify", "disney", "prime video", "youtube", "hbo", "apple.com/bill"], ["assin", "stream", "lazer"], "merchant_subscription", 94),
        new(["farmacia", "drogasil", "droga raia", "pague menos"], ["saude", "farm"], "merchant_health", 94),
        new(["aluguel", "condominio", "energia", "enel", "neoenergia", "agua", "sane", "internet", "vivo", "tim", "claro"], ["moradia", "casa", "utilidade", "conta"], "merchant_housing", 92)
    ];

    public async Task<CategorizationSuggestionDto?> SuggestAsync(
        Guid userId,
        MoneyType type,
        string description,
        CancellationToken cancellationToken = default)
    {
        var normalizedDescription = Normalize(description);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
            return null;

        var categories = await categoryRepository.ListForUserAsync(userId, type, cancellationToken);
        if (categories.Count == 0)
            return null;

        var learnedSuggestion = await SuggestFromLearnedFeedbackAsync(userId, type, normalizedDescription, categories, cancellationToken);
        if (learnedSuggestion is not null)
            return learnedSuggestion;

        var historySuggestion = await SuggestFromHistoryAsync(userId, type, normalizedDescription, categories, cancellationToken);
        if (historySuggestion is not null)
            return historySuggestion;

        var merchantSuggestion = SuggestFromMerchantRules(normalizedDescription, categories);
        if (merchantSuggestion is not null)
            return merchantSuggestion;

        var categoryNameSuggestion = SuggestByCategoryName(normalizedDescription, categories);
        return categoryNameSuggestion;
    }

    public async Task LearnAsync(
        Guid userId,
        MoneyType type,
        string description,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var normalizedPattern = NormalizeForLearning(description);
        if (string.IsNullOrWhiteSpace(normalizedPattern))
            return;

        var category = await categoryRepository.GetByIdForUserAsync(categoryId, userId, cancellationToken)
            ?? (await categoryRepository.ListDefaultsAsync(true, cancellationToken))
                .FirstOrDefault(x => x.Id == categoryId);
        if (category is null || category.AppliesTo != type || !category.IsActive)
            return;

        var existing = await dbContext.UserCategorizationFeedback
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Type == type && x.NormalizedPattern == normalizedPattern,
                cancellationToken);

        if (existing is null)
        {
            await dbContext.UserCategorizationFeedback.AddAsync(
                new UserCategorizationFeedback(userId, type, normalizedPattern, categoryId),
                cancellationToken);
        }
        else
        {
            existing.Reinforce(categoryId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Categorization feedback learned for {UserId}. Type={Type} Pattern={Pattern} CategoryId={CategoryId}",
            userId,
            type,
            normalizedPattern,
            categoryId);
    }

    private async Task<CategorizationSuggestionDto?> SuggestFromLearnedFeedbackAsync(
        Guid userId,
        MoneyType type,
        string normalizedDescription,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var learningPattern = NormalizeForLearning(normalizedDescription);
        if (string.IsNullOrWhiteSpace(learningPattern))
            return null;

        var feedback = await dbContext.UserCategorizationFeedback
            .Where(x => x.UserId == userId && x.Type == type)
            .OrderByDescending(x => x.Hits)
            .ThenByDescending(x => x.LastLearnedAt)
            .ToListAsync(cancellationToken);

        var learned = feedback.FirstOrDefault(x =>
            x.NormalizedPattern == learningPattern
            || learningPattern.Contains(x.NormalizedPattern)
            || x.NormalizedPattern.Contains(learningPattern));

        if (learned is null)
            return null;

        var category = categories.FirstOrDefault(x => x.Id == learned.CategoryId);
        return category is null
            ? null
            : BuildSuggestion(category, 99, "learned_feedback");
    }

    private async Task<CategorizationSuggestionDto?> SuggestFromHistoryAsync(
        Guid userId,
        MoneyType type,
        string normalizedDescription,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var plans = await planRepository.ListByUserAsync(userId, type, cancellationToken);
        var matchedCategoryId = plans
            .Where(plan => plan.CategoryId.HasValue)
            .Select(plan => new { plan.CategoryId, Title = Normalize(plan.Title) })
            .Where(x => x.Title == normalizedDescription || x.Title.Contains(normalizedDescription) || normalizedDescription.Contains(x.Title))
            .GroupBy(x => x.CategoryId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        if (!matchedCategoryId.HasValue)
            return null;

        var category = categories.FirstOrDefault(c => c.Id == matchedCategoryId.Value);
        return category is null
            ? null
            : BuildSuggestion(category, 98, "history_match");
    }

    private static CategorizationSuggestionDto? SuggestFromMerchantRules(string normalizedDescription, IReadOnlyList<Category> categories)
    {
        foreach (var rule in Rules)
        {
            if (!rule.DescriptionKeywords.Any(keyword => normalizedDescription.Contains(keyword)))
                continue;

            var category = categories.FirstOrDefault(cat =>
            {
                var normalizedCategory = Normalize(cat.Name);
                return rule.CategoryKeywords.Any(keyword => normalizedCategory.Contains(keyword));
            });

            if (category is not null)
                return BuildSuggestion(category, rule.Score, rule.ReasonCode);
        }

        return null;
    }

    private static CategorizationSuggestionDto? SuggestByCategoryName(string normalizedDescription, IReadOnlyList<Category> categories)
    {
        var category = categories.FirstOrDefault(cat =>
        {
            var normalizedCategory = Normalize(cat.Name);
            return normalizedDescription.Contains(normalizedCategory) || normalizedCategory.Contains(normalizedDescription);
        });

        return category is null
            ? null
            : BuildSuggestion(category, 80, "category_name_match");
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9\\s]", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized.Trim();
    }

    private static string NormalizeForLearning(string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        normalized = Regex.Replace(normalized, "\\b\\d+\\b", " ");
        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized.Trim();
    }

    private static CategorizationSuggestionDto BuildSuggestion(Category category, int score, string reasonCode)
    {
        var bounded = Math.Clamp(score, 0, 100);
        var confidence = bounded / 100m;
        var band = bounded >= 95
            ? "high"
            : bounded >= 85
                ? "medium"
                : "low";

        return new CategorizationSuggestionDto(category.Id, category.Name, confidence, bounded, band, reasonCode);
    }

    private sealed record MerchantRule(
        IReadOnlyList<string> DescriptionKeywords,
        IReadOnlyList<string> CategoryKeywords,
        string ReasonCode,
        int Score);
}
