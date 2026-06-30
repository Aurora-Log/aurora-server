using BuildingBlocks.BFF.Attributes;
using Grpc.Core;
using Shared.Constants;
using IamTenant.Grpc;
using Microsoft.AspNetCore.Mvc;
using RoutePlanningAgent;
using Shared.Security;

namespace StaffBff.Controllers;

[Route("api/v1/dashboard")]
public class DashboardController(
    IamService.IamServiceClient iamClient,
    Greeter.GreeterClient routeClient,
    ICurrentUserService currentUser,
    ILogger<DashboardController> logger) : StaffControllerBase
{
    [HttpGet("summary")]
    [RequirePermission(PermissionConstants.Modules.Bff, PermissionConstants.Read)]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            var userId = currentUser.UserId.ToString()!;
            var tenantId = currentUser.TenantId.ToString()!;

            var userTask = iamClient.GetUserAsync(new GetUserRequest { Id = userId }).ResponseAsync;
            
            var routesTask = routeClient.GetManyRoutesAsync(new GetManyRoutesRequest
            {
                TenantId = tenantId,
                Status = "IN_PROGRESS",
                Page = 1,
                Limit = 5
            }).ResponseAsync;

            await Task.WhenAll(userTask, routesTask);

            var userResponse = await userTask;
            var routesResponse = await routesTask;

            var dashboardData = new DashboardSummaryDto
            {
                UserProfile = new UserProfileDto(
                    userResponse.FirstName, 
                    userResponse.LastName, 
                    userResponse.Email),
                
                ActiveRoutesCount = routesResponse.TotalItems,
                
                RecentActiveRoutes = routesResponse.Routes.Select(r => new RouteShortDto(
                    r.Id, 
                    r.Origin, 
                    r.Destination, 
                    r.Status)).ToList()
            };

            logger.LogInformation("Aggregated dashboard data for user {UserId}", userId);

            return Ok(dashboardData);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "gRPC Error aggregating dashboard data");
            return StatusCode(StatusCodes.Status500InternalServerError, new { detail = "Internal server error connecting to services." });
        }
    }

    // --- Aggregated DTOs ---
    public record DashboardSummaryDto
    {
        public required UserProfileDto UserProfile { get; init; }
        public int ActiveRoutesCount { get; init; }
        public required List<RouteShortDto> RecentActiveRoutes { get; init; }
    }

    public record UserProfileDto(string FirstName, string LastName, string Email);
    public record RouteShortDto(string RouteId, string Origin, string Destination, string Status);
}
