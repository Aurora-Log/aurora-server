using Grpc.Core;
using Grpc.Core.Interceptors;
using Shared.Security;

namespace IamTenant.Infrastructure.Interceptors;

public class MetadataInterceptor() : Interceptor
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
        var permissionsRaw = headers.FirstOrDefault(h => h.Key.Equals("x-user-permissions", StringComparison.OrdinalIgnoreCase))?.Value;

        traceId ??= Guid.NewGuid().ToString(); 

        if (Guid.TryParse(tenantId, out var parsedTenantId))
        {
            tenantId = parsedTenantId.ToString();
        }

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            userId = parsedUserId.ToString();
        }

        List<string> permissionList = [];
        if (!string.IsNullOrWhiteSpace(permissionsRaw))
        {
            permissionList = permissionsRaw.Split(',').Select(p => p.Trim()).ToList();
        }

        return await continuation(request, context);
    }
}
