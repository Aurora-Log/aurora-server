namespace IamTenant.Infrastructure.Auth.Cognito;

public class CognitoOptions
{
    public string UserPoolId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
