namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents a user in the system
    /// </summary>
    public class User : AuditableEntity
    {
        /// <summary>
        /// User's username for local authentication
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// User's email address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// User's first name
        /// </summary>
        public string FirstName { get; set; }

        /// <summary>
        /// User's last name
        /// </summary>
        public string LastName { get; set; }

        /// <summary>
        /// Password hash for local authentication
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// External provider ID (e.g., Google)
        /// </summary>
        public string ExternalId { get; set; }

        /// <summary>
        /// Authentication provider (e.g., "Local", "Google")
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Last login timestamp
        /// </summary>
        public DateTime? LastLogin { get; set; }

        /// <summary>
        /// Flag indicating if the user is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Collection of roles assigned to this user
        /// </summary>
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        /// <summary>
        /// Gets the full name of the user
        /// </summary>
        public string FullName => $"{FirstName} {LastName}";
    }
}
