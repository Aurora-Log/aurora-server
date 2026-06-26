namespace Shared.Entity;

public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public abstract class TenantAuditableEntity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
}
