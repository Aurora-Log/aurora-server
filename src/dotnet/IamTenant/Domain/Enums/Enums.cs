namespace IamTenant.Domain.Enums;

public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    Archived
}

public enum UserStatus
{
    Invited,
    Active,
    Blocked
}

public enum UserType
{
    SystemAdmin,
    TenantAdmin,
    TenantStaff,
    TenantManager
}

public enum StaffType
{
    Normal,
    Compliance,
    Negotiation,
    CustomerAssistant,
    RoutePlanning,
    FinancialTax,
    GpsTracking,
    BillingSettlement,
    Ocr
}
