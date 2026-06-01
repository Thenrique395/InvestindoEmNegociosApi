using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Data;

public static class ReferenceDataSeedExtensions
{
    private static readonly (string Name, MoneyType AppliesTo)[] DefaultCategories =
    [
        ("Salário", MoneyType.Income),
        ("Freela", MoneyType.Income),
        ("Vendas", MoneyType.Income),
        ("Rendimentos", MoneyType.Income),
        ("Reembolso", MoneyType.Income),
        ("Outras receitas", MoneyType.Income),
        ("Moradia", MoneyType.Expense),
        ("Alimentação", MoneyType.Expense),
        ("Transporte", MoneyType.Expense),
        ("Saúde", MoneyType.Expense),
        ("Educação", MoneyType.Expense),
        ("Lazer", MoneyType.Expense),
        ("Compras", MoneyType.Expense),
        ("Assinaturas", MoneyType.Expense),
        ("Impostos", MoneyType.Expense),
        ("Dívidas", MoneyType.Expense),
        ("Outras despesas", MoneyType.Expense)
    ];

    public static async Task SeedReferenceDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvestDbContext>>();

        var defaultCategories = await dbContext.Categories
            .Where(c => c.UserId == null)
            .Select(c => new { c.Name, c.AppliesTo })
            .ToListAsync();

        var existingKeys = defaultCategories
            .Select(c => CreateCategoryKey(c.Name, c.AppliesTo))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCategories = DefaultCategories
            .Where(c => !existingKeys.Contains(CreateCategoryKey(c.Name, c.AppliesTo)))
            .Select(c => new Category(null, c.Name, c.AppliesTo))
            .ToList();

        if (missingCategories.Count == 0)
        {
            logger.LogInformation("Reference categories already seeded.");
            return;
        }

        await dbContext.Categories.AddRangeAsync(missingCategories);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} default categories.", missingCategories.Count);
    }

    private static string CreateCategoryKey(string name, MoneyType? appliesTo)
    {
        return $"{name.Trim().ToUpperInvariant()}|{appliesTo?.ToString() ?? "All"}";
    }
}
