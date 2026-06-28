using Grpc.Core;
using IamTenant.Application.Commands.Auth;
using IamTenant.Application.Queries.Auth;
using MediatR;
using Auth.Grpc;

namespace IamTenant.GrpcServices;

public class AuthGrpcService(IMediator mediator) : AuthService.AuthServiceBase
{
    public override async Task<IdentifyUserResponse> IdentifyUser(IdentifyUserRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new IdentifyUserQuery(request.Email));

            return new IdentifyUserResponse
            {
                Exists = result.Exists,
                TenantCode = result.TenantCode ?? "",
                UserType = result.UserType ?? ""
            };
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
        }
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new LoginCommand(request.Email, request.Password));

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                UserId = result.UserId,
                TenantId = result.TenantId
            };

            response.Roles.AddRange(result.Roles);
            response.Permissions.AddRange(result.Permissions);

            return response;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
    }

    public override async Task<LoginResponse> CompleteInvitation(CompleteInvitationRequest request, ServerCallContext context)
    {
        try
        {
            var result = await mediator.Send(new CompleteInvitationCommand(request.Email, request.NewPassword, request.ConfirmationCode));

            var response = new LoginResponse
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresIn = result.ExpiresIn,
                UserId = result.UserId,
                TenantId = result.TenantId
            };

            response.Roles.AddRange(result.Roles);
            response.Permissions.AddRange(result.Permissions);

            return response;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
    }

    public override async Task<LoginResponse> RefreshToken(RefreshTokenRequest request, ServerCallContext context)
    {
        try
        {
            // Assuming we also have a RefreshTokenCommand similar to LoginCommand
            // For now, I'll assume we send a RefreshTokenCommand which returns a LoginResult
            // var result = await mediator.Send(new RefreshTokenCommand(request.RefreshToken));
            // return new LoginResponse { ... };

            // To make it compile without creating the MediatR command yet, just throw Unimplemented
            throw new RpcException(new Status(StatusCode.Unimplemented, "Refresh Token command not yet implemented"));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
    }

    // Other methods like ForgotPassword...
}
