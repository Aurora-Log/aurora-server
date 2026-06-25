using Grpc.Core;
using Grpc.Core.Interceptors;

namespace iam_tennant.Infrastructure.Interceptors;

public class MetadataInterceptor(CurrentUserService currentUserService) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, 
        ServerCallContext context, 
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var headers = context.RequestHeaders;

        var traceId = headers.FirstOrDefault(h => h.Key.Equals("x-trace-id", StringComparison.OrdinalIgnoreCase))?.Value;
        var tenantId = headers.FirstOrDefault(h => h.Key.Equals("x-tenant-id", StringComparison.OrdinalIgnoreCase))?.Value;
        var userId = headers.FirstOrDefault(h => h.Key.Equals("x-user-id", StringComparison.OrdinalIgnoreCase))?.Value;
        var permissions = headers.FirstOrDefault(h => h.Key.Equals("x-user-permissions", StringComparison.OrdinalIgnoreCase))?.Value;

        currentUserService.TraceId = traceId;

        if (Guid.TryParse(tenantId, out var parsedTenantId))
        {
            currentUserService.TenantId = parsedTenantId;
        }

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            currentUserService.UserId = parsedUserId;
        }

        if (!string.IsNullOrWhiteSpace(permissions))
        {
            currentUserService.Permissions = permissions.Split(',').Select(p => p.Trim()).ToList();
        }

        return await continuation(request, context);
    }
}
