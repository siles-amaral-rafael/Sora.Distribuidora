using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class StaffAccountRepository(SoraDbContext dbContext) : IStaffAccountRepository
{
    public Task<StaffAccount?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.StaffAccounts.SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.StaffAccounts.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);

    public async Task AddAsync(StaffAccount account, CancellationToken cancellationToken)
    {
        dbContext.StaffAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
