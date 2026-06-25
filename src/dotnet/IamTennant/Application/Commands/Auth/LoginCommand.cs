using iam_tennant.Application.Interfaces;
using iam_tennant.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace iam_tennant.Application.Commands.Auth;

public record LoginResult(string AccessToken, string RefreshToken, int ExpiresIn, string UserId, string TenantId, List<string> Roles, List<string> Permissions);

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public class LoginCommandHandler(ICognitoAuthService cognitoService, AppDbContext context) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Authenticate with Cognito
        var authResult = await cognitoService.InitiateAuthAsync(request.Email, request.Password, cancellationToken);
        
        if (authResult.Session != null)
        {
            throw new Exception("NEW_PASSWORD_REQUIRED. Please complete invitation.");
        }

        // 2. Fetch User and Permissions from DB
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            throw new Exception("User not found in database.");
        }

        var roles = user.UserRoles.Select(ur => ur.Role!.Code).Distinct().ToList();
        
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToList();

        return new LoginResult(
            authResult.AccessToken, 
            authResult.RefreshToken, 
            authResult.ExpiresIn, 
            user.Id.ToString(), 
            user.TenantId == Guid.Empty ? "" : user.TenantId.ToString(),
            roles, 
            permissions);
    }
}
