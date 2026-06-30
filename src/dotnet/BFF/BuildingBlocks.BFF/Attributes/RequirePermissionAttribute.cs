using Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Security;


namespace BuildingBlocks.BFF.Attributes;

/// <summary>
/// Áp dụng trên Class hoặc Method để yêu cầu người dùng phải có quyền nhất định.
/// Ví dụ: [RequirePermission(PermissionConstants.Modules.Iam, PermissionConstants.Read)]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute(string module, string action) : Attribute, IAsyncAuthorizationFilter
{
    public string RequiredPermission { get; } = PermissionConstants.Build(module, action);

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // 1. Nếu endpoint có [AllowAnonymous] thì bỏ qua
        if (context.ActionDescriptor.EndpointMetadata.Any(em => em.GetType() == typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute)))
        {
            return Task.CompletedTask;
        }

        // 2. Lấy ICurrentUserService từ DI container
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        // 3. Nếu chưa đăng nhập (không có UserId)
        if (!currentUser.UserId.HasValue)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        // 4. Nếu user có Role là SYSTEM_ADMIN, tự động bypass (Super Admin)
        if (currentUser.RoleIds.Contains("SystemAdmin"))
        {
            return Task.CompletedTask;
        }

        // 5. Kiểm tra xem danh sách quyền của User có chứa quyền yêu cầu không
        if (!currentUser.Permissions.Contains(RequiredPermission))
        {
            // Trả về 403 Forbidden kèm theo thông báo chi tiết
            context.Result = new ObjectResult(new { detail = $"Missing required permission: {RequiredPermission}" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
