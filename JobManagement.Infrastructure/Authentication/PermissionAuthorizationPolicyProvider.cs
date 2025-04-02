using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace JobManagement.Infrastructure.Authentication
{
    /// <summary>
    /// Custom authorization policy provider that dynamically creates policies for permission-based authorization
    /// </summary>
    public class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        private const string PERMISSION_PREFIX = "Permission:";

        public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
        }

        public override async Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            // If the policy name doesn't start with our prefix, use the default provider
            if (!policyName.StartsWith(PERMISSION_PREFIX))
            {
                return await base.GetPolicyAsync(policyName);
            }

            // Extract the permission name from the policy name
            var permission = policyName.Substring(PERMISSION_PREFIX.Length);

            // Create a policy requiring the specified permission claim
            var policy = new AuthorizationPolicyBuilder()
                .RequireClaim("permission", permission)
                .Build();

            return policy;
        }
    }
}
