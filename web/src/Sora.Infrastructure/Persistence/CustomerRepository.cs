using Microsoft.EntityFrameworkCore;
using Sora.Domain.Entities;
using Sora.Domain.Repositories;

namespace Sora.Infrastructure.Persistence;

public sealed class CustomerRepository(SoraDbContext dbContext) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Customers.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(cancellationToken);

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
