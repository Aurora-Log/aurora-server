using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SystemBff.Controllers;

/// <summary>
/// Base Controller dành cho System Admin.
/// Prefix mặc định: /api/v1/system/[tên-controller]
/// Chỉ có tài khoản mang role SystemAdmin mới có thể gọi các API này.
/// </summary>
[ApiController]
[Route("api/v1/system/[controller]")]
[Authorize(Roles = "SystemAdmin")]
public abstract class SystemControllerBase : ControllerBase
{
}
