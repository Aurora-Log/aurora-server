using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Auth;

public record CompleteInvitationCommand(string Email, string NewPassword, string ConfirmationCode) : IRequest<LoginResult>;

public class CompleteInvitationCommandHandler(ICognitoAuthService cognitoService, IamTenantDbContext context) : IRequestHandler<CompleteInvitationCommand, LoginResult>
{
    public async Task<LoginResult> Handle(CompleteInvitationCommand request, CancellationToken cancellationToken)
    {
        // 1. Send the new password and confirmation code (which acts as session ID in this flow for AdminCreateUser,
        // or we could use the RespondToAuthChallenge flow if we stored the session.
        // Assuming the ConfirmationCode here is the session string returned from the first failed login attempt,
        // OR if using ConfirmSignUp, we would use a different AWS API.
        // Since we used AdminCreateUser, the user is in FORCE_CHANGE_PASSWORD state. 
        // They must first login to get the session string, then respond to challenge.
        // For this command, we assume 'ConfirmationCode' holds the 'Session' string returned by InitiateAuth.

        var authResult = await cognitoService.CompleteNewPasswordChallengeAsync(
            request.Email,
            request.NewPassword,
            request.ConfirmationCode,
            cancellationToken);

        // 2. Fetch User and Permissions from DB via internal command/query (reusing logic)
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r!.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("User not found in database.");

        var roles = user.UserRoles.Select(ur => ur.Role!.Code).Distinct().ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToList();

        // 3. Mark User as ACTIVE if they were PENDING/INVITED
        if (user.Status != "ACTIVE")
        {
            user.Status = "ACTIVE";
            await context.SaveChangesAsync(cancellationToken);
        }

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
