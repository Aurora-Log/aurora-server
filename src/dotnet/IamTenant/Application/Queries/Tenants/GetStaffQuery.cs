using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IamTenant.Application.Queries.Tenants;

public record GetStaffQuery(Guid Id, Guid TenantId) : IRequest<StaffDto>;

public class GetStaffHandler(IamTenantDbContext context) : IRequestHandler<GetStaffQuery, StaffDto>
{
    public async Task<StaffDto> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        var staffUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id && u.TenantId == request.TenantId && !u.IsDeleted, cancellationToken) 
            ?? throw new Exception("Staff not found");

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
