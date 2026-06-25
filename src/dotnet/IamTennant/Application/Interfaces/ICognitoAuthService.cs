namespace iam_tennant.Application.Interfaces;

public interface ICognitoAuthService
{
    Task<string> AdminCreateUserAsync(string email, string tempPassword, CancellationToken ct = default);
    Task<AuthResult> InitiateAuthAsync(string email, string password, CancellationToken ct = default);
    Task<AuthResult> CompleteNewPasswordChallengeAsync(string email, string newPassword, string session, CancellationToken ct = default);
    Task<AuthResult> RefreshTokenAsync(string email, string refreshToken, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ConfirmForgotPasswordAsync(string email, string newPassword, string confirmationCode, CancellationToken ct = default);
}

public class AuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string? Session { get; set; } // Used for challenge responses like NEW_PASSWORD_REQUIRED
}
