using JobManagement.Infrastructure.Models.Authentication;

namespace JobManagement.Infrastructure.Interfaces.Authentication
{
    /// <summary>
    /// Interface for Google authentication service
    /// </summary>
    public interface IGoogleAuthService
    {
        /// <summary>
        /// Validates a Google ID token
        /// </summary>
        /// <param name="idToken">The ID token to validate</param>
        /// <returns>User information from the token</returns>
        Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken);
    }
}