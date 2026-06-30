using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminBff.Controllers;

/// <summary>
/// Base Controller dành cho Tenant Admin.
/// Prefix mặc định: /api/v1/admin/[tên-controller]
/// Chỉ có tài khoản mang role TenantAdmin mới có thể gọi các API này.
/// Quyền chi tiết sẽ được kiểm tra ở từng API bằng [RequirePermission].
/// </summary>
[ApiController]
[Route("api/v1/admin/[controller]")]
[Authorize(Roles = "TenantAdmin")]
public abstract class AdminControllerBase : ControllerBase
{
}
