using InvestindoEmNegocio.Domain.Entities;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvestindoEmNegocio.Infrastructure.Data;

// Seed de dados de REFERÊNCIA globais, idempotente e em código (fonte única). Antes, categorias
// eram semeadas aqui e payment methods/card brands/institutions vinham por INSERT no schema.sql —
// com a adoção de migrations, tudo passa a ser semeado por aqui (o schema.sql foi retirado).
// Cada bloco confere o que já existe e só insere o que falta, então roda seguro a cada boot.
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

    private static readonly (int Id, string Name)[] DefaultPaymentMethods =
    [
        (1, "Pix"),
        (2, "Cartão de crédito"),
        (3, "Cartão de débito"),
        (4, "Dinheiro"),
        (5, "Boleto"),
        (6, "Transferência")
    ];

    private static readonly (int Id, string Name, string Code)[] DefaultCardBrands =
    [
        (1, "Visa", "visa"),
        (2, "Mastercard", "mastercard"),
        (3, "Elo", "elo"),
        (4, "American Express", "amex"),
        (5, "Hipercard", "hipercard")
    ];

    private static readonly (string Name, InstitutionType Type)[] DefaultInstitutions =
    [
        ("Nubank", InstitutionType.Bank),
        ("Itaú", InstitutionType.Bank),
        ("Banco do Brasil", InstitutionType.Bank),
        ("Bradesco", InstitutionType.Bank),
        ("Caixa Econômica Federal", InstitutionType.Bank),
        ("Santander", InstitutionType.Bank),
        ("Inter", InstitutionType.Bank),
        ("C6 Bank", InstitutionType.Bank),
        ("XP Investimentos", InstitutionType.Broker),
        ("Rico", InstitutionType.Broker),
        ("Clear", InstitutionType.Broker),
        ("BTG Pactual", InstitutionType.Broker)
    ];

    public static async Task SeedReferenceDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<InvestDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InvestDbContext>>();

        var changes = 0;
        changes += await SeedCategoriesAsync(dbContext, logger);
        changes += await SeedPaymentMethodsAsync(dbContext, logger);
        changes += await SeedCardBrandsAsync(dbContext, logger);
        changes += await SeedInstitutionsAsync(dbContext, logger);

        if (changes > 0)
            await dbContext.SaveChangesAsync();
        else
            logger.LogInformation("Reference data already seeded.");
    }

    private static async Task<int> SeedCategoriesAsync(InvestDbContext db, ILogger logger)
    {
        var existing = await db.Categories
            .Where(c => c.UserId == null)
            .Select(c => new { c.Name, c.AppliesTo })
            .ToListAsync();

        var existingKeys = existing
            .Select(c => CreateCategoryKey(c.Name, c.AppliesTo))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = DefaultCategories
            .Where(c => !existingKeys.Contains(CreateCategoryKey(c.Name, c.AppliesTo)))
            .Select(c => new Category(null, c.Name, c.AppliesTo))
            .ToList();

        if (missing.Count > 0)
        {
            await db.Categories.AddRangeAsync(missing);
            logger.LogInformation("Seeded {Count} default categories.", missing.Count);
        }

        return missing.Count;
    }

    private static async Task<int> SeedPaymentMethodsAsync(InvestDbContext db, ILogger logger)
    {
        var existingIds = (await db.PaymentMethods.Select(p => p.Id).ToListAsync()).ToHashSet();

        var missing = DefaultPaymentMethods
            .Where(p => !existingIds.Contains(p.Id))
            .Select(p => new PaymentMethod(p.Id, p.Name))
            .ToList();

        if (missing.Count > 0)
        {
            await db.PaymentMethods.AddRangeAsync(missing);
            logger.LogInformation("Seeded {Count} payment methods.", missing.Count);
        }

        return missing.Count;
    }

    private static async Task<int> SeedCardBrandsAsync(InvestDbContext db, ILogger logger)
    {
        var existingIds = (await db.CardBrands.Select(b => b.Id).ToListAsync()).ToHashSet();

        var missing = DefaultCardBrands
            .Where(b => !existingIds.Contains(b.Id))
            .Select(b => new CardBrand(b.Id, b.Name, b.Code))
            .ToList();

        if (missing.Count > 0)
        {
            await db.CardBrands.AddRangeAsync(missing);
            logger.LogInformation("Seeded {Count} card brands.", missing.Count);
        }

        return missing.Count;
    }

    private static async Task<int> SeedInstitutionsAsync(InvestDbContext db, ILogger logger)
    {
        var existing = await db.Institutions
            .Select(i => new { i.Name, i.Type })
            .ToListAsync();

        var existingKeys = existing
            .Select(i => CreateInstitutionKey(i.Name, i.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = DefaultInstitutions
            .Where(i => !existingKeys.Contains(CreateInstitutionKey(i.Name, i.Type)))
            .Select(i => new Institution(i.Name, i.Type))
            .ToList();

        if (missing.Count > 0)
        {
            await db.Institutions.AddRangeAsync(missing);
            logger.LogInformation("Seeded {Count} institutions.", missing.Count);
        }

        return missing.Count;
    }

    private static string CreateCategoryKey(string name, MoneyType? appliesTo)
    {
        return $"{name.Trim().ToUpperInvariant()}|{appliesTo?.ToString() ?? "All"}";
    }

    private static string CreateInstitutionKey(string name, InstitutionType type)
    {
        return $"{name.Trim().ToUpperInvariant()}|{type}";
    }
}
