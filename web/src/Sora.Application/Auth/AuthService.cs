using Sora.Domain.Entities;
using Sora.Domain.Repositories;
using Sora.Shared.Auth;

namespace Sora.Application.Auth;

public sealed class AuthService(
    IStaffAccountRepository accounts,
    ITokenIssuer tokenIssuer,
    TimeProvider timeProvider) : IAuthService
{
    private const int BcryptWorkFactor = 12;
    private const string DefaultRoleCode = "SELLER";

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await accounts.EmailExistsAsync(email, cancellationToken))
        {
            return AuthResult.Failure("email_already_registered");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BcryptWorkFactor);
        var now = timeProvider.GetUtcNow();
        var account = new StaffAccount(request.FullName.Trim(), email, passwordHash, now);
        account.AssignRole(DefaultRoleCode, now);

        await accounts.AddAsync(account, cancellationToken);
        return AuthResult.Success(tokenIssuer.Issue(account));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByEmailAsync(NormalizeEmail(request.Email), cancellationToken);
        if (account is null || !account.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
        {
            return AuthResult.Failure("invalid_credentials");
        }

        return AuthResult.Success(tokenIssuer.Issue(account));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
