namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents the many-to-many relationship between roles and permissions
    /// </summary>
    public class RolePermission : BaseEntity
    {
        /// <summary>
        /// ID of the role
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// Reference to the role
        /// </summary>
        public virtual Role Role { get; set; }

        /// <summary>
        /// ID of the permission
        /// </summary>
        public Guid PermissionId { get; set; }

        /// <summary>
        /// Reference to the permission
        /// </summary>
        public virtual Permission Permission { get; set; }
    }
}
