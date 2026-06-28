# BFF Gateway — Comprehensive Production Readiness Review

> **Reviewer Role:** Principal Solution Architect · Security Architect · Platform Engineer · DevOps Architect · Observability Engineer  
> **Target:** AWS EKS Production SaaS Multi-Tenant  
> **Review Date:** 2026-06-29

---

## Overall Score

| Dimension | Score | Verdict |
|-----------|-------|---------|
| Architecture | **7/10** | Solid foundation, minor role leakage |
| Security | **5/10** | ⚠️ Critical gaps requiring immediate fix |
| Authentication | **6/10** | Good direction, execution gaps |
| Authorization | **5/10** | ⚠️ Bypass risk present |
| Multi-Tenant | **5/10** | ⚠️ Tenant Breakout risk exists |
| Observability | **4/10** | ❌ Incomplete — not production-ready |
| Performance | **6/10** | Good base, Redis single-point risk |
| Scalability | **6/10** | Stateless but rate-limit state problem |
| Production Readiness | **4/10** | ❌ Missing critical ops components |

**Overall: 5.3/10 — NOT PRODUCTION READY**

---

## 1. Architecture Review

### Verdict: 7/10 — Good Foundation, Minor Issues

**✅ Đúng vai trò BFF:**
- BFF đúng chỗ xử lý Cookie ↔ JWT translation — đây là BFF responsibility.
- Delegate gRPC calls về IAM — đúng.
- YARP proxy các service khác — đúng pattern.

**⚠️ Business Logic Leak (Minor):**
- `PermissionVersionMiddleware` thực hiện logic nghiệp vụ (kiểm tra phiên bản quyền) trực tiếp tại BFF. Đây borderline acceptable vì đây là **session validation**, không phải domain logic. Tuy nhiên, nếu logic này phức tạp hơn, cần chuyển về service.
- `AuthorizationBehavior` trong MediatR pipeline đọc từ `currentUser.Permissions` — nhưng `Permissions` list đang được khởi tạo là `[]` rỗng tại `CurrentUserContextMiddleware` (line 39). **Điều này có nghĩa Authorization Behavior sẽ KHÔNG BAO GIỜ kiểm tra được permission thực sự.**

**❌ Anti-pattern — Double JWT Parsing:**
- `UseAuthentication()` (JwtBearer) và `CurrentUserContextMiddleware` đều parse JWT riêng rẽ.
- JwtBearer đã validate + parse → đặt `context.User.Claims`.
- `CurrentUserContextMiddleware` lại dùng `JwtSecurityTokenHandler` để parse token **lần nữa** từ cookie.
- **Fix:** Đọc từ `context.User.Claims` thay vì re-parse raw token.

**❌ MediatR CQRS không có Handler nào:**
- `RegisterServicesFromAssembly` đăng ký nhưng không có Command/Query nào trong BFF assembly. MediatR đang chạy idle, adding overhead cho mọi request.
- Nếu không có BFF-internal CQRS, bỏ MediatR đi. Nếu có plan, thì để lại nhưng document rõ.

**❌ gRPC Client không dùng `ClientMetadataInterceptor`:**
- `AuthBffService` gọi gRPC sang IAM nhưng không attach `ClientMetadataInterceptor` từ Shared. Metadata propagation (trace-id, correlation-id) bị mất ở hop này.

---

## 2. Authentication Review

### Verdict: 6/10 — Direction Correct, Execution Gaps

### ✅ Điểm tốt
- HttpOnly Cookie + SameSite=Lax — đúng approach cho SPA trên cùng domain.
- Refresh token có path restriction `/auth/refresh` — đúng.
- `Secure=true` in production — đúng.
- BFF không gọi Cognito trực tiếp — đúng separation.

### 🚨 BLOCKER: Token Not Validated — Only Parsed

```csharp
// CurrentUserContextMiddleware.cs line 27
var jwt = handler.ReadJwtToken(token); // ← CHỈ PARSE, KHÔNG VALIDATE
```

**`ReadJwtToken()` không verify signature, không check expiry, không check issuer.**  
`ValidateJwt()` của JwtBearer middleware ở step 6 đã validate, nhưng `currentUser` được populate ở step 7 bằng cách **re-read token mà không validate lần nữa**.

Điều này an toàn chỉ khi JwtBearer middleware đã reject request trước khi đến step 7. Nhưng với anonymous endpoints, một attacker có thể gửi **forged JWT không ký** để inject UserId/TenantId vào CurrentUserContext trên các anonymous routes.

**Fix:** Dùng `context.User.Claims` (đã validated bởi JwtBearer) thay vì re-parse raw cookie.

### 🚨 BLOCKER: Refresh Token Abuse — No User Context Verification

```csharp
// AuthEndpoints.cs line 86-89
var result = await authClient.RefreshTokenAsync(new Auth.Grpc.RefreshTokenRequest
{
    RefreshToken = refreshToken  // ← Không gửi kèm userId, tenantId để IAM verify
});
```

Khi refresh, BFF gửi đúng refresh_token nhưng không verify nó có thuộc user đang request hay không. Nếu attacker lấy được refresh_token cookie (qua các attack khác), chúng có thể refresh từ bất kỳ đâu.

**Fix:** IAM cần verify refresh_token belongs to claim user. Ngoài ra BFF nên kiểm tra `userId` từ current session (nếu có) khớp với token.

### ⚠️ HIGH: Logout Không Server-Side Revoke

```csharp
// AuthEndpoints.cs line 108
ClearAuthCookies(context); // ← Chỉ xóa cookie phía client
```

Logout chỉ delete cookie. Không có:
- Cognito `GlobalSignOut` revoke
- Redis blacklist token
- Refresh token invalidation ở IAM

**Attack scenario:** User logout → attacker vẫn còn access/refresh token còn TTL → dùng được.

### ⚠️ HIGH: ClockSkew Quá Rộng

```csharp
ClockSkew = TimeSpan.FromSeconds(30) // Acceptable
```

30 giây OK. Nhưng cần đảm bảo server time sync qua NTP (EKS nodes có sẵn, nhưng document lại để ops team biết).

### ⚠️ MEDIUM: Login Response Leaks Internal IDs

```csharp
return Results.Ok(new {
    UserId = result.UserId,      // ← Expose internal UserId
    TenantId = result.TenantId, // ← Expose internal TenantId
    ExpiresIn = result.ExpiresIn
});
```

`UserId` và `TenantId` là internal identifiers. Frontend không cần biết raw Guids này từ login response — chúng đã trong JWT Cookie. Bỏ `UserId`, `TenantId` khỏi response hoặc dùng opaque reference.

### ⚠️ MEDIUM: Cookie Domain Để Trống

```json
"Auth": { "CookieDomain": "" }
```

Domain trống = cookie bind với exact host. Nếu BFF scale với multiple subdomains hoặc deploy multi-region, cần set domain rõ ràng. Cần document và env-specific configuration rõ.

---

## 3. Authorization Review

### Verdict: 5/10 — Authorization Bypass Risk

### 🚨 BLOCKER: Permission List Luôn Rỗng

```csharp
// CurrentUserContextMiddleware.cs line 39
currentUser.Populate(userId, tenantId, traceId, permVersion, roleIds, []); // ← permissions = []
```

`Permissions` được populate với list rỗng. `AuthorizationBehavior` check:
```csharp
if (!currentUser.Permissions.Contains(permReq.RequiredPermission))
    throw new ForbiddenException(...);
```

**Kết quả:** Mọi permission check đều FAIL → `ForbiddenException` cho mọi protected route. Hoặc nếu không có route nào implement `IRequirePermission`, authorization không được kiểm tra.

**Fix:** Load permissions từ Redis cache sau khi validate JWT, hoặc decode từ JWT claims nếu permissions được embed vào token.

### ⚠️ HIGH: Authorization Ở BFF Vs Service

**Hiện tại:** BFF làm coarse-grained check (nếu permissions được load đúng), downstream services làm fine-grained check qua `AuthInterceptor`.

**Verdict:** Architecture này đúng. Nhưng cần rõ ràng:
- BFF: Route-level authorization (user authenticated? has basic role?)
- Service: Resource-level authorization (có quyền với resource cụ thể không?)

### ⚠️ MEDIUM: Direct Service Access Risk

Nếu downstream gRPC services không được bảo vệ bởi network policy, attacker có thể bypass BFF và gọi thẳng service với forged gRPC metadata. Cần:
- Kubernetes NetworkPolicy chỉ cho BFF pod gọi Service pods.
- Service phải validate metadata không thể tự forge (mTLS hoặc shared secret header từ BFF).

---

## 4. Multi-Tenant Security Review

### Verdict: 5/10 — Tenant Breakout Risk

### 🚨 BLOCKER: TenantId Có Thể Bị Override

```csharp
// GrpcMetadataPropagationMiddleware.cs
context.Request.Headers[GrpcMetadataKeys.TenantId] = currentUser.TenantId.ToString();
```

Headers được SET vào `context.Request.Headers`. Nếu attacker gửi request với `x-tenant-id` header tự đặt TRƯỚC khi vào pipeline, middleware này sẽ **override đúng** — tuy nhiên cần đảm bảo YARP không forward headers gốc từ client.

**Attack scenario:** Client gửi `x-tenant-id: <victim-tenant-id>` → nếu không sanitize, header này có thể leaking qua YARP.

**Fix:** Cần `RequestHeaderRemove` transform trong YARP config để strip tất cả `x-*` internal headers từ client request trước khi middleware set chúng.

```json
"Transforms": [
  { "RequestHeaderRemove": "x-user-id" },
  { "RequestHeaderRemove": "x-tenant-id" },
  { "RequestHeaderRemove": "x-trace-id" },
  { "RequestHeaderRemove": "x-permission-version" },
  { "RequestHeaderRemove": "x-role-ids" }
]
```

### 🚨 BLOCKER: X-Resolved-Tenant Leaks TenantId ra Client

```csharp
// TenantResolutionMiddleware.cs line 33
context.Response.Headers["X-Resolved-Tenant"] = currentUser.TenantId.ToString();
```

TenantId được expose trong **response header** về client. Đây là thông tin nhạy cảm — attacker có thể harvest TenantIds bằng cách gửi nhiều requests. **Remove hoàn toàn** header này.

### ⚠️ HIGH: Missing Tenant Validation

```csharp
// TenantResolutionMiddleware.cs line 23-28
if (context.User.Identity?.IsAuthenticated == true && !currentUser.TenantId.HasValue)
{
    logger.LogWarning("Authenticated user missing TenantId claim...");
    // Không reject — SystemAdmin có thể không có TenantId
}
```

Không reject khi TenantId thiếu là nguy hiểm nếu service không handle `null` TenantId đúng cách. Cần:
- Verify user thực sự là SystemAdmin trước khi allow `null` TenantId.
- Non-SystemAdmin mà thiếu TenantId → reject 403.

### ⚠️ HIGH: Tenant Validation Không Check Tenant Status

BFF không verify TenantId có ACTIVE không. Một Tenant đã bị suspend vẫn có thể access nếu còn JWT hợp lệ.

**Fix:** Check Tenant status từ Redis cache (TTL 5 phút) hoặc thêm TenantStatus vào JWT claims.

---

## 5. Middleware Pipeline Review

### Verdict: 6/10 — Thứ tự tốt, thiếu critical middleware

### ✅ Thứ tự Correct:
```
Exception → CorrelationId → SecurityHeaders → CORS → RateLimit → Auth → 
CurrentUser → Tenant → PermissionVersion → GrpcMetadata → Authorization → Endpoints
```

### 🚨 BLOCKER: Authorization Đặt Sau GrpcMetadataPropagation

`UseAuthorization()` ở bước 11, SAU `GrpcMetadataPropagationMiddleware` (bước 10). Điều này có nghĩa metadata đã được propagate trước khi authorization check xong. Không gây lỗi trực tiếp vì YARP chỉ forward sau khi pipeline hoàn thành, nhưng thứ tự semantically sai.

**Fix:** `UseAuthorization()` phải đứng TRƯỚC `GrpcMetadataPropagationMiddleware`.

### ❌ Thiếu: Request Size Limit

```csharp
// MISSING
builder.Services.Configure<FormOptions>(x => x.MultipartBodyLengthLimit = 10 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(opts => opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
```

Không có giới hạn request body → DOS attack bằng large payloads.

### ❌ Thiếu: Request Timeout

```csharp
// MISSING
builder.Services.AddRequestTimeouts(opts =>
    opts.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(30) });
```

Không có timeout → slow upstream service có thể exhaust thread pool.

### ❌ Thiếu: Input Sanitization / Path Traversal Protection

YARP đang proxy tất cả path dưới `/api/iam/**` mà không sanitize. Path traversal (`../`) hoặc null bytes trong path cần được handle.

### ❌ Thiếu: /metrics Endpoint Không Được Bảo Vệ

```csharp
app.MapPrometheusScrapingEndpoint("/metrics"); // ← Public, không auth
```

`/metrics` expose internal system data (request counts, memory, etc.). Phải restrict: chỉ Prometheus scraper (cluster-internal) được access. Dùng NetworkPolicy hoặc IP whitelist.

### ❌ Thiếu: /readyz Và /healthz Có Thể Leak Info

Health endpoints hiện trả về `service: "bff-gateway"`. Cần đảm bảo không leak sensitive info (DB connection strings, internal IPs, etc.) — hiện tại ok nhưng cần monitor khi mở rộng.

### ❌ Thiếu: Anti-CSRF

Với SameSite=Lax, CSRF với cross-origin POST requests từ form submission vẫn có risk trong một số browser configurations. Cần CSRF token cho state-changing operations nếu hỗ trợ non-JavaScript clients.

---

## 6. gRPC Communication Review

### Verdict: 5/10 — Trust Boundary Chưa Được Thiết Lập

### 🚨 BLOCKER: Không Có mTLS / Service Identity

```csharp
// Program.cs
ch.UnsafeUseInsecureChannelCallCredentials = true; // ← Plaintext gRPC
```

Trên EKS, communication giữa BFF và services là HTTP/2 không TLS. Nếu không có:
- **mTLS** (Istio/Linkerd service mesh), hoặc
- **Kubernetes NetworkPolicy** hạn chế traffic,

Bất kỳ pod nào trong cluster đều có thể giả mạo gRPC request với forged metadata.

**Fix (ưu tiên):** Deploy Istio với PeerAuthentication `STRICT` mode cho IAM + service namespaces.

### 🚨 BLOCKER: Header Spoofing — Services Tin Tưởng Metadata Hoàn Toàn

Downstream services dùng `AuthInterceptor` để populate `ICurrentUserService` từ metadata headers. Không có mechanism nào để verify headers này đến từ trusted source (BFF) hay từ attacker đã bypass BFF.

**Attack scenario:** Attacker trong cluster gửi gRPC request trực tiếp với `x-user-id` của admin → service chấp nhận.

### ⚠️ HIGH: Không Có Retry Policy / Circuit Breaker

```csharp
builder.Services.AddGrpcClient<AuthService.AuthServiceClient>(opts => {
    opts.Address = new Uri(iamUrl);
}); // ← No retry, no circuit breaker
```

Nếu IAM service down, BFF sẽ fail mọi auth request ngay lập tức mà không có graceful degradation.

**Fix:** Thêm Polly hoặc gRPC built-in retry:
```csharp
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
    EnableMultipleHttp2Connections = true
})
```

### ⚠️ HIGH: gRPC Timeout Không Được Cấu Hình

Không có deadline trên gRPC calls. Nếu IAM service hung, gRPC call sẽ block indefinitely.

```csharp
// AuthEndpoints.cs — MISSING deadline
var result = await authClient.LoginAsync(grpcRequest); 
// Should be: authClient.LoginAsync(grpcRequest, deadline: DateTime.UtcNow.AddSeconds(5))
```

### ⚠️ MEDIUM: x-correlation-id Không Được Propagate Qua gRPC

`GrpcMetadataPropagationMiddleware` propagate `x-trace-id` nhưng thiếu `x-correlation-id`. Loki logs giữa BFF và IAM sẽ không liên kết được.

---

## 7. OpenTelemetry & Observability Review

### Verdict: 4/10 — Incomplete, Not Production-Ready

### Distributed Tracing

**❌ gRPC Instrumentation Thiếu:**
```csharp
tracing.AddAspNetCoreInstrumentation()
       .AddHttpClientInstrumentation(); // ← HttpClient chứ không phải gRPC
```

gRPC calls đến IAM qua `Grpc.Net.Client` sử dụng HttpClient internally — `AddHttpClientInstrumentation` capture được, nhưng không propagate W3C TraceContext headers vào gRPC metadata. Trace sẽ bị BROKEN ở BFF→IAM hop.

**Fix:** Thêm `GrpcClientInstrumentation` và đảm bảo Activity propagation:
```csharp
tracing.AddGrpcClientInstrumentation()
```

**❌ TraceId Không Được Log Tới Loki:**
Không có structured logging được cấu hình. `ILogger` mặc định của ASP.NET không tự gắn `TraceId` vào logs trừ khi dùng `ActivityTraceId`. Cần Serilog/OpenTelemetry Logging với enricher.

### Metrics

**❌ RED Metrics Thiếu Custom Dimensions:**
```csharp
metrics.AddAspNetCoreInstrumentation()  // Basic HTTP metrics
       .AddRuntimeInstrumentation();     // Runtime metrics
```

Thiếu custom metrics theo domain:
- `bff_auth_login_total{status="success|failure"}`
- `bff_auth_refresh_total{status="success|failure"}`
- `bff_permission_version_mismatch_total`
- `bff_tenant_resolution_failure_total`
- `bff_grpc_iam_latency_histogram{method="..."}`

**❌ /metrics Không Phân Biệt Per-Tenant:**
Không có tenant label trên metrics → không thể detect "Tenant X đang có unusual traffic" trong Grafana.

### Logging

**❌ Không Có Structured Logging:**
Dùng `ILogger` mặc định. Trong production với Loki, cần Serilog với `outputTemplate` JSON để Loki parse được fields.

**❌ UserId/TenantId Không Được Enrich Vào Logs:**
```csharp
// ExceptionHandlingMiddleware.cs
logger.LogError(ex, "Unhandled exception for {Method} {Path}. CorrelationId: {CorrelationId}"...);
// Missing: TenantId, UserId trong log context
```

**Fix:** Dùng `ILogger` scope enrichment:
```csharp
using var scope = logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = currentUser.UserId?.ToString() ?? "anonymous",
    ["TenantId"] = currentUser.TenantId?.ToString() ?? "none",
    ["CorrelationId"] = context.TraceIdentifier
});
```

### Alerting

**❌ Không Có Alerting Rules:**
Không có PrometheusRule CRDs hay AlertManager config. Cần alerts cho:
- Auth failure rate > 10% trong 5 phút
- P99 latency > 2s
- Redis connection failure
- IAM gRPC error rate > 5%
- Rate limit rejection rate spike

### Grafana Dashboard

**❌ Không Có Dashboard:**
Không có Grafana dashboard definition (JSON/Helm). Không có SLO definition.

---

## 8. CloudWatch Cost Optimization Review

### Verdict: OK (Nếu Cấu Hình Đúng)

**Current State:** Không có code nào push logs lên CloudWatch từ BFF. Nếu đúng như design intent, thì:

✅ **Đúng:** BFF logs → Loki (qua OpenTelemetry Collector hoặc Promtail)
✅ **Đúng:** CloudWatch chỉ nhận EKS Control Plane, Node, AWS native logs

**⚠️ Risk:** Nếu `aws-logging` sidecar hoặc Fluentd DaemonSet được cài trên EKS nodes (thường là mặc định với nhiều EKS setup), **tất cả pod logs sẽ tự động vào CloudWatch** dù bạn không muốn.

**Recommended Actions:**
1. Disable `aws-logging` configmap cho BFF namespace, giữ chỉ cho kube-system.
2. Dùng `--log-driver=none` hoặc exclude BFF log group khỏi CloudWatch.
3. Retention policy cho EKS Control Plane logs: 30 days (không cần hơn).
4. Estimated saving: $50–200/month tùy traffic nếu logs không duplicate.

---

## 9. Performance & Scalability Review

### Verdict: 6/10 — Good Base, Real Bottlenecks

### ✅ Điểm tốt
- Stateless design — có thể horizontal scale.
- YARP async non-blocking.
- Sliding Window Rate Limiter per IP — đúng.

### ⚠️ HIGH: Redis Single Point of Failure

`PermissionVersionMiddleware` gọi Redis trên EVERY authenticated request:
```csharp
var cached = await permissionCache.GetAsync(currentUser.UserId.Value);
```

Tại 1,000 rps với 100% authenticated → **1,000 Redis reads/sec**. Nếu Redis down:
- `GetAsync` throw → request fail (nếu không có try/catch)
- Toàn bộ authenticated traffic blocked

**Fix:** 
1. Wrap với try/catch — nếu Redis fail, degrade gracefully (skip version check, log warning).
2. Dùng Redis Cluster hoặc ElastiCache Multi-AZ.
3. Local memory cache với short TTL (1-2s) để reduce Redis pressure.

### ⚠️ HIGH: Rate Limiter Không Distributed

```csharp
opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => ...);
```

Sliding Window Rate Limiter dùng in-memory state. Với 3 BFF pods, mỗi pod có limit riêng. User có thể gửi 100 req/min đến pod 1, 100 đến pod 2, 100 đến pod 3 = **300 req/min**.

**Fix:** Dùng Redis-backed rate limiter:
```csharp
// Dùng RedisRateLimiter hoặc custom IDistributedRateLimiter
```
Hoặc delegate rate limiting về AWS ALB WAF (recommended ở scale).

### ⚠️ MEDIUM: Double JWT Parse Overhead

Mỗi request JWT được parse 2 lần (JwtBearer + CurrentUserContextMiddleware). Tại high throughput, đây là measurable overhead.

### ⚠️ MEDIUM: gRPC Channel Không Được Pooled

Mỗi `AddGrpcClient` tạo channel per-request lifecycle (Scoped). Cần verify channel được reused properly. gRPC channels nên là Singleton.

**Fix:** Đổi sang Singleton gRPC clients với proper channel management.

---

## 10. Production Readiness Review

### Verdict: 4/10 — Critical Gaps

### ❌ Không Có Kubernetes Manifests

Thiếu:
- `Deployment.yaml` với readinessProbe/livenessProbe
- `Service.yaml`
- `HorizontalPodAutoscaler.yaml`
- `PodDisruptionBudget.yaml` (ZDD requirement)
- `NetworkPolicy.yaml`
- `ServiceAccount.yaml` với IRSA annotations

### ❌ Không Có Secret Management

Config nhạy cảm (JWT authority, Redis connection) được đọc từ environment variables. Cần:
- AWS Secrets Manager + External Secrets Operator
- Hoặc AWS SSM Parameter Store
- Không hardcode secrets trong ConfigMap

### ❌ Không Có Health Probe Integration

```csharp
// HealthEndpoints.cs — không check downstream
app.MapGet("/readyz", async ... => { await Task.CompletedTask; return Results.Ok(...) });
```

`/readyz` không check Redis connection, IAM gRPC health → pod được mark Ready khi thực tế chưa có thể serve traffic.

**Fix:** Dùng `Microsoft.Extensions.Diagnostics.HealthChecks`:
```csharp
builder.Services.AddHealthChecks()
    .AddRedis(redisConn)
    .AddGrpcService("iam-grpc", ...);
```

### ❌ Không Có Graceful Shutdown

Không có `app.Lifetime.ApplicationStopping` handler để drain in-flight requests. Cần:
```csharp
builder.Services.Configure<HostOptions>(opts => opts.ShutdownTimeout = TimeSpan.FromSeconds(30));
```

### ❌ Không Có Structured Startup Validation

Nếu Redis không available khi start, app crash với unhandled exception. Cần startup health checks.

---

## 11. Critical Security Assessment

### 🚫 BLOCKERS — Phải Fix Trước Khi Deploy

| # | Issue | Location | Impact |
|---|-------|----------|--------|
| 1 | **JWT not validated in CurrentUserContext** — dùng `ReadJwtToken` không verify signature | `CurrentUserContextMiddleware.cs:27` | Auth Bypass trên anonymous routes |
| 2 | **Permission list luôn rỗng** — permissions `[]` → Authorization Behavior vô dụng | `CurrentUserContextMiddleware.cs:39` | Authorization Bypass toàn bộ |
| 3 | **Client headers x-tenant-id không bị strip** — attacker có thể inject tenant context | `GrpcMetadataPropagationMiddleware` + YARP config | **Tenant Breakout** |
| 4 | **X-Resolved-Tenant trong response** — expose TenantId ra client | `TenantResolutionMiddleware.cs:33` | TenantId Enumeration |
| 5 | **Không có mTLS** — services tin forged gRPC metadata | Infrastructure | **Header Spoofing / Auth Bypass** |
| 6 | **Logout không revoke token** — server-side session vẫn valid | `AuthEndpoints.cs:108` | Session Hijacking sau Logout |

### ⚠️ HIGH RISK — Fix Trước GA

| # | Issue | Impact |
|---|-------|--------|
| 7 | Refresh token không verify user context | Refresh Token Abuse |
| 8 | Không có Circuit Breaker / Retry trên gRPC | Cascading Failure |
| 9 | gRPC deadline không được set | Thread Pool Exhaustion |
| 10 | Rate Limiter in-memory (không distributed) | Rate Limit Bypass khi scale |
| 11 | Redis failure có thể block toàn bộ auth traffic | Total Outage |
| 12 | Missing NetworkPolicy cho service-to-service | Lateral Movement |
| 13 | Không có Request Timeout middleware | Slow Loris / DOS |
| 14 | Không có Request Size Limit | Large Payload DOS |

### ⚡ MEDIUM RISK — Improve Before Scale

| # | Issue | Impact |
|---|-------|--------|
| 15 | Double JWT parsing overhead | Performance |
| 16 | TraceContext không propagate qua gRPC | Broken distributed tracing |
| 17 | Không có custom RED metrics per tenant | Blind to tenant-level anomalies |
| 18 | Login response expose UserId/TenantId | Information leakage |
| 19 | Không có Structured JSON logging | Loki parsing failure |
| 20 | Health check không test downstream services | False Ready state |

---

## Recommended Refactoring

### Architecture
- [ ] Bỏ Double JWT Parse: đọc từ `context.User.Claims` trong `CurrentUserContextMiddleware`
- [ ] Permissions phải được load từ Redis hoặc JWT claims, không để rỗng
- [ ] gRPC clients nên Singleton với proper channel reuse

### Security
- [ ] Strip internal headers (`x-user-id`, `x-tenant-id`, etc.) từ client requests trong YARP transforms
- [ ] Remove `X-Resolved-Tenant` response header
- [ ] Implement server-side token revocation (Redis blacklist hoặc Cognito GlobalSignOut)
- [ ] Add IRSA + Secrets Manager cho credential management

### Middleware
- [ ] Thêm `RequestTimeoutMiddleware` (30s default, 5s cho auth)
- [ ] Thêm Request Body Size Limit (10MB max)
- [ ] Đổi thứ tự: Authorization TRƯỚC GrpcMetadataPropagation
- [ ] Restrict `/metrics` endpoint — internal NetworkPolicy only

### OpenTelemetry
- [ ] Thêm `AddGrpcClientInstrumentation()`  
- [ ] Thêm W3C TraceContext propagation vào gRPC metadata
- [ ] Cài Serilog với JSON formatter + OpenTelemetry sink cho Loki
- [ ] Add `BeginScope` enrichment với UserId, TenantId, CorrelationId

### Prometheus / Custom Metrics
```csharp
// Thêm custom counters
private static readonly Counter<long> _loginTotal = 
    _meter.CreateCounter<long>("bff_auth_login_total");
private static readonly Histogram<double> _iamLatency =
    _meter.CreateHistogram<double>("bff_grpc_iam_duration_seconds");
```

### Loki Logging
- Serilog → `Serilog.Sinks.OpenTelemetry` → OTel Collector → Loki
- Log format: JSON với fields: `timestamp`, `level`, `message`, `traceId`, `spanId`, `userId`, `tenantId`, `correlationId`, `service`, `path`, `statusCode`

### CloudWatch Cost
- Disable `aws-logging` ConfigMap cho BFF/IAM namespace
- Retain CloudWatch only: `kube-apiserver`, `audit`, node logs
- Set retention: EKS control plane = 30 days, Node logs = 7 days
- Estimated saving: 60-80% CloudWatch cost reduction

### Production Infrastructure
```yaml
# Cần thêm vào Helm chart / manifests
resources:
  requests: { cpu: "250m", memory: "256Mi" }
  limits: { cpu: "1000m", memory: "512Mi" }

readinessProbe:
  httpGet: { path: /readyz, port: 8080 }
  initialDelaySeconds: 10
  periodSeconds: 5

livenessProbe:
  httpGet: { path: /healthz, port: 8080 }
  periodSeconds: 10

podDisruptionBudget:
  minAvailable: 1
```

---

## Final Verdict

> ### ❌ NOT READY FOR PRODUCTION

**Lý do:**

1. **Security Blockers (6 issues):** Tồn tại khả năng Tenant Breakout qua header injection, Authorization Bypass do permissions rỗng, và Session Hijacking do thiếu server-side logout — đây là lỗi nghiêm trọng không thể chấp nhận trên SaaS multi-tenant.

2. **Observability Incomplete:** Không có structured logging, distributed tracing bị broken ở BFF→IAM hop, không có custom metrics, không có alerting rules → **vận hành mù** trong production.

3. **Production Infrastructure Missing:** Không có K8s manifests, không có proper health checks, không có graceful shutdown → deployment không an toàn.

4. **Scalability Risk:** In-memory rate limiter và Redis SPOF có thể gây outage khi scale horizontal.

**Estimated Fix Time:** 2–3 sprints (2 tuần) để resolve blockers + high-priority items.

**Ưu tiên sửa (theo thứ tự):**
1. Strip client-supplied `x-*` headers trong YARP (30 phút)
2. Fix JWT validation trong CurrentUserContext — dùng `context.User.Claims` (2 giờ)
3. Load permissions từ Redis/JWT claims (4 giờ)
4. Remove `X-Resolved-Tenant` header (15 phút)
5. Implement server-side logout revocation (1 ngày)
6. Add mTLS via Istio hoặc NetworkPolicy (1 ngày — infra)
7. Serilog + OTel structured logging (1 ngày)
8. Redis-backed rate limiter hoặc WAF delegation (1 ngày)
9. gRPC Circuit Breaker + Deadline (4 giờ)
10. K8s manifests + Health checks (1 ngày)
