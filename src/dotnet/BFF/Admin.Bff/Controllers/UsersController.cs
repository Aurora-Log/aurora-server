using BuildingBlocks.BFF.Attributes;
using Common.Grpc;
using Grpc.Core;
using Shared.Constants;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace AdminBff.Controllers;

[Route("api/v1/admin/users")]
public class UsersController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<UsersController> logger) : AdminControllerBase
{
    [HttpPost("invite")]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Create)]
    public async Task<IActionResult> InviteUser([FromBody] InviteUserBody body)
    {
        try
        {
            var response = await iamClient.InviteUserAsync(
                new InviteUserRequest
                {
                    FirstName   = body.FirstName,
                    LastName    = body.LastName,
                    Email       = body.Email,
                    PhoneNumber = body.PhoneNumber ?? string.Empty,
                    RoleIds     = { body.RoleIds }
                });

            logger.LogInformation(
                "User {Email} invited to tenant {TenantId} by {AdminId}",
                body.Email, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/users/{response.Id}", MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = "A user with this email already exists in the tenant." });
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Read)]
    public async Task<IActionResult> GetUser([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetUserAsync(
                new GetUserRequest { Id = id });

            return Ok(MapUserResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"User '{id}' not found." });
        }
    }

    // --- DTOs ---
    public record InviteUserBody(string FirstName, string LastName, string Email, string? PhoneNumber, List<string> RoleIds);

    private static object MapUserResponse(UserResponse r) => new
    {
        r.Id,
        r.FirstName,
        r.LastName,
        r.Email,
        r.PhoneNumber,
        Status      = r.Status.ToString(),
        r.RoleIds,
        SystemRoles = r.SystemRoles.Select(sr => sr.ToString()),
        CreatedAt   = r.CreatedAt?.ToDateTime(),
        UpdatedAt   = r.UpdatedAt?.ToDateTime()
    };
}
