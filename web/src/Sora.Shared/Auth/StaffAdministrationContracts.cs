using System.ComponentModel.DataAnnotations;

namespace Sora.Shared.Auth;

public sealed class CreateStaffAccountRequest
{
    [Required, StringLength(120, MinimumLength = 2)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(72, MinimumLength = 12)] public string TemporaryPassword { get; set; } = string.Empty;
    [Required, MinLength(1)] public List<string> RoleCodes { get; set; } = [];
}

public sealed record StaffPermissionResponse(string Code, string DisplayName);
public sealed record StaffRoleResponse(string Code, string DisplayName, IReadOnlyList<string> PermissionCodes);
public sealed record StaffAssignedRoleResponse(string Code, string DisplayName);
public sealed record StaffAccountResponse(Guid Id, string FullName, string Email, bool IsActive,
    IReadOnlyList<StaffAssignedRoleResponse> Roles);
public sealed class ReplaceStaffRolesRequest
{
    [Required] public List<string> RoleCodes { get; set; } = [];
}
public sealed class SetStaffActiveRequest
{
    public bool IsActive { get; set; }
}
public sealed class ReplaceRolePermissionsRequest
{
    public List<string> PermissionCodes { get; set; } = [];
}
public sealed record StaffAuditResponse(DateTimeOffset OccurredAtUtc, string ActorName, string Action,
    string TargetType, string TargetId, string Details);
