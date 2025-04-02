using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Services
{
    /// <summary>
    /// Service to get the current authenticated user from HTTP context
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUnitOfWork _unitOfWork;

        public CurrentUserService(
            IHttpContextAccessor httpContextAccessor,
            IUnitOfWork unitOfWork)
        {
            _httpContextAccessor = httpContextAccessor;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Gets the current user ID from the HTTP context
        /// </summary>
        /// <returns>User ID if authenticated, otherwise null</returns>
        public Guid? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }

            return null;
        }

        /// <summary>
        /// Gets the current user entity from the database
        /// </summary>
        /// <returns>User entity if authenticated, otherwise null</returns>
        public async Task<User> GetCurrentUserAsync()
        {
            var userId = GetCurrentUserId();

            if (!userId.HasValue)
            {
                return null;
            }

            return await _unitOfWork.Repository<User>().GetByIdAsync(userId.Value);
        }

        /// <summary>
        /// Gets the current username from the HTTP context
        /// </summary>
        /// <returns>Username if authenticated, otherwise null</returns>
        public string GetCurrentUsername()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }

        /// <summary>
        /// Checks if the current user has a specific role
        /// </summary>
        /// <param name="roleName">Role name to check</param>
        /// <returns>True if the user has the role, otherwise false</returns>
        public bool IsInRole(string roleName)
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole(roleName) ?? false;
        }

        /// <summary>
        /// Checks if the current user has a specific permission
        /// </summary>
        /// <param name="permission">Permission to check</param>
        /// <returns>True if the user has the permission, otherwise false</returns>
        public bool HasPermission(string permission)
        {
            return _httpContextAccessor.HttpContext?.User?.HasClaim(c => c.Type == "permission" && c.Value == permission) ?? false;
        }
    }
}