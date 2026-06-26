# Architectural Review — IAM Service (Production Enterprise)

> **Reviewer role**: Principal Solution Architect — .NET, Clean Architecture, CQRS, DDD, Multi-Tenant SaaS  
> **Code reviewed**: `src/dotnet/IamTenant/` + `src/dotnet/shared/`  
> **Overall Score**: **5.5 / 10** (solid foundation, critical gaps before production)

---

## Summary Scorecard

| Category | Score | Verdict |
|---|---|---|
| CQRS correctness | 6/10 | Minor violations |
| Clean Architecture / SRP | 5/10 | Commands đang làm quá nhiều |
| Query / Include depth | 5/10 | N+1 và Cartesian risk |
| Caching strategy | 7/10 | Tốt nhưng version logic sai |
| Security — Cognito | 4/10 | **Critical gaps** |
| Multi-Tenant isolation | 7/10 | Tốt, một edge case nguy hiểm |
| Transaction / Consistency | 4/10 | **Missing — data inconsistency risk** |
| Outbox Pattern | 2/10 | **Hoàn toàn thiếu** |
| Domain modeling | 6/10 | String enums, thiếu Value Objects |
| Error handling | 3/10 | Raw Exception rải rác |
| Observability | 3/10 | Minimal logging, no metrics |
| Production readiness | 5/10 | Không thể deploy như hiện tại |

---

## Issue 1 — Thiếu Transaction: DB save và Event publish không atomic

**Severity**: 🔴 **CRITICAL**

### Nguyên nhân
Trong `CreateTenantCommand.cs` (và mọi Command publish event):
```csharp
await context.SaveChangesAsync(cancellationToken);  // ← step 1: DB commit
// nếu crash ở đây ↓
await publishEndpoint.Publish(tenantAdminCreatedEvent, cancellationToken); // ← step 2: MQ
```
Không có gì đảm bảo cả 2 bước thành công hoặc đều thất bại.

### Hậu quả
- Tenant được tạo trong DB nhưng email invitation **KHÔNG BAO GIỜ** được gửi.
- Tenant Admin ở trạng thái `PENDING` mãi mãi. Không ai biết lỗi xảy ra.
- Không có cách tự phục hồi (self-healing) — phải sửa bằng tay.

### Cách sửa — Outbox Pattern
Lưu event vào cùng DB transaction. Background worker sẽ relay sang MassTransit.

```csharp
// Domain/OutboxMessage.cs
public class OutboxMessage : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
}

// Trong Handler: lưu event vào DB thay vì publish trực tiếp
var outboxMessage = new OutboxMessage
{
    EventType = nameof(TenantAdminCreatedEvent),
    Payload = JsonSerializer.Serialize(tenantAdminCreatedEvent),
    CreatedAt = DateTimeOffset.UtcNow
};
context.OutboxMessages.Add(outboxMessage);
await context.SaveChangesAsync(cancellationToken); // ← 1 transaction duy nhất

// Background Service: Quartz.NET / IHostedService poll OutboxMessages
// và publish sang RabbitMQ, đánh dấu ProcessedAt khi thành công
```

> [!CAUTION]
> Đây là lỗi thiết kế nghiêm trọng nhất. **Phải fix trước khi production.**

---

## Issue 2 — Cognito: InvitationToken là Guid giả, không tích hợp thật

**Severity**: 🔴 **CRITICAL**

### Nguyên nhân
```csharp
// CreateTenantCommand.cs line 56
var token = Guid.CreateVersion7().ToString(); // ← không liên quan gì đến Cognito!
```
Token này được publish qua event để gửi email, nhưng khi user click link thì **không có cơ chế nào để verify** token này với Cognito.

### Luồng đúng với Cognito AdminCreateUser flow
```
1. CreateTenant handler → AdminCreateUser(email, tempPassword) → nhận CognitoSub
2. Lưu CognitoSub vào User.CognitoSub
3. Publish event với tempPassword (hoặc magic link chứa session)
4. User click link → gọi CompleteInvitation(email, session, newPassword)
5. IAM gọi RespondToAuthChallenge để đổi password
6. User.Status = ACTIVE
```

```csharp
// CreateTenantCommand.cs — cách đúng
var cognitoSub = await cognitoService.AdminCreateUserAsync(
    request.AdminEmail,
    GenerateTempPassword(), // ← password tạm, Cognito sẽ force change
    cancellationToken);

adminUser.CognitoSub = cognitoSub; // ← lưu vào DB

// Event chỉ chứa email — Cognito đã tự gửi FORCE_CHANGE_PASSWORD challenge
await publishEndpoint.Publish(new TenantAdminCreatedEvent
{
    TenantId = tenant.Id,
    UserId = adminUser.Id,
    Email = adminUser.Email,
    // KHÔNG gửi password/token qua message queue
}, cancellationToken);
```

> [!CAUTION]
> Token là `Guid.CreateVersion7()` — bất kỳ ai đoán được format này đều có thể giả mạo. **Security vulnerability.**

---

## Issue 3 — Permission Version: Logic tính version sai

**Severity**: 🔴 **CRITICAL**

### Nguyên nhân
```csharp
// GetUserPermissionsQuery.cs line 52
var newVersion = (cached?.Version ?? 0) + 1; // ← SAI HOÀN TOÀN
```
Version chỉ tăng khi có cache miss. Nếu Redis bị flush/restart, version reset về 0 → mọi JWT cũ với version > 0 sẽ bị coi là "lệch version" → DB query mọi request.

Còn tệ hơn: không có cách nào để biết version hiện tại là bao nhiêu khi JWT được cấp. BFF cần biết version tại thời điểm login để nhúng vào JWT.

### Cách sửa — Version phải sống trong DB, không phải Redis
```csharp
// Domain/User.cs — thêm field
public int PermissionVersion { get; set; } = 1;

// Khi assign role/permission → tăng version trong DB
// AssignPermissionsToRoleCommand.cs
var affectedUsers = await context.UserRoles
    .Where(ur => ur.RoleId == request.RoleId)
    .Include(ur => ur.User)
    .Select(ur => ur.User)
    .ToListAsync(cancellationToken);

foreach (var user in affectedUsers)
{
    user.PermissionVersion++; // ← source of truth
    await permissionCache.InvalidateAsync(user.Id, cancellationToken);
}

// GetUserPermissionsQuery: version lấy từ DB, không tự tính
var userVersion = await context.Users
    .Where(u => u.Id == request.UserId)
    .Select(u => u.PermissionVersion)
    .SingleAsync(cancellationToken);

// JWT khi login chứa User.PermissionVersion
// Mỗi request: nếu JWT.version != Redis.version → reload
```

---

## Issue 4 — SRP Vi phạm: Command Handler chứa quá nhiều trách nhiệm

**Severity**: 🟠 **HIGH**

### Nguyên nhân
`CreateTenantCommand` handler đang làm 5 việc khác nhau:
1. Validate domain uniqueness (Domain Invariant)
2. Validate email format (Domain Invariant)
3. Generate tenant code (Domain Logic)
4. Persist entities (Infrastructure)
5. Publish event (Infrastructure)

### Cách sửa — Domain Object chịu trách nhiệm invariants

```csharp
// Domain/Tenant.cs — Domain Factory Method
public class Tenant : AuditableEntity
{
    private Tenant() { } // EF Core

    public static Tenant Create(string name, string companyDomain, string? taxCode, string planType)
    {
        // Domain Invariant ở đây — không để Handler làm
        if (string.IsNullOrWhiteSpace(companyDomain))
            throw new DomainException("Company domain is required.");

        return new Tenant
        {
            Name = name,
            Code = GenerateCode(companyDomain),
            CompanyDomain = companyDomain.ToLowerInvariant(),
            TaxCode = taxCode,
            PlanType = planType,
            Status = TenantStatus.Provisioning
        };
    }

    public User AddAdmin(string email)
    {
        if (!email.EndsWith($"@{CompanyDomain}", StringComparison.OrdinalIgnoreCase))
            throw new DomainException($"Admin email must belong to domain: {CompanyDomain}");

        return User.CreateTenantAdmin(Id, email);
    }

    private static string GenerateCode(string domain) =>
        domain.Split('.')[0].ToUpperInvariant()[..Math.Min(10, domain.Split('.')[0].Length)];
}
```

---

## Issue 5 — N+1 Query trong AssignPermissionsToRoleCommand

**Severity**: 🟠 **HIGH**

### Nguyên nhân
```csharp
// AssignPermissionsToRoleCommand.cs line 31-34
foreach (var permId in request.PermissionIds)
{
    var permExists = await context.Permissions.AnyAsync(p => p.Id == permId, ...); // ← N queries!
}
```
Với 20 permissions → 20 SELECT queries riêng lẻ.

### Cách sửa — Batch query
```csharp
// 1 query duy nhất lấy tất cả valid permission IDs
var validIds = await context.Permissions
    .Where(p => request.PermissionIds.Contains(p.Id))
    .Select(p => p.Id)
    .ToListAsync(cancellationToken);

var invalidIds = request.PermissionIds.Except(validIds).ToList();
if (invalidIds.Any())
    throw new NotFoundException($"Permissions not found: {string.Join(", ", invalidIds)}");
```

---

## Issue 6 — Cartesian Explosion trong LoginCommand

**Severity**: 🟠 **HIGH**

### Nguyên nhân
```csharp
// LoginCommand.cs
context.Users
    .Include(u => u.UserRoles)
        .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r!.RolePermissions)  // ← depth 3
                .ThenInclude(rp => rp.Permission)   // ← depth 4
```
4 levels deep Include với EF Core sinh ra SQL JOIN tạo ra `Users × UserRoles × RolePermissions × Permissions` rows — **Cartesian Product**.
Với user có 3 roles, mỗi role 10 permissions → 3×10 = 30 rows thay vì 14.

### Cách sửa — Projection, không Include
```csharp
// LoginCommand.cs — Query-only, chỉ lấy đúng dữ liệu cần
var userData = await context.Users
    .AsNoTracking()
    .Where(u => u.Email == request.Email && !u.IsDeleted)
    .Select(u => new
    {
        u.Id,
        u.TenantId,
        u.Status,
        u.PermissionVersion,
        Roles = u.UserRoles.Select(ur => ur.Role!.Code).ToList(),
        Permissions = u.UserRoles
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .ToList()
    })
    .FirstOrDefaultAsync(cancellationToken)
    ?? throw new NotFoundException("User not found.");
```

---

## Issue 7 — Multi-Tenant: CreateTenant không dùng IgnoreQueryFilters

**Severity**: 🟠 **HIGH**

### Nguyên nhân
```csharp
// CreateTenantCommand.cs line 17 — NGUY HIỂM
if (context.Tenants.Any(t => t.CompanyDomain == request.CompanyDomain && !t.IsDeleted))
```
`Tenant` entity có filter `!t.IsDeleted`. Điều kiện `!t.IsDeleted` trong query là **REDUNDANT** nhưng không có hại.

Tuy nhiên, vấn đề thực sự là `CreateTenant` là SystemAdmin action — không có TenantId trong context. Nếu Global Filter trên Tenant có thêm tenant_id isolation trong tương lai → **bug ngay lập tức**.

### Cách sửa — Explicit domain check
```csharp
// Dùng IgnoreQueryFilters() cho system-level operations
var domainExists = await context.Tenants
    .IgnoreQueryFilters() // ← explicit, documented
    .AnyAsync(t => t.CompanyDomain == request.CompanyDomain && !t.IsDeleted, cancellationToken);
```

---

## Issue 8 — ICurrentUserService interface có setter public

**Severity**: 🟠 **HIGH**

### Nguyên nhân
```csharp
public interface ICurrentUserService
{
    Guid? UserId { get; set; }    // ← setter public trong INTERFACE
    Guid? TenantId { get; set; } // ← bất kỳ code nào cũng có thể ghi đè
}
```
Bất kỳ handler nào cũng có thể làm `currentUser.TenantId = Guid.Parse("another-tenant")` — tenant isolation bị bypass hoàn toàn.

### Cách sửa — Tách read interface và populate interface
```csharp
// Interface chỉ read — inject vào handlers
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    int? PermissionVersion { get; }
    IReadOnlyList<string> RoleIds { get; }
}

// Internal interface chỉ AuthInterceptor dùng
internal interface ICurrentUserContext : ICurrentUserService
{
    void Populate(Guid? userId, Guid? tenantId, int? version, string[] roleIds);
}

// Implementation
internal sealed class CurrentUserContext : ICurrentUserService, ICurrentUserContext
{
    public Guid? UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public int? PermissionVersion { get; private set; }
    public IReadOnlyList<string> RoleIds { get; private set; } = [];

    public void Populate(Guid? userId, Guid? tenantId, int? version, string[] roleIds)
    {
        UserId = userId;
        TenantId = tenantId;
        PermissionVersion = version;
        RoleIds = roleIds;
    }
}
```

---

## Issue 9 — CQRS Vi phạm: Command trả về DTO phức tạp

**Severity**: 🟡 **MEDIUM**

### Nguyên nhân
```csharp
public record CreateStaffCommand(...) : IRequest<StaffDto>; // ← Command trả DTO
public record AssignRolesCommand(...) : IRequest<StaffDto>; // ← Command trả DTO
```
Trong CQRS thuần túy (Greg Young / Udi Dahan), Command chỉ return `CommandResult { Id, Success }` hoặc `void/Unit`. Trả về DTO đầy đủ nghĩa là Command phải query lại DB.

### Cách sửa — Tùy mức độ strict
```csharp
// Option A: Strict CQRS — trả về Id để client query
public record CreateStaffCommand(...) : IRequest<Guid>; // chỉ trả Id

// Option B: Pragmatic — trả CreatedResult với minimal data
public record CreatedResult(Guid Id, DateTimeOffset CreatedAt);
public record CreateStaffCommand(...) : IRequest<CreatedResult>;

// KHÔNG làm: re-query DB trong Command để trả DTO đầy đủ
```

---

## Issue 10 — Exception thô: Dùng `throw new Exception()`

**Severity**: 🟡 **MEDIUM**

### Nguyên nhân
```csharp
throw new Exception("Company Domain already exists.");  // ← không phân biệt loại lỗi
throw new Exception("Role not found.");
throw new Exception("Permission {permId} not found.");
```
gRPC sẽ return `StatusCode.Internal (500)` cho mọi exception — client không biết đây là validation error (400) hay not found (404).

### Cách sửa — Domain Exceptions + gRPC Exception Middleware
```csharp
// Shared/Exceptions/DomainException.cs
public class DomainException(string message) : Exception(message);
public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class ForbiddenException(string message) : Exception(message);

// Shared/Interceptors/ExceptionInterceptor.cs
public class ExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(...)
    {
        try { return await continuation(request, context); }
        catch (NotFoundException ex)
            { throw new RpcException(new Status(StatusCode.NotFound, ex.Message)); }
        catch (ConflictException ex)
            { throw new RpcException(new Status(StatusCode.AlreadyExists, ex.Message)); }
        catch (DomainException ex)
            { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
        catch (ForbiddenException ex)
            { throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message)); }
    }
}
```

---

## Issue 11 — Soft Delete: IsDeleted là computed property trên Tenant

**Severity**: 🟡 **MEDIUM**

### Nguyên nhân
```csharp
// Tenant.cs
public DateTimeOffset? DeletedAt { get; set; }
public bool IsDeleted => DeletedAt.HasValue; // ← computed, không map DB column
```
EF Core không thể filter trên computed property trong LINQ-to-SQL. Filter `!t.IsDeleted` sẽ được evaluate **in-memory** sau khi load tất cả rows, hoặc throw runtime exception tùy EF version.

### Cách sửa — Dùng nullable column trực tiếp trong filter
```csharp
// Thay bằng
public bool IsDeleted { get; private set; }

public void SoftDelete()
{
    IsDeleted = true;
    DeletedAt = DateTimeOffset.UtcNow;
}

// Hoặc HasQueryFilter dùng DeletedAt
e.HasQueryFilter(t => t.DeletedAt == null); // ← EF Core có thể translate
```

---

## Issue 12 — String Enums: Status và UserType là magic strings

**Severity**: 🟡 **MEDIUM**

### Nguyên nhân
```csharp
Status = "ACTIVE"    // Tenant
Status = "PENDING"   // User
Status = "INVITED"   // User
UserType = "TENANT_ADMIN"
UserType = "TENANT_STAFF"
```
Không có compile-time safety. Typo sẽ không bị phát hiện cho đến runtime.

### Cách sửa — Strongly typed enums
```csharp
// Domain/Enums/TenantStatus.cs
public enum TenantStatus { Provisioning, Active, Suspended, Archived }
public enum UserStatus { Invited, Active, Suspended, Blocked }
public enum UserType { SystemAdmin, TenantAdmin, TenantStaff }

// EF Core mapping (giữ string trong DB để readable)
e.Property(t => t.Status)
    .HasConversion<string>()
    .HasMaxLength(50);
```

---

## Issue 13 — Missing: Idempotency cho Commands quan trọng

**Severity**: 🟡 **MEDIUM**

### Nguyên nhân
Nếu BFF retry `CreateTenant` do timeout → hai Tenant giống nhau được tạo trong cùng một khoảng thời gian (trước khi Unique Index check kịp chạy trong concurrent scenario).

### Cách sửa — Idempotency Key
```csharp
// BFF gửi kèm IdempotencyKey
public record CreateTenantCommand(
    string Name,
    string CompanyDomain,
    string AdminEmail,
    Guid IdempotencyKey) // ← BFF tạo và lưu trước khi gửi
    : IRequest<TenantDto>;

// Handler check trước
var existing = await context.Tenants
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, ct);
if (existing is not null) return MapToDto(existing); // return ngay, không tạo lại
```

---

## Thứ tự ưu tiên Refactor

| Priority | Issue | Effort | Impact |
|---|---|---|---|
| P0 | #1 Outbox Pattern | High | Prevent data loss |
| P0 | #2 Cognito Integration | High | Security |
| P0 | #3 Permission Version logic | Medium | Auth correctness |
| P1 | #8 ICurrentUserService setter | Low | Security hardening |
| P1 | #6 Cartesian Explosion | Low | Performance |
| P1 | #5 N+1 in AssignPermissions | Low | Performance |
| P2 | #4 SRP + Domain Factory | Medium | Maintainability |
| P2 | #10 Exception handling | Medium | UX + Observability |
| P2 | #11 Soft Delete mapping | Low | Bug prevention |
| P3 | #9 CQRS strictness | Medium | Architecture |
| P3 | #12 String Enums | Low | Type safety |
| P3 | #13 Idempotency | Medium | Reliability |

---

## Đánh giá Production-Ready: **5.5 / 10**

### Điểm mạnh
- ✅ Cấu trúc thư mục CQRS rõ ràng, nhất quán
- ✅ Global Query Filter cho Multi-Tenant đúng hướng (sau khi fix bug)
- ✅ Redis-first permission model phù hợp thiết kế hybrid
- ✅ Shared interceptors tái sử dụng được
- ✅ MassTransit snake_case entity name cho NestJS

### Điểm yếu ngăn production deploy
- ❌ Outbox Pattern thiếu → guaranteed message delivery không có
- ❌ Cognito integration dở dang → invitation flow không chạy được
- ❌ Permission version logic sai → có thể tạo ra auth bugs khó debug
- ❌ Exception handling thô → client nhận 500 cho mọi lỗi
- ❌ Không có health check, metrics, structured logging
- ❌ Chưa có Migration → database chưa được tạo
