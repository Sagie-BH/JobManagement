using Microsoft.AspNetCore.Authorization;

namespace JobManagement.Infrastructure.Authentication
{
    // Handler for permission requirements
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Check if the user has the required permission claim
            var permissionClaim = context.User.Claims
                .FirstOrDefault(c => c.Type == "permission" && c.Value == requirement.Permission);

            if (permissionClaim != null)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
