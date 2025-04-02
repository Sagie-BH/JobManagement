using JobManagement.Infrastructure.Models.Authentication;

namespace JobManagement.Infrastructure.Interfaces.Authentication
{
    /// <summary>
    /// Interface for authentication service
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user
        /// </summary>
        /// <param name="request">Registration details</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>Authentication response with tokens</returns>
        Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress);

        /// <summary>
        /// Authenticates a user
        /// </summary>
        /// <param name="request">Login details</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>Authentication response with tokens</returns>
        Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);

        /// <summary>
        /// Refreshes an authentication token
        /// </summary>
        /// <param name="request">Refresh token details</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>Authentication response with new tokens</returns>
        Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress);

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        /// <param name="token">The token to revoke</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>True if successful</returns>
        Task<bool> RevokeTokenAsync(string token, string ipAddress);

        /// <summary>
        /// Authenticates a user with an external provider
        /// </summary>
        /// <param name="request">External authentication details</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>Authentication response with tokens</returns>
        Task<AuthResponse> ExternalLoginAsync(ExternalAuthRequest request, string ipAddress);
    }
}