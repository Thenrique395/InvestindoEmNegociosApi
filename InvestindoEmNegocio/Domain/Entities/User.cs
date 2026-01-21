namespace InvestindoEmNegocio.Domain.Entities;

public class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockoutUntil { get; private set; }

    // Required by EF Core
    private User()
    {
        Name = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(string name, string email, string passwordHash)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
    }

    public void UpdateLastLogin(DateTime lastLoginAtUtc)
    {
        LastLoginAt = lastLoginAtUtc;
        UpdatedAt = lastLoginAtUtc;
    }

    public bool IsLocked(DateTime nowUtc)
    {
        return LockoutUntil.HasValue && LockoutUntil.Value > nowUtc;
    }

    public void RegisterFailedLogin(DateTime nowUtc, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginAttempts += 1;
        if (FailedLoginAttempts >= maxAttempts)
        {
            LockoutUntil = nowUtc.Add(lockoutDuration);
            FailedLoginAttempts = 0;
        }
        UpdatedAt = nowUtc;
    }

    public void ResetFailedLogins(DateTime nowUtc)
    {
        FailedLoginAttempts = 0;
        LockoutUntil = null;
        UpdatedAt = nowUtc;
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
