using Sora.Shared.Auth;

namespace Sora.Application.Auth;

public interface IStaffAdministrationService
{
    Task<IReadOnlyList<StaffAccountResponse>> ListAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<StaffRoleResponse>> ListRolesAsync(CancellationToken cancellationToken);
    IReadOnlyList<StaffPermissionResponse> ListPermissions();
    Task<IReadOnlyList<StaffAuditResponse>> ListAuditAsync(CancellationToken cancellationToken);
    Task<ServiceResult<StaffAccountResponse>> CreateAsync(CreateStaffAccountRequest request, Guid actorId,
        CancellationToken cancellationToken);
    Task<ServiceResult<bool>> ReplaceRolesAsync(Guid staffId, ReplaceStaffRolesRequest request, Guid actorId,
        CancellationToken cancellationToken);
    Task<ServiceResult<bool>> SetActiveAsync(Guid staffId, SetStaffActiveRequest request, Guid actorId,
        CancellationToken cancellationToken);
    Task<ServiceResult<bool>> ReplacePermissionsAsync(string roleCode, ReplaceRolePermissionsRequest request,
        Guid actorId, CancellationToken cancellationToken);
}
