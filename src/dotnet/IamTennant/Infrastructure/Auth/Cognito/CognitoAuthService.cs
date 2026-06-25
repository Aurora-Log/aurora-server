using System.Security.Cryptography;
using System.Text;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using iam_tennant.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace iam_tennant.Infrastructure.Auth.Cognito;

public class CognitoAuthService(
    IAmazonCognitoIdentityProvider cognito,
    IOptions<CognitoOptions> options) : ICognitoAuthService
{
    private readonly CognitoOptions _options = options.Value;

    private string ComputeSecretHash(string username)
    {
        var key = Encoding.UTF8.GetBytes(_options.ClientSecret);
        var message = Encoding.UTF8.GetBytes(username + _options.ClientId);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(message);
        return Convert.ToBase64String(hash);
    }

    public async Task<string> AdminCreateUserAsync(string email, string tempPassword, CancellationToken ct = default)
    {
        var request = new AdminCreateUserRequest
        {
            UserPoolId = _options.UserPoolId,
            Username = email,
            MessageAction = MessageActionType.SUPPRESS, // Don't send default Cognito email, we will handle it
            TemporaryPassword = tempPassword,
            UserAttributes = new List<AttributeType>
            {
                new() { Name = "email", Value = email },
                new() { Name = "email_verified", Value = "true" }
            }
        };

        var response = await cognito.AdminCreateUserAsync(request, ct);
        
        var subAttribute = response.User.Attributes.FirstOrDefault(a => a.Name == "sub");
        return subAttribute?.Value ?? throw new Exception("Sub not found in Cognito response.");
    }

    public async Task<AuthResult> InitiateAuthAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new InitiateAuthRequest
        {
            ClientId = _options.ClientId,
            AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["PASSWORD"] = password,
                ["SECRET_HASH"] = ComputeSecretHash(email)
            }
        };

        var response = await cognito.InitiateAuthAsync(request, ct);
        
        if (response.ChallengeName == ChallengeNameType.NEW_PASSWORD_REQUIRED)
        {
            return new AuthResult { Session = response.Session };
        }

        var result = response.AuthenticationResult;
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new Exception("Cognito did not return a valid access token.");
        }

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresIn
        };
    }

    public async Task<AuthResult> CompleteNewPasswordChallengeAsync(string email, string newPassword, string session, CancellationToken ct = default)
    {
        var request = new RespondToAuthChallengeRequest
        {
            ClientId = _options.ClientId,
            ChallengeName = ChallengeNameType.NEW_PASSWORD_REQUIRED,
            Session = session,
            ChallengeResponses = new Dictionary<string, string>
            {
                ["USERNAME"] = email,
                ["NEW_PASSWORD"] = newPassword,
                ["SECRET_HASH"] = ComputeSecretHash(email)
            }
        };

        var response = await cognito.RespondToAuthChallengeAsync(request, ct);
        var result = response.AuthenticationResult;
        
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new Exception("Cognito did not return a valid access token after challenge.");
        }

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresIn = result.ExpiresIn
        };
    }

    public async Task<AuthResult> RefreshTokenAsync(string email, string refreshToken, CancellationToken ct = default)
    {
        var request = new InitiateAuthRequest
        {
            ClientId = _options.ClientId,
            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH,
            AuthParameters = new Dictionary<string, string>
            {
                ["REFRESH_TOKEN"] = refreshToken,
                ["SECRET_HASH"] = ComputeSecretHash(email)
            }
        };

        var response = await cognito.InitiateAuthAsync(request, ct);
        var result = response.AuthenticationResult;

        return new AuthResult
        {
            AccessToken = result.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(result.RefreshToken) ? refreshToken : result.RefreshToken,
            ExpiresIn = result.ExpiresIn
        };
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var request = new ForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            SecretHash = ComputeSecretHash(email)
        };

        await cognito.ForgotPasswordAsync(request, ct);
    }

    public async Task ConfirmForgotPasswordAsync(string email, string newPassword, string confirmationCode, CancellationToken ct = default)
    {
        var request = new ConfirmForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            Password = newPassword,
            ConfirmationCode = confirmationCode,
            SecretHash = ComputeSecretHash(email)
        };

        await cognito.ConfirmForgotPasswordAsync(request, ct);
    }
}
