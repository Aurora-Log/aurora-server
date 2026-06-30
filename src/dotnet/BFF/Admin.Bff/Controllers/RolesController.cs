using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Shared.Constants;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using Shared.Security;

namespace AdminBff.Controllers;

[Route("api/v1/admin/roles")]
public class RolesController(
    IamService.IamServiceClient iamClient,
    ICurrentUserService currentUser,
    ILogger<RolesController> logger) : AdminControllerBase
{
    [HttpPost]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Create)]
    public async Task<IActionResult> CreateCustomRole([FromBody] CreateRoleBody body)
    {
        try
        {
            var response = await iamClient.CreateCustomRoleAsync(
                new CreateCustomRoleRequest
                {
                    Code        = body.Code,
                    Name        = body.Name,
                    Description = body.Description ?? string.Empty
                });

            logger.LogInformation(
                "Custom role '{Code}' created in tenant {TenantId} by {AdminId}",
                body.Code, currentUser.TenantId, currentUser.UserId);

            return Created($"/api/v1/admin/roles/{response.Id}", MapRoleResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            return Conflict(new { detail = $"Role code '{body.Code}' already exists." });
        }
    }

    [HttpGet("{id}")]
    [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Read)]
    public async Task<IActionResult> GetRole([FromRoute] string id)
    {
        try
        {
            var response = await iamClient.GetRoleAsync(
                new GetRoleRequest { Id = id });

            return Ok(MapRoleResponse(response));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = $"Role '{id}' not found." });
        }
    }

    // --- DTOs ---
    public record CreateRoleBody(string Code, string Name, string? Description);

    private static object MapRoleResponse(RoleResponse r) => new
    {
        r.Id,
        r.Code,
        r.Name,
        r.Description,
        r.PermissionIds,
        CreatedAt = r.CreatedAt?.ToDateTime(),
        UpdatedAt = r.UpdatedAt?.ToDateTime()
    };
}
