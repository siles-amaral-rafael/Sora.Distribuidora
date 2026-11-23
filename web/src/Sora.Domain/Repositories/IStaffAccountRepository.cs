using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public interface IStaffAccountRepository
{
    Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<StaffAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> RoleExistsAsync(string roleCode, CancellationToken cancellationToken);
    Task<IReadOnlyList<StaffAccount>> ListAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StaffRole>> ListRolesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StaffAuditEntry>> ListAuditEntriesAsync(CancellationToken cancellationToken);
    Task AddAsync(StaffAccount account, CancellationToken cancellationToken);
    Task CreateManagedAsync(StaffAccount account, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<StaffAdminResultCode> BootstrapAdministratorAsync(Guid staffId, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<StaffAdminResultCode> ReplaceRolesAsync(Guid staffId, IReadOnlyCollection<string> roleCodes,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<StaffAdminResultCode> SetActiveAsync(Guid staffId, bool isActive,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<StaffAdminResultCode> ReplaceRolePermissionsAsync(string roleCode,
        IReadOnlyCollection<string> permissionCodes, Guid actorId, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public enum StaffAdminResultCode { Applied, NotFound, InvalidRoles, InvalidPermissions, LastAdministrator }
