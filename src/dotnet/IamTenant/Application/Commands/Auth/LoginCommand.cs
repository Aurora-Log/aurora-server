using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Auth;

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, string UserId, string TenantId, List<string> Roles, List<string> Permissions);

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public class LoginCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticate with Cognito
        var authResult = await cognitoService.InitiateAuthAsync(request.Email, request.Password, cancellationToken);

        if (authResult.Session != null)
        {
            throw new Shared.Exceptions.ForbiddenException("NEW_PASSWORD_REQUIRED. Please complete invitation.");
        }

        // 2. Fetch User and Permissions from DB
        var userData = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Email == request.Email && !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.TenantId,
                u.Status,
                u.PermissionVersion,
                Roles = u.UserRoles.Select(ur => ur.Role!.Code).ToList(),
                Permissions = u.UserRoles
                    .SelectMany(ur => ur.Role!.RolePermissions)
                    .Select(rp => rp.Permission!.Code)
                    .Distinct()
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (userData == null)
        {
            throw new Shared.Exceptions.NotFoundException("User not found");
        }

        return new LoginResult(
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.ExpiresIn,
            userData.Id.ToString(),
            userData.TenantId == Guid.Empty ? "" : userData.TenantId.ToString(),
            userData.Roles,
            userData.Permissions);
    }
}
