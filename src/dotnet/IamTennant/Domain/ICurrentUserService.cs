namespace iam_tennant.Domain;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TraceId { get; }
    List<string> Permissions { get; }
}
