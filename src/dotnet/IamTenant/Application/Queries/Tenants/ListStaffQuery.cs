using IamTenant.Infrastructure.Persistences;
using IamTenant.Application.DTOs.Tenants;
using MediatR;
using Shared.Pagination;

namespace IamTenant.Application.Queries.Tenants;

public class ListStaffQuery : PagedRequest, IRequest<PagedResult<StaffDto>> { };


public class ListStaffHandler(IamTenantDbContext context) : IRequestHandler<ListStaffQuery, PagedResult<StaffDto>>
{
    public async Task<PagedResult<StaffDto>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var query = context.Users
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new StaffDto
            {
                Id = u.Id,
                TenantId = u.TenantId,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                UserType = u.UserType.ToString(),
                Status = u.Status.ToString(),
                StaffType = u.StaffType.ToString(),
                CreatedAt = u.CreatedAt
            });

        return await query.ToPagedResultAsync(request, cancellationToken);
    }
}
