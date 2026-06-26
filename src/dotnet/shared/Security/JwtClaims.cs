namespace Shared.Security;

/// <summary>
/// Hằng số cho tên các claims trong JWT.
/// Dùng chung trên mọi service để tránh hardcode string.
/// </summary>
public static class JwtClaims
{
    public const string UserId = "user_id";
    public const string TenantId = "tenant_id";
    public const string RoleIds = "role_ids";
    public const string PermissionVersion = "permission_version";
}

/// <summary>
/// Keys cho gRPC Metadata headers.
/// </summary>
public static class GrpcMetadataKeys
{
    public const string UserId = "x-user-id";
    public const string TenantId = "x-tenant-id";
    public const string TraceId = "x-trace-id";
    public const string PermissionVersion = "x-permission-version";
    public const string RoleIds = "x-role-ids";
    public const string AccessToken = "x-access-token";
}
