using Microsoft.AspNetCore.Authorization;

namespace JobManagement.Infrastructure.Authentication
{
    // Permission requirement for dynamic permission-based authorization
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }
}
