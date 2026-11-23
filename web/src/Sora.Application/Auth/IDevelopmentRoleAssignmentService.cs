namespace Sora.Application.Auth;

public interface IDevelopmentRoleAssignmentService
{
    Task<(bool Succeeded, string? ErrorCode, IReadOnlyList<string>? Roles)> AssignToCurrentUserAsync(
        Guid staffAccountId, string roleCode, CancellationToken cancellationToken);
}
