using Shared.Entity;
namespace IamTenant.Domain;

public class Tenant : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string CompanyDomain { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public string Status { get; set; } = "PROVISIONING"; // PROVISIONING, ACTIVE, SUSPENDED, ARCHIVED

    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;


    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}

