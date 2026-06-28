using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Auth;

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, string UserId, string TenantId, List<string> Roles, List<string> Permissions);

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public class LoginCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context, ISender mediator) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticate with Cognito
        var authResult = await cognitoService.InitiateAuthAsync(request.Email, request.Password, cancellationToken);

        if (authResult.Session != null)
        {
            throw new Shared.Exceptions.ForbiddenException("NEW_PASSWORD_REQUIRED. Please complete invitation.");
        }

        // 2. Fetch User base info from DB
        var user = await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Email == request.Email && !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.TenantId,
                u.Status,
                u.PermissionVersion
            })
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new Shared.Exceptions.NotFoundException("User not found");

        // 3. Fetch Permissions from Cache / DB
        var userPermissions = await mediator.Send(new IamTenant.Application.Queries.Permissions.GetUserPermissionsQuery(user.Id, user.PermissionVersion), cancellationToken);

        return new LoginResult(
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.ExpiresIn,
            user.Id.ToString(),
            user.TenantId == Guid.Empty ? "" : user.TenantId.ToString(),
            userPermissions.RoleCodes,
            userPermissions.Permissions.Select(p => p.Code).ToList());
    }
}
