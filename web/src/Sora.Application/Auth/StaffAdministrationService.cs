using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Auth;
using Sora.Domain.Security;

namespace Sora.Application.Auth;

public sealed class StaffAdministrationService(IStaffAccountRepository accounts, TimeProvider clock)
    : IStaffAdministrationService
{
    private const int BcryptWorkFactor = 12;

    public async Task<IReadOnlyList<StaffAccountResponse>> ListAccountsAsync(CancellationToken cancellationToken) =>
        (await accounts.ListAccountsAsync(cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<StaffRoleResponse>> ListRolesAsync(CancellationToken cancellationToken) =>
        (await accounts.ListRolesAsync(cancellationToken)).Select(x => new StaffRoleResponse(x.Code, x.DisplayName,
            x.Permissions.Select(p => p.PermissionCode).Order().ToArray())).ToList();

    public IReadOnlyList<StaffPermissionResponse> ListPermissions() =>
        PermissionCatalog.All.Select(x => new StaffPermissionResponse(x.Code, x.DisplayName)).ToArray();

    public async Task<IReadOnlyList<StaffAuditResponse>> ListAuditAsync(CancellationToken cancellationToken) =>
        (await accounts.ListAuditEntriesAsync(cancellationToken)).Select(x =>
            new StaffAuditResponse(x.OccurredAtUtc, x.Actor.FullName, x.Action, x.TargetType, x.TargetId, x.Details))
            .ToArray();

    public async Task<ServiceResult<StaffAccountResponse>> CreateAsync(CreateStaffAccountRequest request,
        Guid actorId, CancellationToken cancellationToken)
    {
        var fullName = request.FullName.Trim();
        var email = request.Email.Trim().ToUpperInvariant();
        var roleCodes = request.RoleCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (actorId == Guid.Empty || fullName.Length < 2 || !IsValidEmail(email) ||
            request.TemporaryPassword.Length is < 12 or > 72 || roleCodes.Length == 0)
            return ServiceResult<StaffAccountResponse>.Failure("invalid_staff_account");
        if (await accounts.EmailExistsAsync(email, cancellationToken))
            return ServiceResult<StaffAccountResponse>.Failure("email_already_registered");
        foreach (var roleCode in roleCodes)
            if (!await accounts.RoleExistsAsync(roleCode, cancellationToken))
                return ServiceResult<StaffAccountResponse>.Failure("role_not_found");

        var now = clock.GetUtcNow();
        var account = new StaffAccount(fullName, email,
            BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword, BcryptWorkFactor), now);
        foreach (var roleCode in roleCodes) account.AssignRole(roleCode, now, actorId);
        await accounts.CreateManagedAsync(account, actorId, now, cancellationToken);
        return ServiceResult<StaffAccountResponse>.Success(Map(account));
    }

    public async Task<ServiceResult<bool>> ReplaceRolesAsync(Guid staffId, ReplaceStaffRolesRequest request,
        Guid actorId, CancellationToken cancellationToken) =>
        MapAdminResult(await accounts.ReplaceRolesAsync(staffId, request.RoleCodes, actorId,
            clock.GetUtcNow(), cancellationToken));

    public async Task<ServiceResult<bool>> SetActiveAsync(Guid staffId, SetStaffActiveRequest request,
        Guid actorId, CancellationToken cancellationToken) =>
        MapAdminResult(await accounts.SetActiveAsync(staffId, request.IsActive, actorId,
            clock.GetUtcNow(), cancellationToken));

    public async Task<ServiceResult<bool>> ReplacePermissionsAsync(string roleCode,
        ReplaceRolePermissionsRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        var permissions = request.PermissionCodes.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        if (roleCode.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) &&
            !permissions.Contains("staff.manage", StringComparer.OrdinalIgnoreCase))
            return ServiceResult<bool>.Failure("administrator_permission_required");
        return MapAdminResult(await accounts.ReplaceRolePermissionsAsync(roleCode, permissions,
            actorId, clock.GetUtcNow(), cancellationToken));
    }

    private static ServiceResult<bool> MapAdminResult(StaffAdminResultCode result) => result switch
    {
        StaffAdminResultCode.Applied => ServiceResult<bool>.Success(true),
        StaffAdminResultCode.NotFound => ServiceResult<bool>.Failure("staff_or_role_not_found"),
        StaffAdminResultCode.InvalidRoles => ServiceResult<bool>.Failure("invalid_roles"),
        StaffAdminResultCode.InvalidPermissions => ServiceResult<bool>.Failure("invalid_permissions"),
        StaffAdminResultCode.LastAdministrator => ServiceResult<bool>.Failure("last_administrator_protected"),
        _ => ServiceResult<bool>.Failure("staff_change_failed")
    };

    private static StaffAccountResponse Map(StaffAccount account) => new(account.Id, account.FullName,
        account.Email, account.IsActive, account.RoleAssignments.Select(x => new StaffAssignedRoleResponse(
            x.RoleCode, x.Role?.DisplayName ?? x.RoleCode)).OrderBy(x => x.DisplayName).ToArray());

    private static bool IsValidEmail(string email) =>
        System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
}
