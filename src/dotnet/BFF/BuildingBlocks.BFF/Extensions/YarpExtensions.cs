// File temporarily commented out.
// YARP configuration has been moved to API.Gateway project.
/*
using Shared.Security;
using Yarp.ReverseProxy.Transforms;

namespace BuildingBlocks.BuildingBlocks.BFF.Extensions;

public static class YarpExtensions
{
    /// <summary>
    /// Đăng ký YARP Reverse Proxy với Header Injection Protection.
    ///
    /// Security model:
    ///   STEP 1 — Strip tất cả internal headers mà client có thể đã inject
    ///   STEP 2 — Re-inject từ validated ICurrentUserService (source of truth duy nhất)
    ///
    /// Kết hợp với RequestHeaderRemove transforms trong appsettings.json
    /// để có dual-layer protection.
    /// </summary>
    public static IServiceCollection AddBffReverseProxy(
        this IServiceCollection services,
        IConfiguration config)
    {
        services
            .AddReverseProxy()
            .LoadFromConfig(config.GetSection("ReverseProxy"))
            .AddTransforms(ctx =>
            {
                ctx.AddRequestTransform(async reqCtx =>
                {
                    // STEP 1: Strip client-injected internal headers
                    reqCtx.ProxyRequest.Headers.Remove("x-user-id");
                    reqCtx.ProxyRequest.Headers.Remove("x-tenant-id");
                    reqCtx.ProxyRequest.Headers.Remove("x-role-ids");
                    reqCtx.ProxyRequest.Headers.Remove("x-permission-version");
                    reqCtx.ProxyRequest.Headers.Remove("x-trace-id");
                    reqCtx.ProxyRequest.Headers.Remove("x-access-token");

                    // STEP 2: Re-inject từ validated ICurrentUserService
                    var currentUser = reqCtx.HttpContext.RequestServices
                        .GetRequiredService<ICurrentUserService>();

                    if (currentUser.UserId.HasValue)
                        reqCtx.ProxyRequest.Headers.TryAddWithoutValidation(
                            "x-user-id", currentUser.UserId.ToString());

                    if (currentUser.TenantId.HasValue)
                        reqCtx.ProxyRequest.Headers.TryAddWithoutValidation(
                            "x-tenant-id", currentUser.TenantId.ToString());

                    if (currentUser.RoleIds.Count > 0)
                        reqCtx.ProxyRequest.Headers.TryAddWithoutValidation(
                            "x-role-ids", string.Join(',', currentUser.RoleIds));

                    if (currentUser.PermissionVersion.HasValue)
                        reqCtx.ProxyRequest.Headers.TryAddWithoutValidation(
                            "x-permission-version", currentUser.PermissionVersion.ToString());

                    reqCtx.ProxyRequest.Headers.TryAddWithoutValidation(
                        "x-trace-id", reqCtx.HttpContext.TraceIdentifier);

                    await Task.CompletedTask;
                });
            });

        return services;
    }
}
*/
