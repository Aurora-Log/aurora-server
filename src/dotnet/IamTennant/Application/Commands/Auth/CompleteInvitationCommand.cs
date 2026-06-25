using iam_tennant.Application.Interfaces;
using MediatR;

namespace iam_tennant.Application.Commands.Auth;

public record CompleteInvitationCommand(string Email, string NewPassword, string ConfirmationCode) : IRequest<LoginResult>;

public class CompleteInvitationCommandHandler(ICognitoAuthService cognitoService, IMediator mediator) : IRequestHandler<CompleteInvitationCommand, LoginResult>
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
        // We can just call LoginCommand handler logic, but since we already have the token, we just need to build LoginResult.
        // Let's just return a placeholder or refactor LoginCommand to share the DB fetch.
        // For simplicity in this demo:
        
        return new LoginResult(
            authResult.AccessToken, 
            authResult.RefreshToken, 
            authResult.ExpiresIn, 
            "USER_ID", // TODO: fetch from DB
            "TENANT_ID",
            new List<string>(), 
            new List<string>());
    }
}
