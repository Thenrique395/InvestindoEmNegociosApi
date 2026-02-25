namespace InvestindoEmNegocio.Domain.Entities;

public class UserFeatureOverride
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string FeatureKey { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserFeatureOverride()
    {
        FeatureKey = string.Empty;
    }

    public UserFeatureOverride(Guid userId, string featureKey, bool isEnabled)
    {
        UserId = userId;
        FeatureKey = featureKey.Trim();
        IsEnabled = isEnabled;
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }
}
