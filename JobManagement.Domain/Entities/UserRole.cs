namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents the many-to-many relationship between users and roles
    /// </summary>
    public class UserRole : BaseEntity
    {
        /// <summary>
        /// ID of the user
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Reference to the user
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// ID of the role
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// Reference to the role
        /// </summary>
        public virtual Role Role { get; set; }
    }
}
