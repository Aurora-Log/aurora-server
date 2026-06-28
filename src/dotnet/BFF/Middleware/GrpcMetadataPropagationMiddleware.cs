using Shared.Security;

namespace BFF.Middleware;

/// <summary>
/// Trước khi YARP forward request tới downstream gRPC services,
/// middleware này gắn các metadata headers từ ICurrentUserService:
///   x-user-id, x-tenant-id, x-trace-id, x-permission-version, x-role-ids
/// Các gRPC services phía sau sẽ đọc bằng AuthInterceptor (Shared.Interceptors).
/// </summary>
public class GrpcMetadataPropagationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        // Gắn metadata vào request headers — YARP sẽ forward chúng
        if (currentUser.UserId.HasValue)
            context.Request.Headers[GrpcMetadataKeys.UserId] = currentUser.UserId.ToString();

        if (currentUser.TenantId.HasValue)
            context.Request.Headers[GrpcMetadataKeys.TenantId] = currentUser.TenantId.ToString();

        if (!string.IsNullOrEmpty(currentUser.TraceId))
            context.Request.Headers[GrpcMetadataKeys.TraceId] = currentUser.TraceId;

        if (currentUser.PermissionVersion.HasValue)
            context.Request.Headers[GrpcMetadataKeys.PermissionVersion] = currentUser.PermissionVersion.ToString();

        if (currentUser.RoleIds.Count > 0)
            context.Request.Headers[GrpcMetadataKeys.RoleIds] = string.Join(',', currentUser.RoleIds);

        await next(context);
    }
}
