using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Commands.Tenants;

public record UpdateStaffCommand(Guid Id, Guid TenantId, string FirstName, string LastName) : IRequest<StaffDto>;

public class UpdateStaffHandler(IamTenantDbContext context) : IRequestHandler<UpdateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken)
            ?? throw new Exception("Staff not found");

        staffUser.FirstName = request.FirstName;
        staffUser.LastName = request.LastName;

        await context.SaveChangesAsync(cancellationToken);

        return new StaffDto
        {
            Id = staffUser.Id,
            TenantId = staffUser.TenantId,
            Email = staffUser.Email,
            FirstName = staffUser.FirstName,
            LastName = staffUser.LastName,
            UserType = staffUser.UserType.ToString(),
            Status = staffUser.Status.ToString(),
            StaffType = staffUser.StaffType.ToString(),
            CreatedAt = staffUser.CreatedAt
        };
    }
}
