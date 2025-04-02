namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents a role in the system for role-based authorization
    /// </summary>
    public class Role : BaseEntity
    {
        /// <summary>
        /// Name of the role
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the role
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Users assigned to this role
        /// </summary>
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        /// <summary>
        /// Permissions assigned to this role
        /// </summary>
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
