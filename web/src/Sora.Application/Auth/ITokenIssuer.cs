using Sora.Domain.Entities;

namespace Sora.Application.Auth;

public interface ITokenIssuer
{
    IssuedToken Issue(StaffAccount account);
}
