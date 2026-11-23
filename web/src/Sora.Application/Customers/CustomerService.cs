using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Customers;

namespace Sora.Application.Customers;

public sealed class CustomerService(ICustomerRepository customers, TimeProvider timeProvider) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await customers.ListAsync(cancellationToken)).Select(ToResponse).ToList();

    public async Task<CustomerResponse?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(id, cancellationToken);
        return customer is null ? null : ToResponse(customer);
    }

    public async Task<ServiceResult<CustomerResponse>> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!CoordinatesAreValid(request.Latitude, request.Longitude))
        {
            return ServiceResult<CustomerResponse>.Failure("coordinates_must_be_provided_together");
        }

        var customer = new Customer(
            request.CustomerType.Trim().ToUpperInvariant(),
            request.FullName.Trim(),
            NormalizePhone(request.Phone),
            request.PrimaryAddress.Trim(),
            request.Latitude,
            request.Longitude,
            timeProvider.GetUtcNow());

        await customers.AddAsync(customer, cancellationToken);
        return ServiceResult<CustomerResponse>.Success(ToResponse(customer));
    }

    public async Task<ServiceResult<CustomerResponse>> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!CoordinatesAreValid(request.Latitude, request.Longitude))
        {
            return ServiceResult<CustomerResponse>.Failure("coordinates_must_be_provided_together");
        }

        var customer = await customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return ServiceResult<CustomerResponse>.Failure("customer_not_found");
        }

        customer.Update(
            request.CustomerType.Trim().ToUpperInvariant(),
            request.FullName.Trim(),
            NormalizePhone(request.Phone),
            request.PrimaryAddress.Trim(),
            request.Latitude,
            request.Longitude);
        await customers.SaveChangesAsync(cancellationToken);
        return ServiceResult<CustomerResponse>.Success(ToResponse(customer));
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return false;
        }

        customer.Deactivate();
        await customers.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool CoordinatesAreValid(decimal? latitude, decimal? longitude) =>
        (latitude is null && longitude is null) || (latitude is not null && longitude is not null);

    private static string NormalizePhone(string phone) => phone.Trim();

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.CustomerType,
        customer.FullName,
        customer.Phone,
        customer.PrimaryAddress,
        customer.Latitude,
        customer.Longitude,
        customer.IsActive,
        customer.CreatedAtUtc);
}
