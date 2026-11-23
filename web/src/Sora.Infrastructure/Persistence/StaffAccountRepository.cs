using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.Json;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class StaffAccountRepository(SoraDbContext dbContext) : IStaffAccountRepository
{
    public Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.StaffAccounts
            .Include(x => x.RoleAssignments)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.StaffAccounts.Include(x => x.RoleAssignments).ThenInclude(x => x.Role)
            .ThenInclude(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.StaffAccounts.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<bool> RoleExistsAsync(string roleCode, CancellationToken cancellationToken) =>
        dbContext.StaffRoles.AnyAsync(x => x.Code == roleCode && x.IsActive, cancellationToken);

    public async Task<IReadOnlyList<StaffAccount>> ListAccountsAsync(CancellationToken cancellationToken) =>
        await dbContext.StaffAccounts.AsNoTracking().Include(x => x.RoleAssignments)
            .ThenInclude(x => x.Role).OrderBy(x => x.FullName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffRole>> ListRolesAsync(CancellationToken cancellationToken) =>
        await dbContext.StaffRoles.AsNoTracking().Where(x => x.IsActive).Include(x => x.Permissions)
            .ThenInclude(x => x.Permission)
            .OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StaffAuditEntry>> ListAuditEntriesAsync(CancellationToken cancellationToken) =>
        await dbContext.StaffAuditEntries.AsNoTracking().Include(x => x.Actor)
            .OrderByDescending(x => x.OccurredAtUtc).Take(200).ToListAsync(cancellationToken);

    public async Task AddAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        dbContext.StaffAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateManagedAsync(StaffAccount account, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        dbContext.StaffAccounts.Add(account);
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(actorId, "staff.created", "staff_account",
            account.Id.ToString(), JsonSerializer.Serialize(new { account.Email, Roles = account.RoleAssignments.Select(x => x.RoleCode) }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<StaffAdminResultCode> BootstrapAdministratorAsync(Guid staffId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var account = await dbContext.StaffAccounts.Include(x => x.RoleAssignments)
            .SingleOrDefaultAsync(x => x.Id == staffId && x.IsActive, cancellationToken);
        if (account is null) return StaffAdminResultCode.NotFound;
        if (await dbContext.StaffAccounts.AnyAsync(x => x.IsActive &&
                x.RoleAssignments.Any(role => role.RoleCode == "ADMIN"), cancellationToken))
            return StaffAdminResultCode.LastAdministrator;
        account.AssignRole("ADMIN", now, staffId);
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(staffId, "staff.initial_admin_bootstrap",
            "staff_account", staffId.ToString(), JsonSerializer.Serialize(new { Role = "ADMIN", Bootstrap = true }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StaffAdminResultCode.Applied;
    }

    public async Task<StaffAdminResultCode> ReplaceRolesAsync(Guid staffId, IReadOnlyCollection<string> roleCodes,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var normalized = roleCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (normalized.Length == 0) return StaffAdminResultCode.InvalidRoles;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var account = await dbContext.StaffAccounts.Include(x => x.RoleAssignments)
            .SingleOrDefaultAsync(x => x.Id == staffId, cancellationToken);
        if (account is null) return StaffAdminResultCode.NotFound;
        if (await dbContext.StaffRoles.CountAsync(x => normalized.Contains(x.Code) && x.IsActive, cancellationToken) != normalized.Length)
            return StaffAdminResultCode.InvalidRoles;
        var before = account.RoleAssignments.Select(x => x.RoleCode).ToArray();
        dbContext.StaffAccountRoles.RemoveRange(account.RoleAssignments);
        account.RoleAssignments.Clear();
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var roleCode in normalized) account.AssignRole(roleCode, now, actorId);
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(actorId, "staff.roles_changed", "staff_account",
            staffId.ToString(), JsonSerializer.Serialize(new { Before = before, After = normalized }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!await HasActiveAdministratorAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return StaffAdminResultCode.LastAdministrator;
        }
        await transaction.CommitAsync(cancellationToken);
        return StaffAdminResultCode.Applied;
    }

    public async Task<StaffAdminResultCode> SetActiveAsync(Guid staffId, bool isActive,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var account = await dbContext.StaffAccounts.SingleOrDefaultAsync(x => x.Id == staffId, cancellationToken);
        if (account is null) return StaffAdminResultCode.NotFound;
        var previous = account.IsActive;
        account.SetActive(isActive);
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(actorId,
            isActive ? "staff.activated" : "staff.deactivated", "staff_account", staffId.ToString(),
            JsonSerializer.Serialize(new { Before = previous, After = isActive }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!await HasActiveAdministratorAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return StaffAdminResultCode.LastAdministrator;
        }
        await transaction.CommitAsync(cancellationToken);
        return StaffAdminResultCode.Applied;
    }

    public async Task<StaffAdminResultCode> ReplaceRolePermissionsAsync(string roleCode,
        IReadOnlyCollection<string> permissionCodes, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        roleCode = roleCode.Trim().ToUpperInvariant();
        var normalized = permissionCodes.Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var role = await dbContext.StaffRoles.Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Code == roleCode && x.IsActive, cancellationToken);
        if (role is null) return StaffAdminResultCode.NotFound;
        if (await dbContext.StaffPermissions.CountAsync(x => normalized.Contains(x.Code), cancellationToken) != normalized.Length)
            return StaffAdminResultCode.InvalidPermissions;
        var before = role.Permissions.Select(x => x.PermissionCode).Order().ToArray();
        dbContext.StaffRolePermissions.RemoveRange(role.Permissions);
        foreach (var permissionCode in normalized)
            dbContext.StaffRolePermissions.Add(new StaffRolePermission(role.Code, permissionCode));
        dbContext.StaffAuditEntries.Add(new StaffAuditEntry(actorId, "role.permissions_changed", "staff_role",
            role.Code, JsonSerializer.Serialize(new { Before = before, After = normalized.Order().ToArray() }), now));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (!await HasActiveAdministratorAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return StaffAdminResultCode.LastAdministrator;
        }
        await transaction.CommitAsync(cancellationToken);
        return StaffAdminResultCode.Applied;
    }

    private Task<bool> HasActiveAdministratorAsync(CancellationToken cancellationToken) =>
        dbContext.StaffAccounts.AnyAsync(x => x.IsActive &&
            x.RoleAssignments.Any(ar => ar.Role.Permissions.Any(rp => rp.PermissionCode == "staff.manage")),
            cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
