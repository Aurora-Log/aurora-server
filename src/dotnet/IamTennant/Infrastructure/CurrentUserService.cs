using shared.Entity;

namespace iam_tennant.Infrastructure;

public class CurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? TraceId { get; set; }
    public List<string> Permissions { get; set; } = new List<string>();
}
