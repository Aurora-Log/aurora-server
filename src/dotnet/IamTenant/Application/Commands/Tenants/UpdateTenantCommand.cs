using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record UpdateTenantCommand(Guid Id, string Name, string? TaxCode, string PlanType) : IRequest<TenantDto>;

public class UpdateTenantHandler(IamTenantDbContext context) : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
                        .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken) 
                            ?? throw new Exception("Tenant not found");

        tenant.Name = request.Name;
        tenant.TaxCode = request.TaxCode;
        tenant.PlanType = request.PlanType;

        await context.SaveChangesAsync(cancellationToken);

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
