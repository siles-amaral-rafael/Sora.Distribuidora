using Sora.Domain.Entities;

namespace Sora.Domain.Repositories;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
    Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
