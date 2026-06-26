using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using IamTenant.Application.Commands.Permissions;
using IamTenant.Application.Commands.Roles;
using IamTenant.Application.Commands.Tenants;
using IamTenant.Application.Commands.Users;
using IamTenant.Application.Queries.Permissions;
using IamTenant.Application.Queries.Roles;
using IamTenant.Application.Queries.Tenants;
using MediatR;
using IamTenant.Grpc;
using Common.Grpc;


namespace IamTenant.GrpcServices;

public class IamGrpcService(IMediator mediator) : IamService.IamServiceBase
{
    // ═══════════════════════════════════════════════════════════════════════
    // TENANT RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<TenantResponse> CreateTenant(CreateTenantRequest request, ServerCallContext context)
    {
        try
        {
            var dto = await mediator.Send(new CreateTenantCommand(
                request.Name,
                request.CompanyDomain,
                request.AdminEmail,
                PlanType: request.PlanType.ToString()), context.CancellationToken);

            return new TenantResponse
            {
                Id = dto.Id.ToString(),
                Name = dto.Name,
                TenantCode = dto.Code,
                Status = MapTenantStatus(dto.Status),
                AdminEmail = request.AdminEmail,
                CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<TenantResponse> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        try
        {
            Guid id;
            if (!Guid.TryParse(request.Id, out id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID."));

            var dto = await mediator.Send(new GetTenantQuery(id), context.CancellationToken);

            return new TenantResponse
            {
                Id = dto.Id.ToString(),
                Name = dto.Name,
                TenantCode = dto.Code,
                Status = MapTenantStatus(dto.Status),
                CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
            };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<TenantResponse> UpdateTenantStatus(UpdateTenantStatusRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.TenantId, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID."));

            var dto = await mediator.Send(new UpdateTenantCommand(
                id,
                Name: string.Empty, // Status-only update — keep existing via command
                TaxCode: null,
                PlanType: string.Empty), context.CancellationToken);

            return new TenantResponse
            {
                Id = dto.Id.ToString(),
                Name = dto.Name,
                TenantCode = dto.Code,
                Status = MapTenantStatus(dto.Status),
                CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
            };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // USER / STAFF RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<UserResponse> InviteUser(InviteUserRequest request, ServerCallContext context)
    {
        try
        {
            // InviteUser = CreateStaff — TenantId comes from ICurrentUserService (populated by AuthInterceptor)
            var dto = await mediator.Send(new CreateStaffCommand(
                Email: request.Email,
                FirstName: request.FirstName,
                LastName: request.LastName), context.CancellationToken);

            return new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatus(dto.Status)
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<UserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var dto = await mediator.Send(new GetStaffQuery(id, Guid.Empty), context.CancellationToken);

            return new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatus(dto.Status)
            };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetManyUsersResponse> GetManyUsers(GetManyUsersRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new ListStaffQuery
        {
            TenantId = Guid.Empty, // Resolved from CurrentUserService
            Page = request.Page,
            Limit = request.Limit
        }, context.CancellationToken);

        var response = new GetManyUsersResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
        response.Users.AddRange(result.Items.Select(u => new UserResponse
        {
            Id = u.Id.ToString(),
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            Status = MapUserStatus(u.Status)
        }));
        return response;
    }

    public override async Task<UserResponse> AssignRoles(AssignRolesRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var roleIds = request.RoleIds.Select(id =>
                Guid.TryParse(id, out var g) ? g : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid role ID: {id}"))).ToList();

            var dto = await mediator.Send(new AssignRolesCommand(userId, roleIds), context.CancellationToken);

            return new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatus(dto.Status)
            };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<UserResponse> SuspendUser(SuspendUserRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            await mediator.Send(new DeactivateStaffCommand(userId, Guid.Empty), context.CancellationToken);

            var dto = await mediator.Send(new GetStaffQuery(userId, Guid.Empty), context.CancellationToken);

            return new UserResponse
            {
                Id = dto.Id.ToString(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Status = MapUserStatus(dto.Status)
            };
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ROLE RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<RoleResponse> CreateCustomRole(CreateCustomRoleRequest request, ServerCallContext context)
    {
        try
        {
            var dto = await mediator.Send(new CreateCustomRoleCommand(
                request.Code, request.Name, request.Description), context.CancellationToken);

            return MapRoleResponse(dto);
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<RoleResponse> GetRole(GetRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            var dto = await mediator.Send(new GetRoleQuery(id), context.CancellationToken);
            return MapRoleResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetManyRolesResponse> GetManyRoles(GetManyRolesRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(new ListRolesQuery
        {
            Page = request.Page,
            Limit = request.Limit
        }, context.CancellationToken);

        var response = new GetManyRolesResponse
        {
            Page = result.Page,
            Limit = result.Limit,
            TotalItems = result.TotalItems,
            TotalPages = result.TotalPages
        };
        response.Roles.AddRange(result.Items.Select(MapRoleResponse));
        return response;
    }

    public override async Task<RoleResponse> UpdateRole(UpdateRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            var dto = await mediator.Send(new UpdateRoleCommand(id, request.Name, request.Description), context.CancellationToken);
            return MapRoleResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<EmptyResponse> DeleteRole(DeleteRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.Id, out var id))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            await mediator.Send(new DeleteRoleCommand(id), context.CancellationToken);
            return new EmptyResponse();
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PERMISSION RPCs
    // ═══════════════════════════════════════════════════════════════════════

    public override async Task<RoleResponse> AssignPermissionsToRole(AssignPermissionsToRoleRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.RoleId, out var roleId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid role ID."));

            var permIds = request.PermissionIds.Select(id =>
                Guid.TryParse(id, out var g) ? g : throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid permission ID: {id}"))).ToList();

            var dto = await mediator.Send(new AssignPermissionsToRoleCommand(roleId, permIds), context.CancellationToken);
            return MapRoleResponse(dto);
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<GetUserPermissionsResponse> GetUserPermissions(GetUserPermissionsRequest request, ServerCallContext context)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID."));

            var dto = await mediator.Send(new GetUserPermissionsQuery(userId), context.CancellationToken);

            var response = new GetUserPermissionsResponse
            {
                UserId = dto.UserId.ToString()
            };
            response.RoleIds.AddRange(dto.RoleIds);
            response.Permissions.AddRange(dto.Permissions.Select(p => new PermissionInfo
            {
                Id = p.Id.ToString(),
                Code = p.Code,
                Module = p.Module
            }));
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MAPPING HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static TenantStatus MapTenantStatus(string status) => status switch
    {
        "TENANT_STATUS_ACTIVE" or "ACTIVE" => TenantStatus.Active,
        "TENANT_STATUS_SUSPENDED" or "SUSPENDED" => TenantStatus.Suspended,
        _ => TenantStatus.Unspecified 
    };

    private static UserStatus MapUserStatus(string status) => status switch
    {
        "USER_STATUS_ACTIVE" or "ACTIVE" => UserStatus.Active,
        "USER_STATUS_INACTIVE" or "INACTIVE" => UserStatus.Inactive,
        "USER_STATUS_BLOCKED" or "BLOCKED" => UserStatus.Blocked,
        _ => UserStatus.Unspecified
    };

    private static RoleResponse MapRoleResponse(Application.DTOs.Roles.RoleDto dto)
    {
        var r = new RoleResponse
        {
            Id = dto.Id.ToString(),
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            CreatedAt = Timestamp.FromDateTimeOffset(dto.CreatedAt)
        };
        r.PermissionIds.AddRange(dto.PermissionIds);
        return r;
    }
}
