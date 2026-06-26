using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Cache;
using Shared.Interceptors;
using Shared.Security;

namespace Shared.Extensions;

public static class SharedServiceExtensions
{
    /// <summary>
    /// Đăng ký tất cả shared services: CurrentUserService, Redis, Interceptors.
    /// Gọi một lần trong Program.cs của mỗi service.
    /// </summary>
    public static IServiceCollection AddSharedServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Security context — Scoped để mỗi request có instance riêng
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
        services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserService>());

        // Redis Permission Cache
        var redisConn = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is required");
        services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConn);
        services.AddScoped<IPermissionCacheService, PermissionCacheService>();

        // gRPC Interceptors
        services.AddScoped<AuthInterceptor>();
        services.AddTransient<ClientMetadataInterceptor>();

        // Audit EF Core Interceptor
        services.AddScoped<AuditSaveChangesInterceptor>();

        return services;
    }

    /// <summary>
    /// Cấu hình MassTransit + RabbitMQ với Raw JSON cho cross-platform interop (NestJS).
    /// Snake-case exchange names via [EntityName] attribute trên Event records.
    /// </summary>
    public static IServiceCollection AddSharedMassTransit(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddMassTransit(x =>
        {
            configureBus?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    configuration["RabbitMQ:Host"] ?? "localhost",
                    "/",
                    h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                // Bắt buộc để NestJS (và bất kỳ service non-.NET nào) đọc được message
                cfg.UseRawJsonSerializer();

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
