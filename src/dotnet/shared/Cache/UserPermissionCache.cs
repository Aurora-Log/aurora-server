namespace Shared.Cache;

/// <summary>
/// Model lưu trong Redis: user:{userId}:permissions
/// </summary>
public class UserPermissionCache
{
    public int Version { get; set; }
    public List<string> Permissions { get; set; } = [];
    public List<string> RoleIds { get; set; } = [];
    public DateTimeOffset CachedAt { get; set; } = DateTimeOffset.UtcNow;
}
