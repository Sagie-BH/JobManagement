namespace JobManagement.Domain.Entities
{
    /// <summary>
    /// Represents a refresh token for JWT authentication
    /// </summary>
    public class RefreshToken : BaseEntity
    {
        /// <summary>
        /// ID of the user that owns this refresh token
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Reference to the user
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// Token value
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Expiration date and time
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Date and time when the token was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date and time when the token was revoked (if applicable)
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// IP address of the client that requested the token
        /// </summary>
        public string CreatedByIp { get; set; } = string.Empty;

        /// <summary>
        /// IP address of the client that revoked the token (if applicable)
        /// </summary>
        public string RevokedByIp { get; set; } = string.Empty;

        /// <summary>
        /// Replacement token (when this token is revoked)
        /// </summary>
        public string ReplacedByToken { get; set; } = string.Empty;

        /// <summary>
        /// Reason for revocation (if applicable)
        /// </summary>
        public string ReasonRevoked { get; set; } = string.Empty;

        /// <summary>
        /// Whether the token is active (not expired and not revoked)
        /// </summary>
        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}
