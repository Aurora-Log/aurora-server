namespace Shared.Cache;

/// <summary>
/// Chuẩn hóa key pattern cho Redis trên toàn bộ hệ thống.
/// </summary>
public static class CacheKeys
{
    /// <summary>user:{userId} — Lưu danh sách permissions + permission_version</summary>
    public static string UserPermissions(Guid userId) => $"user:{userId}:permissions";

    /// <summary>tenant:{tenantId}:config — Lưu metadata của tenant</summary>
    // public static string TenantConfig(Guid tenantId) => $"tenant:{tenantId}:config";
}
