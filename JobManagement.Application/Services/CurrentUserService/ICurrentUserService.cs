using JobManagement.Domain.Entities;

namespace JobManagement.Infrastructure.Services
{
    public interface ICurrentUserService
    {
        Task<User> GetCurrentUserAsync();
        Guid? GetCurrentUserId();
        string GetCurrentUsername();
        bool HasPermission(string permission);
        bool IsInRole(string roleName);
    }
}