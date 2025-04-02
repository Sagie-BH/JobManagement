namespace JobManagement.Infrastructure.Settings
{
    /// <summary>
    /// Settings for Google OAuth authentication
    /// </summary>
    public class GoogleAuthSettings
    {
        /// <summary>
        /// Client ID for Google OAuth
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Client secret for Google OAuth
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Redirect URI for Google OAuth
        /// </summary>
        public string RedirectUri { get; set; }
    }
}
