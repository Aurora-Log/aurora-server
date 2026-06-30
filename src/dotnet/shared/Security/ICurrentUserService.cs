namespace Shared.Security;

/// <summary>
/// Cung cấp thông tin về người dùng hiện tại, được populate từ JWT bởi CurrentUserContextMiddleware
/// và enriched với permissions từ Redis bởi PermissionVersionMiddleware.
/// Được đăng ký Scoped để mỗi request có context riêng biệt.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TraceId { get; }
    int? PermissionVersion { get; }
    IReadOnlyList<string> RoleIds { get; }
    IReadOnlyList<string> Permissions { get; }
}

public interface ICurrentUserContext : ICurrentUserService
{
    /// <summary>
    /// Populate identity fields từ validated ClaimsPrincipal (JWT claims).
    /// Permissions được để trống tại bước này — xem PopulatePermissions.
    /// </summary>
    void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion,
                  List<string> roleIds, List<string> permissions);

    /// <summary>
    /// Populate permissions và roleIds từ Redis cache (sau khi version đã được xác nhận).
    /// Được gọi bởi PermissionVersionMiddleware.
    /// </summary>
    void PopulatePermissions(List<string> permissions, List<string> roleIds);
}

public class CurrentUserService : ICurrentUserContext
{
    public Guid?   UserId            { get; private set; }
    public Guid?   TenantId          { get; private set; }
    public string? TraceId           { get; private set; }
    public int?    PermissionVersion { get; private set; }
    public IReadOnlyList<string> RoleIds     { get; private set; } = [];
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    public void Populate(Guid? userId, Guid? tenantId, string? traceId, int? permissionVersion,
                         List<string> roleIds, List<string> permissions)
    {
        UserId            = userId;
        TenantId          = tenantId;
        TraceId           = traceId;
        PermissionVersion = permissionVersion;
        RoleIds           = roleIds;
        Permissions       = permissions;
    }

    public void PopulatePermissions(List<string> permissions, List<string> roleIds)
    {
        Permissions = permissions;
        RoleIds     = roleIds;
    }
}
