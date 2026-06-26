using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Shared.Security;

namespace Shared.Interceptors;

/// <summary>
/// gRPC Server-side interceptor: đọc metadata từ BFF/client,
/// populate ICurrentUserService để tất cả handlers downstream dùng được.
/// Mọi gRPC service đều phải đăng ký interceptor này.
/// </summary>
public class AuthInterceptor(ICurrentUserContext currentUser, ILogger<AuthInterceptor> logger)
    : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        PopulateCurrentUser(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        PopulateCurrentUser(context);
        await continuation(request, responseStream, context);
    }

    private void PopulateCurrentUser(ServerCallContext context)
    {
        var headers = context.RequestHeaders;

        var userIdStr = headers.GetValue(GrpcMetadataKeys.UserId);
        var tenantIdStr = headers.GetValue(GrpcMetadataKeys.TenantId);
        var traceId = headers.GetValue(GrpcMetadataKeys.TraceId);
        var versionStr = headers.GetValue(GrpcMetadataKeys.PermissionVersion);
        var roleIdsStr = headers.GetValue(GrpcMetadataKeys.RoleIds);

        Guid? parsedUserId = null;
        Guid? parsedTenantId = null;
        int? parsedVersion = null;

        if (Guid.TryParse(userIdStr, out var userId)) parsedUserId = userId;
        if (Guid.TryParse(tenantIdStr, out var tenantId)) parsedTenantId = tenantId;
        if (int.TryParse(versionStr, out var version)) parsedVersion = version;

        var roleIds = roleIdsStr?.Split(',').ToList() ?? new List<string>();

        currentUser.Populate(parsedUserId, parsedTenantId, traceId, parsedVersion, roleIds, new List<string>());

        logger.LogDebug("AuthInterceptor: UserId={UserId} TenantId={TenantId} Version={Version}",
            currentUser.UserId, currentUser.TenantId, currentUser.PermissionVersion);
    }
}
