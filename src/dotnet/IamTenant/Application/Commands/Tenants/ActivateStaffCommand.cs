using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record ActivateStaffCommand(Guid Id, Guid TenantId) : IRequest;

public class ActivateStaffHandler(IamTenantDbContext context) : IRequestHandler<ActivateStaffCommand>
{
    public async Task Handle(ActivateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("Staff not found");

        staffUser.Status = Domain.Enums.UserStatus.Active;

        await context.SaveChangesAsync(cancellationToken);
    }
}
