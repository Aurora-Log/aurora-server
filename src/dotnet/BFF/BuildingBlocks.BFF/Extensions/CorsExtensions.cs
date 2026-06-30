namespace BuildingBlocks.BFF.Extensions;

public static class CorsExtensions
{
    /// <summary>
    /// Đăng ký CORS policies cho BFF.
    /// Cho phép các origins được định nghĩa trong cấu hình truy cập.
    /// Hỗ trợ credentials (để gửi/nhận HttpOnly Cookies).
    /// </summary>
    public static IServiceCollection AddBffCors(
        this IServiceCollection services,
        IConfiguration config)
    {
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(opts =>
        {
            opts.AddDefaultPolicy(policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()); // Bắt buộc phải có để dùng HttpOnly Cookie
        });

        return services;
    }
}
