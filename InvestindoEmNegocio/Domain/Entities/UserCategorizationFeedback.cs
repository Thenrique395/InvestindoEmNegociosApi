using InvestindoEmNegocio.Domain.Enums;

namespace InvestindoEmNegocio.Domain.Entities;

public sealed class UserCategorizationFeedback
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public MoneyType Type { get; private set; }
    public string NormalizedPattern { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public int Hits { get; private set; } = 1;
    public DateTime FirstLearnedAt { get; private set; } = DateTime.UtcNow;
    public DateTime LastLearnedAt { get; private set; } = DateTime.UtcNow;

    private UserCategorizationFeedback() { }

    public UserCategorizationFeedback(Guid userId, MoneyType type, string normalizedPattern, Guid categoryId)
    {
        UserId = userId;
        Type = type;
        NormalizedPattern = normalizedPattern.Trim();
        CategoryId = categoryId;
    }

    public void Reinforce(Guid categoryId)
    {
        CategoryId = categoryId;
        Hits++;
        LastLearnedAt = DateTime.UtcNow;
    }
}
