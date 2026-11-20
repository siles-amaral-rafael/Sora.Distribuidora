namespace Sora.Domain.Entities;

public sealed class StaffAccount
{
    private StaffAccount() { }

    public StaffAccount(string fullName, string email, string passwordHash, string roleName, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        RoleName = roleName;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
