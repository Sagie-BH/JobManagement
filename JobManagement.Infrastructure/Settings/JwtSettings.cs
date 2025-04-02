namespace JobManagement.Infrastructure.Settings
{
    /// <summary>
    /// Settings for JWT authentication
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// Secret key used to sign JWT tokens
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// Issuer of the JWT token
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Audience of the JWT token
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Expiration time for access tokens in minutes
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; }

        /// <summary>
        /// Expiration time for refresh tokens in days
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; }

        /// <summary>
        /// Inactivity timeout in minutes (default: 15 minutes)
        /// After this period of inactivity, the token will be considered expired
        /// </summary>
        public int InactivityTimeoutMinutes { get; set; } = 15;
    }
}