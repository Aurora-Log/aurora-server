using Shared.Entity;

namespace IamTenant.Domain;

public class Tenant : AuditableEntity
{
    private Tenant() { } // EF Core

    public static Tenant Create(string name, string companyDomain, string? taxCode, string planType, Guid idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(companyDomain))
            throw new Shared.Exceptions.DomainException("Company domain is required.");

        return new Tenant
        {
            Name = name,
            Code = GenerateCode(companyDomain),
            CompanyDomain = companyDomain.ToLowerInvariant(),
            TaxCode = taxCode,
            PlanType = planType,
            Status = Enums.TenantStatus.Active,
            IdempotencyKey = idempotencyKey
        };
    }

    private static string GenerateCode(string domain) =>
        domain.Split('.')[0].ToUpperInvariant()[..Math.Min(10, domain.Split('.')[0].Length)];

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string CompanyDomain { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public Enums.TenantStatus Status { get; set; } = Enums.TenantStatus.Provisioning;
    public Guid IdempotencyKey { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted { get; private set; }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
