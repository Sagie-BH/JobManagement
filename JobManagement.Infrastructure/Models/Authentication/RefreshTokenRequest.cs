using System.ComponentModel.DataAnnotations;

namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Request model for refreshing tokens
    /// </summary>
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}