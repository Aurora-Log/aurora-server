using IamTenant.Application.Interfaces;
using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IamTenant.Domain.Enums;

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

        // 2. Fetch User from DB to update status
        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("User not found in database.");

        // Fetch Roles and Permissions via Projection to avoid deep Includes
        var userPermissions = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => new 
            {
                RoleCode = ur.Role!.Code,
                Permissions = ur.Role.RolePermissions.Select(rp => rp.Permission!.Code).ToList()
            })
            .ToListAsync(cancellationToken);

        var roles = userPermissions.Select(x => x.RoleCode).Distinct().ToList();
        var permissions = userPermissions.SelectMany(x => x.Permissions).Distinct().ToList();

        // 3. Mark User as ACTIVE if they were PENDING/INVITED
        if (user.Status != UserStatus.Active)
        {
            user.Status = UserStatus.Active;
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
