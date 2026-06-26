using MassTransit;

namespace Shared.Events;

[EntityName("tenant_admin_created_event")]
public record TenantAdminCreatedEvent
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
}

[EntityName("tenant_staff_created_event")]
public record TenantStaffCreatedEvent
{
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}

[EntityName("tenant_staff_password_reset_event")]
public record TenantStaffPasswordResetEvent
{
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
}
