using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Shared.Security;

namespace BuildingBlocks.BFF.Extensions;

public static class AuthExtensions
{
    /// <summary>
    /// Đăng ký JWT Bearer (đọc từ HttpOnly Cookie) + Authorization.
    /// </summary>
    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = config["Auth:Jwt:Authority"];
                opts.Audience = config["Auth:Jwt:Audience"];
                opts.RequireHttpsMetadata = config.GetValue("Auth:CookieSecure", true);

                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Đọc token từ HttpOnly Cookie thay vì Authorization header
                opts.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Cookies["access_token"];
                        if (!string.IsNullOrWhiteSpace(token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // ICurrentUserService — Scoped theo request
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        return services;
    }
}
