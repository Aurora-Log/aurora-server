namespace IamTenant.Application.Constants;

public static class AuditActions
{
    // Tenant
    public const string CreateTenant = "CREATE_TENANT";
    public const string UpdateTenant = "UPDATE_TENANT";
    public const string DeleteTenant = "DELETE_TENANT";
    
    // User
    public const string CreateUser = "CREATE_USER";
    public const string UpdateUser = "UPDATE_USER";
    public const string DeleteUser = "DELETE_USER";
    public const string LockUser = "LOCK_USER";
    public const string UnlockUser = "UNLOCK_USER";
    public const string ResetPassword = "RESET_PASSWORD";

    // Role
    public const string CreateRole = "CREATE_ROLE";
    public const string UpdateRole = "UPDATE_ROLE";
    public const string DeleteRole = "DELETE_ROLE";
    public const string AssignRole = "ASSIGN_ROLE";
    public const string RemoveRole = "REMOVE_ROLE";

    // Permission
    public const string AssignPermission = "ASSIGN_PERMISSION";
    public const string RemovePermission = "REMOVE_PERMISSION";
}
