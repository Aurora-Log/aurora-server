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
    Suspended,
    Blocked
}

public enum UserType
{
    SystemAdmin,
    TenantAdmin,
    TenantStaff
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
