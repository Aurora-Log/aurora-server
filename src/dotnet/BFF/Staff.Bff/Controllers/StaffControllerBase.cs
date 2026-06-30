using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StaffBff.Controllers;

/// <summary>
/// Base Controller dành cho các nghiệp vụ chung (Staff).
/// Prefix mặc định: /api/v1/[tên-controller]
/// Bắt buộc đăng nhập. Phân quyền chi tiết ở từng action bằng [RequirePermission].
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public abstract class StaffControllerBase : ControllerBase
{
}
