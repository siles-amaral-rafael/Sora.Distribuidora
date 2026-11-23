using Sora.Domain.Repositories;

namespace Sora.Application.Auth;

public sealed class DevelopmentRoleAssignmentService(
    IStaffAccountRepository accounts,
    TimeProvider timeProvider) : IDevelopmentRoleAssignmentService
{
    public async Task<(bool Succeeded, string? ErrorCode, IReadOnlyList<string>? Roles)> AssignToCurrentUserAsync(
        Guid staffAccountId, string roleCode, CancellationToken cancellationToken)
    {
        var normalizedRoleCode = roleCode.Trim().ToUpperInvariant();
        // Development bootstrap can only promote the first account to administrator.
        // Further role changes must go through audited staff.manage operations.
        if (normalizedRoleCode != "ADMIN")
            return (false, "administrator_bootstrap_closed", null);
        var result = await accounts.BootstrapAdministratorAsync(staffAccountId, timeProvider.GetUtcNow(), cancellationToken);
        if (result == StaffAdminResultCode.Applied) return (true, null, ["ADMIN"]);
        return (false, result == StaffAdminResultCode.LastAdministrator
            ? "administrator_bootstrap_closed" : "staff_account_not_found", null);
    }
}
