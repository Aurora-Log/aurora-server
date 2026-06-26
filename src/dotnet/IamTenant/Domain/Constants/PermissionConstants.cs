namespace IamTenant.Domain.Constants;

public static class PermissionConstants
{
    // Define standard actions
    public const string Create = "create";
    public const string Read = "read";
    public const string Update = "update";
    public const string Delete = "delete";

    // Define Modules / Services            system, admin, staff
    public static class Modules
    {
        public const string Bff = "bff";
        public const string Ocr = "ocr";
        public const string Compliance = "compliance";
        public const string CarrierMarketplace = "carrier_marketplace";
        public const string Negotiation = "negotiation";
        public const string CustomerAssistant = "customer_assistant";
        public const string Iam = "iam";
        public const string RoutePlanning = "route_planning";
        public const string FinancialTax = "financial_tax";
        public const string GpsTracking = "gps_tracking";
        public const string BillingSettlement = "billing_settlement";
        public const string AiOps = "aiops";

        public static readonly IReadOnlyList<string> All =
        [
            Bff, Ocr, Compliance, CarrierMarketplace, Negotiation, CustomerAssistant,
            Iam, RoutePlanning, FinancialTax, GpsTracking, BillingSettlement, AiOps
        ];
    }

    /// <summary>
    /// Generate a permission code like "route_planning:create"
    /// </summary>
    public static string Build(string module, string action) => $"{module}:{action}";

    /// <summary>
    /// Gets all possible permissions for all modules (Create, Read, Update, Delete)
    /// </summary>
    public static List<string> GetAllPermissions()
    {
        var permissions = new List<string>();
        foreach (var module in Modules.All)
        {
            permissions.Add(Build(module, Create));
            permissions.Add(Build(module, Read));
            permissions.Add(Build(module, Update));
            permissions.Add(Build(module, Delete));
        }
        return permissions;
    }

    /// <summary>
    /// Gets default permissions for a standard Staff role (Only Create and Read)
    /// </summary>
    public static List<string> GetDefaultStaffPermissions()
    {
        var permissions = new List<string>();
        foreach (var module in Modules.All)
        {
            // By default, staff can only create and read
            permissions.Add(Build(module, Create));
            permissions.Add(Build(module, Read));
        }
        return permissions;
    }
}
