namespace BFF.Middleware;

/// <summary>
/// Gắn các security headers bảo mật OWASP vào mỗi response.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Ngăn clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Ngăn MIME sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Ngăn XSS (cho browser cũ)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Chỉ cho phép HTTPS trong tương lai (HSTS)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Referrer Policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Không cache response nhạy cảm
        headers["Cache-Control"] = "no-store";

        // Content Security Policy cơ bản (điều chỉnh theo Frontend cụ thể)
        headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none';";

        // Loại bỏ Server header để ẩn thông tin stack
        headers.Remove("Server");

        await next(context);
    }
}
