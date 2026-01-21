namespace InvestindoEmNegocio.Domain.Entities;

public class UserOnboarding
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public int Step { get; private set; }
    public bool Completed { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserOnboarding() { }

    public UserOnboarding(Guid userId, int step = 0, bool completed = false)
    {
        UserId = userId;
        Update(step, completed);
    }

    public void Update(int step, bool completed)
    {
        Step = step;
        Completed = completed;
        UpdatedAt = DateTime.UtcNow;
    }
}
