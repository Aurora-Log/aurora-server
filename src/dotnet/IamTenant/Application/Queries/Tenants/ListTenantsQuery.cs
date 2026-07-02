using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Queries.Tenants;

public class ListTenantsQuery : PagedRequest, IRequest<PagedResult<TenantDto>>
{
}

public class ListTenantsHandler(IamTenantDbContext context) : IRequestHandler<ListTenantsQuery, PagedResult<TenantDto>>
{
    public async Task<PagedResult<TenantDto>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Tenants
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                TaxCode = t.TaxCode,
                CompanyDomain = t.CompanyDomain,
                PlanType = t.PlanType,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt
            });

        return await query.ToPagedResultAsync(request, cancellationToken);
    }
}
