using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record DeleteTenantCommand(Guid Id) : IRequest;

public class DeleteTenantHandler(IamTenantDbContext context) : IRequestHandler<DeleteTenantCommand>
{
    public async Task Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.Id && !t.IsDeleted, cancellationToken) 
                ?? throw new Exception("Tenant not found");

        tenant.SoftDelete();
        
        await context.SaveChangesAsync(cancellationToken);
    }
}
