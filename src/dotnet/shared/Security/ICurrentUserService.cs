namespace Shared.Security;

/// <summary>
/// Cung cấp thông tin về người dùng hiện tại, được populate từ JWT bởi AuthInterceptor.
/// Được đăng ký Scoped để mỗi gRPC request có context riêng biệt.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; set; }
    Guid? TenantId { get; set; }
    string? TraceId { get; set; }
    int? PermissionVersion { get; set; }
    List<string> RoleIds { get; set; }
    List<string> Permissions { get; set; }
}

public class CurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? TraceId { get; set; }
    public int? PermissionVersion { get; set; }
    public List<string> RoleIds { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
}
