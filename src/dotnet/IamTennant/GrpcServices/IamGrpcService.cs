using Grpc.Core;
using iam;
using MediatR;

namespace iam_tennant.GrpcServices;

public class IamGrpcService(IMediator mediator) : IamService.IamServiceBase
{
    public override async Task<TenantResponse> CreateTenant(CreateTenantRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<TenantResponse> GetTenant(GetTenantRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<TenantResponse> UpdateTenantStatus(UpdateTenantStatusRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<UserResponse> InviteUser(InviteUserRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<UserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<GetManyUsersResponse> GetManyUsers(GetManyUsersRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<UserResponse> AssignRoles(AssignRolesRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<UserResponse> SuspendUser(SuspendUserRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<RoleResponse> CreateCustomRole(CreateCustomRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<RoleResponse> GetRole(GetRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<GetManyRolesResponse> GetManyRoles(GetManyRolesRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<RoleResponse> UpdateRole(UpdateRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<EmptyResponse> DeleteRole(DeleteRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<RoleResponse> AssignPermissionsToRole(AssignPermissionsToRoleRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }

    public override async Task<GetUserPermissionsResponse> GetUserPermissions(GetUserPermissionsRequest request, ServerCallContext context)
    {
        throw new RpcException(new Status(StatusCode.Unimplemented, "Not implemented yet"));
    }
}
