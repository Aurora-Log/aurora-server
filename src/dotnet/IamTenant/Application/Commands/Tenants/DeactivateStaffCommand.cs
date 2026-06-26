using IamTenant.Infrastructure.Persistences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record DeactivateStaffCommand(Guid Id, Guid TenantId) : IRequest;

public class DeactivateStaffHandler(IamTenantDbContext context) : IRequestHandler<DeactivateStaffCommand>
{
    public async Task Handle(DeactivateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("Staff not found");

        staffUser.Status = Domain.Enums.UserStatus.Blocked;

        await context.SaveChangesAsync(cancellationToken);
    }
}
