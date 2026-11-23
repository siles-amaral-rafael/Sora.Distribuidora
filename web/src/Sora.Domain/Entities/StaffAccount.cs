namespace Sora.Domain.Entities;

public sealed class StaffAccount
{
    private StaffAccount() { }

    public StaffAccount(string fullName, string email, string passwordHash, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        FullName = fullName;
        Email = email;
        PasswordHash = passwordHash;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public void AssignRole(string roleCode, DateTimeOffset assignedAtUtc, Guid? assignedById = null)
    {
        if (RoleAssignments.Any(x => x.RoleCode == roleCode))
        {
            return;
        }

        RoleAssignments.Add(new StaffAccountRole(Id, roleCode, assignedAtUtc, assignedById));
    }

    public void ReplaceRoles(IEnumerable<string> roleCodes, DateTimeOffset assignedAtUtc, Guid assignedById)
    {
        RoleAssignments.Clear();
        foreach (var roleCode in roleCodes.Distinct(StringComparer.OrdinalIgnoreCase))
            RoleAssignments.Add(new StaffAccountRole(Id, roleCode, assignedAtUtc, assignedById));
    }

    public void SetActive(bool active) => IsActive = active;

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ICollection<StaffAccountRole> RoleAssignments { get; } = new List<StaffAccountRole>();
}
