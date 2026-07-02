namespace IamTenant.Domain;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? TenantId { get; set; }
    
    // UserId hoặc System
    public string Actor { get; set; } = string.Empty; 
    
    // e.g., "ASSIGN_PERMISSION"
    public string Action { get; set; } = string.Empty; 
    
    // e.g., "Role: 123"
    public string Resource { get; set; } = string.Empty; 
    
    // JSON payload
    public string Details { get; set; } = string.Empty; 
    
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
