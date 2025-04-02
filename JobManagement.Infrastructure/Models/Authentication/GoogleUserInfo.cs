namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Model for Google user information
    /// </summary>
    public class GoogleUserInfo
    {
        /// <summary>
        /// Google's unique ID for the user
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// User's email address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Is the email verified
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>
        /// User's first name
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// User's last name
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// URL of user's profile picture
        /// </summary>
        public string PictureUrl { get; set; }
    }
}