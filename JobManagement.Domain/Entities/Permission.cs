namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents a permission that can be assigned to roles
    /// </summary>
    public class Permission : BaseEntity
    {
        /// <summary>
        /// Name of the permission
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of the permission
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Roles that have this permission
        /// </summary>
        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
