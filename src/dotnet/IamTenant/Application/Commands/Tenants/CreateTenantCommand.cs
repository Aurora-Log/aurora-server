using IamTenant.Domain;
using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MassTransit;
using MediatR;
using Shared.Events;

namespace IamTenant.Application.Commands.Tenants;

public record CreateTenantCommand(string Name, string CompanyDomain, string AdminEmail, string? TaxCode = null, string PlanType = "FREE") : IRequest<TenantDto>;

public class CreateTenantHandler(IamTenantDbContext context, IPublishEndpoint publishEndpoint) : IRequestHandler<CreateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        // 1. Check if CompanyDomain is already used
        if (context.Tenants.Any(t => t.CompanyDomain == request.CompanyDomain && !t.IsDeleted))
        {
            throw new Exception("Company Domain already exists.");
        }

        // 2. Validate AdminEmail belongs to CompanyDomain
        if (!request.AdminEmail.EndsWith($"@{request.CompanyDomain}", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Admin Email must belong to the Company Domain.");
        }

        // 3. Create Tenant
        var tenant = new Tenant
        {
            Name = request.Name,
            Code = request.CompanyDomain.Split('.')[0].ToUpper(), // Simple code generation
            CompanyDomain = request.CompanyDomain,
            TaxCode = request.TaxCode,
            PlanType = request.PlanType,
            Status = "ACTIVE"
        };

        context.Tenants.Add(tenant);

        // 4. Create Tenant Admin
        var adminUser = new User
        {
            TenantId = tenant.Id,
            Email = request.AdminEmail,
            UserType = "TENANT_ADMIN",
            Status = "PENDING"
        };

        context.Users.Add(adminUser);

        // 5. Save changes (AuditInterceptor will assign CreatedBy/CreatedAt)
        await context.SaveChangesAsync(cancellationToken);

        // 6. Generate a dummy token (In real world, maybe call Auth Service or generate JWT here)
        var token = Guid.CreateVersion7().ToString();

        // 7. Publish Event
        var tenantAdminCreatedEvent = new TenantAdminCreatedEvent
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            UserId = adminUser.Id,
            Email = adminUser.Email,
            InvitationToken = token
        };

        await publishEndpoint.Publish(tenantAdminCreatedEvent, cancellationToken);

        return new TenantDto
        {
            Id = tenant.Id,
            Code = tenant.Code,
            Name = tenant.Name,
            TaxCode = tenant.TaxCode,
            CompanyDomain = tenant.CompanyDomain,
            PlanType = tenant.PlanType,
            Status = tenant.Status,
            CreatedAt = tenant.CreatedAt
        };
    }
}
