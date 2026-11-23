using Sora.Shared.Customers;

namespace Sora.Application.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken cancellationToken);
    Task<CustomerResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<CustomerResponse>> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken);
    Task<ServiceResult<CustomerResponse>> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
