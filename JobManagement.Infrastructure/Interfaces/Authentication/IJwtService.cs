using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Models.Authentication;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Interfaces.Authentication
{
    /// <summary>
    /// Interface for JWT token management
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Generates a JWT access token for a user
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="roles">User's roles</param>
        /// <param name="permissions">User's permissions</param>
        /// <returns>JWT token string</returns>
        string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);

        /// <summary>
        /// Generates a refresh token for a user
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>The refresh token</returns>
        Task<RefreshToken> GenerateRefreshTokenAsync(User user, string ipAddress);

        /// <summary>
        /// Validates a refresh token and issues a new access token
        /// </summary>
        /// <param name="refreshToken">The refresh token</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <returns>Authentication response with new tokens</returns>
        Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress);

        /// <summary>
        /// Revokes a refresh token
        /// </summary>
        /// <param name="token">The token to revoke</param>
        /// <param name="ipAddress">IP address of the requester</param>
        /// <param name="reason">Reason for revocation</param>
        /// <param name="replacementToken">Optional replacement token</param>
        /// <returns>True if successful</returns>
        Task<bool> RevokeTokenAsync(string token, string ipAddress, string reason = null, string replacementToken = null);

        /// <summary>
        /// Validates a JWT token
        /// </summary>
        /// <param name="token">Token to validate</param>
        /// <returns>True if valid</returns>
        bool ValidateToken(string token);

        /// <summary>
        /// Gets user ID from token
        /// </summary>
        /// <param name="token">JWT token</param>
        /// <returns>User ID</returns>
        int? GetUserIdFromToken(string token);

        /// <summary>
        /// Updates the last activity timestamp in the token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>A new token with updated last activity timestamp</returns>
        string UpdateLastActivity(string token);
    }
}