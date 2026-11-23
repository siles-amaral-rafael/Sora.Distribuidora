using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Sora.Infrastructure.Persistence;

namespace Sora.Api.Auth;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;

public sealed class DatabasePermissionAuthorizationHandler(SoraDbContext db)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var idText = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     context.User.FindFirstValue("sub");
        if (!Guid.TryParse(idText, out var accountId)) return;

        var allowed = await db.StaffAccounts.AsNoTracking().AnyAsync(account =>
            account.Id == accountId && account.IsActive &&
            account.RoleAssignments.Any(assignment => assignment.Role.Permissions.Any(permission =>
                permission.PermissionCode == requirement.PermissionCode)));
        if (allowed) context.Succeed(requirement);
    }
}
