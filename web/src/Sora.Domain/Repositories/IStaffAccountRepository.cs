using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public interface IStaffAccountRepository
{
    Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddAsync(StaffAccount account, CancellationToken cancellationToken);
}
