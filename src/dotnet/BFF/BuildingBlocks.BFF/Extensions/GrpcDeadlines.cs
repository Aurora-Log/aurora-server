using Grpc.Core;

namespace BuildingBlocks.BFF.Extensions;

/// <summary>
/// Centralized gRPC deadline configuration.
/// Mọi gRPC call trong BFF phải dùng helper này — không bao giờ gọi gRPC không có deadline.
/// </summary>
public static class GrpcDeadlines
{
    /// <summary>5 giây — Login: IAM có crypto ops (bcrypt), cần thêm thời gian.</summary>
    public static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(5);

    /// <summary>3 giây — Refresh token: Redis lookup + Cognito API, nhanh hơn login.</summary>
    public static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(3);

    /// <summary>10 giây — Business service calls: depends on operation complexity.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>2 giây — Health check pings: phải nhanh để không block EKS probe.</summary>
    public static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Tạo CallOptions với deadline từ thời điểm hiện tại.</summary>
    public static CallOptions WithDeadline(TimeSpan timeout, CancellationToken ct = default)
        => new(deadline: DateTime.UtcNow.Add(timeout), cancellationToken: ct);
}
