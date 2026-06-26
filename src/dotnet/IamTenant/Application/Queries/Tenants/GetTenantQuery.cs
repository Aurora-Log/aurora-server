using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Queries.Tenants;

public record GetTenantQuery(Guid Id) : IRequest<TenantDto>;

public class GetTenantHandler(IamTenantDbContext context) : IRequestHandler<GetTenantQuery, TenantDto>
{
    public async Task<TenantDto> Handle(GetTenantQuery request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken) 
            ?? throw new Exception("Tenant not found");

        return new TenantDto
        {
            Id = tenant.Id,
            Code = tenant.Code,
            Name = tenant.Name,
            TaxCode = tenant.TaxCode,
            CompanyDomain = tenant.CompanyDomain,
            PlanType = tenant.PlanType,
            Status = tenant.Status.ToString(),
            CreatedAt = tenant.CreatedAt
        };
    }
}
